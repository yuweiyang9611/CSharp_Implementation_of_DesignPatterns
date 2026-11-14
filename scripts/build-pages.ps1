#requires -Version 7.0

[CmdletBinding()]
param(
  [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$siteSource = Join-Path $root 'site'
$stageDirectory = Join-Path (Join-Path $root 'output') 'pages-site'
$guideDirectory = Join-Path $stageDirectory 'guides'
$patternDirectory = Join-Path $stageDirectory 'patterns'
$assetsDirectory = Join-Path $stageDirectory 'assets'
$exportScript = Join-Path $PSScriptRoot 'export-guide.ps1'
$solution = Join-Path $root 'DesignPatterns.sln'
$runnerProject = Join-Path (Join-Path (Join-Path $root 'src') 'DesignPatterns.Runner') 'DesignPatterns.Runner.csproj'
$learningCatalogPath = Join-Path (Join-Path $siteSource 'data') 'learning-catalog.json'
$siteManifestPath = Join-Path $PSScriptRoot 'site-manifest.psd1'
$defaultBlobBase = 'https://github.com/yuweiyang9611/CSharp_Implementation_of_DesignPatterns/blob/main/'
$defaultPagesBase = 'https://yuweiyang9611.github.io/CSharp_Implementation_of_DesignPatterns/'

function Encode-Html {
  param([AllowEmptyString()][string]$Value)

  return [Net.WebUtility]::HtmlEncode($Value)
}

function Get-RepositoryUrl {
  param(
    [Parameter(Mandatory)][string]$RelativePath,
    [Parameter(Mandatory)][string]$BlobBase
  )

  $escapedPath = ($RelativePath -split '[\\/]' | ForEach-Object {
      [Uri]::EscapeDataString($_)
    }) -join '/'
  return $BlobBase.TrimEnd('/') + '/' + $escapedPath
}

function Get-RepositoryRootUrl {
  param([Parameter(Mandatory)][string]$BlobBase)

  $trimmed = $BlobBase.TrimEnd('/')
  $marker = $trimmed.IndexOf('/blob/', [StringComparison]::OrdinalIgnoreCase)
  if ($marker -ge 0) { return $trimmed.Substring(0, $marker) }
  return $trimmed
}

function Get-JsonLdScript {
  param([Parameter(Mandatory)][hashtable]$Value)

  $json = ($Value | ConvertTo-Json -Depth 12 -Compress).Replace('</', '<\/')
  return "<script type=`"application/ld+json`">$json</script>"
}

if (-not (Test-Path -LiteralPath $siteManifestPath)) { throw "Site manifest is missing: $siteManifestPath" }
$siteManifest = Import-PowerShellDataFile -LiteralPath $siteManifestPath
$pages = @($siteManifest.Guides | ForEach-Object { [pscustomobject]$_ })


$resolvedRoot = [IO.Path]::GetFullPath($root).TrimEnd([IO.Path]::DirectorySeparatorChar)
$resolvedStage = [IO.Path]::GetFullPath($stageDirectory)
if (-not $resolvedStage.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Refusing to stage GitHub Pages outside the repository: $resolvedStage"
}
if (-not (Test-Path -LiteralPath (Join-Path $siteSource 'index.html'))) {
  throw "Site source is missing index.html: $siteSource"
}
if (-not (Test-Path -LiteralPath $learningCatalogPath)) {
  throw "Site learning catalog is missing: $learningCatalogPath"
}

if (-not $NoBuild) {
  dotnet restore $solution --locked-mode
  if ($LASTEXITCODE -ne 0) { throw 'Solution locked restore failed.' }
  dotnet build $solution --configuration Release --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
}

& (Join-Path $PSScriptRoot 'sync-pattern-index.ps1') -Check -NoBuild
& (Join-Path $PSScriptRoot 'test-learning-catalog.ps1') -NoBuild

$blobBaseVariable = 'CSHARP_DESIGN_PATTERNS_REPOSITORY_BLOB_BASE'
$hadBlobBase = Test-Path "Env:$blobBaseVariable"
$oldBlobBase = [Environment]::GetEnvironmentVariable($blobBaseVariable, 'Process')
$blobBase = if ([string]::IsNullOrWhiteSpace($oldBlobBase)) { $defaultBlobBase } else { $oldBlobBase.TrimEnd('/') + '/' }
$pagesBase = if ([string]::IsNullOrWhiteSpace($env:CSHARP_DESIGN_PATTERNS_PAGES_BASE)) {
  $defaultPagesBase
} else {
  $env:CSHARP_DESIGN_PATTERNS_PAGES_BASE.TrimEnd('/') + '/'
}
$repositoryUrl = Get-RepositoryRootUrl -BlobBase $blobBase
$repositoryOwner = try { ([Uri]$repositoryUrl).Segments[1].Trim('/') } catch { 'yuweiyang9611' }

$runnerArguments = @(
  'run', '--project', $runnerProject, '--configuration', 'Release', '--no-build', '--', '--catalog-json'
)
$coreCatalogJson = (& dotnet @runnerArguments | Out-String)
if ($LASTEXITCODE -ne 0) { throw 'Runner catalog JSON export failed.' }
$coreCatalog = @($coreCatalogJson | ConvertFrom-Json)
$evidenceArguments = @(
  'run', '--project', $runnerProject, '--configuration', 'Release', '--no-build', '--', '--evidence-json'
)
$evidenceCatalogJson = (& dotnet @evidenceArguments | Out-String)
if ($LASTEXITCODE -ne 0) { throw 'Runner evidence JSON export failed.' }
$evidenceByKey = @{}
foreach ($entry in @($evidenceCatalogJson | ConvertFrom-Json)) {
  if ($evidenceByKey.ContainsKey($entry.key)) { throw "Duplicate Runner evidence key: $($entry.key)" }
  $evidenceByKey[$entry.key] = $entry
}
$learningCatalog = Get-Content -LiteralPath $learningCatalogPath -Raw -Encoding utf8 | ConvertFrom-Json
$exerciseJson = Get-Content -LiteralPath (Join-Path $siteSource 'data/coding-exercises.json') -Raw -Encoding utf8
$exerciseCatalog = $exerciseJson | ConvertFrom-Json
$taskCount = @($coreCatalog).Count * 4 + ($learningCatalog.learningItems.milestones | Measure-Object).Count
$itemCount = @($coreCatalog).Count + @($learningCatalog.learningItems).Count
function Expand-Counts([string]$Html) {
  return $Html.Replace('{{QUIZ_COUNT}}', [string]@($learningCatalog.quizzes).Count).Replace('{{EXERCISE_COUNT}}', [string]@($exerciseCatalog.exercises).Count).Replace('{{TASK_COUNT}}', [string]$taskCount).Replace('{{ITEM_COUNT}}', [string]$itemCount)
}
$enrichmentByKey = @{}
foreach ($entry in $learningCatalog.patterns) {
  if ($enrichmentByKey.ContainsKey($entry.key)) { throw "Duplicate learning catalog key: $($entry.key)" }
  $enrichmentByKey[$entry.key] = $entry
}

$patterns = [Collections.Generic.List[object]]::new()
foreach ($core in $coreCatalog) {
  if (-not $enrichmentByKey.ContainsKey($core.key)) { throw "Missing learning catalog entry: $($core.key)" }
  if (-not $evidenceByKey.ContainsKey($core.key)) { throw "Missing Runner evidence entry: $($core.key)" }
  $extra = $enrichmentByKey[$core.key]
  $runnerEvidence = $evidenceByKey[$core.key]
  $nameParts = @($core.name -split '\s*/\s*', 2)
  $english = $nameParts[0].Trim()
  $chinese = if ($nameParts.Count -gt 1) { ($nameParts[1].Trim() -replace '模式$', '') } else { $english }
  $templateMapping = $learningCatalog.tagToExerciseTemplate.PSObject.Properties[[string]$extra.problemTags[0]]
  if ($null -eq $templateMapping) { throw "Missing exercise template mapping for $($core.key): $($extra.problemTags[0])" }
  $templateId = [string]$templateMapping.Value
  $templateText = [string]$learningCatalog.exerciseTemplates.PSObject.Properties[$templateId].Value
  $expectedOutput = @($runnerEvidence.output)
  $evidenceCards = @(
    [pscustomobject][ordered]@{
      id = 'read'; kind = 'read'; title = '说清变化轴'; href = '#evidence-read'
      task = "先不看实现，用一句话说明 $english 隔离了什么变化。"
      acceptance = @("我能说明：$($extra.changeAxis)", "我能指出不适用场景：$($extra.avoidWhen)")
    },
    [pscustomobject][ordered]@{
      id = 'run'; kind = 'run'; title = '运行并核对输出'; href = '#evidence-run'
      task = '先预测哪一行会变化，再运行命令并核对真实输出。'
      command = "dotnet run --project src/DesignPatterns.Runner -- $($core.key)"
      expectedOutput = $expectedOutput
    },
    [pscustomobject][ordered]@{
      id = 'change'; kind = 'change'; title = '制造一个最小变化'; href = '#evidence-change'
      task = $templateText
      target = [string]$extra.source
      acceptance = @('修改前写下输出预测。', '只修改隔离这个变化的代码。', '客户端原有使用方式保持不变。')
    },
    [pscustomobject][ordered]@{
      id = 'verify'; kind = 'verify'; title = '验证并解释取舍'; href = '#evidence-verify'
      task = "运行全部模式烟雾测试，再回答为什么此场景不优先使用 $($extra.related[0])。"
      command = 'dotnet run --project tests/DesignPatterns.SmokeTests --configuration Release'
      reflectionPattern = [string]$extra.related[0]
      acceptance = @('烟雾测试通过 23/23。', ('我能结合“{0}”解释选择。' -f $extra.whenUse.TrimEnd('。')))
    }
  )
  $patterns.Add([pscustomobject][ordered]@{
      number = [int]$core.number
      key = [string]$core.key
      english = $english
      chinese = $chinese
      name = [string]$core.name
      category = [string]$core.category
      intent = [string]$core.intent
      scenario = [string]$extra.scenario
      source = [string]$extra.source
      practice = [string]$extra.practice
      practiceLabel = [string]$extra.practiceLabel
      guide = [string]$extra.guide
      problemTags = @($extra.problemTags)
      changeAxis = [string]$extra.changeAxis
      whenUse = [string]$extra.whenUse
      avoidWhen = [string]$extra.avoidWhen
      related = @($extra.related)
      evidenceCards = $evidenceCards
      sourceUrl = Get-RepositoryUrl -RelativePath $extra.source -BlobBase $blobBase
      practiceUrl = Get-RepositoryUrl -RelativePath $extra.practice -BlobBase $blobBase
      guideUrl = $pagesBase + 'guides/fundamentals.html' + $extra.guide
      pageUrl = $pagesBase + 'patterns/' + $core.key + '.html'
    })
}
foreach ($extraKey in $enrichmentByKey.Keys) {
  if (-not ($coreCatalog.key -contains $extraKey)) { throw "Orphan learning catalog entry: $extraKey" }
}

$publishedCatalog = [pscustomobject][ordered]@{
  problemTags = @($learningCatalog.problemTags)
  patterns = @($patterns)
  learningItems = @($learningCatalog.learningItems)
  quizzes = @($learningCatalog.quizzes)
}
$publishedCatalogJson = $publishedCatalog | ConvertTo-Json -Depth 12

if (Test-Path -LiteralPath $stageDirectory) {
  Remove-Item -LiteralPath $stageDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDirectory, $guideDirectory, $patternDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $siteSource 'index.html') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $siteSource 'quiz.html') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $siteSource 'playground.html') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $siteSource 'source.html') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $siteSource 'assets') -Destination $stageDirectory -Recurse
[IO.File]::WriteAllText((Join-Path $assetsDirectory 'exercises.js'), "window.ExerciseCatalog = $exerciseJson;`n", [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $siteSource 'data/coding-exercises.json') -Destination (Join-Path $assetsDirectory 'exercises.json')
& (Join-Path $PSScriptRoot 'build-browser-exercises.ps1') -Destination (Join-Path $assetsDirectory 'compiler')

# Source read in place: every file a lesson or guide can point at, copied at this revision as plain text,
# so the panel on a page never drifts from the repository. Only files a reader can learn from are bundled.
$sourceRoots = @('src', 'examples', 'labs', 'tests', 'tools', 'docs', 'site', 'scripts')
$sourceExtensions = @('.cs', '.csproj', '.props', '.sln', '.json', '.md', '.ps1', '.psd1', '.cmd', '.sh', '.yml', '.html', '.css', '.js', '.mjs', '.txt')
$sourceDirectory = Join-Path $assetsDirectory 'source'
$bundledFiles = [Collections.Generic.List[object]]::new()
$bundledPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($sourceRoot in $sourceRoots) {
  $rootPath = Join-Path $root $sourceRoot
  if (-not (Test-Path -LiteralPath $rootPath)) { continue }
  foreach ($file in Get-ChildItem -LiteralPath $rootPath -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($root, $file.FullName) -replace '\\', '/'
    # Case-sensitive: examples/DocumentWorkflow/Output is course code, the repository's output/ is not.
    if ($relative -cmatch '(^|/)(bin|obj|node_modules|TestResults|\.git)/' -or $relative -cmatch '^output/') { continue }
    if ($sourceExtensions -notcontains $file.Extension.ToLowerInvariant()) { continue }
    if ($file.Length -gt 256KB) { continue }
    $text = [IO.File]::ReadAllText($file.FullName)
    $target = Join-Path $sourceDirectory ($relative + '.txt')
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    [IO.File]::WriteAllText($target, $text, [Text.UTF8Encoding]::new($false))
    [void]$bundledPaths.Add($relative)
    $lineCount = [regex]::Matches($text, "`n").Count
    if ($text.Length -gt 0 -and -not $text.EndsWith("`n")) { $lineCount += 1 }
    $bundledFiles.Add([pscustomobject][ordered]@{ path = $relative; bytes = [Text.Encoding]::UTF8.GetByteCount($text); lines = $lineCount })
  }
}
foreach ($pattern in $patterns) {
  foreach ($required in @($pattern.source, $pattern.practice, 'src/DesignPatterns/IPatternDemo.cs')) {
    if (-not $bundledPaths.Contains($required)) { throw "Pattern $($pattern.key) points at a file outside the source bundle: $required" }
  }
}
$sourceRevision = if ($env:GITHUB_SHA -match '^[0-9a-fA-F]{40}$') { $env:GITHUB_SHA.ToLowerInvariant() } else { ([string](& git -C $root rev-parse HEAD)).Trim().ToLowerInvariant() }
$sourceIndex = [ordered]@{
  version = 1
  revision = $sourceRevision
  repository = $repositoryUrl
  blobBase = $blobBase
  files = @($bundledFiles | Sort-Object path)
}
[IO.File]::WriteAllText((Join-Path $sourceDirectory 'index.json'), ($sourceIndex | ConvertTo-Json -Depth 4 -Compress), [Text.UTF8Encoding]::new($false))

# A GitHub file or folder link whose target is in the bundle becomes a link into the site.
$blobLinkPattern = '(?<=href=")' + [regex]::Escape($blobBase) + '(?<path>[^"#?]+)(?<fragment>#[^"]*)?(?=")'
$treeLinkPattern = '(?<=href=")' + [regex]::Escape($repositoryUrl) + '/tree/[^/"]+/(?<path>[^"#?]+?)/?(?=")'
function Convert-RepositoryLinks([string]$Html, [string]$Prefix) {
  $Html = [regex]::Replace($Html, $blobLinkPattern, {
      param($match)
      $path = [Uri]::UnescapeDataString($match.Groups['path'].Value)
      if (-not $bundledPaths.Contains($path)) { return $match.Value }
      $fragment = $match.Groups['fragment'].Value
      if ($fragment -notmatch '^#L\d+(-L?\d+)?$') { $fragment = '' }
      return $Prefix + 'source.html?path=' + [Uri]::EscapeDataString($path) + $fragment
    })
  return [regex]::Replace($Html, $treeLinkPattern, {
      param($match)
      $path = [Uri]::UnescapeDataString($match.Groups['path'].Value).TrimEnd('/')
      $inside = @($bundledPaths | Where-Object { $_.StartsWith($path + '/', [StringComparison]::Ordinal) })
      if ($inside.Count -eq 0) { return $match.Value }
      return $Prefix + 'source.html?dir=' + [Uri]::EscapeDataString($path)
    })
}

[IO.File]::WriteAllText(
  (Join-Path $assetsDirectory 'catalog.json'),
  $publishedCatalogJson,
  [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText(
  (Join-Path $assetsDirectory 'catalog.js'),
  "window.PatternCatalog = $publishedCatalogJson;`n",
  [Text.UTF8Encoding]::new($false))

try {
  [Environment]::SetEnvironmentVariable($blobBaseVariable, $blobBase, 'Process')
  foreach ($page in $pages) {
    $inputPath = Join-Path $root ($page.Input -replace '/', [IO.Path]::DirectorySeparatorChar)
    $outputPath = Join-Path $guideDirectory $page.Output
    if (-not (Test-Path -LiteralPath $inputPath)) { throw "Guide source is missing: $($page.Input)" }
    & $exportScript -InputPath $inputPath -OutputPath $outputPath -HtmlOnly -NoBuild
    if ($LASTEXITCODE -ne 0) { throw "Guide export failed: $($page.Input)" }
  }
}
finally {
  if ($hadBlobBase) {
    [Environment]::SetEnvironmentVariable($blobBaseVariable, $oldBlobBase, 'Process')
  } else {
    [Environment]::SetEnvironmentVariable($blobBaseVariable, $null, 'Process')
  }
}

$routeMap = @{}
foreach ($page in $pages) {
  $routeMap[(Get-RepositoryUrl -RelativePath $page.Input -BlobBase $blobBase)] = $page.Output
}

foreach ($page in $pages) {
  $outputPath = Join-Path $guideDirectory $page.Output
  $html = Get-Content -LiteralPath $outputPath -Raw -Encoding utf8
  foreach ($entry in $routeMap.GetEnumerator()) {
    $html = $html.Replace('href="' + $entry.Key, 'href="' + $entry.Value)
  }
  $html = Convert-RepositoryLinks -Html $html -Prefix '../'

  $titleMatch = [regex]::Match($html, '<title>(?<title>.*?)</title>', [Text.RegularExpressions.RegexOptions]::Singleline)
  $pageTitle = if ($titleMatch.Success) { [Net.WebUtility]::HtmlDecode($titleMatch.Groups['title'].Value) } else { 'C# 设计模式学习地图' }
  $encodedTitle = Encode-Html $pageTitle
  $encodedDescription = Encode-Html $page.Description
  $pageUrl = $pagesBase + 'guides/' + $page.Output
  $imageUrl = $pagesBase + 'assets/og.jpg'
  $jsonLd = Get-JsonLdScript -Value ([ordered]@{
      '@context' = 'https://schema.org'
      '@type' = 'LearningResource'
      name = $pageTitle
      description = $page.Description
      url = $pageUrl
      inLanguage = 'zh-CN'
      isAccessibleForFree = $true
      learningResourceType = $page.Type
      isPartOf = [ordered]@{ '@type' = 'Course'; name = 'C# 设计模式学习地图'; url = $pagesBase }
    })
  $metadata = @"
  <meta name="description" content="$encodedDescription">
  <meta property="og:type" content="article">
  <meta property="og:site_name" content="C# 设计模式学习地图">
  <meta property="og:locale" content="zh_CN">
  <meta property="og:title" content="$encodedTitle">
  <meta property="og:description" content="$encodedDescription">
  <meta property="og:url" content="$pageUrl">
  <meta property="og:image" content="$imageUrl">
  <meta property="og:image:width" content="1672">
  <meta property="og:image:height" content="941">
  <meta property="og:image:alt" content="C# 设计模式学习地图">
  <meta name="twitter:card" content="summary_large_image">
  <meta name="twitter:title" content="$encodedTitle">
  <meta name="twitter:description" content="$encodedDescription">
  <meta name="twitter:image" content="$imageUrl">
  <link rel="canonical" href="$pageUrl">
  <link rel="icon" href="../assets/favicon.svg" type="image/svg+xml">
  <script src="../assets/theme.js"></script>
  <link rel="stylesheet" href="../assets/notebook.css">
  <link rel="stylesheet" href="../assets/guide.css">
  $jsonLd
"@

  $guideId = [IO.Path]::GetFileNameWithoutExtension($page.Output)
  $headingMatches = [regex]::Matches(
    $html,
    '<h(?<level>[23]) id="(?<id>[^"]+)">(?<text>.*?)</h[23]>',
    [Text.RegularExpressions.RegexOptions]::Singleline)
  $headingEntries = @($headingMatches | ForEach-Object {
      $plainText = [Net.WebUtility]::HtmlDecode([regex]::Replace($_.Groups['text'].Value, '<[^>]+>', '')).Trim()
      [pscustomobject]@{
        Level = [int]$_.Groups['level'].Value
        Id = $_.Groups['id'].Value
        Text = $plainText
      }
    })
  $chapters = @($headingEntries | Where-Object Level -eq 2)
  $toc = ''
  $layoutClass = 'learning-layout without-toc'
  if ($chapters.Count -gt 0) {
    $layoutClass = 'learning-layout'
    $tocBuilder = [Text.StringBuilder]::new('<ol class="toc-chapters">')
    $chapterOpen = $false
    $sectionsOpen = $false
    foreach ($heading in $headingEntries) {
      $id = Encode-Html $heading.Id
      $text = Encode-Html $heading.Text
      if ($heading.Level -eq 2) {
        if ($sectionsOpen) { [void]$tocBuilder.Append('</ol>'); $sectionsOpen = $false }
        if ($chapterOpen) { [void]$tocBuilder.Append('</li>') }
        [void]$tocBuilder.Append('<li class="depth-1"><a href="#' + $id + '" data-heading-id="' + $id + '" data-chapter="true">' + $text + '</a>')
        $chapterOpen = $true
      } else {
        if (-not $chapterOpen) { continue }
        if (-not $sectionsOpen) { [void]$tocBuilder.Append('<ol class="toc-sections">'); $sectionsOpen = $true }
        [void]$tocBuilder.Append('<li class="depth-2"><a href="#' + $id + '" data-heading-id="' + $id + '" data-chapter="false">' + $text + '</a></li>')
      }
    }
    if ($sectionsOpen) { [void]$tocBuilder.Append('</ol>') }
    if ($chapterOpen) { [void]$tocBuilder.Append('</li>') }
    [void]$tocBuilder.Append('</ol>')
    $tocItems = $tocBuilder.ToString()
    $toc = @"
<aside class="guide-toc" aria-label="本页学习导航">
  <div class="guide-toc-head"><p class="guide-toc-title">本页目录</p><button class="guide-toc-toggle" type="button" aria-expanded="false" aria-controls="guide-toc-list">展开目录</button></div>
  <nav class="guide-toc-list" id="guide-toc-list" aria-label="本页目录">$tocItems</nav>
  <a class="guide-continue" data-guide-continue hidden></a>
  <nav class="guide-chapter-nav" aria-label="章节导航"><a data-guide-prev hidden></a><a data-guide-next hidden></a></nav>
  <a class="guide-top-link" href="#main">返回顶部</a>
</aside>
"@
  }

  $currentLearning = if ($page.Output -eq 'learning-path.html') { ' aria-current="page"' } else { '' }
  $currentPatterns = if ($page.Output -in @('pattern-index.html', 'fundamentals.html')) { ' aria-current="page"' } else { '' }
  $currentProjects = if ($page.Output -in @('projects.html', 'practice.html', 'online-store.html', 'smart-home.html', 'document-workflow.html')) { ' aria-current="page"' } else { '' }
  $currentLabs = if ($page.Output -in @('labs.html', 'refactoring.html', 'reliable-checkout.html')) { ' aria-current="page"' } else { '' }
  $learningItem = @($learningCatalog.learningItems | Where-Object url -eq ('guides/' + $page.Output) | Select-Object -First 1)
  $learningAttribute = ''
  $milestonePanel = ''
  if ($learningItem.Count -eq 1) {
    $item = $learningItem[0]
    $learningAttribute = ' data-learning-item-id="' + (Encode-Html $item.id) + '"'
    $milestoneCards = @($item.milestones | ForEach-Object {
        $milestone = $_
        $commandHtml = if ($milestone.PSObject.Properties.Name -contains 'command') {
          '<div class="guide-milestone-command"><code tabindex="0">' + (Encode-Html $milestone.command) + '</code><button type="button" data-copy-command="' + (Encode-Html $milestone.command) + '">复制命令</button></div>'
        } else { '' }
        '<article class="guide-milestone nb-entry"><div class="guide-milestone-copy"><strong>' + (Encode-Html $milestone.title) + '</strong><p>' +
          (Encode-Html $milestone.task) + '</p>' + $commandHtml + '</div><label class="guide-milestone-check"><input type="checkbox" data-progress-task="' +
          (Encode-Html $milestone.id) + '"><span>我已完成：' + (Encode-Html $milestone.title) + '</span></label></article>'
      }) -join ''
    $milestonePanel = '<section class="guide-milestones" aria-labelledby="guide-milestones-title"><div><h2 id="guide-milestones-title">用可验证里程碑推进</h2><span id="guide-milestone-summary">0 / ' + $item.milestones.Count + ' 已完成</span></div><div class="guide-milestone-list">' + $milestoneCards + '</div><p class="guide-milestone-note">里程碑必须按顺序完成；记录会与首页的课程进度同步。</p></section>'
  }
  $header = @"
<header class="learning-site-header nb-head">
  <a class="learning-site-brand nb-brand" href="../index.html">C# 设计模式</a>
  <button class="learning-nav-toggle nb-nav-toggle" type="button" aria-expanded="false" aria-controls="learning-site-nav">菜单</button>
  <nav class="learning-site-nav nb-nav nb-nav--collapsible" id="learning-site-nav" aria-label="课程导航"><a href="pattern-index.html"$currentPatterns>模式</a><a href="learning-path.html"$currentLearning>路线</a><a href="../playground.html">编码练习</a><a href="../quiz.html">辨析训练</a><a href="projects.html"$currentProjects>项目</a><a href="labs.html"$currentLabs>实验</a><a href="../index.html#site-search">搜索</a><a href="$repositoryUrl">GitHub</a><a href="https://yuweiyang9611.github.io/">全部项目</a></nav>
</header>
"@
  $footer = '<footer class="learning-site-footer">内容来自同一 GitHub 仓库并随主分支自动更新 · <a href="../index.html">返回学习地图</a></footer>'

  $html = $html.Replace('</head>', $metadata + '</head>')
  $html = $html.Replace('<body><main>', '<body data-guide-id="' + $guideId + '"' + $learningAttribute + '><a class="skip-link" href="#main">跳到正文</a>' + $header + '<div class="' + $layoutClass + '">' + $toc + '<main id="main">')
  $html = [regex]::Replace($html, '<h1\b[^>]*>.*?</h1>', {
      param($match)
      [regex]::Replace($match.Value, '(\d) (?=\p{IsCJKUnifiedIdeographs})', '$1&nbsp;')
    }, [Text.RegularExpressions.RegexOptions]::Singleline)
  if ($milestonePanel) {
    # After the title, so a guide opens with what it is before its checklist.
    if ($html.Contains('</h1>')) { $html = ([regex]'</h1>').Replace($html, '</h1>' + $milestonePanel.Replace('$', '$$'), 1) }
    else { $html = $html.Replace('<main id="main">', '<main id="main">' + $milestonePanel) }
  }
  $html = $html.Replace('</head>', '<link rel="stylesheet" href="../assets/storage-status.css"></head>')
  $html = $html.Replace('</main></body>', '</main></div>' + $footer + '<p class="guide-announcement" id="guide-announcement" aria-live="polite"></p><script src="../assets/storage-status.js" defer></script><script src="../assets/progress.js" defer></script><script src="../assets/catalog.js" defer></script><script src="../assets/guide.js" defer></script><script src="../assets/code-colors.js" defer></script></body>')
  [IO.File]::WriteAllText($outputPath, $html, [Text.UTF8Encoding]::new($false))
}

$categoryLabels = @{ Creational = '创建型'; Structural = '结构型'; Behavioral = '行为型' }
$categoryHues = @{ Creational = 'blue'; Structural = 'purple'; Behavioral = 'cyan' }
$tagById = @{}
foreach ($tag in $learningCatalog.problemTags) { $tagById[$tag.id] = $tag }
for ($index = 0; $index -lt $patterns.Count; $index++) {
  $pattern = $patterns[$index]
  $pageUrl = $pattern.pageUrl
  $title = "$($pattern.english) / $($pattern.chinese)模式 · C# 设计模式学习地图"
  $description = $pattern.intent
  $imageUrl = $pagesBase + 'assets/og.jpg'
  $tagsHtml = @($pattern.problemTags | ForEach-Object {
      $tag = $tagById[$_]
      '<a href="../index.html?problems=' + [Uri]::EscapeDataString($_) + '#patterns">' + (Encode-Html $tag.label) + '</a>'
    }) -join ''
  $relatedHtml = @($pattern.related | ForEach-Object {
      $related = $patterns | Where-Object key -eq $_ | Select-Object -First 1
      if ($null -ne $related) {
        '<li><a href="' + $related.key + '.html"><strong>' + (Encode-Html $related.english) + '</strong><span>' + (Encode-Html $related.chinese) + '模式</span></a></li>'
      }
    }) -join ''
  $previous = if ($index -gt 0) { $patterns[$index - 1] } else { $null }
  $next = if ($index -lt $patterns.Count - 1) { $patterns[$index + 1] } else { $null }
  $previousHtml = if ($null -ne $previous) { '<a href="' + $previous.key + '.html">上一个：' + (Encode-Html $previous.english) + '</a>' } else { '<a href="../index.html#patterns">返回模式地图</a>' }
  $nextHtml = if ($null -ne $next) { '<a href="' + $next.key + '.html">下一个：' + (Encode-Html $next.english) + '</a>' } else { '<a href="../guides/practice.html">进入组合项目</a>' }
  $jsonLd = Get-JsonLdScript -Value ([ordered]@{
      '@context' = 'https://schema.org'
      '@type' = 'LearningResource'
      name = "$($pattern.english) / $($pattern.chinese)模式"
      description = $description
      url = $pageUrl
      inLanguage = 'zh-CN'
      isAccessibleForFree = $true
      learningResourceType = 'Pattern lesson'
      teaches = @($pattern.intent, $pattern.changeAxis)
      isPartOf = [ordered]@{ '@type' = 'Course'; name = 'C# 设计模式学习地图'; url = $pagesBase }
    })
  $evidenceCardsHtml = @($pattern.evidenceCards | ForEach-Object {
      $card = $_
      $commandHtml = ''
      if ($card.PSObject.Properties.Name -contains 'command') {
        $encodedCommand = Encode-Html $card.command
        $commandHtml = '<div class="evidence-command"><code tabindex="0">' + $encodedCommand + '</code><button type="button" data-copy-command="' + $encodedCommand + '">复制命令</button></div>'
        if ($card.kind -eq 'run') { $commandHtml += '<p class="evidence-run-here"><button type="button" class="nb-button" data-run-demo>或者在旁边的面板里直接运行</button></p>' }
      }
      $expectedHtml = ''
      if ($card.PSObject.Properties.Name -contains 'expectedOutput') {
        $outputText = Encode-Html (@($card.expectedOutput) -join "`n")
        $expectedHtml = '<details class="expected-output"><summary>查看真实预期输出</summary><pre>' + $outputText + '</pre></details>'
      }
      $targetHtml = ''
      if ($card.PSObject.Properties.Name -contains 'target') {
        $targetHtml = '<p class="evidence-target">修改入口：<a href="#source-panel" data-source-tab="' + (Encode-Html $pattern.source) + '"><code>' + (Encode-Html $card.target) + '</code></a>，在面板里点「改一改再跑」就能试。</p>'
      }
      $acceptanceHtml = ''
      if ($card.PSObject.Properties.Name -contains 'acceptance') {
        $items = @($card.acceptance | ForEach-Object { '<li>' + (Encode-Html $_) + '</li>' }) -join ''
        $acceptanceHtml = '<ul class="evidence-acceptance">' + $items + '</ul>'
      }
      @"
<article class="evidence-card nb-entry" id="evidence-$($card.id)" data-evidence-card="$($card.id)">

  <h3 id="$($pattern.key)-evidence-$($card.id)-title">$(Encode-Html $card.title)</h3>
  <p>$(Encode-Html $card.task)</p>
  $targetHtml$commandHtml$expectedHtml$acceptanceHtml
  <label class="evidence-complete"><input type="checkbox" data-progress-task="$($card.id)" aria-labelledby="$($pattern.key)-evidence-$($card.id)-title"><span>我已完成：$(Encode-Html $card.title)</span></label>
</article>
"@
    }) -join "`n"
  $sourceFile = [IO.Path]::GetFileName($pattern.source)
  $practiceFile = [IO.Path]::GetFileName($pattern.practice)
  $demoType = 'DesignPatterns.' + $pattern.category + '.' + [IO.Path]::GetFileNameWithoutExtension($pattern.source)
  $practicePageUrl = '../source.html?path=' + [Uri]::EscapeDataString($pattern.practice)
  $firstTag = $tagById[[string]$pattern.problemTags[0]]
  $sourceConfig = ([ordered]@{
      base = '../assets/source/'
      blobBase = $blobBase
      demo = [ordered]@{ path = $pattern.source; type = $demoType }
      files = @(
        [ordered]@{ path = $pattern.source; label = $sourceFile },
        [ordered]@{ path = 'src/DesignPatterns/IPatternDemo.cs'; label = 'IPatternDemo.cs' },
        [ordered]@{ path = $pattern.practice; label = $practiceFile }
      )
    } | ConvertTo-Json -Depth 6 -Compress).Replace('</', '<\/')
  $patternHtml = @"
<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="description" content="$(Encode-Html $description)">
  <meta property="og:type" content="article">
  <meta property="og:site_name" content="C# 设计模式学习地图">
  <meta property="og:locale" content="zh_CN">
  <meta property="og:title" content="$(Encode-Html $title)">
  <meta property="og:description" content="$(Encode-Html $description)">
  <meta property="og:url" content="$pageUrl">
  <meta property="og:image" content="$imageUrl">
  <meta property="og:image:width" content="1672">
  <meta property="og:image:height" content="941">
  <meta property="og:image:alt" content="C# 设计模式学习地图">
  <meta name="twitter:card" content="summary_large_image">
  <meta name="twitter:title" content="$(Encode-Html $title)">
  <meta name="twitter:description" content="$(Encode-Html $description)">
  <meta name="twitter:image" content="$imageUrl">
  <link rel="canonical" href="$pageUrl">
  <link rel="icon" href="../assets/favicon.svg" type="image/svg+xml">
  <script src="../assets/theme.js"></script>
  <link rel="stylesheet" href="../assets/notebook.css">
  <link rel="stylesheet" href="../assets/pattern.css">
  <title>$(Encode-Html $title)</title>
  $jsonLd
  <link rel="stylesheet" href="../assets/storage-status.css">
  <script src="../assets/storage-status.js" defer></script>
  <script src="../assets/progress.js" defer></script>
  <script src="../assets/review.js" defer></script>
  <script src="../assets/notes.js" defer></script>
  <script src="../assets/catalog.js" defer></script>
  <script src="../assets/code-colors.js" defer></script>
  <script src="../assets/source-panel.js" defer></script>
  <script src="../assets/demo-runner.js" defer></script>
  <script src="../assets/lesson.js" defer></script>
  <script src="../assets/lesson-source.js" defer></script>
  <script src="../assets/lesson-quiz.js" defer></script>
  <script src="../assets/lesson-notes.js" defer></script>
</head>
<body data-pattern-key="$($pattern.key)">
  <a class="skip-link" href="#main">跳到正文</a>
  <header class="lesson-header nb-head">
    <a class="lesson-brand nb-brand" href="../index.html">C# 设计模式</a>
    <button class="lesson-nav-toggle nb-nav-toggle" id="lesson-nav-toggle" type="button" aria-expanded="false" aria-controls="lesson-nav">菜单</button>
    <nav class="lesson-nav nb-nav nb-nav--collapsible" id="lesson-nav" aria-label="课程导航"><a href="../index.html#patterns" aria-current="page">模式</a><a href="../guides/learning-path.html">路线</a><a href="../playground.html">编码练习</a><a href="../quiz.html">辨析训练</a><a href="../guides/projects.html">项目</a><a href="../guides/labs.html">实验</a><a href="../source.html">源码</a><a href="../index.html#site-search">搜索</a><a href="$repositoryUrl">GitHub</a><a href="https://yuweiyang9611.github.io/">全部项目</a></nav>
  </header>
  <main class="lesson-main" id="main">
    <p class="lesson-breadcrumb"><a href="../index.html#patterns">模式地图</a> / $(Encode-Html $categoryLabels[$pattern.category]) / $(Encode-Html $pattern.english)</p>
    <header class="lesson-band" data-hue="$($categoryHues[$pattern.category])">
      <div>
        <p class="lesson-band-eyebrow">$(Encode-Html $categoryLabels[$pattern.category]) · $(('{0:00}' -f $pattern.number)) · $(Encode-Html $firstTag.label)</p>
        <h1>$(Encode-Html $pattern.english)<span class="lesson-chinese">$(Encode-Html $pattern.chinese)模式</span></h1>
      </div>
      <div class="lesson-progress"><p>证据 <strong id="lesson-progress-count">0 / 4</strong> · <span id="lesson-progress-summary">全课程 0% · 0 / $itemCount 已验证</span></p><div class="lesson-progress-track nb-bar nb-bar--on-fill" role="progressbar" aria-label="$(Encode-Html $pattern.english) 证据进度" aria-valuemin="0" aria-valuemax="4" aria-valuenow="0"><span id="lesson-progress-bar"></span></div></div>
      <ol class="nb-steps nb-steps--on-fill lesson-steps" id="lesson-steps" aria-label="这一课的五步">
        <li data-step="read" data-state="pending"><a href="#evidence-read">读</a></li>
        <li data-step="run" data-state="pending"><a href="#evidence-run">跑</a></li>
        <li data-step="change" data-state="pending"><a href="#evidence-change">改</a></li>
        <li data-step="verify" data-state="pending"><a href="#evidence-verify">验证</a></li>
        <li data-step="quiz" data-state="pending"><a href="#lesson-quiz">自测</a><span class="nb-steps-count"></span></li>
      </ol>
    </header>
    <p class="lesson-intent">$(Encode-Html $pattern.intent)</p>
    <div class="lesson-columns">
      <div class="lesson-read">
        <section class="lesson-panel"><h2>它解决什么变化</h2><p>$(Encode-Html $pattern.changeAxis)</p><div class="problem-tags" role="group" aria-label="适用问题">$tagsHtml</div><h3>示例场景</h3><p class="scenario">$(Encode-Html $pattern.scenario)</p></section>
        <section class="lesson-panel" data-hue="cyan"><h2>看代码、跑一遍</h2><p>旁边是这个模式的独立 Demo，和仓库同一版本：先预测输出，点「运行」在浏览器里跑，再「改一改再跑」看哪一行变了。实战落点在第三个页签。</p><div class="lesson-links"><a href="#source-panel" data-source-tab="$($pattern.source)">独立 Demo 源码</a><a href="$practicePageUrl">实战项目落点</a><a href="../guides/fundamentals.html$($pattern.guide)">完整教程章节</a></div></section>
        <section class="evidence-section" aria-labelledby="evidence-title"><div class="evidence-heading"><h2 id="evidence-title">用四项证据完成这个模式</h2><p>按顺序完成；取消前一项会同时撤销后续证据。</p></div><div class="evidence-grid">$evidenceCardsHtml</div></section>
        <section class="lesson-panel"><h2>何时使用</h2><p>$(Encode-Html $pattern.whenUse)</p></section>
        <section class="lesson-panel" data-hue="red"><h2>何时避免</h2><p>$(Encode-Html $pattern.avoidWhen)</p></section>
        <section class="lesson-quiz" id="lesson-quiz" data-hue="cyan" aria-labelledby="lesson-quiz-title"><div class="evidence-heading"><h2 id="lesson-quiz-title">自测</h2><p id="lesson-quiz-summary">辨析题载入中</p></div><div id="lesson-quiz-list"></div><noscript><p class="lesson-quiz-empty">自测需要 JavaScript；可以去 <a href="../quiz.html">辨析训练</a>。</p></noscript></section>
        <section class="lesson-notes" id="lesson-notes" aria-labelledby="lesson-notes-title"><h2 id="lesson-notes-title">记一笔</h2><label class="sr-only" for="lesson-note">这一课的笔记</label><textarea id="lesson-note" rows="4" placeholder="这个模式让你想到什么、和哪个模式容易混、下次要试什么。"></textarea><p class="lesson-note-status" id="lesson-note-status">只保存在这个浏览器里。</p></section>
        <section class="lesson-panel"><h2>相似模式对比</h2><ul class="related-list">$relatedHtml</ul></section>
      </div>
      <aside class="lesson-code" aria-labelledby="lesson-code-title">
        <p class="lesson-code-title" id="lesson-code-title">源码<span>$(Encode-Html $sourceFile) · 与仓库同一版本</span></p>
        <div class="nb-code" id="source-panel"><div class="nb-code-tabs"><span class="nb-code-tab" aria-selected="true">$(Encode-Html $sourceFile)</span><span class="nb-code-tab">IPatternDemo.cs</span><span class="nb-code-tab">$(Encode-Html $practiceFile)</span></div><pre class="nb-code-view"><code><noscript>看代码需要 JavaScript。源文件在 GitHub：<a href="$($pattern.sourceUrl)">$(Encode-Html $pattern.source)</a>、<a href="$($pattern.practiceUrl)">$(Encode-Html $pattern.practice)</a>。</noscript></code></pre></div>
        <script type="application/json" id="lesson-source">$sourceConfig</script>
      </aside>
    </div>
    <nav class="lesson-pager" aria-label="模式课件导航">$previousHtml$nextHtml</nav>
  </main>
  <footer class="lesson-footer">完成阶段后回到 <a href="../index.html#patterns">学习地图</a> 查看全课程进度。</footer>
  <p class="sr-only" id="lesson-announcement" aria-live="polite"></p>
</body>
</html>
"@
  [IO.File]::WriteAllText((Join-Path $patternDirectory ($pattern.key + '.html')), $patternHtml, [Text.UTF8Encoding]::new($false))
}

$homePath = Join-Path $stageDirectory 'index.html'
$homeHtml = Get-Content -LiteralPath $homePath -Raw -Encoding utf8
# Without JavaScript there is no record to read, so the fallback card has no ring.
$fallbackCards = @($patterns | ForEach-Object {
    $pattern = $_
    @"
<article class="pattern-card nb-card nb-entry" data-hue="$($categoryHues[$pattern.category])">
  <div class="nb-card-head"><div><p class="nb-card-eyebrow"><span class="category-badge">$(Encode-Html $categoryLabels[$pattern.category])</span> · <span class="pattern-number">$(('{0:00}' -f $pattern.number))</span></p><h3><a href="patterns/$($pattern.key).html">$(Encode-Html $pattern.english)</a><span class="pattern-chinese">$(Encode-Html $pattern.chinese)</span></h3></div></div>
  <div class="nb-card-body"><p class="pattern-intent">$(Encode-Html $pattern.intent)</p><div class="nb-card-meta"><a class="pattern-open" href="patterns/$($pattern.key).html">打开课件</a></div></div>
</article>
"@
  }) -join "`n"
$courseJsonLd = Get-JsonLdScript -Value ([ordered]@{
    '@context' = 'https://schema.org'
    '@type' = 'Course'
    name = 'C# 设计模式学习地图'
    description = '用现代 C# 15 / .NET 11 跑通 GoF 23 种设计模式，从独立示例走向真实项目与生产可靠性。'
    url = $pagesBase
    inLanguage = 'zh-CN'
    isAccessibleForFree = $true
    provider = [ordered]@{ '@type' = 'Person'; name = $repositoryOwner; url = "https://github.com/$repositoryOwner" }
    hasCourseInstance = [ordered]@{ '@type' = 'CourseInstance'; courseMode = 'online' }
  })
$homeHtml = Expand-Counts $homeHtml
$homeHtml = $homeHtml.Replace('{{PAGES_BASE}}', $pagesBase)
$homeHtml = $homeHtml.Replace('{{REPOSITORY_URL}}', $repositoryUrl)
$homeHtml = $homeHtml.Replace('{{PATTERN_FALLBACK}}', $fallbackCards)
$homeHtml = $homeHtml.Replace('https://github.com/yuweiyang9611/CSharp_Implementation_of_DesignPatterns', $repositoryUrl)
$homeHtml = $homeHtml.Replace('</head>', '  ' + $courseJsonLd + "`n</head>")
[IO.File]::WriteAllText($homePath, $homeHtml, [Text.UTF8Encoding]::new($false))

$quizPath = Join-Path $stageDirectory 'quiz.html'
$quizHtml = Get-Content -LiteralPath $quizPath -Raw -Encoding utf8
$quizUrl = $pagesBase + 'quiz.html'
$quizJsonLd = Get-JsonLdScript -Value ([ordered]@{
    '@context' = 'https://schema.org'
    '@type' = 'LearningResource'
    name = '设计模式辨析训练'
    description = '用业务场景辨析设计模式和普通重构方案，并通过本地间隔复习巩固决策规则。'
    url = $quizUrl
    inLanguage = 'zh-CN'
    isAccessibleForFree = $true
    learningResourceType = 'Quiz'
    isPartOf = [ordered]@{ '@type' = 'Course'; name = 'C# 设计模式学习地图'; url = $pagesBase }
  })
$quizHtml = Expand-Counts $quizHtml
$quizHtml = $quizHtml.Replace('{{PAGES_BASE}}', $pagesBase)
$quizHtml = $quizHtml.Replace('{{REPOSITORY_URL}}', $repositoryUrl)
$quizHtml = $quizHtml.Replace('</head>', '  ' + $quizJsonLd + "`n</head>")
[IO.File]::WriteAllText($quizPath, $quizHtml, [Text.UTF8Encoding]::new($false))

$playgroundUrl = $pagesBase + 'playground.html'
$playgroundPath = Join-Path $stageDirectory 'playground.html'
$playgroundHtml = Expand-Counts (Get-Content -LiteralPath $playgroundPath -Raw -Encoding utf8)
$playgroundJsonLd = Get-JsonLdScript -Value (@{
  '@context' = 'https://schema.org'; '@type' = 'LearningResource'; name = '浏览器 C# 编码练习'; url = $playgroundUrl
  inLanguage = 'zh-CN'; isAccessibleForFree = $true; learningResourceType = 'Exercise'
})
$playgroundHtml = $playgroundHtml.Replace('{{PAGES_BASE}}', $pagesBase).Replace('{{REPOSITORY_URL}}', $repositoryUrl).Replace('</head>', $playgroundJsonLd + '</head>')
[IO.File]::WriteAllText($playgroundPath, $playgroundHtml, [Text.UTF8Encoding]::new($false))

$sourceUrl = $pagesBase + 'source.html'
$sourcePagePath = Join-Path $stageDirectory 'source.html'
$sourcePageHtml = Get-Content -LiteralPath $sourcePagePath -Raw -Encoding utf8
$sourceJsonLd = Get-JsonLdScript -Value (@{
  '@context' = 'https://schema.org'; '@type' = 'LearningResource'; name = '课程源码'; url = $sourceUrl
  inLanguage = 'zh-CN'; isAccessibleForFree = $true; learningResourceType = 'Source'
})
$sourcePageHtml = $sourcePageHtml.Replace('{{PAGES_BASE}}', $pagesBase).Replace('{{REPOSITORY_URL}}', $repositoryUrl).Replace('</head>', $sourceJsonLd + '</head>')
[IO.File]::WriteAllText($sourcePagePath, $sourcePageHtml, [Text.UTF8Encoding]::new($false))

& (Join-Path $PSScriptRoot 'new-pages-search-index.ps1') -SiteDirectory $stageDirectory

$allUrls = @($pagesBase) +
  @($quizUrl, $playgroundUrl, $sourceUrl) +
  @($pages | ForEach-Object { $pagesBase + 'guides/' + $_.Output }) +
  @($patterns | ForEach-Object { $_.pageUrl })
$sitemapItems = $allUrls | ForEach-Object { '  <url><loc>' + [Security.SecurityElement]::Escape($_) + '</loc></url>' }
$sitemap = "<?xml version=`"1.0`" encoding=`"UTF-8`"?>`n<urlset xmlns=`"http://www.sitemaps.org/schemas/sitemap/0.9`">`n$($sitemapItems -join "`n")`n</urlset>`n"
[IO.File]::WriteAllText((Join-Path $stageDirectory 'sitemap.xml'), $sitemap, [Text.UTF8Encoding]::new($false))
$robots = "User-agent: *`nAllow: /`nSitemap: $($pagesBase)sitemap.xml`n"
[IO.File]::WriteAllText((Join-Path $stageDirectory 'robots.txt'), $robots, [Text.UTF8Encoding]::new($false))

$revision = if ($env:GITHUB_SHA -match '^[0-9a-fA-F]{40}$') {
  $env:GITHUB_SHA.ToLowerInvariant()
} else {
  $gitRevision = & git -C $root rev-parse HEAD
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitRevision)) {
    throw 'Unable to determine the Git revision for version.json.'
  }
  ([string]$gitRevision).Trim().ToLowerInvariant()
}
$version = [ordered]@{ commit = $revision } | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $stageDirectory 'version.json'), $version, [Text.UTF8Encoding]::new($false))

& (Join-Path $PSScriptRoot 'verify-pages-site.ps1') `
  -SiteDirectory $stageDirectory `
  -RepositoryRoot $root `
  -ExpectedPagesBase $pagesBase

Write-Host "GitHub Pages site generated: 1 learning dashboard, $($pages.Count) guides, $($patterns.Count) pattern lessons, $($bundledFiles.Count) source files read in place."
Write-Host "Validated by verify-pages-site.ps1: $($allUrls.Count) canonical sitemap URLs."
Write-Host "Output: $stageDirectory"

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
$defaultBlobBase = 'https://github.com/yuweiyang9611/CSharp_Implementation_of_DesignPatterns/blob/main/'
$defaultPagesBase = 'https://yuweiyang9611.github.io/CSharp_Implementation_of_DesignPatterns/'

function Decode-Name {
  param([Parameter(Mandatory)][string]$Base64)

  return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Base64))
}

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

$pages = @(
  [pscustomobject]@{ Input = 'README.md'; Output = 'repository-overview.html'; Description = 'C# 设计模式学习项目的课程结构、运行方式与仓库说明。'; Type = 'Guide' },
  [pscustomobject]@{ Input = 'START_HERE.md'; Output = 'learning-path.html'; Description = '从 30 分钟到 14 周的 C# 设计模式学习路线。'; Type = 'Learning Path' },
  [pscustomobject]@{ Input = 'docs/' + (Decode-Name '5qih5byP57Si5byVLm1k'); Output = 'pattern-index.html'; Description = 'GoF 23 种设计模式的 Runner key、源码、实战落点与教程索引。'; Type = 'Reference' },
  [pscustomobject]@{ Input = 'docs/' + (Decode-Name 'Q1NoYXJw6K6+6K6h5qih5byP5a2m5Lmg5oyH5Y2XLm1k'); Output = 'fundamentals.html'; Description = 'GoF 23 种设计模式的现代 C# 实现、意图、角色、取舍与练习。'; Type = 'Guide' },
  [pscustomobject]@{ Input = 'docs/' + (Decode-Name '6K6+6K6h5qih5byP5a6e5oiY6aG555uu5a2m5Lmg5oyH5Y2XLm1k'); Output = 'practice.html'; Description = 'OnlineStore、SmartHome 与 DocumentWorkflow 的设计模式组合实战指南。'; Type = 'Guide' },
  [pscustomobject]@{ Input = 'examples/README.md'; Output = 'projects.html'; Description = '三个教学项目的模式覆盖、运行方式与建议学习顺序。'; Type = 'Reference' },
  [pscustomobject]@{ Input = 'examples/OnlineStore/README.md'; Output = 'online-store.html'; Description = '用电商结算、支付与订单生命周期学习七种设计模式。'; Type = 'Project Guide' },
  [pscustomobject]@{ Input = 'examples/SmartHome/README.md'; Output = 'smart-home.html'; Description = '用智能家居设备接入、联动、撤销与恢复学习八种设计模式。'; Type = 'Project Guide' },
  [pscustomobject]@{ Input = 'examples/DocumentWorkflow/README.md'; Output = 'document-workflow.html'; Description = '用报表筛选、合规检查与多渠道发布学习八种设计模式。'; Type = 'Project Guide' },
  [pscustomobject]@{ Input = 'labs/README.md'; Output = 'labs.html'; Description = '从模式组合继续走向安全重构与生产可靠性的高级实验地图。'; Type = 'Lab Index' },
  [pscustomobject]@{ Input = 'labs/CheckoutRefactoringKata/README.md'; Output = 'refactoring.html'; Description = '从坏代码经特征测试逐步重构出 Strategy、Chain、State 与 Facade。'; Type = 'Lab' },
  [pscustomobject]@{ Input = 'labs/ReliableCheckout/README.md'; Output = 'reliable-checkout.html'; Description = '用 HTTP、SQLite、幂等、Outbox 与重试保护结账业务不变量。'; Type = 'Lab' }
)

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
$learningCatalog = Get-Content -LiteralPath $learningCatalogPath -Raw -Encoding utf8 | ConvertFrom-Json
$enrichmentByKey = @{}
foreach ($entry in $learningCatalog.patterns) {
  if ($enrichmentByKey.ContainsKey($entry.key)) { throw "Duplicate learning catalog key: $($entry.key)" }
  $enrichmentByKey[$entry.key] = $entry
}

$patterns = [Collections.Generic.List[object]]::new()
foreach ($core in $coreCatalog) {
  if (-not $enrichmentByKey.ContainsKey($core.key)) { throw "Missing learning catalog entry: $($core.key)" }
  $extra = $enrichmentByKey[$core.key]
  $nameParts = @($core.name -split '\s*/\s*', 2)
  $english = $nameParts[0].Trim()
  $chinese = if ($nameParts.Count -gt 1) { ($nameParts[1].Trim() -replace '模式$', '') } else { $english }
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
      guide = [string]$extra.guide
      problemTags = @($extra.problemTags)
      changeAxis = [string]$extra.changeAxis
      whenUse = [string]$extra.whenUse
      avoidWhen = [string]$extra.avoidWhen
      related = @($extra.related)
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
}
$publishedCatalogJson = $publishedCatalog | ConvertTo-Json -Depth 12

if (Test-Path -LiteralPath $stageDirectory) {
  Remove-Item -LiteralPath $stageDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDirectory, $guideDirectory, $patternDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $siteSource 'index.html') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $siteSource 'assets') -Destination $stageDirectory -Recurse

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
    $tocItems = $headingEntries | ForEach-Object {
      $depth = if ($_.Level -eq 2) { 1 } else { 2 }
      $chapter = if ($_.Level -eq 2) { 'true' } else { 'false' }
      '<li class="depth-' + $depth + '"><a href="#' + (Encode-Html $_.Id) + '" data-heading-id="' +
        (Encode-Html $_.Id) + '" data-chapter="' + $chapter + '">' + (Encode-Html $_.Text) + '</a></li>'
    }
    $toc = @"
<aside class="guide-toc" aria-label="本页学习导航">
  <div class="guide-toc-head"><p class="guide-toc-title">本页目录</p><button class="guide-toc-toggle" type="button" aria-expanded="false" aria-controls="guide-toc-list">展开目录</button></div>
  <nav class="guide-toc-list" id="guide-toc-list" aria-label="本页目录"><ol>$($tocItems -join '')</ol></nav>
  <a class="guide-continue" data-guide-continue hidden></a>
  <nav class="guide-chapter-nav" aria-label="章节导航"><a data-guide-prev hidden></a><a data-guide-next hidden></a></nav>
  <a class="guide-top-link" href="#main">返回顶部 ↑</a>
</aside>
"@
  }

  $currentLearning = if ($page.Output -eq 'learning-path.html') { ' aria-current="page"' } else { '' }
  $currentPatterns = if ($page.Output -in @('pattern-index.html', 'fundamentals.html')) { ' aria-current="page"' } else { '' }
  $currentProjects = if ($page.Output -in @('projects.html', 'practice.html', 'online-store.html', 'smart-home.html', 'document-workflow.html')) { ' aria-current="page"' } else { '' }
  $currentLabs = if ($page.Output -in @('labs.html', 'refactoring.html', 'reliable-checkout.html')) { ' aria-current="page"' } else { '' }
  $header = @"
<header class="learning-site-header">
  <a class="learning-site-brand" href="../index.html"><span class="learning-site-mark" aria-hidden="true">{ }</span><span>C# 设计模式学习地图</span></a>
  <button class="learning-nav-toggle" type="button" aria-expanded="false" aria-controls="learning-site-nav">菜单</button>
  <nav class="learning-site-nav" id="learning-site-nav" aria-label="课程导航"><a href="learning-path.html"$currentLearning>学习路线</a><a href="pattern-index.html"$currentPatterns>23 种模式</a><a href="projects.html"$currentProjects>实战项目</a><a href="labs.html"$currentLabs>高级实验</a><a href="../index.html">返回首页</a></nav>
</header>
"@
  $footer = '<footer class="learning-site-footer">内容来自同一 GitHub 仓库并随主分支自动更新 · <a href="../index.html">返回学习地图</a></footer>'

  $html = $html.Replace('</head>', $metadata + '</head>')
  $html = $html.Replace('<body><main>', '<body data-guide-id="' + $guideId + '"><a class="skip-link" href="#main">跳到正文</a>' + $header + '<div class="' + $layoutClass + '">' + $toc + '<main id="main">')
  $html = $html.Replace('</main></body>', '</main></div>' + $footer + '<script src="../assets/guide.js" defer></script></body>')
  [IO.File]::WriteAllText($outputPath, $html, [Text.UTF8Encoding]::new($false))
}

$categoryLabels = @{ Creational = '创建型'; Structural = '结构型'; Behavioral = '行为型' }
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
  $previousHtml = if ($null -ne $previous) { '<a href="' + $previous.key + '.html">← ' + (Encode-Html $previous.english) + '</a>' } else { '<a href="../index.html#patterns">← 返回模式地图</a>' }
  $nextHtml = if ($null -ne $next) { '<a href="' + $next.key + '.html">' + (Encode-Html $next.english) + ' →</a>' } else { '<a href="../guides/practice.html">进入组合项目 →</a>' }
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
  $command = 'dotnet run --project src/DesignPatterns.Runner -- ' + $pattern.key
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
  <link rel="stylesheet" href="../assets/pattern.css">
  <title>$(Encode-Html $title)</title>
  $jsonLd
  <script src="../assets/progress.js" defer></script>
  <script src="../assets/catalog.js" defer></script>
  <script src="../assets/lesson.js" defer></script>
</head>
<body data-pattern-key="$($pattern.key)">
  <a class="skip-link" href="#main">跳到正文</a>
  <header class="lesson-header">
    <a class="lesson-brand" href="../index.html"><span class="lesson-mark" aria-hidden="true">{ }</span><span>C# 设计模式学习地图</span></a>
    <button class="lesson-nav-toggle" id="lesson-nav-toggle" type="button" aria-expanded="false" aria-controls="lesson-nav">菜单</button>
    <nav class="lesson-nav" id="lesson-nav" aria-label="课程导航"><a href="../guides/learning-path.html">学习路线</a><a href="../index.html#patterns" aria-current="page">23 种模式</a><a href="../guides/projects.html">实战项目</a><a href="$repositoryUrl">GitHub ↗</a></nav>
  </header>
  <main class="lesson-main" id="main">
    <p class="lesson-breadcrumb"><a href="../index.html#patterns">模式地图</a> / $(Encode-Html $categoryLabels[$pattern.category]) / $(Encode-Html $pattern.english)</p>
    <section class="lesson-hero" data-number="$(('{0:00}' -f $pattern.number))">
      <div class="lesson-copy"><p class="lesson-kicker">$(Encode-Html $categoryLabels[$pattern.category]) · Pattern $(('{0:00}' -f $pattern.number))</p><h1>$(Encode-Html $pattern.english)</h1><p class="lesson-chinese">$(Encode-Html $pattern.chinese)模式</p><p class="lesson-intent">$(Encode-Html $pattern.intent)</p></div>
      <aside class="lesson-progress"><label for="lesson-progress">我的学习阶段<select id="lesson-progress" aria-label="$(Encode-Html $pattern.english) 学习阶段"></select></label><p id="lesson-progress-summary">全课程 0% · 0 / 28 已验证</p></aside>
    </section>
    <div class="lesson-grid">
      <section class="lesson-panel"><h2>它解决什么变化</h2><p>$(Encode-Html $pattern.changeAxis)</p><div class="problem-tags" aria-label="适用问题">$tagsHtml</div><h3>示例场景</h3><p class="scenario">$(Encode-Html $pattern.scenario)</p></section>
      <section class="lesson-panel"><h2>运行并追踪</h2><p>先预测输出，再从 Runner 进入完整 Demo，最后沿实战落点观察业务价值。</p><div class="command-box"><div><span>从仓库根目录运行</span><button type="button" data-copy-command="$command">复制命令</button></div><code>$command</code></div><div class="lesson-links"><a href="$($pattern.sourceUrl)">独立 Demo 源码 <span>↗</span></a><a href="$($pattern.practiceUrl)">实战项目落点 <span>↗</span></a><a href="../guides/fundamentals.html$($pattern.guide)">完整教程章节 <span>→</span></a></div></section>
      <section class="lesson-panel"><h2>何时使用</h2><p>$(Encode-Html $pattern.whenUse)</p></section>
      <section class="lesson-panel"><h2>何时避免</h2><p>$(Encode-Html $pattern.avoidWhen)</p></section>
      <section class="lesson-panel full"><h2>相似模式对比</h2><ul class="related-list">$relatedHtml</ul></section>
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
$categoryColors = @{ Creational = 'amber'; Structural = 'coral'; Behavioral = 'teal' }
$categoryShort = @{ Creational = 'C'; Structural = 'S'; Behavioral = 'B' }
$fallbackCards = @($patterns | ForEach-Object {
    $pattern = $_
    $tagSpans = @($pattern.problemTags | ForEach-Object {
        '<span>' + (Encode-Html $tagById[$_].label) + '</span>'
      }) -join ''
    @"
<article class="pattern-card $($categoryColors[$pattern.category])">
  <div class="pattern-card-top"><span class="pattern-number">$(('{0:00}' -f $pattern.number))</span><span class="category-badge"><i>$($categoryShort[$pattern.category])</i>$(Encode-Html $categoryLabels[$pattern.category])</span></div>
  <div class="pattern-body"><h3>$(Encode-Html $pattern.english)</h3><p class="pattern-chinese">$(Encode-Html $pattern.chinese)模式</p><p class="pattern-intent">$(Encode-Html $pattern.intent)</p><div class="pattern-problems" aria-label="适用问题">$tagSpans</div><a class="pattern-open" href="patterns/$($pattern.key).html">打开课件 <span aria-hidden="true">→</span></a></div>
  <div class="pattern-card-footer"><a href="patterns/$($pattern.key).html">完整课件</a></div>
</article>
"@
  }) -join "`n"
$courseJsonLd = Get-JsonLdScript -Value ([ordered]@{
    '@context' = 'https://schema.org'
    '@type' = 'Course'
    name = 'C# 设计模式学习地图'
    description = '用现代 C# 14 / .NET 10 跑通 GoF 23 种设计模式，从独立示例走向真实项目与生产可靠性。'
    url = $pagesBase
    inLanguage = 'zh-CN'
    isAccessibleForFree = $true
    provider = [ordered]@{ '@type' = 'Person'; name = $repositoryOwner; url = "https://github.com/$repositoryOwner" }
    hasCourseInstance = [ordered]@{ '@type' = 'CourseInstance'; courseMode = 'online' }
  })
$homeHtml = $homeHtml.Replace('{{PAGES_BASE}}', $pagesBase)
$homeHtml = $homeHtml.Replace('{{REPOSITORY_URL}}', $repositoryUrl)
$homeHtml = $homeHtml.Replace('{{PATTERN_FALLBACK}}', $fallbackCards)
$homeHtml = $homeHtml.Replace('https://github.com/yuweiyang9611/CSharp_Implementation_of_DesignPatterns', $repositoryUrl)
$homeHtml = $homeHtml.Replace('</head>', '  ' + $courseJsonLd + "`n</head>")
[IO.File]::WriteAllText($homePath, $homeHtml, [Text.UTF8Encoding]::new($false))

$allUrls = @($pagesBase) +
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

$requiredFiles = @(
  (Join-Path $stageDirectory 'index.html'),
  (Join-Path $stageDirectory 'sitemap.xml'),
  (Join-Path $stageDirectory 'robots.txt'),
  (Join-Path $stageDirectory 'version.json'),
  (Join-Path $assetsDirectory 'styles.css'),
  (Join-Path $assetsDirectory 'guide.css'),
  (Join-Path $assetsDirectory 'pattern.css'),
  (Join-Path $assetsDirectory 'app.js'),
  (Join-Path $assetsDirectory 'guide.js'),
  (Join-Path $assetsDirectory 'lesson.js'),
  (Join-Path $assetsDirectory 'progress.js'),
  (Join-Path $assetsDirectory 'catalog.js'),
  (Join-Path $assetsDirectory 'catalog.json'),
  (Join-Path $assetsDirectory 'favicon.svg'),
  (Join-Path $assetsDirectory 'og.jpg')
) + @($pages | ForEach-Object { Join-Path $guideDirectory $_.Output }) +
  @($patterns | ForEach-Object { Join-Path $patternDirectory ($_.key + '.html') })

$issues = [Collections.Generic.List[string]]::new()
foreach ($path in $requiredFiles) {
  if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -eq 0) {
    $issues.Add("Missing or empty Pages output: $path")
  }
}
$socialImagePath = Join-Path $assetsDirectory 'og.jpg'
if ((Test-Path -LiteralPath $socialImagePath) -and (Get-Item -LiteralPath $socialImagePath).Length -gt 600kb) {
  $issues.Add("Open Graph image must stay below 600 KB: $socialImagePath")
}

if ($patterns.Count -ne 23) { $issues.Add("Pattern catalog must contain 23 entries; found $($patterns.Count).") }
if (@($patterns.key | Sort-Object -Unique).Count -ne 23) { $issues.Add('Pattern catalog keys must be unique.') }
$expectedCategoryCounts = @{ Creational = 5; Structural = 7; Behavioral = 11 }
foreach ($category in $expectedCategoryCounts.Keys) {
  $actualCount = @($patterns | Where-Object category -eq $category).Count
  if ($actualCount -ne $expectedCategoryCounts[$category]) {
    $issues.Add("Pattern category $category must contain $($expectedCategoryCounts[$category]) entries; found $actualCount.")
  }
}
foreach ($pattern in $patterns) {
  if ($pattern.problemTags.Count -eq 0) { $issues.Add("Pattern has no problem tags: $($pattern.key)") }
  foreach ($tagId in $pattern.problemTags) {
    if (-not $tagById.ContainsKey($tagId)) { $issues.Add("Unknown problem tag for $($pattern.key): $tagId") }
  }
  foreach ($relativeTarget in @($pattern.source, $pattern.practice)) {
    $targetPath = Join-Path $root ($relativeTarget -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $targetPath)) { $issues.Add("Pattern points to a missing repository file: $relativeTarget") }
  }
}

$fundamentalsContent = Get-Content -LiteralPath (Join-Path $guideDirectory 'fundamentals.html') -Raw -Encoding utf8
foreach ($pattern in $patterns) {
  $anchor = $pattern.guide.TrimStart('#')
  if ($fundamentalsContent -notmatch ('id="' + [regex]::Escape($anchor) + '"')) {
    $issues.Add("Pattern points to a missing guide anchor: $($pattern.key) -> $anchor")
  }
}

foreach ($page in $pages) {
  $content = Get-Content -LiteralPath (Join-Path $guideDirectory $page.Output) -Raw -Encoding utf8
  if ($content -notmatch 'learning-site-header') { $issues.Add("Generated guide is missing site navigation: $($page.Output)") }
  if ($content -notmatch 'assets/guide\.js') { $issues.Add("Generated guide is missing online behavior: $($page.Output)") }
  if ($content -match '@@(?:CODE|LINK)\d+@@') { $issues.Add("Generated guide contains an unresolved inline token: $($page.Output)") }
  $h2Count = [regex]::Matches($content, '<h2\s').Count
  if ($h2Count -gt 0 -and $content -notmatch 'class="guide-toc"') { $issues.Add("Generated guide is missing a table of contents: $($page.Output)") }
}

$sitemapDocument = [xml]$sitemap
$sitemapLocations = @($sitemapDocument.urlset.url | ForEach-Object { [string]$_.loc })
if ($sitemapLocations.Count -ne $allUrls.Count) { $issues.Add("Sitemap must contain $($allUrls.Count) URLs; found $($sitemapLocations.Count).") }
if (@($sitemapLocations | Sort-Object -Unique).Count -ne $sitemapLocations.Count) { $issues.Add('Sitemap URLs must be unique.') }
if (@($sitemapLocations | Where-Object { $_ -notmatch '^https://' -or $_ -match '[#?]' }).Count -gt 0) { $issues.Add('Sitemap URLs must be canonical HTTPS URLs without query strings or fragments.') }

$checkedLocalLinks = 0
$htmlCache = @{}
$htmlCanonicalUrls = [Collections.Generic.List[string]]::new()
foreach ($htmlFile in Get-ChildItem -LiteralPath $stageDirectory -Recurse -File -Filter '*.html') {
  $content = Get-Content -LiteralPath $htmlFile.FullName -Raw -Encoding utf8
  if ($content -match '\{\{(?:PAGES_BASE|REPOSITORY_URL|PATTERN_FALLBACK)\}\}') { $issues.Add("Unresolved site placeholder: $($htmlFile.FullName)") }

  $canonicalMatches = [regex]::Matches($content, '<link\s+rel="canonical"\s+href="(?<url>[^"]+)"')
  $ogUrlMatches = [regex]::Matches($content, '<meta\s+property="og:url"\s+content="(?<url>[^"]+)"')
  if ($canonicalMatches.Count -ne 1) { $issues.Add("HTML page must contain exactly one canonical URL: $($htmlFile.FullName)") }
  if ($ogUrlMatches.Count -ne 1) { $issues.Add("HTML page must contain exactly one Open Graph URL: $($htmlFile.FullName)") }
  if ($canonicalMatches.Count -eq 1) { $htmlCanonicalUrls.Add($canonicalMatches[0].Groups['url'].Value) }
  if ($canonicalMatches.Count -eq 1 -and $ogUrlMatches.Count -eq 1 -and $canonicalMatches[0].Groups['url'].Value -ne $ogUrlMatches[0].Groups['url'].Value) {
    $issues.Add("Canonical and Open Graph URL differ: $($htmlFile.FullName)")
  }
  $jsonLdMatches = [regex]::Matches($content, '<script\s+type="application/ld\+json">(?<json>.*?)</script>', [Text.RegularExpressions.RegexOptions]::Singleline)
  if ($jsonLdMatches.Count -ne 1) {
    $issues.Add("HTML page must contain one JSON-LD resource: $($htmlFile.FullName)")
  } else {
    try {
      $jsonLd = $jsonLdMatches[0].Groups['json'].Value | ConvertFrom-Json
      $expectedType = if ($htmlFile.FullName.Equals($homePath, [StringComparison]::OrdinalIgnoreCase)) { 'Course' } else { 'LearningResource' }
      if ($jsonLd.'@type' -ne $expectedType) { $issues.Add("Unexpected JSON-LD type in $($htmlFile.FullName): $($jsonLd.'@type')") }
      if ($canonicalMatches.Count -eq 1 -and $jsonLd.url -ne $canonicalMatches[0].Groups['url'].Value) {
        $issues.Add("JSON-LD URL differs from canonical: $($htmlFile.FullName)")
      }
    } catch {
      $issues.Add("Invalid JSON-LD in $($htmlFile.FullName): $($_.Exception.Message)")
    }
  }
  if ($content -notmatch '<meta\s+property="og:image"\s+content="https://') {
    $issues.Add("HTML page is missing an HTTPS Open Graph image: $($htmlFile.FullName)")
  }
  if ($content -notmatch '<meta\s+name="twitter:card"\s+content="summary_large_image"') {
    $issues.Add("HTML page is missing a Twitter large-image card: $($htmlFile.FullName)")
  }
  if ($content -notmatch '<meta\s+name="twitter:image"\s+content="https://') {
    $issues.Add("HTML page is missing an HTTPS Twitter image: $($htmlFile.FullName)")
  }

  $ids = @([regex]::Matches($content, '\sid="(?<id>[^"]+)"') | ForEach-Object { $_.Groups['id'].Value })
  foreach ($duplicate in @($ids | Group-Object | Where-Object Count -gt 1)) {
    $issues.Add("Duplicate HTML id '$($duplicate.Name)': $($htmlFile.FullName)")
  }

  foreach ($match in [regex]::Matches($content, '(?:href|src)="(?<target>[^"]+)"')) {
    $target = $match.Groups['target'].Value
    if ($target -match '^(https?:|mailto:|data:)') { continue }
    $pathPart = ($target -split '[#?]', 2)[0]
    $fragment = if ($target.Contains('#')) {
      [Uri]::UnescapeDataString(($target -split '#', 2)[1].Split('?', 2)[0])
    } else { '' }
    $candidate = if ([string]::IsNullOrWhiteSpace($pathPart)) {
      $htmlFile.FullName
    } else {
      $decodedPath = [Uri]::UnescapeDataString($pathPart).Replace('/', [IO.Path]::DirectorySeparatorChar)
      [IO.Path]::GetFullPath((Join-Path $htmlFile.DirectoryName $decodedPath))
    }
    if (Test-Path -LiteralPath $candidate -PathType Container) { $candidate = Join-Path $candidate 'index.html' }
    $insideStage = $candidate.Equals($resolvedStage, [StringComparison]::OrdinalIgnoreCase) -or
      $candidate.StartsWith($resolvedStage.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    $checkedLocalLinks++
    if (-not $insideStage -or -not (Test-Path -LiteralPath $candidate)) {
      $relativeHtml = $htmlFile.FullName.Substring($resolvedStage.Length).TrimStart([char[]]'\/')
      $issues.Add("Broken local link: $relativeHtml -> $target")
    } elseif (-not [string]::IsNullOrWhiteSpace($fragment) -and [IO.Path]::GetExtension($candidate).Equals('.html', [StringComparison]::OrdinalIgnoreCase)) {
      if (-not $htmlCache.ContainsKey($candidate)) { $htmlCache[$candidate] = Get-Content -LiteralPath $candidate -Raw -Encoding utf8 }
      if ($htmlCache[$candidate] -notmatch ('id="' + [regex]::Escape($fragment) + '"')) {
        $relativeHtml = $htmlFile.FullName.Substring($resolvedStage.Length).TrimStart([char[]]'\/')
        $issues.Add("Broken local anchor: $relativeHtml -> $target")
      }
    }
  }
}

foreach ($difference in @(Compare-Object -ReferenceObject $sitemapLocations -DifferenceObject @($htmlCanonicalUrls))) {
  $issues.Add("Sitemap/canonical mismatch: $($difference.InputObject) ($($difference.SideIndicator)).")
}
if ($revision -notmatch '^[0-9a-f]{40}$') { $issues.Add("version.json commit must be a 40-character SHA; found '$revision'.") }

if ($issues.Count -gt 0) {
  throw "GitHub Pages validation failed:`n- $($issues -join "`n- ")"
}

Write-Host "GitHub Pages site generated: 1 learning dashboard, $($pages.Count) guides, $($patterns.Count) pattern lessons."
Write-Host "Validated $checkedLocalLinks local links and $($allUrls.Count) canonical sitemap URLs."
Write-Host "Output: $stageDirectory"

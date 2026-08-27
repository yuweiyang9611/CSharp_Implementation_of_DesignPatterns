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
$exportScript = Join-Path $PSScriptRoot 'export-guide.ps1'
$exporterProject = Join-Path (Join-Path (Join-Path $root 'tools') 'GuideExporter') 'GuideExporter.csproj'
$defaultBlobBase = 'https://github.com/yuweiyang9611/CSharp_Implementation_of_DesignPatterns/blob/main/'
$defaultPagesBase = 'https://yuweiyang9611.github.io/CSharp_Implementation_of_DesignPatterns/'

function Decode-Name {
  param([Parameter(Mandatory)][string]$Base64)

  return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Base64))
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

$pages = @(
  [pscustomobject]@{ Input = 'README.md'; Output = 'repository-overview.html'; Description = 'C# 设计模式学习项目的课程结构、运行方式与仓库说明。' },
  [pscustomobject]@{ Input = 'START_HERE.md'; Output = 'learning-path.html'; Description = '从 30 分钟到 14 周的 C# 设计模式学习路线。' },
  [pscustomobject]@{ Input = 'docs/' + (Decode-Name '5qih5byP57Si5byVLm1k'); Output = 'pattern-index.html'; Description = 'GoF 23 种设计模式的 Runner key、源码、实战落点与教程索引。' },
  [pscustomobject]@{ Input = 'docs/' + (Decode-Name 'Q1NoYXJw6K6+6K6h5qih5byP5a2m5Lmg5oyH5Y2XLm1k'); Output = 'fundamentals.html'; Description = 'GoF 23 种设计模式的现代 C# 实现、意图、角色、取舍与练习。' },
  [pscustomobject]@{ Input = 'docs/' + (Decode-Name '6K6+6K6h5qih5byP5a6e5oiY6aG555uu5a2m5Lmg5oyH5Y2XLm1k'); Output = 'practice.html'; Description = 'OnlineStore、SmartHome 与 DocumentWorkflow 的设计模式组合实战指南。' },
  [pscustomobject]@{ Input = 'examples/README.md'; Output = 'projects.html'; Description = '三个教学项目的模式覆盖、运行方式与建议学习顺序。' },
  [pscustomobject]@{ Input = 'examples/OnlineStore/README.md'; Output = 'online-store.html'; Description = '用电商结算、支付与订单生命周期学习七种设计模式。' },
  [pscustomobject]@{ Input = 'examples/SmartHome/README.md'; Output = 'smart-home.html'; Description = '用智能家居设备接入、联动、撤销与恢复学习八种设计模式。' },
  [pscustomobject]@{ Input = 'examples/DocumentWorkflow/README.md'; Output = 'document-workflow.html'; Description = '用报表筛选、合规检查与多渠道发布学习八种设计模式。' },
  [pscustomobject]@{ Input = 'labs/README.md'; Output = 'labs.html'; Description = '从模式组合继续走向安全重构与生产可靠性的高级实验地图。' },
  [pscustomobject]@{ Input = 'labs/CheckoutRefactoringKata/README.md'; Output = 'refactoring.html'; Description = '从坏代码经特征测试逐步重构出 Strategy、Chain、State 与 Facade。' },
  [pscustomobject]@{ Input = 'labs/ReliableCheckout/README.md'; Output = 'reliable-checkout.html'; Description = '用 HTTP、SQLite、幂等、Outbox 与重试保护结账业务不变量。' }
)

$resolvedRoot = [IO.Path]::GetFullPath($root).TrimEnd([IO.Path]::DirectorySeparatorChar)
$resolvedStage = [IO.Path]::GetFullPath($stageDirectory)
if (-not $resolvedStage.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Refusing to stage GitHub Pages outside the repository: $resolvedStage"
}

if (-not (Test-Path -LiteralPath (Join-Path $siteSource 'index.html'))) {
  throw "Site source is missing index.html: $siteSource"
}

if (Test-Path -LiteralPath $stageDirectory) {
  Remove-Item -LiteralPath $stageDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $stageDirectory, $guideDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $siteSource 'index.html') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $siteSource 'assets') -Destination $stageDirectory -Recurse

if (-not $NoBuild) {
  dotnet restore $exporterProject --locked-mode
  if ($LASTEXITCODE -ne 0) {
    throw 'GuideExporter locked restore failed.'
  }

  dotnet build $exporterProject --configuration Release --no-restore
  if ($LASTEXITCODE -ne 0) {
    throw 'GuideExporter build failed.'
  }
}

$blobBaseVariable = 'CSHARP_DESIGN_PATTERNS_REPOSITORY_BLOB_BASE'
$hadBlobBase = Test-Path "Env:$blobBaseVariable"
$oldBlobBase = [Environment]::GetEnvironmentVariable($blobBaseVariable, 'Process')
$blobBase = if ([string]::IsNullOrWhiteSpace($oldBlobBase)) { $defaultBlobBase } else { $oldBlobBase }
$pagesBase = if ([string]::IsNullOrWhiteSpace($env:CSHARP_DESIGN_PATTERNS_PAGES_BASE)) {
  $defaultPagesBase
} else {
  $env:CSHARP_DESIGN_PATTERNS_PAGES_BASE.TrimEnd('/') + '/'
}

try {
  [Environment]::SetEnvironmentVariable($blobBaseVariable, $blobBase, 'Process')

  foreach ($page in $pages) {
    $inputPath = Join-Path $root ($page.Input -replace '/', [IO.Path]::DirectorySeparatorChar)
    $outputPath = Join-Path $guideDirectory $page.Output
    if (-not (Test-Path -LiteralPath $inputPath)) {
      throw "Guide source is missing: $($page.Input)"
    }

    & $exportScript -InputPath $inputPath -OutputPath $outputPath -HtmlOnly -NoBuild
    if ($LASTEXITCODE -ne 0) {
      throw "Guide export failed: $($page.Input)"
    }
  }
}
finally {
  if ($hadBlobBase) {
    [Environment]::SetEnvironmentVariable($blobBaseVariable, $oldBlobBase, 'Process')
  }
  else {
    [Environment]::SetEnvironmentVariable($blobBaseVariable, $null, 'Process')
  }
}

$routeMap = @{}
foreach ($page in $pages) {
  $routeMap[(Get-RepositoryUrl -RelativePath $page.Input -BlobBase $blobBase)] = $page.Output
}

$guideStyles = @'
    .learning-site-header { max-width: 178mm; margin: 0 auto; padding: 13px 18px; background: #10252a; color: #fffdf8; display: flex; align-items: center; justify-content: space-between; gap: 18px; font-family: "Microsoft YaHei", "PingFang SC", sans-serif; }
    .learning-site-header a { color: inherit; text-decoration: none; }
    .learning-site-brand { display: flex; align-items: center; gap: 10px; font-weight: 800; }
    .learning-site-mark { display: grid; place-items: center; width: 30px; height: 30px; border: 1px solid #f0b429; color: #f0b429; font: 700 10px Consolas, monospace; }
    .learning-site-nav { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 12px; font-size: 9pt; }
    .learning-site-nav a { color: #bdd0cd; }
    .learning-site-nav a:hover { color: #ffd874; }
    .learning-site-footer { max-width: 178mm; margin: 0 auto; padding: 18px; background: #10252a; color: #bdd0cd; text-align: center; font: 9pt "Microsoft YaHei", "PingFang SC", sans-serif; }
    .learning-site-footer a { color: #ffd874; text-decoration: none; }
    @media screen { .learning-site-header { position: sticky; top: 0; z-index: 20; box-shadow: 0 6px 20px #10252a30; } }
    @media (max-width: 720px) { .learning-site-header { align-items: flex-start; flex-direction: column; } .learning-site-nav { justify-content: flex-start; } }
    @media print { .learning-site-header, .learning-site-footer { display: none; } }
'@

foreach ($page in $pages) {
  $outputPath = Join-Path $guideDirectory $page.Output
  $html = Get-Content -LiteralPath $outputPath -Raw -Encoding utf8

  foreach ($entry in $routeMap.GetEnumerator()) {
    $html = $html.Replace('href="' + $entry.Key, 'href="' + $entry.Value)
  }

  $encodedDescription = [Net.WebUtility]::HtmlEncode($page.Description)
  $titleMatch = [regex]::Match($html, '<title>(?<title>.*?)</title>', [Text.RegularExpressions.RegexOptions]::Singleline)
  $pageTitle = if ($titleMatch.Success) { [Net.WebUtility]::HtmlDecode($titleMatch.Groups['title'].Value) } else { 'C# 设计模式学习地图' }
  $encodedTitle = [Net.WebUtility]::HtmlEncode($pageTitle)
  $pageUrl = $pagesBase + 'guides/' + $page.Output
  $metadata = @"
  <meta name="description" content="$encodedDescription">
  <meta property="og:type" content="article">
  <meta property="og:title" content="$encodedTitle">
  <meta property="og:description" content="$encodedDescription">
  <meta property="og:url" content="$pageUrl">
  <meta name="twitter:card" content="summary">
  <meta name="twitter:title" content="$encodedTitle">
  <meta name="twitter:description" content="$encodedDescription">
"@
  $header = @'
<header class="learning-site-header">
  <a class="learning-site-brand" href="../index.html"><span class="learning-site-mark">{ }</span><span>C# 设计模式学习地图</span></a>
  <nav class="learning-site-nav" aria-label="课程导航"><a href="learning-path.html">学习路线</a><a href="pattern-index.html">23 种模式</a><a href="projects.html">实战项目</a><a href="labs.html">高级实验</a><a href="../index.html">返回首页</a></nav>
</header>
'@
  $footer = @'
<footer class="learning-site-footer">内容来自同一 GitHub 仓库并随主分支自动更新 · <a href="../index.html">返回学习地图</a></footer>
'@

  $html = $html.Replace('</head>', $metadata + '</head>')
  $html = $html.Replace('</style>', $guideStyles + '</style>')
  $html = $html.Replace('<body><main>', '<body>' + $header + '<main>')
  $html = $html.Replace('</main></body>', '</main>' + $footer + '</body>')
  [IO.File]::WriteAllText($outputPath, $html, [Text.UTF8Encoding]::new($false))
}

$requiredFiles = @(
  (Join-Path $stageDirectory 'index.html'),
  (Join-Path (Join-Path $stageDirectory 'assets') 'styles.css'),
  (Join-Path (Join-Path $stageDirectory 'assets') 'app.js'),
  (Join-Path (Join-Path $stageDirectory 'assets') 'og.png')
) + @($pages | ForEach-Object { Join-Path $guideDirectory $_.Output })

$issues = [Collections.Generic.List[string]]::new()
foreach ($path in $requiredFiles) {
  if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -eq 0) {
    $issues.Add("Missing or empty Pages output: $path")
  }
}

$appScript = Get-Content -LiteralPath (Join-Path (Join-Path $stageDirectory 'assets') 'app.js') -Raw -Encoding utf8
$patternCount = [regex]::Matches($appScript, 'key:\s*"[^"]+"').Count
if ($patternCount -ne 23) {
  $issues.Add("Pattern explorer must contain 23 entries; found $patternCount.")
}
$patternKeys = @([regex]::Matches($appScript, 'key:\s*"(?<value>[^"]+)"') | ForEach-Object {
    $_.Groups['value'].Value
  })
if (@($patternKeys | Sort-Object -Unique).Count -ne 23) {
  $issues.Add('Pattern explorer keys must be unique.')
}
$expectedCategoryCounts = @{ Creational = 5; Structural = 7; Behavioral = 11 }
$patternCategories = @([regex]::Matches($appScript, 'category:\s*"(?<value>Creational|Structural|Behavioral)"') | ForEach-Object {
    $_.Groups['value'].Value
  })
foreach ($category in $expectedCategoryCounts.Keys) {
  $actualCount = @($patternCategories | Where-Object { $_ -eq $category }).Count
  if ($actualCount -ne $expectedCategoryCounts[$category]) {
    $issues.Add("Pattern category $category must contain $($expectedCategoryCounts[$category]) entries; found $actualCount.")
  }
}
$patternTargets = @(
  [regex]::Matches($appScript, '(?:source|practice):\s*"(?<value>[^"]+)"') | ForEach-Object {
    $_.Groups['value'].Value
  }
)
foreach ($relativeTarget in $patternTargets) {
  $targetPath = Join-Path $root ($relativeTarget -replace '/', [IO.Path]::DirectorySeparatorChar)
  if (-not (Test-Path -LiteralPath $targetPath)) {
    $issues.Add("Pattern explorer points to a missing repository file: $relativeTarget")
  }
}
$fundamentalsContent = Get-Content -LiteralPath (Join-Path $guideDirectory 'fundamentals.html') -Raw -Encoding utf8
$patternGuideAnchors = [regex]::Matches($appScript, 'guide:\s*"#(?<anchor>[^"]+)"')
if ($patternGuideAnchors.Count -ne 23) {
  $issues.Add("Pattern explorer must contain 23 guide anchors; found $($patternGuideAnchors.Count).")
}
foreach ($match in $patternGuideAnchors) {
  $anchor = $match.Groups['anchor'].Value
  if ($fundamentalsContent -notmatch ('id="' + [regex]::Escape($anchor) + '"')) {
    $issues.Add("Pattern explorer points to a missing guide anchor: $anchor")
  }
}

foreach ($page in $pages) {
  $content = Get-Content -LiteralPath (Join-Path $guideDirectory $page.Output) -Raw -Encoding utf8
  if ($content -notmatch 'learning-site-header') {
    $issues.Add("Generated guide is missing site navigation: $($page.Output)")
  }
  if ($content -match '@@(?:CODE|LINK)\d+@@') {
    $issues.Add("Generated guide contains an unresolved inline token: $($page.Output)")
  }
}

$checkedLocalLinks = 0
$htmlCache = @{}
foreach ($htmlFile in Get-ChildItem -LiteralPath $stageDirectory -Recurse -File -Filter '*.html') {
  $content = Get-Content -LiteralPath $htmlFile.FullName -Raw -Encoding utf8
  foreach ($match in [regex]::Matches($content, '(?:href|src)="(?<target>[^"]+)"')) {
    $target = $match.Groups['target'].Value
    if ($target -match '^(https?:|mailto:|data:|#)') { continue }

    $pathPart = ($target -split '[#?]', 2)[0]
    $fragment = if ($target.Contains('#')) {
      [Uri]::UnescapeDataString(($target -split '#', 2)[1].Split('?', 2)[0])
    } else { '' }
    if ([string]::IsNullOrWhiteSpace($pathPart)) { continue }

    $decodedPath = [Uri]::UnescapeDataString($pathPart).Replace('/', [IO.Path]::DirectorySeparatorChar)
    $candidate = [IO.Path]::GetFullPath((Join-Path $htmlFile.DirectoryName $decodedPath))
    $insideStage = $candidate.Equals($resolvedStage, [StringComparison]::OrdinalIgnoreCase) -or
      $candidate.StartsWith($resolvedStage.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    $checkedLocalLinks++
    if (-not $insideStage -or -not (Test-Path -LiteralPath $candidate)) {
      $relativeHtml = $htmlFile.FullName.Substring($resolvedStage.Length).TrimStart([char[]]'\/')
      $issues.Add("Broken local link: $relativeHtml -> $target")
    }
    elseif (-not [string]::IsNullOrWhiteSpace($fragment) -and
      [IO.Path]::GetExtension($candidate).Equals('.html', [StringComparison]::OrdinalIgnoreCase)) {
      if (-not $htmlCache.ContainsKey($candidate)) {
        $htmlCache[$candidate] = Get-Content -LiteralPath $candidate -Raw -Encoding utf8
      }
      if ($htmlCache[$candidate] -notmatch ('id="' + [regex]::Escape($fragment) + '"')) {
        $relativeHtml = $htmlFile.FullName.Substring($resolvedStage.Length).TrimStart([char[]]'\/')
        $issues.Add("Broken local anchor: $relativeHtml -> $target")
      }
    }
  }
}

if ($issues.Count -gt 0) {
  throw "GitHub Pages validation failed:`n- $($issues -join "`n- ")"
}

Write-Host "GitHub Pages site generated: 1 interactive homepage, $($pages.Count) guides, 23 searchable patterns."
Write-Host "Validated $checkedLocalLinks local links."
Write-Host "Output: $stageDirectory"

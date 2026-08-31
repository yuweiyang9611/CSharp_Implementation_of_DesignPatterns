#requires -Version 7.0

[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$SiteDirectory,
  [string]$RepositoryRoot,
  [string]$ExpectedPagesBase
)

$ErrorActionPreference = 'Stop'
$root = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { Split-Path -Parent $PSScriptRoot } else { $RepositoryRoot }
$root = [IO.Path]::GetFullPath($root).TrimEnd([IO.Path]::DirectorySeparatorChar)
$site = [IO.Path]::GetFullPath($SiteDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
$manifest = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot 'site-manifest.psd1')
$guides = @($manifest.Guides | ForEach-Object { [pscustomobject]$_ })
$issues = [Collections.Generic.List[string]]::new()
$checkedLinks = 0

if (-not (Test-Path -LiteralPath $site -PathType Container)) { throw "Pages site directory does not exist: $site" }

$assetDirectory = Join-Path $site 'assets'
$guideDirectory = Join-Path $site 'guides'
$patternDirectory = Join-Path $site 'patterns'
$catalogPath = Join-Path $assetDirectory 'catalog.json'
$requiredRelativeFiles = @(
  'index.html', 'quiz.html', 'sitemap.xml', 'robots.txt', 'version.json',
  'assets/styles.css', 'assets/guide.css', 'assets/pattern.css', 'assets/quiz.css',
  'assets/app.js', 'assets/guide.js', 'assets/lesson.js', 'assets/progress.js',
  'assets/review.js', 'assets/search.js', 'assets/quiz.js', 'assets/catalog.js',
  'assets/catalog.json', 'assets/search-index.json', 'assets/favicon.svg', 'assets/og.jpg'
)
foreach ($relative in $requiredRelativeFiles) {
  $path = Join-Path $site ($relative -replace '/', [IO.Path]::DirectorySeparatorChar)
  if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
    $issues.Add("Missing or empty Pages output: $relative")
  }
}

if (-not (Test-Path -LiteralPath $catalogPath)) {
  throw "Cannot validate Pages site without assets/catalog.json: $site"
}
$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding utf8 | ConvertFrom-Json
$patterns = @($catalog.patterns)
$learningItems = @($catalog.learningItems)
$quizzes = @($catalog.quizzes)

if ($guides.Count -ne 12) { $issues.Add("Site manifest must contain 12 guides; found $($guides.Count).") }
if ($patterns.Count -ne 23) { $issues.Add("Published catalog must contain 23 patterns; found $($patterns.Count).") }
if ($learningItems.Count -ne 5) { $issues.Add("Published catalog must contain 5 project/lab learning items; found $($learningItems.Count).") }
if ($quizzes.Count -lt 6) { $issues.Add("Published catalog must contain at least 6 scenario quizzes; found $($quizzes.Count).") }
if (@($patterns.key | Sort-Object -Unique).Count -ne $patterns.Count) { $issues.Add('Published pattern keys must be unique.') }
if (@($learningItems.id | Sort-Object -Unique).Count -ne $learningItems.Count) { $issues.Add('Published learning item ids must be unique.') }
if (@($quizzes.id | Sort-Object -Unique).Count -ne $quizzes.Count) { $issues.Add('Published quiz ids must be unique.') }

$expectedCategoryCounts = @{ Creational = 5; Structural = 7; Behavioral = 11 }
foreach ($category in $expectedCategoryCounts.Keys) {
  $count = @($patterns | Where-Object category -eq $category).Count
  if ($count -ne $expectedCategoryCounts[$category]) { $issues.Add("Pattern category $category must contain $($expectedCategoryCounts[$category]) entries; found $count.") }
}
foreach ($pattern in $patterns) {
  if (@($pattern.evidenceCards).Count -ne 4) { $issues.Add("Pattern must publish four evidence cards: $($pattern.key)") }
  foreach ($relativeTarget in @($pattern.source, $pattern.practice)) {
    $target = Join-Path $root ([string]$relativeTarget -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $target)) { $issues.Add("Pattern points to a missing repository file: $relativeTarget") }
  }
}

$homePath = Join-Path $site 'index.html'
$homeContent = if (Test-Path -LiteralPath $homePath) { Get-Content -LiteralPath $homePath -Raw -Encoding utf8 } else { '' }
$homeCanonical = [regex]::Match($homeContent, '<link\s+rel="canonical"\s+href="(?<url>[^"]+)"').Groups['url'].Value
$pagesBase = if (-not [string]::IsNullOrWhiteSpace($ExpectedPagesBase)) { $ExpectedPagesBase.TrimEnd('/') + '/' } else { $homeCanonical.TrimEnd('/') + '/' }
if ([string]::IsNullOrWhiteSpace($pagesBase) -or $pagesBase -notmatch '^https://') { $issues.Add("Unable to derive an HTTPS Pages base URL from index.html: '$pagesBase'") }
if (-not [string]::IsNullOrWhiteSpace($ExpectedPagesBase) -and $homeCanonical -ne $pagesBase) { $issues.Add("Homepage canonical differs from expected Pages base: $homeCanonical") }

$expectedHtml = @('index.html', 'quiz.html') +
  @($guides | ForEach-Object { 'guides/' + $_.Output }) +
  @($patterns | ForEach-Object { 'patterns/' + $_.key + '.html' })
$expectedUrls = @($pagesBase, ($pagesBase + 'quiz.html')) +
  @($guides | ForEach-Object { $pagesBase + 'guides/' + $_.Output }) +
  @($patterns | ForEach-Object { $pagesBase + 'patterns/' + $_.key + '.html' })

if ($expectedHtml.Count -ne 37) { $issues.Add("Expected 37 HTML outputs; derived $($expectedHtml.Count).") }
foreach ($relative in $expectedHtml) {
  if (-not (Test-Path -LiteralPath (Join-Path $site ($relative -replace '/', [IO.Path]::DirectorySeparatorChar)))) { $issues.Add("Missing expected HTML page: $relative") }
}
$actualHtml = @(Get-ChildItem -LiteralPath $site -Recurse -File -Filter '*.html')
if ($actualHtml.Count -ne $expectedHtml.Count) { $issues.Add("Pages artifact must contain exactly $($expectedHtml.Count) HTML files; found $($actualHtml.Count).") }

$fundamentalsPath = Join-Path $guideDirectory 'fundamentals.html'
$fundamentals = if (Test-Path -LiteralPath $fundamentalsPath) { Get-Content -LiteralPath $fundamentalsPath -Raw -Encoding utf8 } else { '' }
foreach ($pattern in $patterns) {
  $anchor = ([string]$pattern.guide).TrimStart('#')
  if ($fundamentals -notmatch ('id="' + [regex]::Escape($anchor) + '"')) { $issues.Add("Pattern points to a missing fundamentals anchor: $($pattern.key) -> $anchor") }
}

foreach ($guide in $guides) {
  $path = Join-Path $guideDirectory $guide.Output
  if (-not (Test-Path -LiteralPath $path)) { continue }
  $content = Get-Content -LiteralPath $path -Raw -Encoding utf8
  if ($content -notmatch 'learning-site-header') { $issues.Add("Generated guide is missing site navigation: $($guide.Output)") }
  if ($content -notmatch 'assets/guide\.js') { $issues.Add("Generated guide is missing online behavior: $($guide.Output)") }
  if ($content -match '@@(?:CODE|LINK)\d+@@') { $issues.Add("Generated guide contains an unresolved inline token: $($guide.Output)") }
  $h2Count = [regex]::Matches($content, '<h2\s').Count
  if ($h2Count -gt 0 -and $content -notmatch 'class="guide-toc"') { $issues.Add("Generated guide is missing a table of contents: $($guide.Output)") }
  $learningItem = @($learningItems | Where-Object url -eq ('guides/' + $guide.Output))
  if ($learningItem.Count -eq 1 -and $content -notmatch ('data-learning-item-id="' + [regex]::Escape($learningItem[0].id) + '"')) {
    $issues.Add("Project/lab guide is missing its milestone tracker: $($guide.Output)")
  }
}

foreach ($item in $learningItems) {
  $relative = [string]$item.url
  $path = Join-Path $site ($relative -replace '/', [IO.Path]::DirectorySeparatorChar)
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $issues.Add("Learning item points to a missing guide: $relative"); continue }
  $content = Get-Content -LiteralPath $path -Raw -Encoding utf8
  foreach ($milestone in @($item.milestones)) {
    $anchor = ([string]$milestone.anchor).TrimStart('#')
    if ($content -notmatch ('id="' + [regex]::Escape($anchor) + '"')) { $issues.Add("Learning milestone points to a missing guide anchor: $($item.id)/$($milestone.id) -> $anchor") }
  }
}
foreach ($pattern in $patterns) {
  $path = Join-Path $patternDirectory ($pattern.key + '.html')
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
  $content = Get-Content -LiteralPath $path -Raw -Encoding utf8
  foreach ($card in @($pattern.evidenceCards)) {
    $anchor = ([string]$card.href).TrimStart('#')
    if ($content -notmatch ('id="' + [regex]::Escape($anchor) + '"')) { $issues.Add("Pattern evidence points to a missing lesson anchor: $($pattern.key)/$($card.id) -> $anchor") }
  }
}

$searchIndexPath = Join-Path $assetDirectory 'search-index.json'
if (Test-Path -LiteralPath $searchIndexPath) {
  try {
    $searchIndex = Get-Content -LiteralPath $searchIndexPath -Raw -Encoding utf8 | ConvertFrom-Json
    if ($searchIndex.version -ne 1 -or @($searchIndex.entries).Count -lt 35) { $issues.Add('Full-text search index must contain guide sections and all pattern lessons.') }
    foreach ($entry in @($searchIndex.entries)) {
      if ([string]::IsNullOrWhiteSpace($entry.title) -or [string]::IsNullOrWhiteSpace($entry.url)) { $issues.Add('Full-text search index contains an incomplete entry.'); break }
    }
  } catch { $issues.Add("Invalid full-text search index: $($_.Exception.Message)") }
}

$sitemapPath = Join-Path $site 'sitemap.xml'
$sitemapLocations = @()
if (Test-Path -LiteralPath $sitemapPath) {
  try {
    $sitemapDocument = [xml](Get-Content -LiteralPath $sitemapPath -Raw -Encoding utf8)
    $sitemapLocations = @($sitemapDocument.urlset.url | ForEach-Object { [string]$_.loc })
    if ($sitemapLocations.Count -ne $expectedUrls.Count) { $issues.Add("Sitemap must contain $($expectedUrls.Count) URLs; found $($sitemapLocations.Count).") }
    if (@($sitemapLocations | Sort-Object -Unique).Count -ne $sitemapLocations.Count) { $issues.Add('Sitemap URLs must be unique.') }
    if (@($sitemapLocations | Where-Object { $_ -notmatch '^https://' -or $_ -match '[#?]' }).Count -gt 0) { $issues.Add('Sitemap URLs must be canonical HTTPS URLs without query strings or fragments.') }
  } catch { $issues.Add("Invalid sitemap.xml: $($_.Exception.Message)") }
}

$htmlCache = @{}
$canonicalUrls = [Collections.Generic.List[string]]::new()
foreach ($htmlFile in $actualHtml) {
  $content = Get-Content -LiteralPath $htmlFile.FullName -Raw -Encoding utf8
  $relativeHtml = $htmlFile.FullName.Substring($site.Length).TrimStart([char[]]'\/')
  if ($content -match '\{\{(?:PAGES_BASE|REPOSITORY_URL|PATTERN_FALLBACK)\}\}') { $issues.Add("Unresolved site placeholder: $relativeHtml") }
  $canonicalMatches = [regex]::Matches($content, '<link\s+rel="canonical"\s+href="(?<url>[^"]+)"')
  $ogUrlMatches = [regex]::Matches($content, '<meta\s+property="og:url"\s+content="(?<url>[^"]+)"')
  if ($canonicalMatches.Count -ne 1) { $issues.Add("HTML page must contain exactly one canonical URL: $relativeHtml") }
  if ($ogUrlMatches.Count -ne 1) { $issues.Add("HTML page must contain exactly one Open Graph URL: $relativeHtml") }
  if ($canonicalMatches.Count -eq 1) { $canonicalUrls.Add($canonicalMatches[0].Groups['url'].Value) }
  if ($canonicalMatches.Count -eq 1 -and $ogUrlMatches.Count -eq 1 -and $canonicalMatches[0].Groups['url'].Value -ne $ogUrlMatches[0].Groups['url'].Value) { $issues.Add("Canonical and Open Graph URL differ: $relativeHtml") }
  $uniqueSocialMetadata = @(
    @{ Label = 'Open Graph site name'; Pattern = '<meta\s+property="og:site_name"\s+content="[^"]+"' },
    @{ Label = 'Open Graph locale'; Pattern = '<meta\s+property="og:locale"\s+content="[^"]+"' },
    @{ Label = 'Open Graph image'; Pattern = '<meta\s+property="og:image"\s+content="https://[^"]+"' },
    @{ Label = 'Open Graph image width'; Pattern = '<meta\s+property="og:image:width"\s+content="[^"]+"' },
    @{ Label = 'Open Graph image height'; Pattern = '<meta\s+property="og:image:height"\s+content="[^"]+"' },
    @{ Label = 'Open Graph image alt'; Pattern = '<meta\s+property="og:image:alt"\s+content="[^"]+"' },
    @{ Label = 'Twitter card'; Pattern = '<meta\s+name="twitter:card"\s+content="summary_large_image"' },
    @{ Label = 'Twitter title'; Pattern = '<meta\s+name="twitter:title"\s+content="[^"]+"' },
    @{ Label = 'Twitter description'; Pattern = '<meta\s+name="twitter:description"\s+content="[^"]+"' },
    @{ Label = 'Twitter image'; Pattern = '<meta\s+name="twitter:image"\s+content="https://[^"]+"' }
  )
  foreach ($metadata in $uniqueSocialMetadata) {
    if ([regex]::Matches($content, $metadata.Pattern).Count -ne 1) {
      $issues.Add("HTML page must contain exactly one $($metadata.Label) tag: $relativeHtml")
    }
  }
  $jsonLdMatches = [regex]::Matches($content, '<script\s+type="application/ld\+json">(?<json>.*?)</script>', 'Singleline')
  if ($jsonLdMatches.Count -ne 1) { $issues.Add("HTML page must contain one JSON-LD resource: $relativeHtml") }
  else {
    try {
      $jsonLd = $jsonLdMatches[0].Groups['json'].Value | ConvertFrom-Json
      $expectedType = if ($htmlFile.FullName.Equals($homePath, [StringComparison]::OrdinalIgnoreCase)) { 'Course' } else { 'LearningResource' }
      if ($jsonLd.'@type' -ne $expectedType) { $issues.Add("Unexpected JSON-LD type in ${relativeHtml}: $($jsonLd.'@type')") }
      if ($canonicalMatches.Count -eq 1 -and $jsonLd.url -ne $canonicalMatches[0].Groups['url'].Value) { $issues.Add("JSON-LD URL differs from canonical: $relativeHtml") }
    } catch { $issues.Add("Invalid JSON-LD in ${relativeHtml}: $($_.Exception.Message)") }
  }
  $ids = @([regex]::Matches($content, '\sid="(?<id>[^"]+)"') | ForEach-Object { $_.Groups['id'].Value })
  foreach ($duplicate in @($ids | Group-Object | Where-Object Count -gt 1)) { $issues.Add("Duplicate HTML id '$($duplicate.Name)': $relativeHtml") }

  foreach ($match in [regex]::Matches($content, '(?:href|src)="(?<target>[^"]+)"')) {
    $target = $match.Groups['target'].Value
    if ($target -match '^(https?:|mailto:|data:)') { continue }
    $pathPart = ($target -split '[#?]', 2)[0]
    $fragment = if ($target.Contains('#')) { [Uri]::UnescapeDataString(($target -split '#', 2)[1].Split('?', 2)[0]) } else { '' }
    $candidate = if ([string]::IsNullOrWhiteSpace($pathPart)) { $htmlFile.FullName } else {
      $decodedPath = [Uri]::UnescapeDataString($pathPart).Replace('/', [IO.Path]::DirectorySeparatorChar)
      [IO.Path]::GetFullPath((Join-Path $htmlFile.DirectoryName $decodedPath))
    }
    if (Test-Path -LiteralPath $candidate -PathType Container) { $candidate = Join-Path $candidate 'index.html' }
    $inside = $candidate.StartsWith($site + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    $checkedLinks++
    if (-not $inside -or -not (Test-Path -LiteralPath $candidate)) { $issues.Add("Broken local link: $relativeHtml -> $target") }
    elseif (-not [string]::IsNullOrWhiteSpace($fragment) -and [IO.Path]::GetExtension($candidate).Equals('.html', [StringComparison]::OrdinalIgnoreCase)) {
      if (-not $htmlCache.ContainsKey($candidate)) { $htmlCache[$candidate] = Get-Content -LiteralPath $candidate -Raw -Encoding utf8 }
      if ($htmlCache[$candidate] -notmatch ('id="' + [regex]::Escape($fragment) + '"')) { $issues.Add("Broken local anchor: $relativeHtml -> $target") }
    }
  }
}

foreach ($difference in @(Compare-Object -ReferenceObject @($expectedUrls) -DifferenceObject @($sitemapLocations))) { $issues.Add("Unexpected sitemap URL set: $($difference.InputObject) ($($difference.SideIndicator)).") }
foreach ($difference in @(Compare-Object -ReferenceObject @($expectedUrls) -DifferenceObject @($canonicalUrls))) { $issues.Add("Unexpected canonical URL set: $($difference.InputObject) ($($difference.SideIndicator)).") }

$versionPath = Join-Path $site 'version.json'
if (Test-Path -LiteralPath $versionPath) {
  try {
    $revision = (Get-Content -LiteralPath $versionPath -Raw -Encoding utf8 | ConvertFrom-Json).commit
    if ($revision -notmatch '^[0-9a-f]{40}$') { $issues.Add("version.json commit must be a 40-character lowercase SHA; found '$revision'.") }
  } catch { $issues.Add("Invalid version.json: $($_.Exception.Message)") }
}
$socialImage = Join-Path $assetDirectory 'og.jpg'
if ((Test-Path -LiteralPath $socialImage) -and (Get-Item -LiteralPath $socialImage).Length -gt 600kb) { $issues.Add('Open Graph image must stay below 600 KB.') }

if ($issues.Count -gt 0) { throw "GitHub Pages validation failed:`n- $($issues -join "`n- ")" }
Write-Host "Pages artifact valid: 37 HTML pages, $checkedLinks local links, $($expectedUrls.Count) canonical URLs."

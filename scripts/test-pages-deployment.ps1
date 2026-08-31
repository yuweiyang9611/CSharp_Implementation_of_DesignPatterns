#requires -Version 7.0

[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$BaseUrl,
  [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$ExpectedRevision,
  [ValidateRange(1, 12)][int]$Attempts = 8,
  [ValidateRange(1, 15)][int]$DelaySeconds = 5
)

$ErrorActionPreference = 'Stop'
$siteBase = $BaseUrl.TrimEnd('/') + '/'
$expected = $ExpectedRevision.ToLowerInvariant()
$headers = @{ 'Cache-Control' = 'no-cache'; Pragma = 'no-cache' }

function Get-SiteResource {
  param([Parameter(Mandatory)][AllowEmptyString()][string]$RelativePath)

  $separator = if ($RelativePath.Contains('?')) { '&' } else { '?' }
  $requestUri = $siteBase + $RelativePath + $separator + 'revision=' + $expected
  for ($requestAttempt = 1; $requestAttempt -le 3; $requestAttempt++) {
    try {
      return Invoke-WebRequest -Uri $requestUri -Headers $headers -MaximumRedirection 5 -TimeoutSec 20
    }
    catch {
      if ($requestAttempt -eq 3) { throw }
      Start-Sleep -Seconds 1
    }
  }
}

$versionResponse = $null
for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
  try {
    $versionResponse = Get-SiteResource -RelativePath 'version.json'
    $version = $versionResponse.Content | ConvertFrom-Json
    if ([string]$version.commit -eq $expected) { break }
    Write-Host "Deployment version has not propagated yet (attempt $attempt/$Attempts)."
  }
  catch {
    if ($attempt -eq $Attempts) { throw }
    Write-Host "Deployment version is not available yet (attempt $attempt/$Attempts)."
  }

  if ($attempt -lt $Attempts) { Start-Sleep -Seconds $DelaySeconds }
}

if ($null -eq $versionResponse) { throw 'version.json was not retrieved.' }
$version = $versionResponse.Content | ConvertFrom-Json
if ([string]$version.commit -ne $expected) {
  throw "GitHub Pages is serving commit '$($version.commit)' instead of '$expected'."
}
if ([string]$versionResponse.Headers.'Content-Type' -notmatch 'application/json') {
  throw "version.json has an unexpected content type: $($versionResponse.Headers.'Content-Type')"
}

$homeResponse = Get-SiteResource -RelativePath ''
if ([string]$homeResponse.Headers.'Content-Type' -notmatch 'text/html') { throw 'Homepage is not served as HTML.' }
if ($homeResponse.Content -notmatch '<title>C# 设计模式学习地图</title>') { throw 'Homepage title marker is missing.' }

$app = Get-SiteResource -RelativePath 'assets/app.js'
if ([string]$app.Headers.'Content-Type' -notmatch '(javascript|text/plain)') { throw 'app.js has an unexpected content type.' }
if ($app.Content -notmatch 'LearningProgress') { throw 'app.js content marker is missing.' }

$guide = Get-SiteResource -RelativePath 'guides/fundamentals.html'
if ([string]$guide.Headers.'Content-Type' -notmatch 'text/html') { throw 'Fundamentals guide is not served as HTML.' }
if ($guide.Content -notmatch 'learning-site-header' -or $guide.Content -notmatch 'Adapter') { throw 'Fundamentals guide markers are missing.' }

$lesson = Get-SiteResource -RelativePath 'patterns/adapter.html'
if ($lesson.Content -notmatch 'data-pattern-key="adapter"' -or $lesson.Content -notmatch 'LearningResource') { throw 'Adapter lesson markers are missing.' }

$quiz = Get-SiteResource -RelativePath 'quiz.html'
if ([string]$quiz.Headers.'Content-Type' -notmatch 'text/html' -or $quiz.Content -notmatch 'question-options' -or $quiz.Content -notmatch 'assets/review\.js') {
  throw 'Scenario quiz page markers are missing.'
}

$searchIndexResponse = Get-SiteResource -RelativePath 'assets/search-index.json'
if ([string]$searchIndexResponse.Headers.'Content-Type' -notmatch '(application/json|text/plain)') { throw 'Search index has an unexpected content type.' }
$searchIndex = $searchIndexResponse.Content | ConvertFrom-Json
if (@($searchIndex.entries).Count -lt 35) { throw 'Search index is missing guide sections or pattern lessons.' }

$sitemapResponse = Get-SiteResource -RelativePath 'sitemap.xml'
if ([string]$sitemapResponse.Headers.'Content-Type' -notmatch '(xml|text/plain)') { throw 'sitemap.xml has an unexpected content type.' }
$sitemap = [xml]$sitemapResponse.Content
$locations = @($sitemap.urlset.url | ForEach-Object { [string]$_.loc })
if ($locations.Count -ne 37 -or @($locations | Sort-Object -Unique).Count -ne 37) {
  throw "sitemap.xml must contain 37 unique URLs; found $($locations.Count)."
}

$robots = Get-SiteResource -RelativePath 'robots.txt'
$siteUri = [Uri]$siteBase
$robotsHasSitemap = if ($siteUri.IsLoopback) {
  $robots.Content -match '(?m)^Sitemap:\s+https://\S+/sitemap\.xml\s*$'
} else {
  $robots.Content -match [regex]::Escape($siteBase + 'sitemap.xml')
}
if (-not $robotsHasSitemap) { throw 'robots.txt does not reference the deployed sitemap.' }

$socialImage = Get-SiteResource -RelativePath 'assets/og.jpg'
if ([string]$socialImage.Headers.'Content-Type' -notmatch '^image/jpeg') { throw 'Open Graph image is not served as JPEG.' }
if ($socialImage.RawContentLength -lt 10kb) { throw 'Open Graph image is unexpectedly small.' }

Write-Host "GitHub Pages smoke test passed for commit ${expected}: homepage, app, guide, lesson, quiz, search index, sitemap, robots, and social image."

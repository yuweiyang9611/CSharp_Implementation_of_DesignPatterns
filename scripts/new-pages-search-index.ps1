#requires -Version 7.0

[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$SiteDirectory
)

$ErrorActionPreference = 'Stop'
$site = [IO.Path]::GetFullPath($SiteDirectory)
$entries = [Collections.Generic.List[object]]::new()

function ConvertFrom-HtmlText {
  param([AllowEmptyString()][string]$Html)

  $withoutCode = [regex]::Replace($Html, '<(script|style)\b[^>]*>.*?</\1>', ' ', 'Singleline,IgnoreCase')
  $withoutTags = [regex]::Replace($withoutCode, '<[^>]+>', ' ')
  return ([Net.WebUtility]::HtmlDecode($withoutTags) -replace '\s+', ' ').Trim()
}

function Get-PageTitle {
  param([string]$Html, [string]$Fallback)

  $match = [regex]::Match($Html, '<title>(?<title>.*?)</title>', 'Singleline,IgnoreCase')
  if (-not $match.Success) { return $Fallback }
  return (ConvertFrom-HtmlText $match.Groups['title'].Value) -replace '\s*·\s*C# 设计模式学习地图\s*$', ''
}

$guideDirectory = Join-Path $site 'guides'
foreach ($file in @(Get-ChildItem -LiteralPath $guideDirectory -Filter '*.html' | Sort-Object Name)) {
  $html = Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8
  $pageTitle = Get-PageTitle -Html $html -Fallback $file.BaseName
  $mainMatch = [regex]::Match($html, '<main\b[^>]*>(?<body>.*?)</main>', 'Singleline,IgnoreCase')
  $body = if ($mainMatch.Success) { $mainMatch.Groups['body'].Value } else { $html }
  $headings = @([regex]::Matches($body, '<h(?<level>[23])\b[^>]*id="(?<id>[^"]+)"[^>]*>(?<text>.*?)</h[23]>', 'Singleline,IgnoreCase'))
  if ($headings.Count -eq 0) {
    $entries.Add([ordered]@{ kind = '指南'; title = $pageTitle; section = ''; url = 'guides/' + $file.Name; body = ConvertFrom-HtmlText $body })
    continue
  }
  for ($index = 0; $index -lt $headings.Count; $index++) {
    $heading = $headings[$index]
    $start = $heading.Index + $heading.Length
    $end = if ($index -lt $headings.Count - 1) { $headings[$index + 1].Index } else { $body.Length }
    $sectionHtml = $body.Substring($start, $end - $start)
    $entries.Add([ordered]@{
        kind = '指南'
        title = $pageTitle
        section = ConvertFrom-HtmlText $heading.Groups['text'].Value
        url = 'guides/' + $file.Name + '#' + $heading.Groups['id'].Value
        body = ConvertFrom-HtmlText $sectionHtml
      })
  }
}

$patternDirectory = Join-Path $site 'patterns'
foreach ($file in @(Get-ChildItem -LiteralPath $patternDirectory -Filter '*.html' | Sort-Object Name)) {
  $html = Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8
  $mainMatch = [regex]::Match($html, '<main\b[^>]*>(?<body>.*?)</main>', 'Singleline,IgnoreCase')
  $entries.Add([ordered]@{
      kind = '模式课件'
      title = Get-PageTitle -Html $html -Fallback $file.BaseName
      section = '四项学习证据与取舍'
      url = 'patterns/' + $file.Name
      body = ConvertFrom-HtmlText $(if ($mainMatch.Success) { $mainMatch.Groups['body'].Value } else { $html })
    })
}

$payload = [ordered]@{ version = 1; entries = @($entries) } | ConvertTo-Json -Depth 6
$destination = Join-Path (Join-Path $site 'assets') 'search-index.json'
[IO.File]::WriteAllText($destination, $payload, [Text.UTF8Encoding]::new($false))
Write-Host "Built full-text search index with $($entries.Count) searchable entries."

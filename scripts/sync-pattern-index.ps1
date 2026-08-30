[CmdletBinding()]
param(
  [switch]$Check,
  [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$indexPath = Join-Path $root 'docs/模式索引.md'
$learningCatalogPath = Join-Path $root 'site/data/learning-catalog.json'
$beginMarker = '<!-- BEGIN GENERATED PATTERN INDEX -->'
$endMarker = '<!-- END GENERATED PATTERN INDEX -->'

Push-Location $root
try {
  if (-not $NoBuild) {
    dotnet build src/DesignPatterns.Runner/DesignPatterns.Runner.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Runner build failed while synchronizing the pattern index.' }
  }

  $runnerOutput = @(dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- --catalog-json)
  if ($LASTEXITCODE -ne 0) { throw 'Runner catalog command failed while synchronizing the pattern index.' }
  $corePatterns = @(($runnerOutput -join [Environment]::NewLine) | ConvertFrom-Json)
  $learningCatalog = Get-Content -LiteralPath $learningCatalogPath -Raw -Encoding utf8 | ConvertFrom-Json
  $enrichmentPatterns = @($learningCatalog.patterns)

  if ($corePatterns.Count -ne 23) { throw "Runner catalog must contain 23 patterns; found $($corePatterns.Count)." }
  if ($enrichmentPatterns.Count -ne 23) { throw "Learning catalog must contain 23 patterns; found $($enrichmentPatterns.Count)." }

  $enrichmentByKey = @{}
  foreach ($pattern in $enrichmentPatterns) {
    if ([string]::IsNullOrWhiteSpace($pattern.key) -or $enrichmentByKey.ContainsKey($pattern.key)) {
      throw "Learning catalog contains an empty or duplicate key: $($pattern.key)"
    }
    $enrichmentByKey[$pattern.key] = $pattern
  }

  $categoryLabels = @{
    Creational = '创建型'
    Structural = '结构型'
    Behavioral = '行为型'
  }
  $lines = [Collections.Generic.List[string]]::new()
  $lines.Add('| # | 模式 | 分类 | Runner key | 独立源码 | 实战落点 | 教程章节 |')
  $lines.Add('| ---: | --- | --- | --- | --- | --- | --- |')
  $seenKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

  for ($index = 0; $index -lt $corePatterns.Count; $index++) {
    $core = $corePatterns[$index]
    $expectedNumber = $index + 1
    if ($core.number -ne $expectedNumber) { throw "Runner pattern numbers must be continuous; expected $expectedNumber, found $($core.number)." }
    if (-not $seenKeys.Add([string]$core.key)) { throw "Runner catalog contains duplicate key: $($core.key)" }
    if (-not $categoryLabels.ContainsKey([string]$core.category)) { throw "Unknown pattern category: $($core.category)" }
    if (-not $enrichmentByKey.ContainsKey([string]$core.key)) { throw "Learning catalog is missing key: $($core.key)" }

    $extra = $enrichmentByKey[[string]$core.key]
    foreach ($field in @('source', 'practice', 'practiceLabel', 'guide')) {
      if ([string]::IsNullOrWhiteSpace([string]$extra.$field)) { throw "Learning catalog field '$field' is empty for $($core.key)." }
    }
    if (-not ([string]$extra.guide).StartsWith('#')) { throw "Guide target must be an anchor for $($core.key): $($extra.guide)" }
    foreach ($path in @([string]$extra.source, [string]$extra.practice)) {
      if ([IO.Path]::IsPathRooted($path) -or $path -match '(^|[\\/])\.\.([\\/]|$)') { throw "Catalog path must stay inside the repository: $path" }
      if (-not (Test-Path -LiteralPath (Join-Path $root $path))) { throw "Catalog path does not exist: $path" }
    }

    $nameParts = @(([string]$core.name) -split '\s+/\s+', 2)
    if ($nameParts.Count -ne 2) { throw "Pattern name must use 'English / 中文模式': $($core.name)" }
    $english = $nameParts[0]
    $chinese = $nameParts[1] -replace '模式$', ''
    $displayName = "$english（$chinese）"
    $category = $categoryLabels[[string]$core.category]
    $source = ([string]$extra.source).Replace('\', '/')
    $practice = ([string]$extra.practice).Replace('\', '/')
    $sourceLabel = [IO.Path]::GetFileName($source)
    $guideTarget = 'CSharp设计模式学习指南.md' + [string]$extra.guide
    $lines.Add("| $expectedNumber | $displayName | $category | ``$($core.key)`` | [$sourceLabel](../$source) | [$($extra.practiceLabel)](../$practice) | [第 $expectedNumber 章]($guideTarget) |")
  }

  foreach ($extra in $enrichmentPatterns) {
    if (-not $seenKeys.Contains([string]$extra.key)) { throw "Learning catalog contains orphan key: $($extra.key)" }
  }

  $content = Get-Content -LiteralPath $indexPath -Raw -Encoding utf8
  $beginIndex = $content.IndexOf($beginMarker, [StringComparison]::Ordinal)
  $endIndex = $content.IndexOf($endMarker, [StringComparison]::Ordinal)
  if ($beginIndex -lt 0 -or $endIndex -le $beginIndex) { throw 'Pattern index generated markers are missing or out of order.' }

  $newline = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
  $blockStart = $beginIndex + $beginMarker.Length
  $currentBlock = $content.Substring($blockStart, $endIndex - $blockStart).Trim("`r", "`n")
  $expectedBlock = $lines -join $newline

  if ($Check) {
    if ($currentBlock -cne $expectedBlock) {
      $currentLines = @($currentBlock -split '\r?\n')
      $expectedLines = @($expectedBlock -split '\r?\n')
      $differenceAt = 0
      while ($differenceAt -lt [Math]::Min($currentLines.Count, $expectedLines.Count) -and
        $currentLines[$differenceAt] -ceq $expectedLines[$differenceAt]) {
        $differenceAt++
      }
      $actual = if ($differenceAt -lt $currentLines.Count) { $currentLines[$differenceAt] } else { '<missing>' }
      $expected = if ($differenceAt -lt $expectedLines.Count) { $expectedLines[$differenceAt] } else { '<missing>' }
      throw "docs/模式索引.md is out of sync at generated line $($differenceAt + 1). Expected '$expected', found '$actual'. Run ./scripts/sync-pattern-index.ps1 and commit the result."
    }
    Write-Host 'Pattern index is synchronized with Runner and learning catalog (23 rows).'
    return
  }

  $updated = $content.Substring(0, $blockStart) + $newline + $expectedBlock + $newline + $content.Substring($endIndex)
  [IO.File]::WriteAllText($indexPath, $updated, [Text.UTF8Encoding]::new($false))
  Write-Host 'Pattern index synchronized: docs/模式索引.md (23 generated rows).'
}
finally {
  Pop-Location
}

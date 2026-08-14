[CmdletBinding()]
param(
  [string]$ResultsDirectory = 'output/test-results',
  [double]$MinimumLineRate = 0.55,
  [double]$MinimumBranchRate = 0.40,
  [int]$ExpectedReports = 4
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$resolvedResults = [IO.Path]::GetFullPath((Join-Path $root $ResultsDirectory))

if (-not (Test-Path -LiteralPath $resolvedResults)) {
  throw "Coverage results directory does not exist: $resolvedResults"
}

$allReports = @(
  Get-ChildItem -LiteralPath $resolvedResults -Recurse -File -Filter 'coverage.cobertura.xml' |
    Sort-Object FullName
)
$reports = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
$nonCanonicalReports = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
$pathSeparators = [char[]]'\/'

foreach ($report in $allReports) {
  # VSTest copies each final attachment to <results>/<run-id>/coverage.cobertura.xml,
  # but hosted runners can retain deeper staging copies such as <run>/In/<machine>/....
  if ($null -ne $report.Directory.Parent -and
      $report.Directory.Parent.FullName -eq $resolvedResults) {
    $reports.Add($report)
  }
  else {
    $nonCanonicalReports.Add($report)
  }
}

if ($nonCanonicalReports.Count -gt 0) {
  Write-Host ('Ignoring {0} non-canonical Cobertura report(s), including VSTest staging copies:' -f
      $nonCanonicalReports.Count)
  foreach ($report in $nonCanonicalReports) {
    $relativePath = $report.FullName.Substring($resolvedResults.Length).TrimStart($pathSeparators)
    Write-Host "  ignored: $relativePath"
  }
}

if ($reports.Count -ne $ExpectedReports) {
  throw (
    "Expected $ExpectedReports Cobertura reports, but found $($reports.Count) canonical reports " +
    "($($allReports.Count) total; $($nonCanonicalReports.Count) non-canonical reports ignored) " +
    "in $resolvedResults."
  )
}

$failures = [System.Collections.Generic.List[string]]::new()
foreach ($report in $reports) {
  [xml]$coverage = Get-Content -LiteralPath $report.FullName -Raw -Encoding utf8
  $lineRate = [double]::Parse(
    $coverage.coverage.'line-rate',
    [Globalization.CultureInfo]::InvariantCulture)
  $branchRate = [double]::Parse(
    $coverage.coverage.'branch-rate',
    [Globalization.CultureInfo]::InvariantCulture)
  $packages = @($coverage.coverage.packages.package | ForEach-Object { $_.name }) -join ', '

  Write-Host ('Coverage: line {0:P1}, branch {1:P1} - {2}' -f $lineRate, $branchRate, $packages)

  if ($lineRate -lt $MinimumLineRate) {
    $failures.Add(('{0}: line coverage {1:P1} is below {2:P1}.' -f
        $packages, $lineRate, $MinimumLineRate))
  }

  if ($branchRate -lt $MinimumBranchRate) {
    $failures.Add(('{0}: branch coverage {1:P1} is below {2:P1}.' -f
        $packages, $branchRate, $MinimumBranchRate))
  }
}

if ($failures.Count -gt 0) {
  Write-Error ("Coverage verification failed:`n- " + ($failures -join "`n- "))
}

Write-Host ('Coverage verification passed: {0} reports, minimum line {1:P0}, minimum branch {2:P0}.' -f
    $reports.Count, $MinimumLineRate, $MinimumBranchRate)

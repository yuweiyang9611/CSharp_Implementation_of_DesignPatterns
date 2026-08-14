[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$verifier = Join-Path $root 'scripts/verify-coverage.ps1'
$tmpRoot = [IO.Path]::GetFullPath((Join-Path $root 'tmp'))
$fixtureName = 'coverage-verifier-self-test-' + [Guid]::NewGuid().ToString('N')
$fixtureRelativePath = Join-Path 'tmp' $fixtureName
$fixtureRoot = [IO.Path]::GetFullPath((Join-Path $root $fixtureRelativePath))
$tmpPrefix = $tmpRoot.TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar

if (-not $fixtureRoot.StartsWith($tmpPrefix, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Refusing to create a coverage fixture outside the repository tmp directory: $fixtureRoot"
}

function New-CoverageReport {
  param(
    [Parameter(Mandatory)]
    [string]$Path
  )

  $directory = Split-Path -Parent $Path
  New-Item -ItemType Directory -Path $directory -Force | Out-Null
  @'
<?xml version="1.0" encoding="utf-8"?>
<coverage line-rate="0.80" branch-rate="0.60">
  <packages>
    <package name="CoverageVerifierFixture" />
  </packages>
</coverage>
'@ | Set-Content -LiteralPath $Path -Encoding utf8
}

function Assert-CoverageCountFailure {
  param(
    [Parameter(Mandatory)]
    [int]$ExpectedFound
  )

  $failedAsExpected = $false
  try {
    & $verifier -ResultsDirectory $fixtureRelativePath -ExpectedReports 4
  }
  catch {
    $expectedMessage = "Expected 4 Cobertura reports, but found $ExpectedFound canonical reports"
    if ($_.Exception.Message -notlike "*$expectedMessage*") {
      throw
    }

    $failedAsExpected = $true
  }

  if (-not $failedAsExpected) {
    throw "Coverage verifier unexpectedly accepted $ExpectedFound eligible reports."
  }
}

try {
  $canonicalReports = [System.Collections.Generic.List[string]]::new()
  for ($index = 1; $index -le 4; $index++) {
    $canonicalDirectory = Join-Path $fixtureRoot ([Guid]::NewGuid().ToString())
    $canonicalReport = Join-Path $canonicalDirectory 'coverage.cobertura.xml'
    New-CoverageReport -Path $canonicalReport
    $canonicalReports.Add($canonicalReport)

    $stagingReport = Join-Path $fixtureRoot (
      "runneradmin_ci_$index/In/fixture-host/coverage.cobertura.xml"
    )
    New-Item -ItemType Directory -Path (Split-Path -Parent $stagingReport) -Force | Out-Null
    Copy-Item -LiteralPath $canonicalReport -Destination $stagingReport
  }

  $physicalReports = @(
    Get-ChildItem -LiteralPath $fixtureRoot -Recurse -File -Filter 'coverage.cobertura.xml'
  )
  if ($physicalReports.Count -ne 8) {
    throw "Coverage fixture should contain 8 physical reports, but found $($physicalReports.Count)."
  }

  & $verifier -ResultsDirectory $fixtureRelativePath -ExpectedReports 4

  $nestedStagingReport = Join-Path $fixtureRoot 'runner_ci/Staging/fixture-host/coverage.cobertura.xml'
  New-CoverageReport -Path $nestedStagingReport
  & $verifier -ResultsDirectory $fixtureRelativePath -ExpectedReports 4
  Remove-Item -LiteralPath $nestedStagingReport -Force

  $extraCanonicalDirectory = Join-Path $fixtureRoot ([Guid]::NewGuid().ToString())
  $extraCanonicalReport = Join-Path $extraCanonicalDirectory 'coverage.cobertura.xml'
  New-CoverageReport -Path $extraCanonicalReport
  Assert-CoverageCountFailure -ExpectedFound 5
  Remove-Item -LiteralPath $extraCanonicalReport -Force

  Remove-Item -LiteralPath $canonicalReports[0] -Force
  Assert-CoverageCountFailure -ExpectedFound 3

  Write-Host 'Coverage verifier self-test passed: staging duplicates were ignored and strict report counts were preserved.'
}
finally {
  if (Test-Path -LiteralPath $fixtureRoot) {
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
  }
}

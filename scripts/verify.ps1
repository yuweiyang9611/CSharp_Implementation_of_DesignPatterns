#requires -Version 7.0

[CmdletBinding()]
param(
  [ValidateSet('Quick', 'Full')]
  [string]$Mode = 'Full',
  [switch]$SkipPdf,
  [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
  if ($Mode -eq 'Full' -and $NoRestore) {
    throw 'Full verification requires a locked restore. Use -Mode Quick -NoRestore for an offline code check.'
  }
  $requiredCommands = @('dotnet', 'node')
  if ($Mode -eq 'Full') { $requiredCommands += 'npm' }
  foreach ($command in $requiredCommands) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
      throw "Required command '$command' was not found. Install .NET 10 SDK and Node.js 24 (including npm), then retry."
    }
  }
  Write-Host "Running $Mode verification."

  if ($Mode -eq 'Full') {
    dotnet restore DesignPatterns.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked dependency restore failed.' }

    dotnet format DesignPatterns.sln --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Formatting verification failed.' }
  }

  $buildArguments = @('build', 'DesignPatterns.sln', '--configuration', 'Release')
  if ($NoRestore -or $Mode -eq 'Full') { $buildArguments += '--no-restore' }

  dotnet @buildArguments
  if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }

  $testArguments = @('test', 'DesignPatterns.sln', '--configuration', 'Release', '--no-build', '--no-restore')
  if ($Mode -eq 'Full') {
    # Keep every run isolated so stale reports cannot satisfy or break the coverage gate.
    $resultsDirectory = 'output/test-results/verify-' + [Guid]::NewGuid().ToString('N')
    $testArguments += @('--logger', 'trx', '--results-directory', $resultsDirectory, '--collect', 'XPlat Code Coverage')
  }
  dotnet @testArguments
  if ($LASTEXITCODE -ne 0) { throw 'Solution tests failed.' }
  if ($Mode -eq 'Full') {
    & (Join-Path $root 'tests/Scripts/verify-coverage.self-test.ps1')
    & (Join-Path $PSScriptRoot 'verify-coverage.ps1') -ResultsDirectory $resultsDirectory
  }

  dotnet run --project tests/DesignPatterns.SmokeTests --configuration Release --no-build
  if ($LASTEXITCODE -ne 0) { throw 'Smoke tests failed.' }

  dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- --list
  if ($LASTEXITCODE -ne 0) { throw 'Runner catalog failed.' }

  & (Join-Path $PSScriptRoot 'test-learning-catalog.ps1') -NoBuild

  & (Join-Path $PSScriptRoot 'run-teaching-projects.ps1') -SelfTest -NoBuild

  & (Join-Path $PSScriptRoot 'verify-docs.ps1') -NoBuild

  if ($Mode -eq 'Quick') {
    node tests/review-scheduler.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Review scheduler tests failed.' }
    node tests/storage-failure.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Storage failure tests failed.' }
    node tests/exercise-state.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Exercise catalog and storage tests failed.' }
    Write-Host 'Quick verification passed. Formatting, coverage gates, site build, browser regression and PDF export require Full mode.'
    return
  }

  & (Join-Path $root 'tests/Scripts/learning-catalog.self-test.ps1')

  $pagesSite = Join-Path $root 'output/pages-site'
  & (Join-Path $PSScriptRoot 'build-pages.ps1') -NoBuild
  & (Join-Path $PSScriptRoot 'verify-pages-site.ps1') -SiteDirectory $pagesSite -RepositoryRoot $root
  & (Join-Path $root 'tests/Scripts/pages-site-verifier.self-test.ps1') -SiteDirectory $pagesSite

  npm ci --ignore-scripts
  if ($LASTEXITCODE -ne 0) { throw 'Locked browser dependency installation failed.' }
  npm run test:site
  if ($LASTEXITCODE -ne 0) { throw 'Browser and accessibility regression failed. A system Chrome or Edge installation is required (or set CHROME_PATH).' }

  & (Join-Path $PSScriptRoot 'export-all-guides.ps1') -HtmlOnly:$SkipPdf -NoBuild
  Write-Host "Full verification passed. Coverage reports: $resultsDirectory"
}
finally {
  Pop-Location
}

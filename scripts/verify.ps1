[CmdletBinding()]
param(
  [switch]$SkipPdf,
  [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
  $buildArguments = @('build', 'DesignPatterns.sln', '--configuration', 'Release')
  if ($NoRestore) { $buildArguments += '--no-restore' }

  dotnet @buildArguments
  if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }

  dotnet test DesignPatterns.sln --configuration Release --no-build --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Solution tests failed.' }

  & (Join-Path $root 'tests/Scripts/verify-coverage.self-test.ps1')

  dotnet run --project tests/DesignPatterns.SmokeTests --configuration Release --no-build
  if ($LASTEXITCODE -ne 0) { throw 'Smoke tests failed.' }

  dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- --list
  if ($LASTEXITCODE -ne 0) { throw 'Runner catalog failed.' }

  & (Join-Path $PSScriptRoot 'run-teaching-projects.ps1') -SelfTest -NoBuild

  & (Join-Path $PSScriptRoot 'verify-docs.ps1') -NoBuild

  & (Join-Path $PSScriptRoot 'export-all-guides.ps1') -HtmlOnly:$SkipPdf -NoBuild
}
finally {
  Pop-Location
}

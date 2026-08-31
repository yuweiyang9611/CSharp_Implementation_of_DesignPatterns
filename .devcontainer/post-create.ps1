#requires -Version 7.0

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
  dotnet restore DesignPatterns.sln --locked-mode
  if ($LASTEXITCODE -ne 0) { throw 'Locked .NET restore failed.' }
  dotnet build DesignPatterns.sln --configuration Release --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
  npm ci --ignore-scripts
  if ($LASTEXITCODE -ne 0) { throw 'Node dependency restore failed.' }
  & (Join-Path $root 'scripts/build-pages.ps1') -NoBuild
  Write-Host 'Ready. Run the VS Code task “Site: preview” to open the learning site on port 4173.'
}
finally {
  Pop-Location
}

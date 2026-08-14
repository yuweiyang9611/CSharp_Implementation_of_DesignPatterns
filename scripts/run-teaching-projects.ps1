[CmdletBinding()]
param(
  [switch]$SelfTest,
  [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projects = @(
  'examples/OnlineStore/DesignPatterns.TeachingProjects.OnlineStore.csproj',
  'examples/SmartHome/DesignPatterns.TeachingProjects.SmartHome.csproj',
  'examples/DocumentWorkflow/DesignPatterns.TeachingProjects.DocumentWorkflow.csproj'
)

Push-Location $root

try {
  foreach ($project in $projects) {
    $arguments = @('run', '--project', $project, '--configuration', 'Release')
    if ($NoBuild) {
      $arguments += '--no-build'
    }

    if ($SelfTest) {
      $arguments += @('--', '--self-test')
    }

    Write-Host "`n=== $project ===" -ForegroundColor Cyan
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
      throw "Teaching project failed: $project"
    }
  }
}
finally {
  Pop-Location
}

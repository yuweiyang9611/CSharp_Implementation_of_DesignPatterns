#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'tools/BrowserExercises'
$dotnet = if ($env:CSHARP_DESIGN_PATTERNS_WASM_DOTNET) { $env:CSHARP_DESIGN_PATTERNS_WASM_DOTNET } else { 'dotnet' }
# dotnet looks for global.json from the working directory, so run here to get the project's pinned SDK.
Push-Location $project
try {
  & $dotnet restore BrowserExercises.csproj --locked-mode
  if ($LASTEXITCODE -ne 0) { throw 'Browser restore failed. Install the SDK pinned in tools/BrowserExercises/global.json, plus wasm-tools and wasm-experimental.' }
  & $dotnet publish BrowserExercises.csproj -c Release --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Browser compiler publish failed.' }
  $output = Join-Path $project 'bin/Release/net10.0'
  $framework = Join-Path $output 'publish/wwwroot/_framework'
  if (-not (Test-Path $framework)) { throw "Published browser framework is missing: $framework" }
  New-Item -ItemType Directory -Force -Path $Destination | Out-Null
  Copy-Item -LiteralPath $framework -Destination $Destination -Recurse -Force
  $references = @('System.Runtime.dll', 'System.Collections.dll', 'System.Linq.dll', 'System.Console.dll',
    'System.Runtime.Extensions.dll', 'System.ObjectModel.dll', 'System.Reflection.dll', 'System.Reflection.Extensions.dll',
    'System.Threading.dll', 'System.Threading.Tasks.dll', 'netstandard.dll')
  $referenceDirectory = Join-Path $Destination 'refs'
  New-Item -ItemType Directory -Force -Path $referenceDirectory | Out-Null
  foreach ($reference in $references) {
    Copy-Item -LiteralPath (Join-Path $output "compiler-refs/$reference") -Destination $referenceDirectory
  }
  $references | ConvertTo-Json | Set-Content (Join-Path $referenceDirectory 'manifest.json') -Encoding utf8
  Write-Host 'Browser C# compiler published with the exercise reference set.'
} finally { Pop-Location }

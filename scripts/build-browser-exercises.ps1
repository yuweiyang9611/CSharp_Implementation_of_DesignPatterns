#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'tools/BrowserExercises'
$dotnet = if ($env:CSHARP_DESIGN_PATTERNS_WASM_DOTNET) { $env:CSHARP_DESIGN_PATTERNS_WASM_DOTNET } else { 'dotnet' }
# Any .NET SDK from 11 up builds this; no global.json pins one. The WebAssembly workloads are wasm-tools and
# wasm-experimental on a .NET 11 SDK, and wasm-tools-net11 and wasm-experimental-net11 on a newer SDK.
# The host is not locked (see BrowserExercises.csproj), so a package added there would escape the lock files:
# keep packages in ExerciseEngine.
if ((Get-Content -LiteralPath (Join-Path $project 'BrowserExercises.csproj') -Raw) -match '<PackageReference\b') {
  throw 'BrowserExercises.csproj has a PackageReference. The host has no lock file; add the package to ExerciseEngine, whose lock file covers it.'
}
Push-Location $project
try {
  & $dotnet restore BrowserExercises.csproj --locked-mode
  if ($LASTEXITCODE -ne 0) {
    throw ('Browser restore failed. Use a .NET SDK 11 or later with the WebAssembly workloads: ' +
      'wasm-tools and wasm-experimental on a .NET 11 SDK, wasm-tools-net11 and wasm-experimental-net11 on a newer SDK. ' +
      'If NU1004 names a Roslyn package, refresh the ExerciseEngine lock file with `dotnet restore --force-evaluate`.')
  }
  & $dotnet publish BrowserExercises.csproj -c Release --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Browser compiler publish failed.' }
  $output = Join-Path $project 'bin/Release/net11.0'
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

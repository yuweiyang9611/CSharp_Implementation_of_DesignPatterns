[CmdletBinding()]
param(
  [string]$InputPath,
  [string]$OutputPath,
  [switch]$HtmlOnly,
  [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projectDirectory = Join-Path (Join-Path $root 'tools') 'GuideExporter'
$projectPath = Join-Path $projectDirectory 'GuideExporter.csproj'
$inputVariable = 'CSHARP_DESIGN_PATTERNS_GUIDE_INPUT_PATH'
$outputVariable = 'CSHARP_DESIGN_PATTERNS_GUIDE_OUTPUT_PATH'
$restoreInput = $false
$restoreOutput = $false

try {
  # Windows PowerShell 5.1 can corrupt non-ASCII native-process arguments.
  # Environment variables preserve the Unicode paths when the C# tool reads them.
  # When omitted, let the compiled C# defaults supply the Chinese filenames so
  # this BOM-free script itself contains only ASCII path literals.
  if (-not [string]::IsNullOrWhiteSpace($InputPath)) {
    $hadInput = Test-Path "Env:$inputVariable"
    $oldInput = [System.Environment]::GetEnvironmentVariable($inputVariable, 'Process')
    [System.Environment]::SetEnvironmentVariable(
      $inputVariable,
      [System.IO.Path]::GetFullPath($InputPath),
      'Process')
    $restoreInput = $true
  }

  if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $hadOutput = Test-Path "Env:$outputVariable"
    $oldOutput = [System.Environment]::GetEnvironmentVariable($outputVariable, 'Process')
    [System.Environment]::SetEnvironmentVariable(
      $outputVariable,
      [System.IO.Path]::GetFullPath($OutputPath),
      'Process')
    $restoreOutput = $true
  }

  $runArguments = @('run', '--project', $projectPath, '--configuration', 'Release')
  if ($NoBuild) { $runArguments += @('--no-build', '--no-restore') }
  if ($HtmlOnly) { $runArguments += @('--', '--html-only') }

  dotnet @runArguments
  if ($LASTEXITCODE -ne 0) {
    throw "Guide export failed with exit code $LASTEXITCODE."
  }
}
finally {
  if ($restoreInput) {
    if ($hadInput) {
      [System.Environment]::SetEnvironmentVariable($inputVariable, $oldInput, 'Process')
    }
    else {
      [System.Environment]::SetEnvironmentVariable($inputVariable, $null, 'Process')
    }
  }

  if ($restoreOutput) {
    if ($hadOutput) {
      [System.Environment]::SetEnvironmentVariable($outputVariable, $oldOutput, 'Process')
    }
    else {
      [System.Environment]::SetEnvironmentVariable($outputVariable, $null, 'Process')
    }
  }
}

[CmdletBinding()]
param(
  [switch]$HtmlOnly,
  [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exportScript = Join-Path $PSScriptRoot 'export-guide.ps1'
$outputDirectory = Join-Path (Join-Path $root 'output') 'pdf'

function Decode-Name {
  param([string]$Base64)

  return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Base64))
}

$exports = @(
  @{
    Input = Join-Path (Join-Path $root 'docs') (Decode-Name 'Q1NoYXJw6K6+6K6h5qih5byP5a2m5Lmg5oyH5Y2XLm1k')
    Output = Join-Path $outputDirectory (Decode-Name 'Q1NoYXJw6K6+6K6h5qih5byP5a2m5Lmg5oyH5Y2XLnBkZg==')
  },
  @{
    Input = Join-Path (Join-Path $root 'docs') (Decode-Name '6K6+6K6h5qih5byP5a6e5oiY6aG555uu5a2m5Lmg5oyH5Y2XLm1k')
    Output = Join-Path $outputDirectory (Decode-Name '6K6+6K6h5qih5byP5a6e5oiY6aG555uu5a2m5Lmg5oyH5Y2XLnBkZg==')
  },
  @{
    Input = Join-Path $root 'START_HERE.md'
    Output = Join-Path $outputDirectory 'CSharp-Design-Patterns-Learning-Path.pdf'
  },
  @{
    Input = Join-Path (Join-Path (Join-Path $root 'labs') 'CheckoutRefactoringKata') 'README.md'
    Output = Join-Path $outputDirectory 'Checkout-Refactoring-Workshop.pdf'
  },
  @{
    Input = Join-Path (Join-Path (Join-Path $root 'labs') 'ReliableCheckout') 'README.md'
    Output = Join-Path $outputDirectory 'Reliable-Checkout-Graduation-Project.pdf'
  }
)

if (-not $NoBuild) {
  $exporterProject = Join-Path (Join-Path (Join-Path $root 'tools') 'GuideExporter') 'GuideExporter.csproj'
  dotnet build $exporterProject --configuration Release
  if ($LASTEXITCODE -ne 0) {
    throw 'GuideExporter build failed.'
  }
}

foreach ($export in $exports) {
  if (-not (Test-Path -LiteralPath $export.Input)) {
    throw "Guide input does not exist: $($export.Input)"
  }

  & $exportScript -InputPath $export.Input -OutputPath $export.Output -HtmlOnly:$HtmlOnly -NoBuild
  if ($LASTEXITCODE -ne 0) {
    throw "Guide export failed: $($export.Input)"
  }
}

$kind = if ($HtmlOnly) { 'HTML previews' } else { 'PDF and HTML files' }
Write-Host "Generated $($exports.Count) guide $kind in $outputDirectory"

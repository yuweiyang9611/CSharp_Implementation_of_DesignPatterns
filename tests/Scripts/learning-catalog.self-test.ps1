#requires -Version 7.0

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$validator = Join-Path $root 'scripts/test-learning-catalog.ps1'
$catalogPath = Join-Path $root 'site/data/learning-catalog.json'
$schemaPath = Join-Path $root 'site/data/learning-catalog.schema.json'
$catalogJson = Get-Content -LiteralPath $catalogPath -Raw -Encoding utf8
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('design-patterns-catalog-self-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Write-MutatedCatalog {
  param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][scriptblock]$Mutation
  )

  $candidate = $catalogJson | ConvertFrom-Json -Depth 100
  & $Mutation $candidate
  $path = Join-Path $tempRoot ($Name + '.json')
  [IO.File]::WriteAllText($path, ($candidate | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
  return $path
}

function Assert-Rejected {
  param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$ExpectedMessage,
    [Parameter(Mandatory)][scriptblock]$Mutation
  )

  $path = Write-MutatedCatalog -Name $Name -Mutation $Mutation
  $rejected = $false
  $diagnostic = ''
  try {
    & $validator -CatalogPath $path -SchemaPath $schemaPath -SkipRunner | Out-Null
  }
  catch {
    $rejected = $true
    $diagnostic = $_.Exception.Message
  }
  if (-not $rejected) { throw "Catalog self-test '$Name' should have failed." }
  if ([string]::IsNullOrWhiteSpace($diagnostic)) { throw "Catalog self-test '$Name' returned no diagnostic." }
  if ($diagnostic -notmatch $ExpectedMessage) { throw "Catalog self-test '$Name' failed for the wrong reason: $diagnostic" }
}

try {
  & $validator -CatalogPath $catalogPath -SchemaPath $schemaPath -SkipRunner
  Assert-Rejected -Name 'duplicate-tag' -ExpectedMessage 'Duplicate problem tag id' -Mutation { param($catalog) $catalog.problemTags[1].id = $catalog.problemTags[0].id }
  Assert-Rejected -Name 'unknown-tag' -ExpectedMessage 'references unknown tag' -Mutation { param($catalog) $catalog.patterns[0].problemTags[0] = 'missing-tag' }
  Assert-Rejected -Name 'self-related' -ExpectedMessage 'must not reference itself' -Mutation { param($catalog) $catalog.patterns[0].related[0] = $catalog.patterns[0].key }
  Assert-Rejected -Name 'unsafe-path' -ExpectedMessage 'safe forward-slash repository path' -Mutation { param($catalog) $catalog.patterns[0].source = '../outside.cs' }
  Assert-Rejected -Name 'invalid-quiz-answer' -ExpectedMessage 'correctKey must appear' -Mutation { param($catalog) $catalog.quizzes[0].correctKey = 'adapter' }
  Assert-Rejected -Name 'unexpected-property' -ExpectedMessage 'schema validation failed' -Mutation { param($catalog) $catalog | Add-Member -NotePropertyName unexpected -NotePropertyValue $true }
  Write-Host 'Learning catalog self-test passed: schema, references, paths, and quiz answers reject invalid data.'
}
finally {
  if (Test-Path -LiteralPath $tempRoot) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
  }
}

[CmdletBinding()]
param(
  [string]$Email
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))

function Test-GitHubNoreplyEmail {
  param([string]$Value)

  if ([string]::IsNullOrWhiteSpace($Value)) { return $false }

  $candidate = $Value.Trim()
  return $candidate -eq 'noreply@github.com' -or
    $candidate -match '^[^@\s]+@users\.noreply\.github\.com$'
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
  throw 'Git was not found on PATH.'
}

$topLevelOutput = & git -C $root rev-parse --show-toplevel 2>$null
if ($LASTEXITCODE -ne 0) {
  throw 'This script must be run from a cloned Git repository.'
}

$topLevel = [System.IO.Path]::GetFullPath(($topLevelOutput | Select-Object -Last 1).Trim())
if (-not $topLevel.Equals($root, [System.StringComparison]::OrdinalIgnoreCase)) {
  throw 'The script location does not match the Git repository root.'
}

$requiredHooks = @(
  (Join-Path $root '.githooks/pre-commit'),
  (Join-Path $root '.githooks/pre-push')
)

foreach ($hook in $requiredHooks) {
  if (-not (Test-Path -LiteralPath $hook -PathType Leaf)) {
    throw "Required hook is missing: $hook"
  }
}

if (-not [string]::IsNullOrWhiteSpace($Email)) {
  $candidateEmail = $Email.Trim()
  if (-not (Test-GitHubNoreplyEmail $candidateEmail)) {
    throw 'The supplied email is not a GitHub noreply address. No Git configuration was changed.'
  }

  & git -C $root config --local user.email $candidateEmail
  if ($LASTEXITCODE -ne 0) { throw 'Failed to set the local Git user.email.' }
}

$effectiveEmailOutput = & git -C $root config --get user.email 2>$null
$effectiveEmail = if ($LASTEXITCODE -eq 0) {
  ($effectiveEmailOutput | Select-Object -Last 1).Trim()
}
else {
  ''
}

if (-not (Test-GitHubNoreplyEmail $effectiveEmail)) {
  throw 'The effective Git user.email is not a GitHub noreply address (value redacted). Rerun with -Email YOUR_NOREPLY_ADDRESS.'
}

& git -C $root config --local user.email $effectiveEmail
if ($LASTEXITCODE -ne 0) { throw 'Failed to pin the noreply email in the local repository configuration.' }

& git -C $root config --local core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) { throw 'Failed to configure core.hooksPath.' }

$configuredHooksPath = & git -C $root config --local --get core.hooksPath
if ($LASTEXITCODE -ne 0 -or ($configuredHooksPath | Select-Object -Last 1).Trim() -ne '.githooks') {
  throw 'core.hooksPath verification failed.'
}

& git -C $root hook run pre-commit
if ($LASTEXITCODE -ne 0) { throw 'The pre-commit hook self-check failed.' }

Write-Host 'Git hooks enabled for this clone.'
Write-Host 'Local user.email is a GitHub noreply address (value not displayed).'
Write-Host 'core.hooksPath=.githooks'

#requires -Version 7.0

[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$SiteDirectory
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$verifier = Join-Path $root 'scripts/verify-pages-site.ps1'
$source = [IO.Path]::GetFullPath($SiteDirectory)
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$tempRoot = Join-Path $tempBase ('csharp-patterns-pages-selftest-' + [Guid]::NewGuid().ToString('N'))

function New-TestCopy {
  param([string]$Name)

  $destination = Join-Path $tempRoot $Name
  New-Item -ItemType Directory -Path $destination -Force | Out-Null
  Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
  return $destination
}

function Assert-Rejected {
  param(
    [string]$Name,
    [string]$ExpectedMessage,
    [scriptblock]$Mutation
  )

  $copy = New-TestCopy -Name $Name
  & $Mutation $copy
  try {
    & $verifier -SiteDirectory $copy -RepositoryRoot $root
    throw "Verifier unexpectedly accepted mutation: $Name"
  } catch {
    if ($_.Exception.Message -like 'Verifier unexpectedly accepted*') { throw }
    if ($_.Exception.Message -notmatch $ExpectedMessage) {
      throw "Verifier rejected '$Name' for the wrong reason: $($_.Exception.Message)"
    }
  }
}

if (-not $tempRoot.StartsWith($tempBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Refusing to use an unsafe self-test directory: $tempRoot"
}

try {
  & $verifier -SiteDirectory $source -RepositoryRoot $root
  Assert-Rejected -Name 'missing-progress' -ExpectedMessage 'Missing or empty Pages output: assets/progress\.js' -Mutation {
    param($copy)
    Remove-Item -LiteralPath (Join-Path $copy 'assets/progress.js') -Force
  }
  Assert-Rejected -Name 'missing-canonical' -ExpectedMessage 'exactly one canonical URL: quiz\.html' -Mutation {
    param($copy)
    $path = Join-Path $copy 'quiz.html'
    $content = Get-Content -LiteralPath $path -Raw -Encoding utf8
    $content = [regex]::Replace($content, '\s*<link rel="canonical"[^>]+>', '', 1)
    [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false))
  }
  Assert-Rejected -Name 'invalid-version' -ExpectedMessage '40-character lowercase SHA' -Mutation {
    param($copy)
    [IO.File]::WriteAllText((Join-Path $copy 'version.json'), '{"commit":"not-a-sha"}', [Text.UTF8Encoding]::new($false))
  }
  Write-Host 'Pages verifier self-test passed: missing assets, canonical metadata, and invalid revisions are rejected.'
}
finally {
  if ((Test-Path -LiteralPath $tempRoot) -and $tempRoot.StartsWith($tempBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
  }
}

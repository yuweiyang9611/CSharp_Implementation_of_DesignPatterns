[CmdletBinding()]
param(
  [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

function Get-MarkdownAnchors {
  param([string]$Path)

  $anchors = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  $occurrences = @{}

  foreach ($line in Get-Content -LiteralPath $Path -Encoding utf8) {
    if ($line -notmatch '^#{1,6}\s+(.+?)\s*#*\s*$') { continue }

    $heading = $Matches[1]
    $heading = [regex]::Replace($heading, '!\[([^\]]*)\]\([^)]*\)', '$1')
    $heading = [regex]::Replace($heading, '\[([^\]]+)\]\([^)]*\)', '$1')
    $heading = [regex]::Replace($heading, '<[^>]+>', '')
    $heading = $heading.Replace('`', '').ToLowerInvariant()
    $slug = [regex]::Replace($heading, '[^\p{L}\p{N}\p{M}\s_-]', '')
    $slug = [regex]::Replace($slug.Trim(), '\s+', '-')

    if (-not $occurrences.ContainsKey($slug)) {
      $occurrences[$slug] = 0
      [void]$anchors.Add($slug)
    }
    else {
      $occurrences[$slug]++
      [void]$anchors.Add("$slug-$($occurrences[$slug])")
    }
  }

  return $anchors
}

try {
  $issues = [System.Collections.Generic.List[string]]::new()
  $checkedLinks = 0
  $checkedAnchors = 0
  $markdownFiles = Get-ChildItem -Path $root -Recurse -File -Filter '*.md' |
    Where-Object {
      $_.FullName -notmatch '[\\/](\.git|bin|node_modules|obj|output|tmp)[\\/]'
    }

  foreach ($markdownFile in $markdownFiles) {
    $content = Get-Content -LiteralPath $markdownFile.FullName -Raw -Encoding utf8
    $matches = [regex]::Matches($content, '!?\[[^\]]*\]\((?<target>[^)]+)\)')

    foreach ($match in $matches) {
      $rawTarget = $match.Groups['target'].Value.Trim()
      if ($rawTarget -match '^(https?://|mailto:|data:)') { continue }

      if ($rawTarget.StartsWith('<') -and $rawTarget.EndsWith('>')) {
        $rawTarget = $rawTarget.Substring(1, $rawTarget.Length - 2)
      }

      $parts = $rawTarget.Split('#', 2)
      $pathPart = [Uri]::UnescapeDataString($parts[0])
      $anchorPart = if ($parts.Count -eq 2) { [Uri]::UnescapeDataString($parts[1]) } else { '' }
      $targetPath = if ([string]::IsNullOrWhiteSpace($pathPart)) {
        $markdownFile.FullName
      }
      else {
        [IO.Path]::GetFullPath((Join-Path $markdownFile.DirectoryName $pathPart))
      }

      $relativeSource = $markdownFile.FullName.Substring($root.Length).TrimStart([char[]]'\/')
      $checkedLinks++
      if (-not (Test-Path -LiteralPath $targetPath)) {
        $issues.Add("$relativeSource -> missing target: $rawTarget")
        continue
      }

      if (-not [string]::IsNullOrWhiteSpace($anchorPart) -and
          [IO.Path]::GetExtension($targetPath).Equals('.md', [StringComparison]::OrdinalIgnoreCase)) {
        $checkedAnchors++
        $anchors = Get-MarkdownAnchors -Path $targetPath
        if (-not $anchors.Contains($anchorPart)) {
          $issues.Add("$relativeSource -> missing anchor: $rawTarget")
        }
      }
    }
  }

  $syncParameters = @{ Check = $true; NoBuild = [bool]$NoBuild }
  & (Join-Path $PSScriptRoot 'sync-pattern-index.ps1') @syncParameters

  if ($issues.Count -gt 0) {
    Write-Error ("Documentation verification failed:`n- " + ($issues -join "`n- "))
  }

  Write-Host "Documentation verification passed: $($markdownFiles.Count) files, $checkedLinks local links, $checkedAnchors anchors, and a synchronized 23-row pattern index."
}
finally {
  Pop-Location
}

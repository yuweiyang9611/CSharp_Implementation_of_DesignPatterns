#requires -Version 7.0

[CmdletBinding()]
param(
  [string]$CatalogPath,
  [string]$SchemaPath,
  [switch]$NoBuild,
  [switch]$SkipRunner
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($CatalogPath)) {
  $CatalogPath = Join-Path $root 'site/data/learning-catalog.json'
}
if ([string]::IsNullOrWhiteSpace($SchemaPath)) {
  $SchemaPath = Join-Path $root 'site/data/learning-catalog.schema.json'
}

$CatalogPath = [IO.Path]::GetFullPath($CatalogPath)
$SchemaPath = [IO.Path]::GetFullPath($SchemaPath)
$issues = [Collections.Generic.List[string]]::new()

if (-not (Test-Path -LiteralPath $CatalogPath -PathType Leaf)) { throw "Learning catalog is missing: $CatalogPath" }
if (-not (Test-Path -LiteralPath $SchemaPath -PathType Leaf)) { throw "Learning catalog schema is missing: $SchemaPath" }

$catalogJson = Get-Content -LiteralPath $CatalogPath -Raw -Encoding utf8
try {
  if (-not ($catalogJson | Test-Json -SchemaFile $SchemaPath -ErrorAction Stop)) {
    $issues.Add('Catalog does not satisfy learning-catalog.schema.json.')
  }
} catch {
  $issues.Add("Catalog schema validation failed: $($_.Exception.Message)")
}

try {
  $catalog = $catalogJson | ConvertFrom-Json -Depth 100
} catch {
  throw "Learning catalog is not valid JSON: $($_.Exception.Message)"
}

function Add-DuplicateIssues {
  param(
    [Parameter(Mandatory)][object[]]$Values,
    [Parameter(Mandatory)][string]$Label
  )

  foreach ($duplicate in @($Values | Group-Object | Where-Object Count -gt 1)) {
    $issues.Add("Duplicate ${Label}: $($duplicate.Name)")
  }
}

$tagIds = @($catalog.problemTags | ForEach-Object { [string]$_.id })
$patternKeys = @($catalog.patterns | ForEach-Object { [string]$_.key })
$learningIds = @($catalog.learningItems | ForEach-Object { [string]$_.id })
$learningUrls = @($catalog.learningItems | ForEach-Object { [string]$_.url })
$quizIds = @($catalog.quizzes | ForEach-Object { [string]$_.id })
Add-DuplicateIssues -Values $tagIds -Label 'problem tag id'
Add-DuplicateIssues -Values $patternKeys -Label 'pattern key'
Add-DuplicateIssues -Values $learningIds -Label 'learning item id'
Add-DuplicateIssues -Values $learningUrls -Label 'learning item URL'
Add-DuplicateIssues -Values $quizIds -Label 'quiz id'

$tagSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$patternSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$templateSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($id in $tagIds) { [void]$tagSet.Add($id) }
foreach ($key in $patternKeys) { [void]$patternSet.Add($key) }
foreach ($property in $catalog.exerciseTemplates.PSObject.Properties) { [void]$templateSet.Add($property.Name) }

$usedTags = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($pattern in $catalog.patterns) {
  foreach ($tagId in @($pattern.problemTags)) {
    if (-not $tagSet.Contains($tagId)) { $issues.Add("patterns.$($pattern.key).problemTags references unknown tag '$tagId'.") }
    [void]$usedTags.Add($tagId)
  }
  foreach ($relatedKey in @($pattern.related)) {
    if ($relatedKey -eq $pattern.key) { $issues.Add("patterns.$($pattern.key).related must not reference itself.") }
    elseif (-not $patternSet.Contains($relatedKey)) { $issues.Add("patterns.$($pattern.key).related references unknown pattern '$relatedKey'.") }
  }
  foreach ($field in 'source', 'practice') {
    $relativePath = [string]$pattern.$field
    if ([IO.Path]::IsPathRooted($relativePath) -or $relativePath.Contains('..') -or $relativePath.Contains('\')) {
      $issues.Add("patterns.$($pattern.key).$field must be a safe forward-slash repository path: $relativePath")
      continue
    }
    $resolvedPath = [IO.Path]::GetFullPath((Join-Path $root ($relativePath -replace '/', [IO.Path]::DirectorySeparatorChar)))
    if (-not $resolvedPath.StartsWith([IO.Path]::GetFullPath($root) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
      -not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
      $issues.Add("patterns.$($pattern.key).$field points to a missing repository file: $relativePath")
    }
  }
}
foreach ($tagId in $tagIds) {
  if (-not $usedTags.Contains($tagId)) { $issues.Add("Problem tag is never used: $tagId") }
  $mapping = $catalog.tagToExerciseTemplate.PSObject.Properties[$tagId]
  if ($null -eq $mapping) { $issues.Add("Problem tag has no exercise template mapping: $tagId") }
  elseif (-not $templateSet.Contains([string]$mapping.Value)) { $issues.Add("Problem tag '$tagId' maps to unknown exercise template '$($mapping.Value)'.") }
}
foreach ($mapping in $catalog.tagToExerciseTemplate.PSObject.Properties) {
  if (-not $tagSet.Contains($mapping.Name)) { $issues.Add("Exercise mapping references unknown problem tag '$($mapping.Name)'.") }
  if (-not $templateSet.Contains([string]$mapping.Value)) { $issues.Add("Exercise mapping references unknown template '$($mapping.Value)'.") }
}

foreach ($item in $catalog.learningItems) {
  $expectedPrefix = "$($item.type):"
  if (-not ([string]$item.id).StartsWith($expectedPrefix, [StringComparison]::Ordinal)) {
    $issues.Add("learningItems.$($item.id).id must begin with '$expectedPrefix'.")
  }
  Add-DuplicateIssues -Values @($item.milestones | ForEach-Object { [string]$_.id }) -Label "milestone id in $($item.id)"
  foreach ($milestone in $item.milestones) {
    if ($milestone.PSObject.Properties.Name -contains 'command') {
      $command = [string]$milestone.command
      $targetMatch = [regex]::Match($command, '(?:--project\s+|dotnet\s+test\s+)(?<target>[^\s]+)')
      if ($targetMatch.Success) {
        $target = $targetMatch.Groups['target'].Value
        $targetPath = [IO.Path]::GetFullPath((Join-Path $root ($target -replace '/', [IO.Path]::DirectorySeparatorChar)))
        if (-not (Test-Path -LiteralPath $targetPath)) {
          $issues.Add("learningItems.$($item.id).milestones.$($milestone.id).command references missing target '$target'.")
        }
      }
    }
  }
}

foreach ($quiz in $catalog.quizzes) {
  foreach ($key in @($quiz.patternKeys)) {
    if (-not $patternSet.Contains($key)) { $issues.Add("quizzes.$($quiz.id).patternKeys references unknown pattern '$key'.") }
  }
  if (@($quiz.patternKeys) -notcontains $quiz.correctKey) {
    $issues.Add("quizzes.$($quiz.id).correctKey must appear in patternKeys.")
  }
}

if (-not $SkipRunner) {
  $runnerProject = Join-Path $root 'src/DesignPatterns.Runner/DesignPatterns.Runner.csproj'
  if (-not $NoBuild) {
    dotnet build $runnerProject --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Runner build failed before catalog validation.' }
  }
  $runnerJson = (& dotnet run --project $runnerProject --configuration Release --no-build -- --catalog-json | Out-String)
  if ($LASTEXITCODE -ne 0) { throw 'Runner catalog export failed during learning catalog validation.' }
  $runnerKeys = @(($runnerJson | ConvertFrom-Json) | ForEach-Object { [string]$_.key })
  foreach ($difference in @(Compare-Object -ReferenceObject ($runnerKeys | Sort-Object) -DifferenceObject ($patternKeys | Sort-Object))) {
    $issues.Add("Runner/catalog key mismatch: $($difference.InputObject) ($($difference.SideIndicator)).")
  }
  if ($runnerKeys.Count -ne 23) { $issues.Add("Runner must expose 23 patterns; found $($runnerKeys.Count).") }
}

if ($issues.Count -gt 0) {
  throw "Learning catalog validation failed:`n- $($issues -join "`n- ")"
}

$milestoneCount = @($catalog.learningItems | ForEach-Object { $_.milestones } | ForEach-Object { $_ }).Count
Write-Host "Learning catalog valid: $($tagIds.Count) tags, $($patternKeys.Count) patterns, $milestoneCount milestones, $($quizIds.Count) quizzes."

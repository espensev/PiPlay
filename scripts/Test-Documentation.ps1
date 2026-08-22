#Requires -Version 7.0
<#
.SYNOPSIS
  Validates the maintained Markdown surface without network access.
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$Quiet,
    [switch]$SkipFixtureTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath($RepositoryRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "RepositoryRoot not found: $root"
}

$pathComparison = if ($IsWindows) {
    [System.StringComparison]::OrdinalIgnoreCase
} else {
    [System.StringComparison]::Ordinal
}
$rootPrefix = $root.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

function Test-InRepository([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    return $fullPath.Equals($root, $pathComparison) -or
        $fullPath.StartsWith($rootPrefix, $pathComparison)
}

$trackedRelativePaths = $null
$maintainedRelativePaths = $null
if (Get-Command git -ErrorAction SilentlyContinue) {
    $topLevel = & git -C $root rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -eq 0 -and $topLevel) {
        $topLevel = [System.IO.Path]::GetFullPath(([string]$topLevel).Trim())
        if ($topLevel.Equals($root, $pathComparison)) {
            $trackedRelativePaths = @(& git -C $root ls-files)
            if ($LASTEXITCODE -ne 0) {
                throw "Could not enumerate tracked repository files."
            }
            $maintainedRelativePaths = @(
                & git -C $root ls-files --cached --others --exclude-standard)
            if ($LASTEXITCODE -ne 0) {
                throw "Could not enumerate maintained repository files."
            }
        }
    }
}

$pathComparer = if ($IsWindows) {
    [System.StringComparer]::OrdinalIgnoreCase
} else {
    [System.StringComparer]::Ordinal
}
$trackedPathSet = [System.Collections.Generic.HashSet[string]]::new($pathComparer)
if ($null -ne $trackedRelativePaths) {
    foreach ($relativePath in $trackedRelativePaths) {
        [void]$trackedPathSet.Add(([string]$relativePath).Replace('\', '/'))
    }
}
$maintainedPathSet = [System.Collections.Generic.HashSet[string]]::new($pathComparer)
if ($null -ne $maintainedRelativePaths) {
    foreach ($relativePath in $maintainedRelativePaths) {
        [void]$maintainedPathSet.Add(([string]$relativePath).Replace('\', '/'))
    }
}

$docPaths = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
if ($null -ne $maintainedRelativePaths) {
    foreach ($relativePath in $maintainedRelativePaths) {
        $normalized = ([string]$relativePath).Replace('\', '/')
        if ($normalized -notmatch '(?i)\.md$') { continue }
        if ($normalized.Contains('/') -and
            $normalized -notmatch '^(?i:\.github|docs|tests)/') { continue }
        $path = Join-Path $root $relativePath
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            [void]$docPaths.Add([System.IO.Path]::GetFullPath($path))
        }
    }
} else {
    foreach ($relativeRoot in @(".", ".github", "docs", "tests")) {
        $scanRoot = Join-Path $root $relativeRoot
        if (-not (Test-Path -LiteralPath $scanRoot -PathType Container)) { continue }
        $recurse = $relativeRoot -ne "."
        foreach ($file in @(Get-ChildItem -LiteralPath $scanRoot -Filter "*.md" -File -Recurse:$recurse)) {
            [void]$docPaths.Add($file.FullName)
        }
    }
}

$failures = [System.Collections.Generic.List[string]]::new()
$linkCount = 0

function Add-Failure([string]$message) {
    $failures.Add($message)
}

function Test-MarkdownTarget {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Document,
        [Parameter(Mandatory = $true)][string]$RawTarget
    )

    $target = $RawTarget.Trim().Trim('<', '>')
    if ($target -match '^(?i:https?://|mailto:|#)') { return }
    $target = ($target -split '#', 2)[0]
    if ([string]::IsNullOrWhiteSpace($target)) { return }

    try {
        $decoded = [System.Uri]::UnescapeDataString($target).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        if ([System.IO.Path]::IsPathRooted($decoded)) {
            throw "rooted path"
        }
        $candidate = [System.IO.Path]::GetFullPath((Join-Path $Document.DirectoryName $decoded))
    } catch {
        $relativeDocument = [System.IO.Path]::GetRelativePath($root, $Document.FullName)
        Add-Failure "${relativeDocument}: invalid Markdown link target '$RawTarget'"
        return
    }

    if (-not (Test-InRepository $candidate) -or -not (Test-Path -LiteralPath $candidate)) {
        $relativeDocument = [System.IO.Path]::GetRelativePath($root, $Document.FullName)
        Add-Failure "${relativeDocument}: missing Markdown link target '$RawTarget'"
    }
}

$evidenceExtensions = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
foreach ($extension in @(
    ".cs", ".csproj", ".xaml", ".js", ".cjs", ".html", ".ps1", ".yml", ".yaml",
    ".json", ".md", ".manifest", ".sln", ".props", ".targets", ".xml")) {
    [void]$evidenceExtensions.Add($extension)
}
$evidenceExtensionPattern = @(
    $evidenceExtensions | Sort-Object Length -Descending | ForEach-Object {
        [regex]::Escape($_.TrimStart('.'))
    }) -join '|'
# Runtime output names are code literals, not repository paths. Keep each
# exception bound to the maintained source file that defines its exact value.
$codeOwnedFileLiteralOwners = [System.Collections.Generic.Dictionary[string, string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
$codeOwnedFileLiteralOwners["settings.json"] = "src/PiPlay/Services/AppPaths.cs"

$repositoryFilesByName = [System.Collections.Generic.Dictionary[
    string, System.Collections.Generic.List[string]]]::new($pathComparer)

function Add-RepositoryEvidenceFile([string]$RelativePath) {
    $normalized = $RelativePath.Replace('\', '/')
    $path = Join-Path $root $normalized
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return }
    $file = Get-Item -LiteralPath $path
    $key = $file.Name
    if (-not $repositoryFilesByName.ContainsKey($key)) {
        $repositoryFilesByName[$key] = [System.Collections.Generic.List[string]]::new()
    }
    $repositoryFilesByName[$key].Add($normalized)
}

if ($null -ne $maintainedRelativePaths) {
    foreach ($relativePath in $maintainedRelativePaths) {
        Add-RepositoryEvidenceFile -RelativePath ([string]$relativePath)
    }
} else {
    foreach ($relativeRoot in @(".", ".github", "docs", "scripts", "src", "tests")) {
        $scanRoot = Join-Path $root $relativeRoot
        if (-not (Test-Path -LiteralPath $scanRoot -PathType Container)) { continue }
        $recurse = $relativeRoot -ne "."
        foreach ($file in @(Get-ChildItem -LiteralPath $scanRoot -File -Recurse:$recurse)) {
            if ($file.FullName -match '[\\/](?:bin|obj)[\\/]') { continue }
            $relativePath = [System.IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
            [void]$maintainedPathSet.Add($relativePath)
            Add-RepositoryEvidenceFile -RelativePath $relativePath
        }
    }
}

function Test-RepositoryEvidenceTarget {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Document,
        [Parameter(Mandatory = $true)][string]$CodeSpan
    )

    $trimmed = $CodeSpan.Trim()
    $relativeDocument = [System.IO.Path]::GetRelativePath($root, $Document.FullName)
    if ($codeOwnedFileLiteralOwners.ContainsKey($trimmed)) {
        $ownerRelative = $codeOwnedFileLiteralOwners[$trimmed]
        $ownerPath = Join-Path $root $ownerRelative
        $literalPattern = '"' + [regex]::Escape($trimmed) + '"'
        if (-not $maintainedPathSet.Contains($ownerRelative) -or
            -not (Test-Path -LiteralPath $ownerPath -PathType Leaf) -or
            (Get-Content -LiteralPath $ownerPath -Raw) -notmatch $literalPattern) {
            Add-Failure "${relativeDocument}: unverified code-owned file literal '$trimmed'"
        }
        return
    }

    $evidencePattern = '^(?<path>[^`\r\n\s]+?\.(?:' + $evidenceExtensionPattern + ')' +
        ')(?::[A-Za-z0-9_.-]+)?$'
    $match = [regex]::Match(
        $trimmed, $evidencePattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        if ($trimmed -match '^[A-Za-z0-9_.-]+$' -and
            $repositoryFilesByName.ContainsKey($trimmed)) {
            $exactMatches = @($repositoryFilesByName[$trimmed] | Sort-Object -Unique)
            if ($exactMatches.Count -gt 1) {
                Add-Failure "${relativeDocument}: ambiguous repository evidence target '$trimmed'; use a root-relative path"
            }
            return
        }
        $looksLikeFile = $trimmed -notmatch '\s' -and
            $trimmed -match "(?i)\.(?:$evidenceExtensionPattern)(?:$|[^A-Za-z0-9])"
        if ($looksLikeFile) {
            Add-Failure "${relativeDocument}: invalid repository evidence target '$trimmed'"
        }
        return
    }

    $rawPath = $match.Groups['path'].Value
    $normalized = $rawPath.Replace('\', '/')
    if ($rawPath -match '[:*?"<>|]') {
        Add-Failure "${relativeDocument}: invalid repository evidence target '$rawPath'"
        return
    }
    if ($normalized.Contains('/')) {
        $normalized = $normalized -replace '^\./', ''
        try {
            if ($normalized -match '^[A-Za-z]:/' -or
                [System.IO.Path]::IsPathRooted($normalized) -or
                $normalized -match '(^|/)\.\.(/|$)' -or
                $normalized.StartsWith('//')) {
                throw "unsafe path"
            }
            $candidate = [System.IO.Path]::GetFullPath((Join-Path $root $normalized))
        } catch {
            Add-Failure "${relativeDocument}: invalid repository evidence target '$rawPath'"
            return
        }
        $canonicalRelative = [System.IO.Path]::GetRelativePath($root, $candidate).Replace('\', '/')
        if (-not (Test-InRepository $candidate)) {
            Add-Failure "${relativeDocument}: invalid repository evidence target '$rawPath'"
        } elseif (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            Add-Failure "${relativeDocument}: missing repository evidence target '$rawPath'"
        } elseif (-not $maintainedPathSet.Contains($canonicalRelative)) {
            Add-Failure "${relativeDocument}: unmaintained repository evidence target '$rawPath'"
        }
        return
    }

    $key = $rawPath
    if (-not $repositoryFilesByName.ContainsKey($key)) {
        Add-Failure "${relativeDocument}: missing repository evidence target '$rawPath'"
        return
    }
    $matches = @($repositoryFilesByName[$key] | Sort-Object -Unique)
    if ($matches.Count -gt 1) {
        Add-Failure "${relativeDocument}: ambiguous repository evidence target '$rawPath'; use a root-relative path"
    }
}

foreach ($path in @($docPaths | Sort-Object)) {
    $document = Get-Item -LiteralPath $path
    $content = Get-Content -LiteralPath $path -Raw
    $relative = [System.IO.Path]::GetRelativePath($root, $path)

    # A setext-H1 underline is a full line of equals signs, so '=======' alone is not evidence of
    # a conflict; every real conflict also carries the angle-bracket (or diff3 pipe) marker lines.
    if ($content -match '(?m)^(<{7}( |$)|\|{7}( |$)|>{7}( |$))') {
        Add-Failure "${relative}: merge-conflict marker"
    }
    if ($content -match '(?<![A-Za-z0-9+.-])[A-Za-z]:[\\/]') {
        Add-Failure "${relative}: literal drive-root path; derive it from an environment variable or parameter"
    }

    foreach ($match in [regex]::Matches($content, '!?' + '\[[^\]\r\n]*\]\((?<target>[^)\s]+)')) {
        $linkCount++
        Test-MarkdownTarget -Document $document -RawTarget $match.Groups['target'].Value
    }
    foreach ($match in [regex]::Matches($content, '`(?<target>[^`\r\n]+)`')) {
        Test-RepositoryEvidenceTarget -Document $document -CodeSpan $match.Groups['target'].Value
    }
}

$sourceExtensions = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
foreach ($extension in @(
    ".cs", ".csproj", ".xaml", ".js", ".cjs", ".html", ".ps1", ".yml", ".yaml",
    ".json", ".manifest", ".props", ".targets", ".xml")) {
    [void]$sourceExtensions.Add($extension)
}

$identifierPattern = '(?<![A-Za-z0-9-])(?<id>Q-\d+|REQ-[A-Z0-9]+(?:-[A-Z0-9]+)+|ADR-\d{4})(?![A-Za-z0-9-])'
$identifierSources = @{}

foreach ($relativeRoot in @(".github", "scripts", "src", "tests")) {
    $scanRoot = Join-Path $root $relativeRoot
    if (-not (Test-Path -LiteralPath $scanRoot -PathType Container)) { continue }

    foreach ($file in @(Get-ChildItem -LiteralPath $scanRoot -File -Recurse)) {
        if (-not $sourceExtensions.Contains($file.Extension)) { continue }
        if ($file.FullName -eq $PSCommandPath) { continue }
        if ($file.FullName -match '[\\/](?:bin|obj)[\\/]') { continue }

        $content = Get-Content -LiteralPath $file.FullName -Raw
        $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName)
        if ($content -match '(?i)(?<![A-Z0-9+.-])[A-Z]:[\\/]Users[\\/][^\\/\r\n]+') {
            Add-Failure "${relative}: literal user-profile path; derive it from an environment variable or parameter"
        }

        foreach ($match in [regex]::Matches(
            $content,
            '(?<![A-Za-z0-9_.-])(?<target>(?:\.github|docs|tests)[\\/][A-Za-z0-9_.\\/-]+\.md)')) {
            Test-RepositoryEvidenceTarget -Document $file -CodeSpan $match.Groups['target'].Value
        }

        $normalizedRelative = $relative.Replace('\', '/')
        $isIdentifierSource = $normalizedRelative -match '^(?:src|tests)/' -and
            ($null -eq $trackedRelativePaths -or $trackedPathSet.Contains($normalizedRelative))
        if ($isIdentifierSource) {
            foreach ($match in [regex]::Matches($content, $identifierPattern)) {
                $identifier = $match.Groups['id'].Value
                if (-not $identifierSources.ContainsKey($identifier)) {
                    $identifierSources[$identifier] = [System.Collections.Generic.HashSet[string]]::new(
                        [System.StringComparer]::OrdinalIgnoreCase)
                }
                [void]$identifierSources[$identifier].Add($normalizedRelative)
            }
        }
    }
}

$contractPath = Join-Path $root "docs/PiPlay_Product_Engineering_Spec.md"
if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf)) {
    Add-Failure "docs/PiPlay_Product_Engineering_Spec.md: sole product contract is missing"
} else {
    $contractContent = Get-Content -LiteralPath $contractPath -Raw
    $contractIdentifiers = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($match in [regex]::Matches($contractContent, $identifierPattern)) {
        [void]$contractIdentifiers.Add($match.Groups['id'].Value)
    }
    foreach ($identifier in @($identifierSources.Keys | Sort-Object)) {
        if ($contractIdentifiers.Contains($identifier)) { continue }
        $sources = @($identifierSources[$identifier] | Sort-Object)
        Add-Failure (
            "docs/PiPlay_Product_Engineering_Spec.md: identifier '$identifier' cited by " +
            ($sources -join ", ") + " is absent from the sole product contract")
    }
}

if ($failures.Count -gt 0) {
    $detail = ($failures | Sort-Object -Unique | ForEach-Object { "  - $_" }) -join [Environment]::NewLine
    throw "DOCUMENTATION CHECK: FAIL ($($failures.Count) issue(s))$([Environment]::NewLine)$detail"
}

if (-not $SkipFixtureTests) {
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
        "PiPlayDocumentationPolicy-" + [Guid]::NewGuid().ToString("N"))
    $fixtureFailures = [System.Collections.Generic.List[string]]::new()

    function Invoke-RejectedDocumentationFixture {
        param(
            [Parameter(Mandatory = $true)][string]$Name,
            [Parameter(Mandatory = $true)][hashtable]$Files,
            [Parameter(Mandatory = $true)][string]$ExpectedPattern,
            [switch]$InitializeGit,
            [string]$ExpectedIgnoredPath,
            [hashtable]$UntrackedFiles
        )

        $caseRoot = Join-Path $fixtureRoot $Name
        foreach ($entry in $Files.GetEnumerator()) {
            $path = Join-Path $caseRoot ([string]$entry.Key)
            [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($path)) | Out-Null
            [System.IO.File]::WriteAllText($path, [string]$entry.Value)
        }

        if ($InitializeGit) {
            & git -C $caseRoot init --quiet
            if ($LASTEXITCODE -ne 0) {
                throw "Could not initialize '$Name' Git fixture."
            }
            & git -C $caseRoot add --all 2>$null
            if ($LASTEXITCODE -ne 0) {
                throw "Could not index maintained files for '$Name' Git fixture."
            }
            if ($ExpectedIgnoredPath) {
                & git -C $caseRoot check-ignore --quiet -- $ExpectedIgnoredPath
                if ($LASTEXITCODE -ne 0) {
                    throw "'$ExpectedIgnoredPath' is not ignored in '$Name' Git fixture."
                }
                & git -C $caseRoot ls-files --error-unmatch -- $ExpectedIgnoredPath 2>$null
                if ($LASTEXITCODE -eq 0) {
                    throw "'$ExpectedIgnoredPath' unexpectedly entered the '$Name' Git index."
                }
            }
            if ($UntrackedFiles) {
                foreach ($entry in $UntrackedFiles.GetEnumerator()) {
                    $path = Join-Path $caseRoot ([string]$entry.Key)
                    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($path)) | Out-Null
                    [System.IO.File]::WriteAllText($path, [string]$entry.Value)
                    $listed = @(& git -C $caseRoot ls-files --others --exclude-standard -- ([string]$entry.Key))
                    if ($LASTEXITCODE -ne 0 -or $listed.Count -ne 1) {
                        throw "'$($entry.Key)' is not an untracked maintained file in '$Name' Git fixture."
                    }
                }
            }
        }

        $output = & pwsh -NoProfile -File $PSCommandPath -RepositoryRoot $caseRoot `
            -Quiet -SkipFixtureTests 2>&1 | Out-String
        $normalizedOutput = $output -replace '(?m)^\s*\|\s?', ''
        $normalizedOutput = [regex]::Replace($normalizedOutput, '\s+', ' ')
        if ($LASTEXITCODE -eq 0) {
            $fixtureFailures.Add("Documentation validator accepted invalid '$Name' fixture.")
            return
        }
        if ($normalizedOutput -notmatch 'DOCUMENTATION CHECK: FAIL') {
            $fixtureFailures.Add(
                "Documentation validator rejected '$Name' without its normal failure verdict: $output")
            return
        }
        if ($normalizedOutput -notmatch $ExpectedPattern) {
            $fixtureFailures.Add(
                "Documentation validator rejected '$Name' for the wrong reason: $output")
        }
    }

    try {
        Invoke-RejectedDocumentationFixture -Name "broken-document-relative-link" -Files @{
            "docs/PiPlay_Product_Engineering_Spec.md" = "# Contract`n[wrong](target.md)`n"
            "target.md" = "# Root-only target`n"
        } -ExpectedPattern "(?s)missing Markdown.*link target 'target\.md'"
        Invoke-RejectedDocumentationFixture -Name "missing-evidence-file" -Files @{
            "docs/PiPlay_Product_Engineering_Spec.md" = "# Contract`nEvidence: ``src/Missing.cs``.`n"
        } -ExpectedPattern "(?s)missing.*repository.*evidence target 'src/Missing\.cs'"
        Invoke-RejectedDocumentationFixture -Name "missing-identifier" -Files @{
            "docs/PiPlay_Product_Engineering_Spec.md" = "# Contract`n"
            "src/Feature.cs" = "// REQ-MISSING-01`n"
        } -ExpectedPattern "(?s)identifier.*'REQ-MISSING-01'.*is absent"
        Invoke-RejectedDocumentationFixture -Name "absolute-drive-evidence" -Files @{
            "docs/PiPlay_Product_Engineering_Spec.md" = "# Contract`nEvidence: ``C:/outside.cs``.`n"
        } -ExpectedPattern "(?s)invalid.*repository.*evidence target 'C:/outside\.cs'"
        Invoke-RejectedDocumentationFixture -Name "missing-bare-json-evidence" -Files @{
            "docs/PiPlay_Product_Engineering_Spec.md" = "# Contract`nEvidence: ``global.json``.`n"
        } -ExpectedPattern "(?s)missing.*repository.*evidence target 'global\.json'"
        Invoke-RejectedDocumentationFixture -Name "ignored-evidence" -Files @{
            ".gitignore" = "src/Ignored.cs`n"
            "docs/PiPlay_Product_Engineering_Spec.md" = "# Contract`nEvidence: ``src/Ignored.cs``.`n"
            "src/Ignored.cs" = "// REQ-IGNORED-01`n"
        } -ExpectedPattern "(?s)unmaintained.*repository.*evidence target 'src/Ignored\.cs'" `
            -InitializeGit -ExpectedIgnoredPath "src/Ignored.cs"
        Invoke-RejectedDocumentationFixture -Name "untracked-maintained-markdown" -Files @{
            "docs/PiPlay_Product_Engineering_Spec.md" = "# Contract`n"
        } -UntrackedFiles @{
            "docs/Untracked.md" = "# Untracked`n[broken](missing.md)`n"
        } -ExpectedPattern "(?s)docs.Untracked\.md.*missing Markdown link target 'missing\.md'" `
            -InitializeGit
        Invoke-RejectedDocumentationFixture -Name "forward-drive-prose" -Files @{
            "docs/PiPlay_Product_Engineering_Spec.md" = "# Contract`nDo not use C:/work/output here.`n"
        } -ExpectedPattern "(?s)literal.*drive-root path"
        Invoke-RejectedDocumentationFixture -Name "forward-user-source" -Files @{
            "docs/PiPlay_Product_Engineering_Spec.md" = "# Contract`n"
            "src/Feature.cs" = "// Do not retain C:/Users/Dev/private state.`n"
        } -ExpectedPattern "(?s)literal.*user-profile path" -InitializeGit
        if ($fixtureFailures.Count -gt 0) {
            throw ($fixtureFailures -join [Environment]::NewLine)
        }
    } finally {
        if (Test-Path -LiteralPath $fixtureRoot) {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
    }
}

if (-not $Quiet) {
    Write-Host "DOCUMENTATION CHECK: PASS ($($docPaths.Count) files, $linkCount links)" -ForegroundColor Green
}

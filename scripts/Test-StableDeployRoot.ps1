#Requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "StableDeployRoot.ps1")

$script:assertionCount = 0
$script:currentPowerShellHost = `
    [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)][string]$Expected,
        [Parameter(Mandatory = $true)][string]$Actual,
        [Parameter(Mandatory = $true)][string]$Case
    )

    if ($Expected -cne $Actual) {
        throw "$Case failed. Expected '$Expected'; got '$Actual'."
    }
    $script:assertionCount++
}

function Assert-ThrowsLike {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Action,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Case
    )

    $message = $null
    try {
        & $Action | Out-Null
    } catch {
        $message = $_.Exception.Message
    }

    if ([string]::IsNullOrWhiteSpace($message)) {
        throw "$Case failed. Expected an exception matching '$Pattern'."
    }
    if ($message -notlike $Pattern) {
        throw "$Case failed. Exception '$message' did not match '$Pattern'."
    }
    $script:assertionCount++
}

function Invoke-ChildPowerShell {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell converts native stderr into ErrorRecord objects and honors the caller's
        # Stop preference. Keep the child failure as captured evidence instead of aborting this test.
        $ErrorActionPreference = 'Continue'
        $captured = @(& $script:currentPowerShellHost @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ($captured | Out-String)
    }
}

$previous = [Environment]::GetEnvironmentVariable("PIPLAY_STABLE_ROOT", "Process")
$validationSandbox = Join-Path ([System.IO.Path]::GetTempPath()) `
    ("PiPlayPathValidation-" + [guid]::NewGuid().ToString("N"))
$repositoryAlias = $null
$artifactAlias = $null
try {
    $explicitRoot = Join-Path ([System.IO.Path]::GetTempPath()) "PiPlay-explicit-root"
    $environmentRoot = Join-Path ([System.IO.Path]::GetTempPath()) "PiPlay-environment-root"
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $repositoryParent = Split-Path -Parent $repositoryRoot
    $repositoryChild = Join-Path $repositoryRoot "deploy"
    $repositorySibling = Join-Path ([System.IO.Path]::GetTempPath()) "PiPlay-stable-sibling"

    [Environment]::SetEnvironmentVariable("PIPLAY_STABLE_ROOT", $environmentRoot, "Process")
    Assert-Equal -Expected ([System.IO.Path]::GetFullPath($explicitRoot)) `
        -Actual (Resolve-StableDeployRoot -DeployRoot $explicitRoot) `
        -Case "explicit root wins"
    Assert-Equal -Expected ([System.IO.Path]::GetFullPath($environmentRoot)) `
        -Actual (Resolve-StableDeployRoot) `
        -Case "environment fallback"
    Assert-Equal -Expected "\\server.invalid\share\PiPlay" `
        -Actual (Resolve-StableDeployRoot -DeployRoot "\\server.invalid\share\PiPlay") `
        -Case "fully-qualified UNC root is supported"
    Assert-Equal -Expected ([System.IO.Path]::GetFullPath($repositorySibling)) `
        -Actual (Resolve-StableDeployRoot -DeployRoot $repositorySibling) `
        -Case "repository sibling is supported"

    [Environment]::SetEnvironmentVariable("PIPLAY_STABLE_ROOT", $null, "Process")
    Assert-ThrowsLike -Action { Resolve-StableDeployRoot } `
        -Pattern "*PIPLAY_STABLE_ROOT*DeployRoot*" `
        -Case "missing root fails closed"
    Assert-ThrowsLike -Action { Resolve-StableDeployRoot -DeployRoot "relative\PiPlay" } `
        -Pattern "*fully qualified*" `
        -Case "relative root fails closed"
    Assert-ThrowsLike -Action { Resolve-StableDeployRoot -DeployRoot "C:PiPlay" } `
        -Pattern "*fully qualified*" `
        -Case "drive-relative root fails closed"
    Assert-ThrowsLike -Action { Resolve-StableDeployRoot -DeployRoot "C:" } `
        -Pattern "*fully qualified*" `
        -Case "drive-current-directory root fails closed"
    Assert-ThrowsLike -Action { Resolve-StableDeployRoot -DeployRoot "\PiPlay" } `
        -Pattern "*fully qualified*" `
        -Case "current-drive-rooted backslash path fails closed"
    Assert-ThrowsLike -Action { Resolve-StableDeployRoot -DeployRoot "/PiPlay" } `
        -Pattern "*fully qualified*" `
        -Case "current-drive-rooted slash path fails closed"
    Assert-ThrowsLike -Action {
        Resolve-StableDeployRoot -DeployRoot ([System.IO.Path]::GetPathRoot($explicitRoot))
    } -Pattern "*filesystem root*" -Case "filesystem root fails closed"
    Assert-ThrowsLike -Action {
        Resolve-StableDeployRoot -DeployRoot "\\server.invalid\share"
    } -Pattern "*filesystem root*" -Case "UNC share root fails closed"
    Assert-ThrowsLike -Action {
        Resolve-StableDeployRoot -DeployRoot $repositoryRoot
    } -Pattern "*overlap*repository*" -Case "repository root fails closed"
    Assert-ThrowsLike -Action {
        Resolve-StableDeployRoot -DeployRoot $repositoryParent
    } -Pattern "*overlap*repository*" -Case "repository ancestor fails closed"
    Assert-ThrowsLike -Action {
        Resolve-StableDeployRoot -DeployRoot $repositoryChild
    } -Pattern "*overlap*repository*" -Case "repository descendant fails closed"

    $aliasParent = Join-Path $validationSandbox "repository-alias"
    New-Item -ItemType Directory -Path $aliasParent -Force | Out-Null
    $repositoryAlias = Join-Path $aliasParent "repo"
    New-Item -ItemType Junction -Path $repositoryAlias -Target $repositoryRoot | Out-Null
    Assert-ThrowsLike -Action {
        Resolve-StableDeployRoot -DeployRoot $repositoryAlias
    } -Pattern "*reparse point*" -Case "repository junction alias fails closed"
    Assert-ThrowsLike -Action {
        Resolve-StableDeployRoot -DeployRoot (Join-Path $repositoryAlias "candidate")
    } -Pattern "*reparse point*" -Case "repository junction descendant fails closed"

    $namespaceRepositoryAliases = @(
        "\\?\$repositoryParent",
        "\\?\$repositoryRoot",
        "\\?\$repositoryChild",
        "\\.\$repositoryParent",
        "\\.\$repositoryRoot",
        "\\.\$repositoryChild",
        ("//?/" + $repositoryRoot.Replace('\', '/')),
        ("//./" + $repositoryRoot.Replace('\', '/')),
        "\??\$repositoryRoot",
        "\\??\$repositoryRoot"
    )
    foreach ($namespaceAlias in $namespaceRepositoryAliases) {
        Assert-ThrowsLike -Action {
            Resolve-StableDeployRoot -DeployRoot $namespaceAlias
        } -Pattern "*Windows device or extended path namespace*" `
            -Case "repository namespace alias '$namespaceAlias' fails closed"
    }
    foreach ($namespaceAlias in @("\\?\$repositorySibling", "\\.\$repositorySibling")) {
        Assert-ThrowsLike -Action {
            Resolve-StableDeployRoot -DeployRoot $namespaceAlias
        } -Pattern "*Windows device or extended path namespace*" `
            -Case "non-repository namespace '$namespaceAlias' fails closed categorically"
    }

    $validEntries = @(
        [pscustomobject]@{ path = "PiPlay.exe" },
        [pscustomobject]@{ path = "runtimes/./win-x64/native.dll" }
    )
    $resolvedEntries = @(Resolve-ManifestArtifactPaths `
        -PayloadRoot $explicitRoot -Entries $validEntries)
    Assert-Equal -Expected "2" -Actual ([string]$resolvedEntries.Count) `
        -Case "nested artifact entries are accepted"
    Assert-Equal -Expected (Join-Path $explicitRoot "runtimes\win-x64\native.dll") `
        -Actual $resolvedEntries[1].FullPath `
        -Case "artifact entries are canonicalized"

    Assert-ThrowsLike -Action {
        Resolve-ManifestArtifactPaths -PayloadRoot $explicitRoot `
            -Entries @([pscustomobject]@{ path = "..\outside.dll" })
    } -Pattern "*traversal*" -Case "parent traversal fails closed"
    Assert-ThrowsLike -Action {
        Resolve-ManifestArtifactPaths -PayloadRoot $explicitRoot `
            -Entries @([pscustomobject]@{ path = "runtimes\..\outside.dll" })
    } -Pattern "*traversal*" -Case "embedded parent traversal fails closed"
    Assert-ThrowsLike -Action {
        Resolve-ManifestArtifactPaths -PayloadRoot $explicitRoot `
            -Entries @([pscustomobject]@{ path = "C:\outside.dll" })
    } -Pattern "*relative*" -Case "drive-rooted artifact fails closed"
    Assert-ThrowsLike -Action {
        Resolve-ManifestArtifactPaths -PayloadRoot $explicitRoot `
            -Entries @([pscustomobject]@{ path = "\\server\share\outside.dll" })
    } -Pattern "*relative*" -Case "UNC artifact fails closed"
    Assert-ThrowsLike -Action {
        Resolve-ManifestArtifactPaths -PayloadRoot $explicitRoot -Entries @(
            [pscustomobject]@{ path = "PiPlay.exe" },
            [pscustomobject]@{ path = "piplay.EXE" })
    } -Pattern "*duplicate*" -Case "case-insensitive duplicate fails closed"
    Assert-ThrowsLike -Action {
        Resolve-ManifestArtifactPaths -PayloadRoot $explicitRoot -Entries @(
            [pscustomobject]@{ path = "runtimes\native.dll" },
            [pscustomobject]@{ path = "runtimes\.\native.dll" })
    } -Pattern "*duplicate*" -Case "canonical duplicate fails closed"
    Assert-ThrowsLike -Action {
        Resolve-ManifestArtifactPaths -PayloadRoot $explicitRoot `
            -Entries @([pscustomobject]@{ path = "" })
    } -Pattern "*non-empty*" -Case "empty artifact path fails closed"

    $reparsePayload = Join-Path $validationSandbox "reparse\payload"
    $reparseOutside = Join-Path $validationSandbox "reparse\outside"
    New-Item -ItemType Directory -Path $reparsePayload -Force | Out-Null
    New-Item -ItemType Directory -Path $reparseOutside -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $reparseOutside "outside.bin") `
        -Value "outside" -Encoding UTF8
    $artifactAlias = Join-Path $reparsePayload "link"
    New-Item -ItemType Junction -Path $artifactAlias -Target $reparseOutside | Out-Null
    Assert-ThrowsLike -Action {
        Resolve-ManifestArtifactPaths -PayloadRoot $reparsePayload `
            -Entries @([pscustomobject]@{ path = "link\outside.bin" })
    } -Pattern "*reparse point*" -Case "reparse-point escape fails closed"

    # Both command-line verifiers must reject an escaping entry before trying to hash it. Locking
    # the outside file makes the ordering observable: an unsafe implementation reports a file-lock
    # error, while the path validator reports traversal without touching the file.
    $publishRoot = Join-Path $validationSandbox "publish"
    $publishTarget = Join-Path $publishRoot "candidate"
    New-Item -ItemType Directory -Path $publishTarget -Force | Out-Null
    $publishOutside = Join-Path $publishRoot "outside.bin"
    Set-Content -LiteralPath $publishOutside -Value "outside" -Encoding UTF8
    $publishOutsideItem = Get-Item -LiteralPath $publishOutside
    $publishEntry = [pscustomobject]@{
        path = "..\outside.bin"
        sha256 = Get-Sha256Hex -Path $publishOutside
        size = [int64]$publishOutsideItem.Length
    }
    [pscustomobject]@{
        project = "PiPlay"
        version = "9.9.9"
        buildNumber = 99
        builtUtc = "2026-08-20T00:00:00Z"
        artifactHashes = @($publishEntry)
        sha256 = $publishEntry.sha256
        size = $publishEntry.size
    } | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $publishTarget "build-info.json") -Encoding UTF8

    $publishLock = [System.IO.File]::Open(
        $publishOutside, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::None)
    try {
        $publishResult = Invoke-ChildPowerShell -Arguments @(
            "-NoProfile", "-File", (Join-Path $PSScriptRoot "Test-PublishMetadata.ps1"),
            "-PublishRoot", $publishRoot, "-PublishLabel", "candidate")
    } finally {
        $publishLock.Dispose()
    }
    Assert-Equal -Expected "1" -Actual ([string]$publishResult.ExitCode) `
        -Case "publish metadata traversal exits nonzero"
    Assert-Equal -Expected "True" -Actual ([string]($publishResult.Output -match 'traversal')) `
        -Case "publish metadata rejects traversal before hashing"
    $publishNamespaceResult = Invoke-ChildPowerShell -Arguments @(
        "-NoProfile", "-File", (Join-Path $PSScriptRoot "Test-PublishMetadata.ps1"),
        "-PublishRoot", "\\?\$publishRoot", "-PublishLabel", "candidate")
    Assert-Equal -Expected "1" -Actual ([string]$publishNamespaceResult.ExitCode) `
        -Case "publish metadata namespace root exits nonzero"
    Assert-Equal -Expected "True" `
        -Actual ([string]($publishNamespaceResult.Output -match 'extended path')) `
        -Case "publish metadata rejects namespace before reading payload"

    $verifyRoot = Join-Path $validationSandbox "verify\deploy"
    New-Item -ItemType Directory -Path $verifyRoot -Force | Out-Null
    $verifyOutside = Join-Path $validationSandbox "verify\outside.bin"
    Set-Content -LiteralPath $verifyOutside -Value "outside" -Encoding UTF8
    $verifyOutsideItem = Get-Item -LiteralPath $verifyOutside
    $verifyEntry = [pscustomobject]@{
        path = "..\outside.bin"
        sha256 = Get-Sha256Hex -Path $verifyOutside
        size = [int64]$verifyOutsideItem.Length
    }
    [pscustomobject]@{
        project = "PiPlay"
        version = "9.9.9"
        buildNumber = 99
        builtUtc = "2026-08-20T00:00:00Z"
        publishLabel = "candidate"
        channel = "Stable"
        artifactHashes = @($verifyEntry)
        sha256 = $verifyEntry.sha256
        size = $verifyEntry.size
    } | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $verifyRoot "build-info.json") -Encoding UTF8

    $verifyLock = [System.IO.File]::Open(
        $verifyOutside, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::None)
    try {
        $verifyResult = Invoke-ChildPowerShell -Arguments @(
            "-NoProfile", "-File", (Join-Path $PSScriptRoot "Verify-StableDeploy.ps1"),
            "-DeployRoot", $verifyRoot, "-AllowNonReleaseEvidence")
    } finally {
        $verifyLock.Dispose()
    }
    Assert-Equal -Expected "1" -Actual ([string]$verifyResult.ExitCode) `
        -Case "stable verifier traversal exits nonzero"
    Assert-Equal -Expected "True" -Actual ([string]($verifyResult.Output -match 'traversal')) `
        -Case "stable verifier rejects traversal before hashing"

    # A required cleanliness query must not share the optional Git probe's null-on-failure meaning.
    # Run the real verifier so both PowerShell hosts cover rev-parse success followed by status
    # failure, as well as an environment where Git is absent.
    $gitVerifyRoot = Join-Path $validationSandbox "git-verify\deploy"
    New-Item -ItemType Directory -Path $gitVerifyRoot -Force | Out-Null
    $gitVerifyExe = Join-Path $gitVerifyRoot "PiPlay.exe"
    Set-Content -LiteralPath $gitVerifyExe -Value "fixture" -Encoding UTF8
    $gitVerifyExeItem = Get-Item -LiteralPath $gitVerifyExe
    $gitVerifyEntry = [pscustomobject]@{
        path = "PiPlay.exe"
        sha256 = Get-Sha256Hex -Path $gitVerifyExe
        size = [int64]$gitVerifyExeItem.Length
    }
    $fakeCommit = "a" * 40
    [pscustomobject]@{
        project = "PiPlay"
        version = "9.9.9"
        buildNumber = 99
        builtUtc = "2026-08-21T00:00:00Z"
        publishLabel = "git-fixture"
        channel = "Stable"
        sourceCommit = $fakeCommit
        releaseEvidence = $false
        sourceDirty = $false
        artifactHashes = @($gitVerifyEntry)
        sha256 = $gitVerifyEntry.sha256
        size = $gitVerifyEntry.size
    } | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $gitVerifyRoot "build-info.json") -Encoding UTF8

    $fakeGitRoot = Join-Path $validationSandbox "git-verify\status-failure"
    New-Item -ItemType Directory -Path $fakeGitRoot -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $fakeGitRoot "git.cmd") -Encoding Ascii -Value @(
        '@echo off',
        'for %%A in (%*) do if /I "%%~A"=="rev-parse" (',
        "  echo $fakeCommit",
        '  exit /b 0',
        ')',
        'for %%A in (%*) do if /I "%%~A"=="tag" exit /b 0',
        'for %%A in (%*) do if /I "%%~A"=="status" exit /b 23',
        'exit /b 29')
    $missingGitRoot = Join-Path $validationSandbox "git-verify\missing"
    New-Item -ItemType Directory -Path $missingGitRoot -Force | Out-Null

    $previousPath = [Environment]::GetEnvironmentVariable("PATH", "Process")
    try {
        [Environment]::SetEnvironmentVariable("PATH", $fakeGitRoot, "Process")
        $statusFailureResult = Invoke-ChildPowerShell -Arguments @(
            "-NoProfile", "-File", (Join-Path $PSScriptRoot "Verify-StableDeploy.ps1"),
            "-DeployRoot", $gitVerifyRoot, "-AllowNonReleaseEvidence")

        [Environment]::SetEnvironmentVariable("PATH", $missingGitRoot, "Process")
        $missingGitResult = Invoke-ChildPowerShell -Arguments @(
            "-NoProfile", "-File", (Join-Path $PSScriptRoot "Verify-StableDeploy.ps1"),
            "-DeployRoot", $gitVerifyRoot, "-AllowNonReleaseEvidence")
    } finally {
        [Environment]::SetEnvironmentVariable("PATH", $previousPath, "Process")
    }

    Assert-Equal -Expected "1" -Actual ([string]$statusFailureResult.ExitCode) `
        -Case "stable verifier status failure exits nonzero"
    Assert-Equal -Expected "True" `
        -Actual ([string]($statusFailureResult.Output -match 'Required git status query failed \(exit 23\)')) `
        -Case "stable verifier distinguishes failed status from clean"
    Assert-Equal -Expected "False" `
        -Actual ([string]($statusFailureResult.Output -match '\[ OK \] Repo working tree is clean')) `
        -Case "stable verifier does not print clean after failed status"
    Assert-Equal -Expected "1" -Actual ([string]$missingGitResult.ExitCode) `
        -Case "stable verifier missing Git exits nonzero"
    Assert-Equal -Expected "True" `
        -Actual ([string]($missingGitResult.Output -match 'Git is required to query source dirty state')) `
        -Case "stable verifier reports missing Git for required status"
    Assert-Equal -Expected "False" `
        -Actual ([string]($missingGitResult.Output -match '\[ OK \] Repo working tree is clean')) `
        -Case "stable verifier does not print clean when Git is missing"
} finally {
    [Environment]::SetEnvironmentVariable("PIPLAY_STABLE_ROOT", $previous, "Process")
    foreach ($alias in @($repositoryAlias, $artifactAlias)) {
        if ($alias -and (Test-Path -LiteralPath $alias)) {
            [System.IO.Directory]::Delete($alias)
        }
    }
    if (Test-Path -LiteralPath $validationSandbox) {
        Remove-Item -LiteralPath $validationSandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "STABLE DEPLOY ROOT TESTS: PASS ($script:assertionCount)" -ForegroundColor Green

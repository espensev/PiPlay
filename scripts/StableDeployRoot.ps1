#Requires -Version 5.1

$script:PiPlayStableRootRepository = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent $PSScriptRoot))

function Get-Sha256Hex {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    # Keep hashing independent of module auto-loading. Windows PowerShell 5.1 can run these
    # scripts in hosts whose inherited PSModulePath does not expose the module-provided
    # hashing cmdlet.
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            return ([System.BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '')
        } finally {
            $sha256.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

function Resolve-FullyQualifiedFileSystemPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Name = "Path",
        [switch]$AllowFileSystemRoot
    )

    $candidate = $Path.Trim()
    $isWindowsNamespace =
        $candidate -match '^[\\/]{2}(?:[?.][\\/]|\?\?[\\/])' -or
        $candidate -match '^[\\/]\?\?[\\/]'
    if ($isWindowsNamespace) {
        throw "$Name must not use a Windows device or extended path namespace (got '$candidate')."
    }

    $isDriveQualified = $candidate -match '^[A-Za-z]:[\\/]'
    $isUncQualified = $candidate -match '^[\\/]{2}[^\\/]+[\\/]+[^\\/]+'
    if (-not ($isDriveQualified -or $isUncQualified)) {
        throw "$Name must be a fully qualified drive or UNC path (got '$candidate')."
    }

    try {
        $resolved = [System.IO.Path]::GetFullPath($candidate)
    } catch {
        throw "$Name is not a valid fully qualified path ('$candidate'): $($_.Exception.Message)"
    }

    $pathRoot = [System.IO.Path]::GetPathRoot($resolved)
    $trimmed = $resolved.TrimEnd([char[]]@('\', '/'))
    $trimmedRoot = $pathRoot.TrimEnd([char[]]@('\', '/'))
    if (-not $AllowFileSystemRoot -and $trimmed -ieq $trimmedRoot) {
        throw "$Name must be below a filesystem root (got '$candidate')."
    }

    return $trimmed
}

function Assert-NoExistingReparsePointComponents {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Name = "Path"
    )

    $resolved = Resolve-FullyQualifiedFileSystemPath `
        -Path $Path -Name $Name -AllowFileSystemRoot
    $pathRoot = [System.IO.Path]::GetPathRoot($resolved)
    $candidates = New-Object 'System.Collections.Generic.List[string]'
    $candidates.Add($pathRoot)

    $relative = $resolved.Substring($pathRoot.Length).TrimStart([char[]]@('\', '/'))
    $current = $pathRoot
    foreach ($segment in @($relative -split '[\\/]' | Where-Object { $_ -ne '' })) {
        $current = Join-Path $current $segment
        $candidates.Add($current)
    }

    foreach ($candidate in $candidates) {
        try {
            $item = Get-Item -LiteralPath $candidate -Force -ErrorAction Stop
        } catch [System.Management.Automation.ItemNotFoundException] {
            break
        } catch [System.Management.Automation.DriveNotFoundException] {
            break
        } catch {
            throw "$Name '$resolved' cannot be checked for reparse points at '$candidate': $($_.Exception.Message)"
        }

        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Name '$resolved' crosses existing reparse point '$candidate'."
        }
    }
}

function Test-FileSystemPathsOverlap {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$FirstPath,
        [Parameter(Mandatory = $true)][string]$SecondPath
    )

    Assert-NoExistingReparsePointComponents -Path $FirstPath -Name "FirstPath"
    Assert-NoExistingReparsePointComponents -Path $SecondPath -Name "SecondPath"

    $first = (Resolve-FullyQualifiedFileSystemPath -Path $FirstPath -Name "FirstPath" -AllowFileSystemRoot).
        Replace('/', '\').TrimEnd('\')
    $second = (Resolve-FullyQualifiedFileSystemPath -Path $SecondPath -Name "SecondPath" -AllowFileSystemRoot).
        Replace('/', '\').TrimEnd('\')

    if ($first -ieq $second) { return $true }
    if ($first.StartsWith($second + '\', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($second.StartsWith($first + '\', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    return $false
}

function Assert-FileSystemPathsDisjoint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$FirstPath,
        [Parameter(Mandatory = $true)][string]$SecondPath,
        [string]$FirstName = "FirstPath",
        [string]$SecondName = "SecondPath"
    )

    if (Test-FileSystemPathsOverlap -FirstPath $FirstPath -SecondPath $SecondPath) {
        throw "$FirstName '$FirstPath' must not overlap $SecondName '$SecondPath'."
    }
}

function Resolve-ManifestArtifactPaths {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$PayloadRoot,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Entries
    )

    $resolvedRoot = Resolve-FullyQualifiedFileSystemPath `
        -Path $PayloadRoot -Name "PayloadRoot"
    $normalizedRoot = $resolvedRoot.Replace('/', '\').TrimEnd('\')
    $rootPrefix = $normalizedRoot + '\'
    $seenPaths = New-Object 'System.Collections.Generic.HashSet[string]' `
        ([System.StringComparer]::OrdinalIgnoreCase)
    $resolvedEntries = New-Object 'System.Collections.Generic.List[object]'

    Assert-NoExistingReparsePointComponents -Path $resolvedRoot -Name "PayloadRoot"

    foreach ($entry in $Entries) {
        $pathProperty = $entry.PSObject.Properties['path']
        if ($null -eq $pathProperty -or [string]::IsNullOrWhiteSpace([string]$pathProperty.Value)) {
            throw "Every artifact manifest entry must have a non-empty path."
        }

        $manifestPath = [string]$pathProperty.Value
        if ([System.IO.Path]::IsPathRooted($manifestPath)) {
            throw "Artifact manifest path '$manifestPath' must be relative to the payload root."
        }

        $segments = @($manifestPath -split '[\\/]')
        if ($segments -contains '..') {
            throw "Artifact manifest path '$manifestPath' contains parent traversal ('..')."
        }

        try {
            $fullPath = [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot $manifestPath))
        } catch {
            throw "Artifact manifest path '$manifestPath' is invalid: $($_.Exception.Message)"
        }
        $normalizedFullPath = $fullPath.Replace('/', '\').TrimEnd('\')
        if (-not $normalizedFullPath.StartsWith(
                $rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Artifact manifest path '$manifestPath' escapes payload root '$resolvedRoot'."
        }

        $currentPath = $normalizedRoot
        foreach ($segment in @($normalizedFullPath.Substring($rootPrefix.Length) -split '\\')) {
            $currentPath = Join-Path $currentPath $segment
            if (-not (Test-Path -LiteralPath $currentPath)) { break }
            $attributes = (Get-Item -LiteralPath $currentPath -Force).Attributes
            if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Artifact manifest path '$manifestPath' crosses reparse point '$currentPath'."
            }
        }
        if (-not $seenPaths.Add($normalizedFullPath)) {
            throw "Artifact manifest contains a duplicate path after canonicalization: '$manifestPath'."
        }

        $resolvedEntries.Add([pscustomobject]@{
            Entry = $entry
            RelativePath = $normalizedFullPath.Substring($rootPrefix.Length)
            FullPath = $fullPath
        })
    }

    return $resolvedEntries.ToArray()
}

function Resolve-StableDeployRoot {
    [CmdletBinding()]
    param([string]$DeployRoot)

    $candidate = $DeployRoot
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = [Environment]::GetEnvironmentVariable("PIPLAY_STABLE_ROOT", "Process")
    }
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        throw "Set PIPLAY_STABLE_ROOT to a fully qualified drive or UNC directory, or pass -DeployRoot."
    }

    $resolved = Resolve-FullyQualifiedFileSystemPath -Path $candidate -Name "DeployRoot"
    $resolvedRepository = Resolve-FullyQualifiedFileSystemPath `
        -Path $script:PiPlayStableRootRepository -Name "RepositoryRoot" -AllowFileSystemRoot
    Assert-FileSystemPathsDisjoint -FirstPath $resolved -SecondPath $resolvedRepository `
        -FirstName "DeployRoot" -SecondName "repository"
    return $resolved
}

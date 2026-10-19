using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PiPlay.Tests;

/// <summary>
/// Behavioral harnesses and static invariants for the PowerShell release scripts.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Markup)]
public class ReleaseScriptPolicyTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null
                   && !File.Exists(Path.Combine(dir.FullName, "PiPlay.sln"))
                   && !File.Exists(Path.Combine(dir.FullName, "scripts", "Test-LocalCI.ps1")))
                dir = dir.Parent;
            if (dir is null)
                throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
            return dir.FullName;
        }
    }

    private static string Script(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static async Task<(int ExitCode, string Output, string Error)> RunPwshScriptAsync(
        string scriptPath, params string[] arguments)
    {
        Assert.True(File.Exists(scriptPath), $"PowerShell script was not found at {scriptPath}.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), $"PowerShell process did not start for {scriptPath}.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        return (process.ExitCode, await outputTask, await errorTask);
    }

    [Theory]
    [InlineData("scripts/Test-PublishLock.ps1", "8 passed, 0 failed")]
    [InlineData("scripts/Test-DeploySwap.ps1", "35 passed, 0 failed")]
    public async Task Release_script_behavioral_harnesses_pass(
        string relativeScriptPath, string expectedSummary)
    {
        var scriptPath = Path.Combine(
            RepoRoot, relativeScriptPath.Replace('/', Path.DirectorySeparatorChar));
        var (exitCode, output, error) = await RunPwshScriptAsync(
            scriptPath, "-RepoRoot", RepoRoot);

        Assert.True(exitCode == 0,
            $"{relativeScriptPath} exited with code {exitCode}.{Environment.NewLine}{error}{Environment.NewLine}{output}");
        Assert.Contains(expectedSummary, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publish_metadata_behavioral_harness_accepts_valid_and_rejects_tampered_hashes()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(), "PiPlayPublishMetadataTest-" + Guid.NewGuid().ToString("N"));
        var labelDir = Path.Combine(tempRoot, "v0.14.0-b40");
        Directory.CreateDirectory(labelDir);
        try
        {
            var exePath = Path.Combine(labelDir, "PiPlay.exe");
            var exeBytes = Encoding.UTF8.GetBytes("synthetic-piplay-binary-payload");
            await File.WriteAllBytesAsync(exePath, exeBytes);
            var exeHash = Convert.ToHexString(SHA256.HashData(exeBytes));

            var buildInfo = new
            {
                project = "PiPlay",
                version = "0.14.0",
                buildNumber = 40,
                builtUtc = "2026-10-19T00:00:00Z",
                sha256 = exeHash,
                size = exeBytes.Length,
                artifactHashes = new[]
                {
                    new
                    {
                        path = "PiPlay.exe",
                        size = exeBytes.Length,
                        sha256 = exeHash,
                    },
                },
            };
            await File.WriteAllTextAsync(
                Path.Combine(labelDir, "build-info.json"),
                JsonSerializer.Serialize(buildInfo));

            var versionTable = new
            {
                project = "PiPlay",
                generatedAtUtc = "2026-10-19T00:00:00Z",
                builds = Array.Empty<object>(),
                buildCount = 1,
            };
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "VERSION_TABLE.json"),
                JsonSerializer.Serialize(versionTable));

            var scriptPath = Path.Combine(RepoRoot, "scripts", "Test-PublishMetadata.ps1");
            var (validExitCode, validOutput, validError) = await RunPwshScriptAsync(
                scriptPath, "-PublishRoot", tempRoot);

            Assert.True(validExitCode == 0,
                $"Test-PublishMetadata.ps1 exited with {validExitCode}.{Environment.NewLine}{validError}{Environment.NewLine}{validOutput}");
            Assert.Contains("Publish metadata validation passed.", validOutput, StringComparison.Ordinal);

            await File.WriteAllBytesAsync(exePath, exeBytes.Concat(new byte[] { 0xFF }).ToArray());
            var (tamperedExitCode, tamperedOutput, tamperedError) = await RunPwshScriptAsync(
                scriptPath, "-PublishRoot", tempRoot);

            Assert.NotEqual(0, tamperedExitCode);
            Assert.Contains("Hash mismatch", tamperedError + tamperedOutput, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* best-effort fixture cleanup */ }
        }
    }

    [Fact]
    public void Publish_stable_defaults_to_exact_source_and_requires_escape_hatches()
    {
        var script = Script("scripts/Publish-Stable.ps1");

        Assert.Contains("[string]$DeployRoot", script);
        Assert.Contains("PIPLAY_STABLE_ROOT", script);
        Assert.DoesNotContain(@"E:\Dev_test_implemenations\PiPlay", script);
        Assert.Contains("[switch]$AllowDirty", script);
        Assert.Contains("[switch]$AllowVersionBump", script);
        Assert.Contains("Release-verified stable deploys require a clean tree", script);
        Assert.Contains("only allowed with -AllowVersionBump", script);
        Assert.Contains("$buildParams[\"NoVersionBump\"] = $true", script);
        Assert.Contains("$buildParams[\"NoBuildNumberBump\"] = $true", script);
        Assert.Contains("Assert-StableTag", script);
        Assert.Contains("-AllowNonReleaseEvidence", script);
    }

    [Fact]
    public void Verify_stable_fail_closes_release_provenance()
    {
        var script = Script("scripts/Verify-StableDeploy.ps1");

        Assert.Contains("[string]$DeployRoot", script);
        Assert.Contains("PIPLAY_STABLE_ROOT", script);
        Assert.DoesNotContain(@"E:\Dev_test_implemenations\PiPlay", script);
        Assert.Contains("function Write-ProvenanceIssue", script);
        Assert.Contains("ProductVersion", script);
        Assert.Contains("Manifest marks this deploy as NOT release evidence", script);
        Assert.Contains("Expected stable tag", script);
        Assert.Contains("Repo working tree is DIRTY", script);
        Assert.Contains("VERDICT: RELEASE VERIFIED", script);
    }

    [Fact]
    public void Ui_smoke_is_isolated_dpi_aware_and_rejects_blank_captures()
    {
        var script = Script("scripts/Test-UiSmoke.ps1");

        Assert.Contains("PIPLAY_DATA_ROOT", script);
        Assert.Contains("SetThreadDpiAwarenessContext", script);
        Assert.Contains("SetForegroundWindow", script);
        Assert.Contains("GetForegroundWindow", script);
        Assert.Contains("GetWindowThreadProcessId", script);
        Assert.Contains("Rendered capture is blank or uniform", script);
        Assert.Contains("SMOKE PASS", script);
    }

    [Fact]
    public void Verify_stable_fails_closed_on_missing_source_commit()
    {
        var script = Script("scripts/Verify-StableDeploy.ps1");

        // A manifest with no sourceCommit cannot be tied to a commit, so default verification must
        // fail closed; -AllowNonReleaseEvidence still downgrades it via Write-ProvenanceIssue.
        Assert.Contains("$commit = [string](Get-ObjectPropertyValue -Object $buildInfo -Name \"sourceCommit\")", script);
        Assert.Contains("Write-ProvenanceIssue \"Manifest has no sourceCommit", script);
        Assert.DoesNotContain("$commit = [string]$buildInfo.sourceCommit", script);
        Assert.DoesNotContain("Write-Warn2 \"Manifest has no sourceCommit", script);
    }

    [Fact]
    public void Build_pipeline_signs_before_manifest_and_records_source_state()
    {
        var script = Script("scripts/Build-PiPlay.ps1");

        Assert.Contains("[string]$SignScript", script);
        Assert.Contains("sourceDirtyEntries", script);
        Assert.Contains("releaseEvidence", script);
        Assert.Contains("signing = if ($SigningEnabled)", script);

        var signStep = script.IndexOf("[4a] Signing publish output before metadata", StringComparison.Ordinal);
        var manifestStep = script.IndexOf("$buildInfoPath = Write-BuildInfo", StringComparison.Ordinal);
        Assert.True(signStep >= 0, "Build script should run the signing step after publish.");
        Assert.True(manifestStep >= 0, "Build script should write build-info.json.");
        Assert.True(signStep < manifestStep, "Signing must happen before manifest hashes are written.");
    }

    [Fact]
    public void Publish_extras_package_the_consolidated_privacy_guidance()
    {
        var script = Script("scripts/Build-PiPlay.ps1");

        // PR #42 folded the standalone privacy map into the consolidated product spec. The release
        // archive must still ship that guidance plus the ADR-0007 data-root reference it depends on,
        // so a recipient is not left with the spec link pointing at nothing.
        var extras = Regex.Match(script, @"\$PublishExtras\s*=\s*@\((?<body>[^)]*)\)", RegexOptions.Singleline);
        Assert.True(extras.Success, "Build script should define a $PublishExtras array.");
        var body = extras.Groups["body"].Value;

        string[] required =
        {
            "README.md",
            @"docs\CHANGELOG.md",
            @"docs\YouTube_Compliance.md",
            @"docs\PiPlay_Product_Engineering_Spec.md",
            @"docs\DECISIONS.md",
            @"docs\Theme_Preset_Differences.md",
            @"docs\UI_Profile_and_Borderless_Review.md",
            @"docs\Test_Suite_Audit_and_Remediation_Plan.md",
            @"docs\AGENTS.md",
        };

        foreach (var entry in required)
        {
            Assert.Contains(entry, body);

            // Copy-PublishExtras silently skips a missing source, so a stale path would ship nothing.
            Assert.True(
                File.Exists(Path.Combine(RepoRoot, entry)),
                $"Packaged documentation '{entry}' must exist in the repository.");
        }
    }

    [Fact]
    public void Diagnostic_publishes_are_recorded_as_non_release_evidence()
    {
        var build = Script("scripts/Build-PiPlay.ps1");
        var publish = Script("scripts/Publish-Stable.ps1");

        // Build can be forced non-release independently of source-tree dirtiness, so a clean no-op
        // diagnostic run cannot mint release evidence.
        Assert.Contains("[string]$NonReleaseReason", build);
        Assert.Contains("$explicitNonRelease = -not [string]::IsNullOrWhiteSpace($NonReleaseReason)", build);
        Assert.Contains("$releaseEvidence = (-not $sourceDirty) -and (-not $explicitNonRelease)", build);

        // Publish hands the reason to the build whenever an escape hatch is used, and refuses to
        // surface a diagnostic deploy as release evidence even if that plumbing regresses.
        Assert.Contains("$buildParams[\"NonReleaseReason\"]", publish);
        Assert.Contains("refusing to present a diagnostic deploy as release evidence", publish);
    }

    [Fact]
    public void Publish_creates_stable_tag_only_after_pretag_verification()
    {
        var publish = Script("scripts/Publish-Stable.ps1");
        var verify = Script("scripts/Verify-StableDeploy.ps1");

        // The verifier has a narrow pre-tag mode that downgrades ONLY the missing expected tag.
        Assert.Contains("[switch]$AllowMissingStableTag", verify);
        Assert.Contains("not yet created (pre-tag verification)", verify);

        // Order: pre-tag verification -> create tag -> final (tag-required) verification.
        var preTagVerify = publish.IndexOf("-AllowMissingStableTag", StringComparison.Ordinal);
        var createTag = publish.IndexOf("Assert-StableTag -TagName", StringComparison.Ordinal);
        var finalVerify = publish.LastIndexOf("& $verifyScript -DeployRoot $DeployRoot", StringComparison.Ordinal);

        Assert.True(preTagVerify >= 0, "Publish should run a pre-tag verification with -AllowMissingStableTag.");
        Assert.True(createTag >= 0, "Publish should create the stable tag via Assert-StableTag.");
        Assert.True(preTagVerify < createTag, "Pre-tag verification must run before the stable tag is created.");
        Assert.True(createTag < finalVerify, "A full verification must run after the tag is created.");
    }

    [Fact]
    public void Publish_preflights_the_stable_tag_before_anything_destructive()
    {
        var publish = Script("scripts/Publish-Stable.ps1");

        // A colliding tag used to surface only at the very end - after the test lane, the build, and
        // a destructive deploy had already replaced the manual-test copy. The check must come first.
        Assert.Contains("already exists at $existingTagCommit, but HEAD is $headCommit", publish);

        var preflight = publish.IndexOf("$expectedTag = \"stable-v$repoVersion-b$repoBuildNumber\"", StringComparison.Ordinal);
        var testGate = publish.IndexOf("& dotnet test", StringComparison.Ordinal);
        var build = publish.IndexOf("& $buildScript @buildParams", StringComparison.Ordinal);
        var deploy = publish.IndexOf("Invoke-StagedDeploy", StringComparison.Ordinal);

        Assert.True(preflight >= 0, "Publish should preflight the stable tag.");
        Assert.True(preflight < testGate, "Tag preflight must run before the test lane.");
        Assert.True(preflight < build, "Tag preflight must run before the build.");
        Assert.True(preflight < deploy, "Tag preflight must run before the deploy.");
    }

    [Fact]
    public void Publish_wires_lock_and_staged_deploy_helpers_into_the_release_flow()
    {
        var publish = Script("scripts/Publish-Stable.ps1");

        Assert.Contains(". (Join-Path $PSScriptRoot \"PublishLock.ps1\")", publish);
        Assert.Contains(". (Join-Path $PSScriptRoot \"DeploySwap.ps1\")", publish);
        Assert.Contains("New-PublishLock -Key \"repo|$repoRoot\"", publish);
        Assert.Contains("New-PublishLock -Key \"deploy|$DeployRoot\"", publish);
        Assert.Contains("Close-PublishLocks", publish);
        Assert.Contains("Repair-InterruptedDeploy", publish);
        Assert.Contains("Invoke-StagedDeploy", publish);
        Assert.DoesNotContain("Copy-Item -Path (Join-Path $latestDir \"*\") -Destination $DeployRoot", publish);
    }

    [Fact]
    public void Native_command_helper_owns_benign_stderr_policy()
    {
        var helper = Script("scripts/NativeCommand.ps1");

        Assert.Contains("function Invoke-NativeCommandQuiet", helper);
        Assert.Contains("$previousErrorActionPreference = $ErrorActionPreference", helper);
        Assert.Contains("$ErrorActionPreference = \"Continue\"", helper);
        Assert.Contains("$ErrorActionPreference = $previousErrorActionPreference", helper);
    }

    [Fact]
    public void Local_ci_wrapper_fails_closed_and_restores_process_state()
    {
        var script = Script("scripts/Test-LocalCI.ps1");

        Assert.Contains("$global:LASTEXITCODE = 0", script);
        Assert.Contains("$exitCode = $LASTEXITCODE", script);
        Assert.Contains("throw \"Local CI step '", script);
        Assert.Contains("Restore-ProcessEnvironment -Previous $testEnvironment", script);
        Assert.Contains("Restore-ProcessEnvironment -Previous $commonEnvironment", script);
        Assert.Contains("Remove-Item -LiteralPath $testDataRoot -Recurse -Force", script);
        Assert.Contains("Pop-Location", script);
        Assert.Contains("Restore-ProcessEnvironment -Previous $previous", script);

        var testBranch = script.IndexOf("elseif ($step.name -eq \"test\")", StringComparison.Ordinal);
        var protectedTry = script.IndexOf("try {", testBranch, StringComparison.Ordinal);
        var createRoot = script.IndexOf("New-Item -ItemType Directory -Path $testDataRoot", testBranch, StringComparison.Ordinal);
        var setTestEnvironment = script.IndexOf("$testEnvironment = Set-ProcessEnvironment", testBranch, StringComparison.Ordinal);
        var cleanupFinally = script.IndexOf("finally {", testBranch, StringComparison.Ordinal);
        Assert.True(testBranch >= 0 && protectedTry > testBranch, "The test-step branch must open a try.");
        Assert.True(createRoot > protectedTry, "Test-root creation must be protected by the cleanup finally.");
        Assert.True(setTestEnvironment > protectedTry, "Test environment setup must be protected by the cleanup finally.");
        Assert.True(cleanupFinally > setTestEnvironment, "Cleanup must run from the test-step finally.");

        // Cleanup is best-effort: a transient lock on the temp root must degrade to a warning, never
        // throw out of the finally - which would either mask a real test failure or flip a fully green
        // run to FAILED. The -ErrorAction Stop remove is wrapped so its failure only warns.
        var cleanupRemove = script.IndexOf("Remove-Item -LiteralPath $testDataRoot", cleanupFinally, StringComparison.Ordinal);
        var cleanupCatch = script.IndexOf("} catch {", cleanupRemove, StringComparison.Ordinal);
        var cleanupWarn = script.IndexOf("Could not remove local CI test data", cleanupRemove, StringComparison.Ordinal);
        Assert.True(cleanupRemove > cleanupFinally, "The cleanup remove must live in the test-step finally.");
        Assert.True(cleanupCatch > cleanupRemove, "The cleanup remove must be wrapped in a try/catch.");
        Assert.True(cleanupWarn > cleanupCatch, "A cleanup failure must degrade to a warning, not throw.");
    }

    [Fact]
    public void Ci_keeps_pull_requests_hosted_and_trusted_events_variable_routed()
    {
        var workflow = Script(".github/workflows/ci.yml");
        var normalized = workflow.Replace("\r\n", "\n");

        Assert.Contains("name: Build and test (Windows)", workflow);
        Assert.Contains("case(github.event_name == 'pull_request', 'windows-latest'", workflow);
        Assert.Contains("vars.PIPLAY_WINDOWS_RUNNER || 'windows-latest'", workflow);
        Assert.Contains("push:\n    branches:\n      - main\n  workflow_dispatch:", normalized);
        Assert.DoesNotContain("\n    tags:", normalized);
        Assert.Contains("run: .\\scripts\\Test-LocalCI.ps1", workflow);
        Assert.Contains("persist-credentials: false", workflow);
        Assert.DoesNotContain("run: dotnet restore", workflow);
        Assert.DoesNotContain("run: dotnet test", workflow);

        var usesLines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("uses: ", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(3, usesLines.Length);
        Assert.All(usesLines, line => Assert.Matches(
            new Regex(@"^uses: [^@\s]+@[0-9a-f]{40}(?:\s+#\s+.+)?$", RegexOptions.CultureInvariant),
            line));
    }

    [Theory]
    [InlineData("scripts/Build-PiPlay.ps1")]
    [InlineData("scripts/Publish-Stable.ps1")]
    [InlineData("scripts/Verify-StableDeploy.ps1")]
    public void Git_helpers_use_shared_native_command_wrapper(string relativePath)
    {
        var script = Script(relativePath);

        Assert.Contains(". (Join-Path $PSScriptRoot \"NativeCommand.ps1\")", script);
        Assert.Contains("Invoke-NativeCommandQuiet", script);
        Assert.DoesNotContain("$previousErrorActionPreference = $ErrorActionPreference", script);
    }

    [Fact]
    public void Build_pipeline_keeps_non_mutating_runs_side_effect_free_and_syncs_archive_before_latest()
    {
        var script = Script("scripts/Build-PiPlay.ps1");
        var csproj = Script("src/PiPlay/PiPlay.csproj");
        var changelog = Script("docs/CHANGELOG.md");

        Assert.Contains("$DefaultProjectVersion = \"0.14.0\"", script);
        Assert.Contains("$DefaultBuildNumber = 40", script);
        Assert.Contains("<Version>0.14.0</Version>", csproj);
        Assert.Contains("<AssemblyVersion>0.14.0.40</AssemblyVersion>", csproj);
        Assert.Contains("<FileVersion>0.14.0.40</FileVersion>", csproj);
        Assert.Contains("<InformationalVersion>0.14.0</InformationalVersion>", csproj);
        Assert.Contains("## 0.14.0 —", changelog);

        Assert.Contains("[switch]$AllowCreate", script);
        Assert.Contains("-AllowCreate:$allowVersionStampCreate", script);
        Assert.Contains("-AllowCreate:$allowBuildNumberStampCreate", script);
        Assert.Contains("-SourceCommit $sourceCommit", script);

        var writeTable = script.IndexOf("$versionTablePath = Write-VersionTable", StringComparison.Ordinal);
        var syncArchive = script.IndexOf("Sync-ReleaseMetadataIntoArchive -ArchivePath $archivePath", StringComparison.Ordinal);
        var setLatest = script.IndexOf("$latestRoot = Set-LatestSnapshot", StringComparison.Ordinal);
        Assert.True(writeTable >= 0 && syncArchive > writeTable, "Release archive metadata sync must happen after VERSION_TABLE.json is written.");
        Assert.True(setLatest > syncArchive, "Release archive metadata sync must finish before latest snapshot copy can fail on a locked file.");
    }

    [Fact]
    public void Release_and_smoke_scripts_normalize_deploy_root_and_clean_up_process_trees()
    {
        var publish = Script("scripts/Publish-Stable.ps1");
        var verify = Script("scripts/Verify-StableDeploy.ps1");
        var smoke = Script("scripts/Test-UiSmoke.ps1");

        var normalizeIndex = publish.IndexOf("$DeployRoot = [System.IO.Path]::GetFullPath($DeployRoot).TrimEnd('\\', '/')", StringComparison.Ordinal);
        var lockIndex = publish.IndexOf("New-PublishLock -Key \"deploy|$DeployRoot\"", StringComparison.Ordinal);
        Assert.True(normalizeIndex >= 0 && normalizeIndex < lockIndex, "DeployRoot must be normalized before taking the deploy mutex.");

        Assert.Contains("--untracked-files=all", verify);
        Assert.Contains("(Test-Path -LiteralPath $versionPath) -and (Test-Path -LiteralPath $buildNumberPath)", verify);
        Assert.Contains("$proc.Kill($true)", smoke);
    }
}

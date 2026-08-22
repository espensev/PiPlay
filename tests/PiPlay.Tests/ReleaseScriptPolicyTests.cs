using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PiPlay.Tests;

/// <summary>
/// Static invariants for the PowerShell release scripts. The scripts are still validated by
/// running their focused commands, but these checks keep the fail-closed provenance policy from
/// being accidentally edited out.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Markup)]
public class ReleaseScriptPolicyTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PiPlay.sln")))
                dir = dir.Parent;
            if (dir is null)
                throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
            return dir.FullName;
        }
    }

    private static string Script(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public void Publish_stable_defaults_to_exact_source_and_requires_escape_hatches()
    {
        var script = Script("scripts/Publish-Stable.ps1");

        Assert.Contains("[switch]$AllowDirty", script);
        Assert.Contains("[switch]$AllowVersionBump", script);
        Assert.Contains("Release-verified stable deploys require a clean tree", script);
        Assert.Contains("only allowed with -AllowVersionBump", script);
        Assert.Contains("$buildParams[\"NoVersionBump\"] = $true", script);
        Assert.Contains("$buildParams[\"NoBuildNumberBump\"] = $true", script);
        Assert.Contains("Assert-StableTag", script);
        Assert.Contains("-AllowNonReleaseEvidence", script);
    }

    [Theory]
    [InlineData("pwsh")]
    [InlineData("powershell.exe")]
    public async Task Skip_deploy_neither_resolves_nor_locks_a_stable_root(string host)
    {
        var result = await RunSkipDeployWithoutStableRootHarnessAsync(host);

        Assert.True(result.ExitCode == 0,
            $"Skip-deploy harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("Stable build ready (not deployed)", result.Output);
        Assert.Single(result.LockKeys);
        Assert.StartsWith("repo|", result.LockKeys[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("pwsh")]
    [InlineData("powershell.exe")]
    public async Task Deploy_lock_failure_releases_the_already_acquired_repository_lock(string host)
    {
        var result = await RunSkipDeployWithoutStableRootHarnessAsync(host, failDeployLock: true);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("fixture deploy lock failure", result.Error + result.Output,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, result.LockKeys.Length);
        Assert.StartsWith("repo|", result.LockKeys[0], StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("deploy|", result.LockKeys[1], StringComparison.OrdinalIgnoreCase);
        Assert.True(result.LockCleanupCalled,
            "The repository lock must be released when acquisition of the deploy-root lock throws.");
    }

    [Fact]
    public void Publish_help_points_to_the_canonical_lifecycle_without_excluding_desk_acceptance()
    {
        var script = Script("scripts/Publish-Stable.ps1");
        var helpEnd = script.IndexOf("#>", StringComparison.Ordinal);
        Assert.True(helpEnd > 0);
        var help = script[..helpEnd];

        Assert.DoesNotContain("ONLY sanctioned", help);
        Assert.Contains("docs\\PiPlay_Product_Engineering_Spec.md", help);
        Assert.Contains("desk-candidate acceptance is not release provenance", help, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_stable_fail_closes_release_provenance()
    {
        var script = Script("scripts/Verify-StableDeploy.ps1");

        Assert.Contains("function Write-ProvenanceIssue", script);
        Assert.Contains("ProductVersion", script);
        Assert.Contains("Manifest marks this deploy as NOT release evidence", script);
        Assert.Contains("Expected stable tag", script);
        Assert.Contains("Repo working tree is DIRTY", script);
        Assert.Contains("VERDICT: RELEASE VERIFIED", script);
    }

    [Fact]
    public void Deploy_scripts_hash_without_module_dependent_cmdlets()
    {
        // Windows PowerShell 5.1 can run these scripts in hosts whose inherited PSModulePath does
        // not auto-load Microsoft.PowerShell.Utility or Microsoft.PowerShell.Archive, so hashing
        // and archive expansion must not depend on module auto-loading. StableDeployRoot.ps1 owns
        // the shared stream-based Get-Sha256Hex; Build-PiPlay.ps1 keeps its own copy because it
        // does not dot-source the shared file.
        var shared = Script("scripts/StableDeployRoot.ps1");
        Assert.Contains("function Get-Sha256Hex", shared);
        Assert.Contains("[System.Security.Cryptography.SHA256]::Create()", shared);

        foreach (var relativePath in new[]
            {
                "scripts/StableDeployRoot.ps1", "scripts/DeploySwap.ps1",
                "scripts/Verify-StableDeploy.ps1", "scripts/Test-UiSmoke.ps1",
                "scripts/Test-PublishMetadata.ps1", "scripts/Test-StableDeployRoot.ps1",
                "scripts/Test-DeploySwap.ps1",
            })
        {
            Assert.DoesNotContain("Get-FileHash", Script(relativePath));
        }

        var build = Script("scripts/Build-PiPlay.ps1");
        Assert.Contains("[System.IO.Compression.ZipFile]::ExtractToDirectory", build);
        Assert.DoesNotContain("Expand-Archive", build);
    }

    [Fact]
    public void Packaged_smoke_release_mode_fails_fast_toward_desk_candidate()
    {
        // Payloads bundle Test-UiSmoke.ps1 without Verify-StableDeploy.ps1 and with a
        // StableDeployRoot.ps1 whose derived repository IS the payload root, so default Release
        // mode can never succeed from a packaged copy; it must fail fast with the usable mode.
        var script = Script("scripts/Test-UiSmoke.ps1");
        Assert.Contains("running from a packaged payload", script);
        Assert.Contains("-Mode DeskCandidate", script);
    }

    [Fact]
    public void Documentation_gate_conflict_scan_ignores_setext_underlines()
    {
        // A markdown setext-H1 underline is a full line of equals signs, which the old
        // '=======' alternation misread as a merge-conflict middle marker. Real conflicts always
        // carry the '<<<<<<<' / '>>>>>>>' (or diff3 '|||||||') lines, so those are sufficient.
        var script = Script("scripts/Test-Documentation.ps1");
        Assert.Contains("<{7}( |$)", script);
        Assert.DoesNotContain("(<<<<<<<|=======|>>>>>>>)", script);
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
    public void Build_metadata_is_bound_to_one_reverified_source_snapshot()
    {
        var script = Script("scripts/Build-PiPlay.ps1");
        var metadataStart = script.IndexOf("function Write-BuildInfo", StringComparison.Ordinal);
        var metadataEnd = script.IndexOf("function Update-BuildInfoArchive", metadataStart, StringComparison.Ordinal);
        Assert.True(metadataStart >= 0 && metadataEnd > metadataStart);
        var metadataFunction = script[metadataStart..metadataEnd];

        Assert.Contains("[string]$SourceCommit", metadataFunction);
        Assert.Contains("[object[]]$SourceDirtyEntries", metadataFunction);
        Assert.DoesNotContain("Get-SourceCommit", metadataFunction);
        Assert.DoesNotContain("Get-SourceDirtyEntries", metadataFunction);

        var initialSnapshot = script.IndexOf("$sourceSnapshot = Get-RequiredSourceSnapshot", StringComparison.Ordinal);
        var build = initialSnapshot < 0 ? -1 : script.IndexOf(
            "Invoke-External -FilePath \"dotnet\" -Arguments $buildArgs", initialSnapshot, StringComparison.Ordinal);
        var extras = build < 0 ? -1 :
            script.IndexOf("Copy-PublishExtras -RepositoryRoot", build, StringComparison.Ordinal);
        var signing = extras < 0 ? -1 : script.IndexOf("Invoke-SignScript", extras, StringComparison.Ordinal);
        var finalSnapshot = signing < 0 ? -1 : script.IndexOf(
            "$finalSourceSnapshot = Get-RequiredSourceSnapshot", signing, StringComparison.Ordinal);
        var compare = finalSnapshot < 0 ? -1 :
            script.IndexOf("Assert-SourceSnapshotUnchanged", finalSnapshot, StringComparison.Ordinal);
        var write = compare < 0 ? -1 :
            script.IndexOf("$buildInfoPath = Write-BuildInfo", compare, StringComparison.Ordinal);
        Assert.True(initialSnapshot >= 0 && initialSnapshot < build && build < extras && extras < signing &&
            signing < finalSnapshot && finalSnapshot < compare && compare < write,
            "Source must be captured before build inputs and reverified after extras/signing before metadata.");
        var writeTail = write < 0 ? string.Empty : script[write..];
        Assert.Contains("-SourceCommit $sourceSnapshot.Commit", writeTail);
        Assert.Contains("-SourceDirtyEntries $sourceSnapshot.DirtyEntries", writeTail);
        Assert.Contains("$summaryBuildInfo.sourceCommit", script);
        Assert.DoesNotContain("if ($sourceCommit)", script);
    }

    [Theory]
    [InlineData("pwsh")]
    [InlineData("powershell.exe")]
    public async Task Clean_publish_builds_from_the_captured_commit_archive_when_live_source_drifts(string host)
    {
        var result = await RunArchivedSourceBuildHarnessAsync(host);

        Assert.True(result.ExitCode == 0,
            $"Archived-source build harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("ARCHIVED SOURCE BUILD PASS", result.Output);
    }

    [Theory]
    [InlineData("pwsh")]
    [InlineData("powershell.exe")]
    public async Task Clean_archived_publish_rejects_no_restore(string host)
    {
        var result = await RunArchivedSourceBuildHarnessAsync(host, noRestore: true);

        Assert.True(result.ExitCode == 0,
            $"Archived-source no-restore harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("ARCHIVED SOURCE NO-RESTORE REJECTED", result.Output);
    }

    [Theory]
    [InlineData("pwsh")]
    [InlineData("powershell.exe")]
    public async Task Clean_publish_rejects_resolved_stamps_that_do_not_match_the_captured_archive(string host)
    {
        var result = await RunArchivedStampMismatchHarnessAsync(host);

        Assert.True(result.ExitCode == 0,
            $"Archived-stamp harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("ARCHIVED SOURCE STAMP DRIFT REJECTED", result.Output);

        var script = Script("scripts/Build-PiPlay.ps1");
        var archive = script.IndexOf("$archivedSource = New-ArchivedSourceSnapshot", StringComparison.Ordinal);
        var stampCheck = archive < 0 ? -1 : script.IndexOf("Assert-ArchivedSourceStamps", archive, StringComparison.Ordinal);
        var build = stampCheck < 0 ? -1 : script.IndexOf(
            "Invoke-External -FilePath \"dotnet\" -Arguments $buildArgs", stampCheck, StringComparison.Ordinal);
        Assert.True(archive >= 0 && archive < stampCheck && stampCheck < build,
            "Archived VERSION/BUILD_NUMBER must be compared with the resolved stamps before dotnet builds.");
    }

    [Theory]
    [InlineData("pwsh")]
    [InlineData("powershell.exe")]
    public async Task Build_help_separates_clean_release_from_intentional_dirty_stamping(string host)
    {
        var result = await RunScriptAsync(host, "scripts/Build-PiPlay.ps1", "-Help");
        var output = (result.Output + result.Error).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.True(result.ExitCode == 0,
            $"Build help under {host} exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("Publish/Release source snapshot", output);
        Assert.Contains("Clean release:", output);
        Assert.Contains("-Stage Release -NoVersionBump -NoBuildNumberBump", output);
        Assert.Contains("Diagnostic stamp:", output);
        Assert.Contains("-Version patch -AllowDirtySource -NonReleaseReason", output);
        Assert.DoesNotContain("  .\\Build-PiPlay.ps1 -Stage Release -Version patch\n", output);
        Assert.DoesNotContain("  .\\Build-PiPlay.ps1 -Stage Release -SelfContained\n", output);
    }

    [Theory]
    [InlineData("changed-head")]
    [InlineData("changed-state")]
    [InlineData("dirty-clean-required")]
    public async Task Source_snapshot_assertion_rejects_drift_and_dirty_clean_evidence(string mode)
    {
        var result = await RunSourceSnapshotHarnessAsync(mode, expectRejection: true);

        Assert.True(result.ExitCode == 0,
            $"Source snapshot harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("SOURCE SNAPSHOT REJECTED", result.Output);
    }

    [Fact]
    public async Task Source_snapshot_assertion_preserves_an_unchanged_explicit_dirty_diagnostic()
    {
        var result = await RunSourceSnapshotHarnessAsync("unchanged-dirty-diagnostic", expectRejection: false);

        Assert.True(result.ExitCode == 0,
            $"Source snapshot harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("SOURCE SNAPSHOT ACCEPTED", result.Output);
    }

    [Fact]
    public async Task Required_source_snapshot_rejects_head_drift_around_its_status_query()
    {
        var result = await RunRequiredSourceSnapshotCoherenceHarnessAsync();

        Assert.True(result.ExitCode == 0,
            $"Required snapshot harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("INTERNAL SOURCE SNAPSHOT DRIFT REJECTED", result.Output);
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
    public void Stable_publish_runs_the_shared_local_ci_gate_in_a_child_host()
    {
        var publish = Script("scripts/Publish-Stable.ps1");

        Assert.Contains("Test-LocalCI.ps1", publish);
        Assert.Contains("& $powerShellPath -NoProfile -File $localCiScript", publish);
        Assert.Contains("$localCiExitCode = $LASTEXITCODE", publish);
        Assert.Contains("if ($localCiExitCode -ne 0)", publish);
        Assert.Contains("Shared local-CI source gate failed (exit $localCiExitCode)", publish);
        Assert.DoesNotContain("& dotnet test", publish);

        var childCall = publish.IndexOf("& $powerShellPath -NoProfile -File $localCiScript", StringComparison.Ordinal);
        var capture = publish.IndexOf("$localCiExitCode = $LASTEXITCODE", childCall, StringComparison.Ordinal);
        var nonzeroBranch = publish.IndexOf("if ($localCiExitCode -ne 0)", capture, StringComparison.Ordinal);
        var failure = publish.IndexOf("Shared local-CI source gate failed", nonzeroBranch, StringComparison.Ordinal);
        Assert.True(childCall >= 0 && childCall < capture && capture < nonzeroBranch && nonzeroBranch < failure,
            "Stable publish must immediately capture the child exit and throw from the nonzero branch.");
    }

    [Theory]
    [InlineData("scripts/Build-PiPlay.ps1", "Build")]
    [InlineData("scripts/Publish-Stable.ps1", "Publish")]
    [InlineData("scripts/Verify-StableDeploy.ps1", "Verify")]
    public async Task Required_source_dirty_queries_fail_closed_when_git_status_fails(
        string relativeScript, string mode)
    {
        var result = await RunGitProvenanceHarnessAsync(relativeScript, mode, statusFailure: true);

        Assert.True(result.ExitCode == 0,
            $"Git status-failure harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("REQUIRED STATUS FAILURE REJECTED", result.Output);
    }

    [Theory]
    [InlineData("scripts/Build-PiPlay.ps1", "Build")]
    [InlineData("scripts/Publish-Stable.ps1", "Publish")]
    [InlineData("scripts/Verify-StableDeploy.ps1", "Verify")]
    public async Task Required_source_dirty_queries_fail_closed_when_git_is_missing(
        string relativeScript, string mode)
    {
        var result = await RunGitProvenanceHarnessAsync(relativeScript, mode, statusFailure: false);

        Assert.True(result.ExitCode == 0,
            $"Missing-Git harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("MISSING GIT REJECTED", result.Output);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("nonzero")]
    [InlineData("success")]
    public async Task Optional_git_probe_handles_missing_failure_and_success_without_stale_exit_state(string mode)
    {
        var result = await RunOptionalGitHarnessAsync(mode);

        Assert.True(result.ExitCode == 0,
            $"Optional Git harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("OPTIONAL GIT PROBE PASS", result.Output);
    }

    [Fact]
    public void Optional_git_probe_explicitly_resolves_and_immediately_captures_git_exit_state()
    {
        var script = Script("scripts/Publish-Stable.ps1");
        var functionStart = script.IndexOf("function Invoke-Git {", StringComparison.Ordinal);
        var functionEnd = script.IndexOf("function Invoke-GitRequired", functionStart, StringComparison.Ordinal);
        Assert.True(functionStart >= 0 && functionEnd > functionStart);
        var function = script[functionStart..functionEnd];

        var resolve = function.IndexOf("Get-Command git", StringComparison.Ordinal);
        var reset = resolve < 0 ? -1 :
            function.IndexOf("$global:LASTEXITCODE = 0", resolve, StringComparison.Ordinal);
        var invoke = reset < 0 ? -1 :
            function.IndexOf("Invoke-NativeCommandQuiet", reset, StringComparison.Ordinal);
        var capture = invoke < 0 ? -1 :
            function.IndexOf("$gitExitCode = $LASTEXITCODE", invoke, StringComparison.Ordinal);
        var nonzero = capture < 0 ? -1 :
            function.IndexOf("$gitExitCode -ne 0", capture, StringComparison.Ordinal);
        Assert.True(resolve >= 0 && resolve < reset && reset < invoke && invoke < capture && capture < nonzero,
            "Optional Git probes must resolve Git, reset stale state, capture immediately, and branch on the capture.");
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
        Assert.Contains("Tag preflight (before tests, build, or deploy)", publish);
        Assert.Contains("already exists at $existingTagCommit, but HEAD is $headCommit", publish);

        var preflight = publish.IndexOf("Tag preflight (before tests, build, or deploy)", StringComparison.Ordinal);
        var testGate = publish.IndexOf("Running the shared full local-CI source gate", StringComparison.Ordinal);
        var build = publish.IndexOf("Building + publishing the Stable channel Release", StringComparison.Ordinal);
        var deploy = publish.IndexOf("Invoke-StagedDeploy", StringComparison.Ordinal);

        Assert.True(preflight >= 0, "Publish should preflight the stable tag.");
        Assert.True(preflight < testGate, "Tag preflight must run before the test lane.");
        Assert.True(preflight < build, "Tag preflight must run before the build.");
        Assert.True(preflight < deploy, "Tag preflight must run before the deploy.");
    }

    [Fact]
    public void Publish_serializes_concurrent_runs()
    {
        var publish = Script("scripts/Publish-Stable.ps1");
        var lockScript = Script("scripts/PublishLock.ps1");

        // Two publishes at once would interleave on bin\publish, on the deploy root mid-swap, and on
        // tag creation. Both the repo and the deploy root are locked, before any expensive step.
        Assert.Contains("function New-PublishLock", lockScript);
        Assert.Contains("Another PiPlay publish is already running against", lockScript);
        Assert.Contains("System.Threading.AbandonedMutexException", lockScript);   // a crashed publish leaves no stale lock

        Assert.Contains("New-PublishLock -Key \"repo|$repoRoot\"", publish);
        Assert.Contains("New-PublishLock -Key \"deploy|$DeployRoot\"", publish);

        var lockTaken = publish.IndexOf("New-PublishLock -Key \"repo|$repoRoot\"", StringComparison.Ordinal);
        var build = publish.IndexOf("Building + publishing the Stable channel Release", StringComparison.Ordinal);
        Assert.True(lockTaken >= 0 && lockTaken < build, "The publish lock must be taken before the build.");
    }

    [Fact]
    public void Publish_releases_its_locks_on_every_exit_path()
    {
        var publish = Script("scripts/Publish-Stable.ps1");
        var lockScript = Script("scripts/PublishLock.ps1");

        // A mutex belongs to the THREAD that took it, and PowerShell's console host reuses its pipeline
        // thread. A publish that ended without releasing left the mutex owned by the still-alive prompt
        // thread, so the NEXT publish (another process) was told "another publish is already running"
        // with nothing running - clearing only if the GC happened to finalize the handle. The release
        // must therefore be in a try/finally, including the acquisition of every lock: a failure while
        // taking the second lock must release the first. PowerShell honours finally on success, throw,
        // AND exit.
        // (scripts\Test-PublishLock.ps1 case 3 is the behavioural guard.)
        Assert.Contains("function Close-PublishLocks", lockScript);
        Assert.Contains("ReleaseMutex()", lockScript);

        var lockTaken = publish.IndexOf("New-PublishLock -Key \"repo|$repoRoot\"", StringComparison.Ordinal);
        var tryOpen = lockTaken < 0 ? -1 : publish.LastIndexOf("\ntry {", lockTaken, StringComparison.Ordinal);
        var release = publish.IndexOf("Close-PublishLocks", lockTaken, StringComparison.Ordinal);
        var finallyBlock = publish.IndexOf("\nfinally {", lockTaken, StringComparison.Ordinal);

        Assert.True(tryOpen >= 0 && tryOpen < lockTaken,
            "Lock acquisition and the publish body must both be protected by the same try.");
        Assert.True(finallyBlock > tryOpen, "That try must have a finally.");
        Assert.True(release > finallyBlock, "Close-PublishLocks must run from the finally, not the success path.");
    }

    [Fact]
    public void Documentation_success_verdict_follows_the_internal_fixture_gate()
    {
        var script = Script("scripts/Test-Documentation.ps1");
        var fixtureGate = script.IndexOf("if (-not $SkipFixtureTests)", StringComparison.Ordinal);
        var passVerdict = script.IndexOf("DOCUMENTATION CHECK: PASS", StringComparison.Ordinal);

        Assert.True(fixtureGate >= 0 && passVerdict > fixtureGate,
            "The validator must not print PASS before its internal regression fixtures complete.");
    }

    [Fact]
    public void Deploy_stages_and_verifies_before_replacing_the_live_copy()
    {
        var publish = Script("scripts/Publish-Stable.ps1");
        var swap = Script("scripts/DeploySwap.ps1");

        // The deploy must never again delete the live payload and then copy over the top of it: an
        // interrupted copy left the only sanctioned manual-test installation broken with no way back.
        Assert.Contains("Invoke-StagedDeploy", publish);
        Assert.Contains("Repair-InterruptedDeploy", publish);
        Assert.DoesNotContain("Copy-Item -Path (Join-Path $latestDir \"*\") -Destination $DeployRoot", publish);

        // Stage -> verify the staged bytes -> only then swap.
        var stage = swap.IndexOf("Copy-Item -Path (Join-Path $SourceDir \"*\")", StringComparison.Ordinal);
        var verify = swap.IndexOf("Test-StagedPayload -StagingDir $paths.Staging", StringComparison.Ordinal);
        var swapIn = swap.IndexOf("Move-Item -LiteralPath $item.FullName -Destination (Join-Path $paths.Backup", StringComparison.Ordinal);

        Assert.True(stage >= 0 && verify >= 0 && swapIn >= 0, "DeploySwap should stage, verify, then swap.");
        Assert.True(stage < verify, "The staged payload must be copied before it is verified.");
        Assert.True(verify < swapIn, "The staged payload must verify BEFORE the live copy is touched.");
    }

    [Fact]
    public void Deploy_rolls_back_from_what_the_backup_actually_holds()
    {
        var swap = Script("scripts/DeploySwap.ps1");

        // Move-Item half-moves a directory whose child is locked and still throws, so a rollback keyed
        // on "the moves I recorded as successful" drops those children and then deletes the backup
        // holding the only copy. Rollback must walk the backup itself and merge. (scripts\Test-DeploySwap.ps1 case C3.)
        Assert.Contains("function Restore-DeployBackup", swap);
        Assert.Contains("Restore-DeployBackup -BackupDir $BackupDir -DeployRoot $DeployRoot", swap);
        Assert.Contains("rollback could not restore a runnable copy", swap);

        // A rollback that could not put everything back must KEEP the backup and say where it is.
        // Deleting it on the strength of "the restore probably worked" is the same data loss in the
        // other direction (scripts\Test-DeploySwap.ps1 case H).
        Assert.Contains("could not fully restore the previous copy", swap);
        Assert.Contains("is PRESERVED at", swap);

        // The runtime data folder is never moved aside (ADR-0007: login/session survive a redeploy).
        // Anchor the guard inside the SWAP loop specifically: the same line also appears in
        // Repair-InterruptedDeploy, so a bare Contains would stay green with the real guard deleted.
        var guard = "if ($item.Name -ieq $DataFolderName) { continue }";
        var swapFn = swap.IndexOf("function Invoke-StagedDeploy", StringComparison.Ordinal);
        Assert.True(swapFn >= 0, "Invoke-StagedDeploy should exist.");
        Assert.True(swap.IndexOf(guard, swapFn, StringComparison.Ordinal) >= 0,
            "The swap loop must skip the runtime data folder (ADR-0007).");
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

        Assert.Contains("-MinimumMajor $localCiPlan.requirements.nodeMinimumMajor", script);
        Assert.Contains("Test-StableDeployRoot.ps1", script);
        Assert.Contains("Test-DeploySwap.ps1", script);
        Assert.Contains("powershell.exe", script);
    }

    [Fact]
    public void Ui_smoke_binds_release_and_candidate_evidence_before_any_interaction()
    {
        var script = Script("scripts/Test-UiSmoke.ps1");

        Assert.Contains("PIPLAY_UI_EVIDENCE_ROOT", script);
        Assert.Contains("[System.IO.Path]::GetTempPath()", script);
        Assert.DoesNotContain(@"..\docs\evidence", script);
        Assert.Contains("[ValidateSet('Release', 'DeskCandidate')]", script);
        Assert.Contains("[switch]$ValidateOnly", script);
        Assert.Contains("Verify-StableDeploy.ps1", script);
        Assert.Contains("Resolve-ManifestArtifactPaths", script);
        Assert.Contains("GitHub Actions desk candidate; final interactive verification pending on SND-DESK", script);
        Assert.Contains("NOT RELEASE EVIDENCE", script);

        var evidenceFunctionStart = script.IndexOf("function Resolve-SmokeEvidenceRoot", StringComparison.Ordinal);
        var evidenceFunctionEnd = evidenceFunctionStart < 0 ? -1 :
            script.IndexOf("function Assert-BoundExecutable", evidenceFunctionStart, StringComparison.Ordinal);
        Assert.True(evidenceFunctionStart >= 0 && evidenceFunctionEnd > evidenceFunctionStart);
        var evidenceFunction = evidenceFunctionStart >= 0 && evidenceFunctionEnd > evidenceFunctionStart
            ? script[evidenceFunctionStart..evidenceFunctionEnd]
            : string.Empty;
        Assert.Contains("Resolve-FullyQualifiedFileSystemPath", evidenceFunction);
        Assert.Contains("Assert-NoExistingReparsePointComponents", evidenceFunction);
        Assert.Contains("Assert-FileSystemPathsDisjoint", evidenceFunction);
        Assert.DoesNotContain("if ($SmokeMode -eq 'DeskCandidate')", evidenceFunction);

        var validationOnly = script.IndexOf("if ($ValidateOnly)", StringComparison.Ordinal);
        var evidenceResolve = script.IndexOf("$EvidenceDir = Resolve-SmokeEvidenceRoot", validationOnly,
            StringComparison.Ordinal);
        var addType = script.IndexOf("Add-Type -AssemblyName", StringComparison.Ordinal);
        var createEvidence = script.IndexOf("New-Item -ItemType Directory -Force -Path $EvidenceDir",
            StringComparison.Ordinal);
        var startSmoke = script.IndexOf("$proc = Start-SmokeProcess", evidenceResolve,
            StringComparison.Ordinal);
        var processLaunch = script.IndexOf("[System.Diagnostics.Process]::Start", StringComparison.Ordinal);
        var releaseVerifier = script.IndexOf("& $powerShellPath -NoProfile -File $verifyScript", StringComparison.Ordinal);
        var verifierExitCheck = script.IndexOf("$verifyExitCode -ne 0", releaseVerifier, StringComparison.Ordinal);
        Assert.True(releaseVerifier >= 0 && verifierExitCheck > releaseVerifier,
            "Release preflight must require the child verifier's successful exit.");
        Assert.True(releaseVerifier < processLaunch,
            "Release verification must precede UI process launch.");
        Assert.True(validationOnly >= 0 && validationOnly < addType,
            "Validation-only mode must return before loading UI assemblies.");
        Assert.True(validationOnly < processLaunch,
            "Validation-only mode must return before process launch.");
        Assert.True(validationOnly < evidenceResolve && evidenceResolve < addType && evidenceResolve < createEvidence,
            "Interactive evidence must be resolved after ValidateOnly exits and before UI load or directory creation.");
        Assert.True(evidenceResolve < startSmoke,
            "Evidence-root containment must pass before either Release or DeskCandidate process launch.");
    }

    [Fact]
    public void Ui_smoke_isolates_candidate_data_and_keep_open_cannot_overstate_human_results()
    {
        var script = Script("scripts/Test-UiSmoke.ps1");

        Assert.Contains("[switch]$KeepOpen", script);
        Assert.Contains("PIPLAY_DESK_CANDIDATE_DATA_ROOT", script);
        Assert.Contains("Assert-FileSystemPathsDisjoint", script);
        Assert.Contains("[System.Diagnostics.ProcessStartInfo]::new()", script);
        Assert.Contains("$startInfo.Environment['PIPLAY_DATA_ROOT'] = $CandidateDataRoot", script);
        Assert.Contains("[System.Diagnostics.Process]::Start($startInfo)", script);
        Assert.DoesNotContain("Start-Process -FilePath", script);
        Assert.Contains("$KeepOpen -and ($Mode -ne 'DeskCandidate' -or $ValidateOnly)", script);
        Assert.Contains("AUTOMATED DESKTOP SMOKE PASS", script);
        Assert.Contains("$proc.WaitForExit()", script);
        Assert.DoesNotContain("human checks passed", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("interactive checks passed", script, StringComparison.OrdinalIgnoreCase);

        var automatedPass = script.IndexOf("AUTOMATED DESKTOP SMOKE PASS", StringComparison.Ordinal);
        var waitForTester = automatedPass < 0 ? -1 :
            script.IndexOf("$proc.WaitForExit()", automatedPass, StringComparison.Ordinal);
        Assert.True(automatedPass >= 0 && waitForTester > automatedPass,
            "KeepOpen must run automated smoke first, then wait for the tester to close PiPlay.");
    }

    [Fact]
    public async Task Desk_candidate_keep_open_is_invalid_for_validation_only()
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync();
        var result = await fixture.ValidateAsync(null, "-KeepOpen");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("KeepOpen is valid only for interactive DeskCandidate smoke", result.Error + result.Output);
    }

    [Fact]
    public async Task Smoke_child_environment_is_sanitized_and_candidate_payload_remains_revalidatable()
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync();
        var firstValidation = await fixture.ValidateAsync();
        Assert.Equal(0, firstValidation.ExitCode);
        var before = fixture.SnapshotPayload();

        var launch = await RunCandidateChildIsolationHarnessAsync(fixture.Root);
        Assert.True(launch.ExitCode == 0,
            $"Candidate child harness exited {launch.ExitCode}.{Environment.NewLine}{launch.Error}{Environment.NewLine}{launch.Output}");
        Assert.Contains("CANDIDATE CHILD ISOLATION PASS", launch.Output);
        Assert.Equal(before, fixture.SnapshotPayload());

        var secondValidation = await fixture.ValidateAsync();
        Assert.Equal(0, secondValidation.ExitCode);
        Assert.Contains("PREFLIGHT VERIFIED - NOT RELEASE EVIDENCE", secondValidation.Output);
    }

    [Fact]
    public async Task Desk_candidate_data_root_rejects_payload_descendants()
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync();
        var result = await RunCandidateDataRootRejectionHarnessAsync(
            fixture.Root, Path.Combine(fixture.Root, "PiPlayData"));

        Assert.True(result.ExitCode == 0,
            $"Candidate data-root harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("CANDIDATE DATA ROOT REJECTED", result.Output);
    }

    [Theory]
    [InlineData("equal")]
    [InlineData("descendant-env")]
    [InlineData("ancestor")]
    [InlineData("canonical")]
    [InlineData("device")]
    [InlineData("junction")]
    [InlineData("relative")]
    public async Task Desk_candidate_evidence_root_rejects_payload_overlap_and_unsafe_aliases(string mode)
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync();
        var before = fixture.SnapshotPayload();

        var result = await RunCandidateEvidenceRootHarnessAsync(fixture.Root, mode, expectSuccess: false);
        Assert.True(result.ExitCode == 0,
            $"Candidate evidence-root harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("CANDIDATE EVIDENCE ROOT REJECTED", result.Output);
        Assert.Equal(before, fixture.SnapshotPayload());

        var repeatValidation = await fixture.ValidateAsync();
        Assert.Equal(0, repeatValidation.ExitCode);
        Assert.Contains("PREFLIGHT VERIFIED - NOT RELEASE EVIDENCE", repeatValidation.Output);
    }

    [Fact]
    public async Task Desk_candidate_external_evidence_preserves_payload_and_repeat_preflight()
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync();
        var firstValidation = await fixture.ValidateAsync();
        Assert.Equal(0, firstValidation.ExitCode);
        var before = fixture.SnapshotPayload();

        var result = await RunCandidateEvidenceRootHarnessAsync(
            fixture.Root, "external-env", expectSuccess: true);
        Assert.True(result.ExitCode == 0,
            $"Candidate evidence-root harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("CANDIDATE EVIDENCE ROOT ACCEPTED", result.Output);
        Assert.Equal(before, fixture.SnapshotPayload());

        var repeatValidation = await fixture.ValidateAsync();
        Assert.Equal(0, repeatValidation.ExitCode);
        Assert.Contains("PREFLIGHT VERIFIED - NOT RELEASE EVIDENCE", repeatValidation.Output);
    }

    [Theory]
    [InlineData("equal")]
    [InlineData("descendant-env")]
    [InlineData("ancestor")]
    [InlineData("canonical")]
    [InlineData("device")]
    [InlineData("junction")]
    [InlineData("relative")]
    public async Task Release_evidence_root_rejects_verified_payload_overlap_and_unsafe_aliases(string mode)
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync();
        var before = fixture.SnapshotPayload();

        var result = await RunCandidateEvidenceRootHarnessAsync(
            fixture.Root, mode, expectSuccess: false, smokeMode: "Release");
        Assert.True(result.ExitCode == 0,
            $"Release evidence-root harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("RELEASE VERIFIER STUB PASS", result.Output);
        Assert.Contains("RELEASE EVIDENCE ROOT REJECTED", result.Output);
        Assert.Equal(before, fixture.SnapshotPayload());
    }

    [Fact]
    public async Task Release_external_evidence_is_accepted_without_ui_or_payload_mutation()
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync();
        var before = fixture.SnapshotPayload();

        var result = await RunCandidateEvidenceRootHarnessAsync(
            fixture.Root, "external-env", expectSuccess: true, smokeMode: "Release");
        Assert.True(result.ExitCode == 0,
            $"Release evidence-root harness exited {result.ExitCode}.{Environment.NewLine}{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Contains("RELEASE VERIFIER STUB PASS", result.Output);
        Assert.Contains("RELEASE EVIDENCE ROOT ACCEPTED", result.Output);
        Assert.Equal(before, fixture.SnapshotPayload());
    }

    [Fact]
    public void Publish_payload_includes_hashed_candidate_validation_entrypoints()
    {
        var script = Script("scripts/Build-PiPlay.ps1");

        Assert.Contains("scripts\\StableDeployRoot.ps1", script);
        Assert.Contains("scripts\\Test-UiSmoke.ps1", script);
        var publishStep = script.IndexOf(
            "Invoke-External -FilePath \"dotnet\" -Arguments $publishArgs", StringComparison.Ordinal);
        var copyExtras = publishStep < 0 ? -1 : script.IndexOf(
            "Copy-PublishExtras -RepositoryRoot $buildSourceRoot -VersionRoot $versionRoot -Extras $PublishExtras",
            publishStep, StringComparison.Ordinal);
        var writeManifest = copyExtras < 0 ? -1 :
            script.IndexOf("$buildInfoPath = Write-BuildInfo", copyExtras, StringComparison.Ordinal);
        Assert.True(publishStep >= 0 && copyExtras > publishStep && writeManifest > copyExtras,
            "Packaged validation entrypoints must be copied before artifact hashes are written.");
    }

    [Fact]
    public void Build_hash_inventory_excludes_only_root_self_manifests()
    {
        var script = Script("scripts/Build-PiPlay.ps1");
        var functionStart = script.IndexOf("function Get-HashEntries", StringComparison.Ordinal);
        var functionEnd = script.IndexOf("function Get-PublishedArtifacts", functionStart, StringComparison.Ordinal);
        Assert.True(functionStart >= 0 && functionEnd > functionStart);
        var function = script[functionStart..functionEnd];

        Assert.Contains("$excludedRootNames = @(\"build-info.json\", \"BUILDINFO.json\")", function);
        Assert.Contains("$relative -notmatch '/'", function);
        Assert.DoesNotContain("VERSION_TABLE.json", function);
    }

    [Theory]
    [InlineData("pwsh")]
    [InlineData("powershell.exe")]
    public async Task Final_manifest_hashes_version_table_without_embedding_its_self_reference(string host)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "PiPlayVersionTable-" + Guid.NewGuid().ToString("N"));
        var toolsRoot = Path.Combine(tempRoot, "tools");
        var publishRoot = Path.Combine(tempRoot, "publish");
        var publishLabel = "version-table-fixture";
        var versionRoot = Path.Combine(publishRoot, publishLabel);
        Directory.CreateDirectory(toolsRoot);
        try
        {
            var historicalRoot = Path.Combine(publishRoot, "historical-self-reference");
            Directory.CreateDirectory(historicalRoot);
            await File.WriteAllTextAsync(Path.Combine(historicalRoot, "build-info.json"), """
                {
                  "version": "0.9.0",
                  "buildNumber": 1,
                  "publishLabel": "historical-self-reference",
                  "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                  "size": 1,
                  "artifactHashes": [
                    {
                      "path": "PiPlay.exe",
                      "size": 1,
                      "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
                    },
                    {
                      "path": "VERSION_TABLE.json",
                      "size": 2,
                      "sha256": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
                    }
                  ]
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(toolsRoot, "dotnet.cmd"), """
                @echo off
                if /I "%~1"=="publish" (
                  if not exist "%PIPLAY_TEST_PUBLISH_OUTPUT%" mkdir "%PIPLAY_TEST_PUBLISH_OUTPUT%"
                  > "%PIPLAY_TEST_PUBLISH_OUTPUT%\PiPlay.exe" echo fixture executable
                )
                exit /b 0
                """);

            var startInfo = new ProcessStartInfo
            {
                FileName = host,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["PATH"] = toolsRoot + Path.PathSeparator +
                (Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
            startInfo.Environment["PIPLAY_TEST_PUBLISH_OUTPUT"] = versionRoot;
            if (host.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
            }
            foreach (var argument in new[]
            {
                "-NoProfile", "-File", Path.Combine(RepoRoot, "scripts", "Build-PiPlay.ps1"),
                "-Stage", "Publish", "-NoVersionBump", "-NoBuildNumberBump",
                "-AllowDirtySource", "-NonReleaseReason", "version-table regression fixture",
                "-PublishRoot", publishRoot, "-PublishLabel", publishLabel,
                "-NoLatest", "-StopProcessName", string.Empty
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };
            Assert.True(process.Start(), "Version-table build harness did not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            var output = await outputTask;
            var error = await errorTask;
            Assert.True(process.ExitCode == 0,
                $"Version-table build harness exited {process.ExitCode}.{Environment.NewLine}{error}{Environment.NewLine}{output}");

            var tablePath = Path.Combine(versionRoot, "VERSION_TABLE.json");
            Assert.True(File.Exists(tablePath), "Publish did not copy VERSION_TABLE.json into the payload.");
            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(versionRoot, "build-info.json")));
            var manifestHashes = manifest.RootElement.GetProperty("artifactHashes").EnumerateArray().ToArray();
            var tableHashes = manifestHashes.Where(entry =>
                string.Equals(entry.GetProperty("path").GetString(), "VERSION_TABLE.json",
                    StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.Single(tableHashes);
            var tableBytes = await File.ReadAllBytesAsync(tablePath);
            Assert.Equal(tableBytes.LongLength, tableHashes[0].GetProperty("size").GetInt64());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(tableBytes)),
                tableHashes[0].GetProperty("sha256").GetString());
            Assert.Equal(manifestHashes.Length, manifest.RootElement.GetProperty("artifactCount").GetInt32());

            using var table = JsonDocument.Parse(await File.ReadAllTextAsync(tablePath));
            var embeddedBuilds = table.RootElement.GetProperty("builds").EnumerateArray().ToArray();
            Assert.True(embeddedBuilds.Length >= 2, "The seeded historical build was not embedded.");
            foreach (var embeddedBuild in embeddedBuilds)
            {
                Assert.DoesNotContain(embeddedBuild.GetProperty("artifactHashes").EnumerateArray(), entry =>
                    string.Equals(entry.GetProperty("path").GetString(), "VERSION_TABLE.json",
                        StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* best-effort fixture cleanup */ }
        }
    }

    [Fact]
    public async Task Desk_candidate_validate_only_accepts_a_valid_extracted_payload_without_launching_ui()
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync();
        var result = await fixture.ValidateAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NOT RELEASE EVIDENCE", result.Output);
        Assert.False(Directory.Exists(fixture.EvidenceRoot),
            "ValidateOnly must not create screenshot evidence or launch the UI.");
    }

    [Theory]
    [InlineData("tampered-artifact")]
    [InlineData("release-evidence-true")]
    [InlineData("wrong-evidence-reason")]
    [InlineData("removed-exe-entry")]
    [InlineData("removed-dll-entry")]
    [InlineData("unlisted-dll")]
    [InlineData("unlisted-exe")]
    [InlineData("unlisted-version-table")]
    [InlineData("unlisted-nested-manifests")]
    [InlineData("missing-published-artifacts")]
    [InlineData("published-artifacts-missing-exe")]
    [InlineData("published-artifacts-extra-ghost")]
    [InlineData("published-artifacts-traversal")]
    [InlineData("published-artifacts-nested")]
    [InlineData("published-artifacts-duplicate")]
    [InlineData("published-artifacts-case-alias")]
    [InlineData("missing-primary-sha")]
    [InlineData("missing-primary-size")]
    [InlineData("wrong-primary-sha")]
    [InlineData("wrong-primary-size")]
    [InlineData("duplicate-exe-entry")]
    public async Task Desk_candidate_validate_only_rejects_tampering_and_wrong_evidence_metadata(string mutation)
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync(mutation);
        var result = await fixture.ValidateAsync();

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("PREFLIGHT VERIFIED", result.Output);
    }

    [Fact]
    public async Task Desk_candidate_treats_nested_manifest_names_as_ordinary_hashed_artifacts()
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync("listed-nested-manifests");
        var result = await fixture.ValidateAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("PREFLIGHT VERIFIED - NOT RELEASE EVIDENCE", result.Output);
    }

    [Fact]
    public async Task Desk_candidate_accepts_an_additional_listed_top_level_published_executable()
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync("listed-additional-published-exe");
        var result = await fixture.ValidateAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("PREFLIGHT VERIFIED - NOT RELEASE EVIDENCE", result.Output);
    }

    [Fact]
    public async Task Desk_candidate_rejects_an_executable_outside_the_selected_root()
    {
        await using var fixture = await DeskCandidateFixture.CreateAsync();
        var outsideExe = Path.Combine(Path.GetTempPath(), "not-the-selected-PiPlay.exe");
        var result = await fixture.ValidateAsync(outsideExe);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("must be the PiPlay.exe under the selected root", result.Error + result.Output);
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
        Assert.Equal(4, usesLines.Length);
        Assert.All(usesLines, line => Assert.Matches(
            new Regex(@"^uses: [^@\s]+@[0-9a-f]{40}(?:\s+#\s+.+)?$", RegexOptions.CultureInvariant),
            line));
    }

    [Fact]
    public void Manual_ci_dispatch_builds_validates_and_uploads_only_the_non_release_payload()
    {
        var workflow = Script(".github/workflows/ci.yml");
        var normalized = workflow.Replace("\r\n", "\n");

        Assert.Equal(2, Regex.Matches(normalized,
            @"(?m)^        if: github\.event_name == 'workflow_dispatch'$",
            RegexOptions.CultureInvariant).Count);
        var gate = workflow.IndexOf("- name: Run deterministic local CI gate", StringComparison.Ordinal);
        var build = workflow.IndexOf("- name: Build and validate desk candidate", StringComparison.Ordinal);
        var upload = workflow.IndexOf("- name: Upload desk candidate", StringComparison.Ordinal);
        Assert.True(gate >= 0 && gate < build && build < upload,
            "The dispatch-only candidate build and upload must follow the shared gate.");
        Assert.Contains("if: github.event_name == 'workflow_dispatch'", workflow[build..upload]);
        Assert.Contains("if: github.event_name == 'workflow_dispatch'", workflow[upload..]);

        Assert.Contains("actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1", workflow);
        Assert.Contains("$env:RUNNER_TEMP", workflow);
        Assert.Contains("[System.IO.Path]::IsPathFullyQualified($publishRoot)", workflow);
        Assert.Contains("$publishLabel = \"desk-candidate-$env:GITHUB_SHA\"", workflow);
        Assert.Contains("-Stage Publish", workflow);
        Assert.Contains("-Configuration Release", workflow);
        Assert.Contains("-Channel Stable", workflow);
        Assert.Contains("-NoVersionBump", workflow);
        Assert.Contains("-NoBuildNumberBump", workflow);
        Assert.Contains("-NoLatest", workflow);
        Assert.Contains("-NoVersionTable", workflow);
        Assert.Contains("-StopProcessName ''", workflow);
        Assert.Contains("GitHub Actions desk candidate; final interactive verification pending on SND-DESK", workflow);
        var buildCall = workflow.IndexOf("& pwsh -NoProfile -File .\\scripts\\Build-PiPlay.ps1", build,
            StringComparison.Ordinal);
        var buildExitCapture = workflow.IndexOf("$buildExitCode = $LASTEXITCODE", buildCall,
            StringComparison.Ordinal);
        var buildExitCheck = workflow.IndexOf("if ($buildExitCode -ne 0)", buildExitCapture,
            StringComparison.Ordinal);
        Assert.True(buildCall >= 0 && buildCall < buildExitCapture && buildExitCapture < buildExitCheck,
            "The workflow must immediately capture and reject a failed child build.");

        Assert.Contains("$manifest.sourceCommit -cne $env:GITHUB_SHA", workflow);
        Assert.Contains("$manifest.sourceDirty -isnot [bool]", workflow);
        Assert.Contains("@($manifest.sourceDirtyEntries).Count -ne 0", workflow);
        Assert.Contains("$manifest.releaseEvidence -isnot [bool]", workflow);
        Assert.Contains("$manifest.releaseEvidenceReason -cne $nonReleaseReason", workflow);
        Assert.Contains("$manifest.channel -cne 'Stable'", workflow);
        Assert.Contains("$manifest.configuration -cne 'Release'", workflow);
        Assert.Contains("$manifest.publishLabel -cne $publishLabel", workflow);
        Assert.Contains(@"scripts\Test-UiSmoke.ps1", workflow);
        Assert.Contains("-Mode DeskCandidate -ValidateOnly", workflow);
        Assert.Contains("if ($smokeExitCode -ne 0)", workflow);
        Assert.Contains("\"payload=$payloadRoot\" | Out-File -FilePath $env:GITHUB_OUTPUT", workflow);

        Assert.Contains("name: PiPlay-desk-candidate-${{ github.sha }}", workflow);
        Assert.Contains("path: ${{ steps.desk-candidate.outputs.payload }}", workflow);
        Assert.Contains("if-no-files-found: error", workflow);
        Assert.DoesNotContain("gh release", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git tag", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actions/create-release", workflow, StringComparison.OrdinalIgnoreCase);

        var readme = Script("README.md");
        Assert.Contains("pwsh -NoProfile -File .\\scripts\\Test-UiSmoke.ps1 -Mode DeskCandidate", readme);
        Assert.Matches(new Regex(@"non-release", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), readme);
    }

    [Fact]
    public void Manual_candidate_requires_a_successful_clean_git_status_before_build_or_upload()
    {
        var workflow = Script(".github/workflows/ci.yml");
        const string statusInvocation =
            "$sourceStatus = @(& git status --porcelain --untracked-files=all)";

        var candidateStep = workflow.IndexOf("- name: Build and validate desk candidate", StringComparison.Ordinal);
        Assert.True(candidateStep >= 0, "The dispatch candidate step must exist.");
        var statusCall = workflow.IndexOf(statusInvocation, candidateStep, StringComparison.Ordinal);
        Assert.True(statusCall > candidateStep,
            "The dispatch candidate step must run the exact required git status query.");
        var statusExitCapture = workflow.IndexOf("$sourceStatusExitCode = $LASTEXITCODE", statusCall,
            StringComparison.Ordinal);
        Assert.True(statusExitCapture > statusCall,
            "The workflow must capture the required git status query's exit code.");
        var statusExitCheck = workflow.IndexOf("if ($sourceStatusExitCode -ne 0)", statusExitCapture,
            StringComparison.Ordinal);
        var unknownCleanlinessFailure = workflow.IndexOf("source cleanliness is unknown", statusExitCheck,
            StringComparison.Ordinal);
        var dirtyEntries = workflow.IndexOf(
            "$sourceDirtyEntries = @($sourceStatus | Where-Object", unknownCleanlinessFailure,
            StringComparison.Ordinal);
        var dirtyCheck = workflow.IndexOf("if ($sourceDirtyEntries.Count -ne 0)", dirtyEntries,
            StringComparison.Ordinal);
        var dirtyFailure = workflow.IndexOf("Desk-candidate source tree must be clean", dirtyCheck,
            StringComparison.Ordinal);
        var buildCall = workflow.IndexOf("& pwsh -NoProfile -File .\\scripts\\Build-PiPlay.ps1", dirtyFailure,
            StringComparison.Ordinal);
        var payloadOutput = workflow.IndexOf("\"payload=$payloadRoot\" | Out-File", buildCall,
            StringComparison.Ordinal);
        var uploadStep = workflow.IndexOf("- name: Upload desk candidate", payloadOutput,
            StringComparison.Ordinal);

        Assert.True(candidateStep >= 0 && candidateStep < statusCall,
            "The clean-source query must be inside the dispatch candidate step.");
        Assert.True(string.IsNullOrWhiteSpace(
                workflow[(statusCall + statusInvocation.Length)..statusExitCapture]),
            "The workflow must capture git status' exit code immediately after the actual invocation.");
        Assert.True(statusCall < statusExitCapture && statusExitCapture < statusExitCheck &&
                    statusExitCheck < unknownCleanlinessFailure,
            "A nonzero git status exit must fail closed before cleanliness is inferred.");
        Assert.True(unknownCleanlinessFailure < dirtyEntries && dirtyEntries < dirtyCheck &&
                    dirtyCheck < dirtyFailure,
            "A successful git status query must still reject every dirty entry.");
        Assert.True(dirtyFailure < buildCall && buildCall < payloadOutput && payloadOutput < uploadStep,
            "Neither candidate build nor payload upload may precede the successful clean-source gate.");
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

    private static async Task<(int ExitCode, string Output, string Error, string[] LockKeys, bool LockCleanupCalled)>
        RunSkipDeployWithoutStableRootHarnessAsync(string host, bool failDeployLock = false)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "PiPlaySkipDeploy-" + Guid.NewGuid().ToString("N"));
        var scriptsRoot = Path.Combine(tempRoot, "scripts");
        var toolsRoot = Path.Combine(tempRoot, "tools");
        Directory.CreateDirectory(scriptsRoot);
        Directory.CreateDirectory(toolsRoot);
        var lockCapture = Path.Combine(tempRoot, "lock-keys.txt");
        var lockCleanupCapture = Path.Combine(tempRoot, "lock-cleanup.txt");
        var deployRoot = Path.Combine(tempRoot, "stable");
        try
        {
            File.Copy(Path.Combine(RepoRoot, "scripts", "Publish-Stable.ps1"),
                Path.Combine(scriptsRoot, "Publish-Stable.ps1"));
            await File.WriteAllTextAsync(Path.Combine(scriptsRoot, "NativeCommand.ps1"), """
                function Invoke-NativeCommandQuiet {
                    param([Parameter(Mandatory = $true)][scriptblock]$Command)
                    & $Command
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(scriptsRoot, "DeploySwap.ps1"), "# unused fixture\n");
            await File.WriteAllTextAsync(Path.Combine(scriptsRoot, "StableDeployRoot.ps1"), """
                function Resolve-StableDeployRoot {
                    if ($env:PIPLAY_TEST_FAIL_DEPLOY_LOCK -eq 'true') {
                        return [System.IO.Path]::GetFullPath($env:PIPLAY_TEST_DEPLOY_ROOT)
                    }
                    throw 'Deploy root resolver ran under -SkipDeploy.'
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(scriptsRoot, "PublishLock.ps1"), """
                function New-PublishLock {
                    param([string]$Key, [string]$What)
                    Add-Content -LiteralPath $env:PIPLAY_TEST_LOCK_CAPTURE -Value $Key
                    if (($env:PIPLAY_TEST_FAIL_DEPLOY_LOCK -eq 'true') -and $Key -like 'deploy|*') {
                        throw 'fixture deploy lock failure'
                    }
                    return $Key
                }
                function Close-PublishLocks {
                    Set-Content -LiteralPath $env:PIPLAY_TEST_LOCK_CLEANUP_CAPTURE -Value 'called'
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(scriptsRoot, "Build-PiPlay.ps1"), """
                param(
                    [string]$Stage,
                    [string]$Channel,
                    [int]$KeepPublishCount,
                    [AllowEmptyString()][string]$StopProcessName,
                    [switch]$NoVersionBump,
                    [switch]$NoBuildNumberBump,
                    [string]$NonReleaseReason,
                    [switch]$AllowDirtySource,
                    [string]$SignScript
                )
                $latest = Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\publish\latest'
                New-Item -ItemType Directory -Path $latest -Force | Out-Null
                [ordered]@{
                    channel = 'Stable'
                    publishLabel = 'skip-deploy-fixture'
                    releaseEvidence = $false
                    version = '1.2.3'
                    buildNumber = 7
                    sourceCommit = ('a' * 40)
                    sourceDirty = $false
                    signing = [ordered]@{ enabled = $false }
                } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $latest 'build-info.json')
                $global:LASTEXITCODE = 0
                """);
            await File.WriteAllTextAsync(Path.Combine(scriptsRoot, "Test-PublishMetadata.ps1"), """
                param([string]$PublishRoot, [string]$PublishLabel)
                $global:LASTEXITCODE = 0
                """);
            await File.WriteAllTextAsync(Path.Combine(toolsRoot, "git.cmd"), "@echo off\r\nexit /b 0\r\n");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "VERSION"), "1.2.3");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "BUILD_NUMBER"), "7");

            var startInfo = new ProcessStartInfo
            {
                FileName = host,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["PATH"] = toolsRoot + Path.PathSeparator +
                (Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
            startInfo.Environment["PIPLAY_TEST_LOCK_CAPTURE"] = lockCapture;
            startInfo.Environment["PIPLAY_TEST_LOCK_CLEANUP_CAPTURE"] = lockCleanupCapture;
            startInfo.Environment["PIPLAY_TEST_FAIL_DEPLOY_LOCK"] = failDeployLock ? "true" : "false";
            startInfo.Environment["PIPLAY_TEST_DEPLOY_ROOT"] = deployRoot;
            startInfo.Environment.Remove("PIPLAY_STABLE_ROOT");
            if (host.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
            }
            var arguments = failDeployLock
                ? new[]
                {
                    "-NoProfile", "-File", Path.Combine(scriptsRoot, "Publish-Stable.ps1"),
                    "-DeployRoot", deployRoot, "-SkipTests", "-AllowDirty"
                }
                : new[]
                {
                    "-NoProfile", "-File", Path.Combine(scriptsRoot, "Publish-Stable.ps1"),
                    "-SkipDeploy", "-SkipTests", "-AllowDirty"
                };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };
            Assert.True(process.Start(), "Skip-deploy harness did not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            var lockKeys = File.Exists(lockCapture)
                ? (await File.ReadAllLinesAsync(lockCapture)).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray()
                : Array.Empty<string>();
            return (process.ExitCode, await outputTask, await errorTask, lockKeys,
                File.Exists(lockCleanupCapture));
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* best-effort fixture cleanup */ }
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunScriptAsync(
        string host, string relativeScript, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = host,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        if (host.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
        }
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(Path.Combine(RepoRoot,
            relativeScript.Replace('/', Path.DirectorySeparatorChar)));
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), $"{relativeScript} did not start under {host}.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunArchivedSourceBuildHarnessAsync(
        string host, bool noRestore = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = host,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["PIPLAY_TEST_BUILD_SCRIPT"] = Path.Combine(RepoRoot, "scripts", "Build-PiPlay.ps1");
        startInfo.Environment["PIPLAY_TEST_NATIVE_SCRIPT"] = Path.Combine(RepoRoot, "scripts", "NativeCommand.ps1");
        startInfo.Environment["PIPLAY_TEST_NO_RESTORE"] = noRestore ? "true" : "false";
        startInfo.ArgumentList.Add("-NoProfile");
        if (host.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
        }
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("""
            $ErrorActionPreference = 'Stop'
            $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) `
                ('PiPlayArchivedSource-' + [guid]::NewGuid().ToString('N'))
            $oldPath = $env:PATH
            $oldLiveMarker = $env:PIPLAY_TEST_LIVE_MARKER
            $oldCapturePath = $env:PIPLAY_TEST_CAPTURE_PATH
            $oldCaptureContent = $env:PIPLAY_TEST_CAPTURE_CONTENT
            $oldPublishOutput = $env:PIPLAY_TEST_PUBLISH_OUTPUT
            try {
                $scriptsRoot = Join-Path $fixtureRoot 'scripts'
                $projectRoot = Join-Path $fixtureRoot 'src\PiPlay'
                $docsRoot = Join-Path $fixtureRoot 'docs'
                $toolsRoot = Join-Path $fixtureRoot '.tools'
                foreach ($path in @($scriptsRoot, $projectRoot, $docsRoot, $toolsRoot)) {
                    New-Item -ItemType Directory -Path $path -Force | Out-Null
                }

                Copy-Item -LiteralPath $env:PIPLAY_TEST_BUILD_SCRIPT `
                    -Destination (Join-Path $scriptsRoot 'Build-PiPlay.ps1')
                Copy-Item -LiteralPath $env:PIPLAY_TEST_NATIVE_SCRIPT `
                    -Destination (Join-Path $scriptsRoot 'NativeCommand.ps1')
                Set-Content -LiteralPath (Join-Path $scriptsRoot 'StableDeployRoot.ps1') `
                    -Value '# fixture helper'
                Set-Content -LiteralPath (Join-Path $scriptsRoot 'Test-UiSmoke.ps1') `
                    -Value '# fixture smoke'
                Set-Content -LiteralPath (Join-Path $projectRoot 'PiPlay.csproj') `
                    -Value '<Project Sdk="Microsoft.NET.Sdk" />'
                Set-Content -LiteralPath (Join-Path $projectRoot 'source-marker.txt') `
                    -Value 'committed' -NoNewline
                Set-Content -LiteralPath (Join-Path $fixtureRoot 'README.md') -Value '# Fixture'
                Set-Content -LiteralPath (Join-Path $docsRoot 'CHANGELOG.md') -Value '# Changelog'
                Set-Content -LiteralPath (Join-Path $docsRoot 'PiPlay_Product_Engineering_Spec.md') `
                    -Value '# Contract'
                Set-Content -LiteralPath (Join-Path $fixtureRoot 'VERSION') -Value '1.2.3' -NoNewline
                Set-Content -LiteralPath (Join-Path $fixtureRoot 'BUILD_NUMBER') -Value '7' -NoNewline
                Set-Content -LiteralPath (Join-Path $fixtureRoot '.gitignore') `
                    -Value "/.tools/`n/out/"

                & git -C $fixtureRoot init --quiet
                if ($LASTEXITCODE -ne 0) { throw 'git init failed.' }
                & git -C $fixtureRoot config user.email 'fixture@example.invalid'
                & git -C $fixtureRoot config user.name 'PiPlay fixture'
                & git -C $fixtureRoot config core.autocrlf false
                & git -C $fixtureRoot add --all
                & git -C $fixtureRoot commit --quiet -m 'fixture baseline'
                if ($LASTEXITCODE -ne 0) { throw 'git commit failed.' }

                $fakeDotnet = @'
            @echo off
            setlocal
            if /I "%~1"=="restore" (
              > "%PIPLAY_TEST_LIVE_MARKER%" echo dirty-live
              > "%PIPLAY_TEST_CAPTURE_PATH%" echo %~f2
              for /f "usebackq delims=" %%L in ("%~dp2source-marker.txt") do > "%PIPLAY_TEST_CAPTURE_CONTENT%" echo %%L
              exit /b 0
            )
            if /I "%~1"=="publish" (
              if not exist "%PIPLAY_TEST_PUBLISH_OUTPUT%" mkdir "%PIPLAY_TEST_PUBLISH_OUTPUT%"
              > "%PIPLAY_TEST_PUBLISH_OUTPUT%\PiPlay.exe" echo fixture executable
              exit /b 0
            )
            exit /b 0
            '@
                Set-Content -LiteralPath (Join-Path $toolsRoot 'dotnet.cmd') `
                    -Value $fakeDotnet -Encoding Ascii

                $capturePath = Join-Path $toolsRoot 'project-path.txt'
                $captureContent = Join-Path $toolsRoot 'source-content.txt'
                $publishRoot = Join-Path $fixtureRoot 'out'
                $publishLabel = 'archive-fixture'
                $env:PATH = $toolsRoot + [System.IO.Path]::PathSeparator + $oldPath
                $env:PIPLAY_TEST_LIVE_MARKER = Join-Path $projectRoot 'source-marker.txt'
                $env:PIPLAY_TEST_CAPTURE_PATH = $capturePath
                $env:PIPLAY_TEST_CAPTURE_CONTENT = $captureContent
                $env:PIPLAY_TEST_PUBLISH_OUTPUT = Join-Path $publishRoot $publishLabel

                $failure = $null
                $noRestore = $env:PIPLAY_TEST_NO_RESTORE -eq 'true'
                try {
                    & (Join-Path $scriptsRoot 'Build-PiPlay.ps1') `
                        -Stage Publish `
                        -NoVersionBump `
                        -NoBuildNumberBump `
                        -NoLatest `
                        -NoVersionTable `
                        -NoRestore:$noRestore `
                        -PublishRoot $publishRoot `
                        -PublishLabel $publishLabel `
                        -StopProcessName ''
                } catch {
                    $failure = $_.Exception.Message
                }

                if ($noRestore) {
                    if ($failure -notlike '*-NoRestore cannot be used for clean Publish/Release*') {
                        throw "Expected clean archived no-restore rejection, got '$failure'."
                    }
                    Write-Output 'ARCHIVED SOURCE NO-RESTORE REJECTED'
                    exit 0
                }

                if ($failure -notlike '*Source dirty state changed during build*') {
                    throw "Expected final live-source drift rejection, got '$failure'."
                }
                $usedProject = (Get-Content -LiteralPath $capturePath -Raw).Trim()
                $usedContent = (Get-Content -LiteralPath $captureContent -Raw).Trim()
                $fixturePrefix = [System.IO.Path]::GetFullPath($fixtureRoot).TrimEnd('\') + '\'
                if ([System.IO.Path]::GetFullPath($usedProject).StartsWith(
                    $fixturePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "Clean publish used the mutable checkout project '$usedProject'."
                }
                if ($usedContent -cne 'committed') {
                    throw "Clean publish read '$usedContent' instead of committed source."
                }
                Write-Output 'ARCHIVED SOURCE BUILD PASS'
                exit 0
            } finally {
                $env:PATH = $oldPath
                $env:PIPLAY_TEST_LIVE_MARKER = $oldLiveMarker
                $env:PIPLAY_TEST_CAPTURE_PATH = $oldCapturePath
                $env:PIPLAY_TEST_CAPTURE_CONTENT = $oldCaptureContent
                $env:PIPLAY_TEST_PUBLISH_OUTPUT = $oldPublishOutput
                if (Test-Path -LiteralPath $fixtureRoot) {
                    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
                }
            }
            """);

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), "Archived-source build harness did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunArchivedStampMismatchHarnessAsync(
        string host)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = host,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["PIPLAY_TEST_BUILD_SCRIPT"] = Path.Combine(RepoRoot, "scripts", "Build-PiPlay.ps1");
        startInfo.ArgumentList.Add("-NoProfile");
        if (host.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
        }
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("""
            $ErrorActionPreference = 'Stop'
            $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) `
                ('PiPlayArchivedStamps-' + [guid]::NewGuid().ToString('N'))
            try {
                New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
                Set-Content -LiteralPath (Join-Path $fixtureRoot 'VERSION') -Value '2.0.0' -NoNewline
                Set-Content -LiteralPath (Join-Path $fixtureRoot 'BUILD_NUMBER') -Value '8' -NoNewline

                $tokens = $null
                $errors = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                    $env:PIPLAY_TEST_BUILD_SCRIPT, [ref]$tokens, [ref]$errors)
                if ($errors.Count -gt 0) { throw ($errors | Out-String) }
                foreach ($name in @('Get-ProjectVersion', 'Get-BuildNumberValue', 'Assert-ArchivedSourceStamps')) {
                    $functionAst = $ast.Find({
                        param($node)
                        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $node.Name -eq $name
                    }, $true)
                    if (-not $functionAst) { throw "$name was not found." }
                    Set-Item -Path ("Function:script:{0}" -f $name) -Value $functionAst.Body.GetScriptBlock()
                }

                try {
                    Assert-ArchivedSourceStamps -SourceRoot $fixtureRoot `
                        -ResolvedVersion '1.2.3' -ResolvedBuildNumber 7
                    [Console]::Error.WriteLine('Archived stamp mismatch was accepted.')
                    exit 5
                } catch {
                    if ($_.Exception.Message -notlike '*do not match captured source commit stamps*') { throw }
                    Write-Output 'ARCHIVED SOURCE STAMP DRIFT REJECTED'
                    exit 0
                }
            } finally {
                if (Test-Path -LiteralPath $fixtureRoot) {
                    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
                }
            }
            """);

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), "Archived-stamp harness did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunRequiredSourceSnapshotCoherenceHarnessAsync()
    {
        var powerShellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell", "7", "pwsh.exe");
        Assert.True(File.Exists(powerShellPath), $"PowerShell 7 was not found at {powerShellPath}.");
        var startInfo = new ProcessStartInfo
        {
            FileName = powerShellPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["PIPLAY_TEST_BUILD_SCRIPT"] = Path.Combine(RepoRoot, "scripts", "Build-PiPlay.ps1");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("""
            $tokens = $null
            $errors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $env:PIPLAY_TEST_BUILD_SCRIPT, [ref]$tokens, [ref]$errors)
            if ($errors.Count -gt 0) { throw ($errors | Out-String) }
            $functionAst = $ast.Find({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                    $node.Name -eq 'Get-RequiredSourceSnapshot'
            }, $true)
            if (-not $functionAst) { throw 'Get-RequiredSourceSnapshot was not found.' }
            Set-Item -Path 'Function:script:Get-RequiredSourceSnapshot' `
                -Value $functionAst.Body.GetScriptBlock()

            $script:commitQueryCount = 0
            function Get-SourceCommit {
                $script:commitQueryCount++
                if ($script:commitQueryCount -eq 1) { return ('a' * 40) }
                return ('b' * 40)
            }
            function Get-SourceDirtyEntries { return @() }

            try {
                Get-RequiredSourceSnapshot -RepositoryRoot 'C:\fixture' | Out-Null
                [Console]::Error.WriteLine('Internally incoherent source snapshot was accepted.')
                exit 5
            } catch {
                Write-Output 'INTERNAL SOURCE SNAPSHOT DRIFT REJECTED'
                exit 0
            }
            """);

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), "Required source snapshot harness did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunSourceSnapshotHarnessAsync(
        string mode, bool expectRejection)
    {
        var powerShellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell", "7", "pwsh.exe");
        Assert.True(File.Exists(powerShellPath), $"PowerShell 7 was not found at {powerShellPath}.");
        var startInfo = new ProcessStartInfo
        {
            FileName = powerShellPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["PIPLAY_TEST_BUILD_SCRIPT"] = Path.Combine(RepoRoot, "scripts", "Build-PiPlay.ps1");
        startInfo.Environment["PIPLAY_TEST_SNAPSHOT_MODE"] = mode;
        startInfo.Environment["PIPLAY_TEST_EXPECT_REJECTION"] = expectRejection ? "true" : "false";
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("""
            $tokens = $null
            $errors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $env:PIPLAY_TEST_BUILD_SCRIPT, [ref]$tokens, [ref]$errors)
            if ($errors.Count -gt 0) { throw ($errors | Out-String) }
            $functionAst = $ast.Find({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                    $node.Name -eq 'Assert-SourceSnapshotUnchanged'
            }, $true)
            if (-not $functionAst) { throw 'Assert-SourceSnapshotUnchanged was not found.' }
            Set-Item -Path 'Function:script:Assert-SourceSnapshotUnchanged' `
                -Value $functionAst.Body.GetScriptBlock()

            $commitA = 'a' * 40
            $commitB = 'b' * 40
            $initial = [pscustomobject]@{ Commit = $commitA; DirtyEntries = @() }
            $current = [pscustomobject]@{ Commit = $commitA; DirtyEntries = @() }
            $requireClean = $true
            switch ($env:PIPLAY_TEST_SNAPSHOT_MODE) {
                'changed-head' { $current.Commit = $commitB }
                'changed-state' { $current.DirtyEntries = @(' M src/PiPlay/App.xaml.cs') }
                'dirty-clean-required' {
                    $initial.DirtyEntries = @(' M VERSION')
                    $current.DirtyEntries = @(' M VERSION')
                }
                'unchanged-dirty-diagnostic' {
                    $initial.DirtyEntries = @(' M VERSION')
                    $current.DirtyEntries = @(' M VERSION')
                    $requireClean = $false
                }
                default { throw "Unknown snapshot mode '$($env:PIPLAY_TEST_SNAPSHOT_MODE)'." }
            }

            $expectRejection = $env:PIPLAY_TEST_EXPECT_REJECTION -eq 'true'
            try {
                Assert-SourceSnapshotUnchanged -InitialSnapshot $initial -CurrentSnapshot $current `
                    -RequireClean:$requireClean
                if ($expectRejection) {
                    [Console]::Error.WriteLine('Source snapshot drift was accepted.')
                    exit 5
                }
                Write-Output 'SOURCE SNAPSHOT ACCEPTED'
                exit 0
            } catch {
                if (-not $expectRejection) {
                    [Console]::Error.WriteLine("Stable diagnostic snapshot was rejected: $($_.Exception.Message)")
                    exit 6
                }
                Write-Output 'SOURCE SNAPSHOT REJECTED'
                exit 0
            }
            """);

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), "Source snapshot harness did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunCandidateChildIsolationHarnessAsync(
        string payloadRoot)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "PiPlayCandidateChild-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var fakeChild = Path.Combine(tempRoot, "fake-child.cmd");
            await File.WriteAllTextAsync(fakeChild, """
                @echo off
                if not exist "%PIPLAY_DATA_ROOT%" mkdir "%PIPLAY_DATA_ROOT%"
                > "%PIPLAY_TEST_CHILD_CAPTURE%" echo %PIPLAY_DATA_ROOT%
                > "%PIPLAY_DATA_ROOT%\state.txt" echo fake runtime state
                exit /b 0
                """);
            var powerShellPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell", "7", "pwsh.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = powerShellPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["PIPLAY_TEST_UI_SCRIPT"] = Path.Combine(RepoRoot, "scripts", "Test-UiSmoke.ps1");
            startInfo.Environment["PIPLAY_TEST_ROOT_HELPER"] = Path.Combine(RepoRoot, "scripts", "StableDeployRoot.ps1");
            startInfo.Environment["PIPLAY_TEST_PAYLOAD_ROOT"] = payloadRoot;
            startInfo.Environment["PIPLAY_TEST_FAKE_CHILD"] = fakeChild;
            startInfo.Environment["PIPLAY_TEST_CHILD_CAPTURE"] = Path.Combine(tempRoot, "captured-data-root.txt");
            startInfo.Environment["PIPLAY_DESK_CANDIDATE_DATA_ROOT"] = Path.Combine(tempRoot, "candidate-data");
            startInfo.Environment["PIPLAY_DATA_ROOT"] = Path.Combine(tempRoot, "caller-data-root");
            startInfo.Environment["PIPLAY_CHANNEL"] = "Default";
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("""
                $tokens = $null
                $errors = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                    $env:PIPLAY_TEST_UI_SCRIPT, [ref]$tokens, [ref]$errors)
                if ($errors.Count -gt 0) { throw ($errors | Out-String) }
                . $env:PIPLAY_TEST_ROOT_HELPER
                foreach ($name in @('Resolve-DeskCandidateDataRoot', 'New-SmokeProcessStartInfo')) {
                    $functionAst = $ast.Find({
                        param($node)
                        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $node.Name -eq $name
                    }, $true)
                    if (-not $functionAst) { throw "Function '$name' was not found." }
                    Set-Item -Path ("Function:script:{0}" -f $name) -Value $functionAst.Body.GetScriptBlock()
                }

                $callerDataRoot = $env:PIPLAY_DATA_ROOT
                $callerChannel = $env:PIPLAY_CHANNEL
                $releaseStartInfo = New-SmokeProcessStartInfo -SelectedExe $env:ComSpec -SmokeMode Release
                if ($releaseStartInfo.Environment.ContainsKey('PIPLAY_DATA_ROOT')) {
                    throw 'Release child inherited PIPLAY_DATA_ROOT.'
                }
                if ($releaseStartInfo.Environment.ContainsKey('PIPLAY_CHANNEL')) {
                    throw 'Release child inherited PIPLAY_CHANNEL.'
                }
                $dataRoot = Resolve-DeskCandidateDataRoot `
                    -SelectedRoot $env:PIPLAY_TEST_PAYLOAD_ROOT -SourceCommit ('a' * 40)
                $startInfo = New-SmokeProcessStartInfo -SelectedExe $env:ComSpec `
                    -SmokeMode DeskCandidate -CandidateDataRoot $dataRoot
                if ($startInfo.Environment.ContainsKey('PIPLAY_CHANNEL')) {
                    throw 'Candidate child inherited PIPLAY_CHANNEL.'
                }
                if ($startInfo.Environment['PIPLAY_DATA_ROOT'] -cne $dataRoot) {
                    throw 'Candidate child did not receive only the resolved data root.'
                }
                $startInfo.ArgumentList.Add('/d')
                $startInfo.ArgumentList.Add('/c')
                $startInfo.ArgumentList.Add($env:PIPLAY_TEST_FAKE_CHILD)
                $process = [System.Diagnostics.Process]::Start($startInfo)
                try {
                    if (-not $process.WaitForExit(10000)) { $process.Kill(); throw 'Fake child timed out.' }
                    if ($process.ExitCode -ne 0) { throw "Fake child exited $($process.ExitCode)." }
                } finally {
                    $process.Dispose()
                }
                if ($env:PIPLAY_DATA_ROOT -cne $callerDataRoot) {
                    throw 'Candidate launch leaked PIPLAY_DATA_ROOT into the caller.'
                }
                if ($env:PIPLAY_CHANNEL -cne $callerChannel) {
                    throw 'Candidate launch leaked PIPLAY_CHANNEL into the caller.'
                }
                $captured = (Get-Content -LiteralPath $env:PIPLAY_TEST_CHILD_CAPTURE -Raw).Trim()
                if ($captured -cne $dataRoot) { throw "Child received '$captured', expected '$dataRoot'." }
                if (-not (Test-Path -LiteralPath (Join-Path $dataRoot 'state.txt') -PathType Leaf)) {
                    throw 'Fake child did not write to the external candidate data root.'
                }
                Write-Output 'CANDIDATE CHILD ISOLATION PASS'
                exit 0
                """);

            using var process = new Process { StartInfo = startInfo };
            Assert.True(process.Start(), "Candidate child isolation harness did not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            return (process.ExitCode, await outputTask, await errorTask);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* best-effort fixture cleanup */ }
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunCandidateDataRootRejectionHarnessAsync(
        string payloadRoot, string candidateDataRoot)
    {
        var powerShellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell", "7", "pwsh.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = powerShellPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["PIPLAY_TEST_UI_SCRIPT"] = Path.Combine(RepoRoot, "scripts", "Test-UiSmoke.ps1");
        startInfo.Environment["PIPLAY_TEST_ROOT_HELPER"] = Path.Combine(RepoRoot, "scripts", "StableDeployRoot.ps1");
        startInfo.Environment["PIPLAY_TEST_PAYLOAD_ROOT"] = payloadRoot;
        startInfo.Environment["PIPLAY_DESK_CANDIDATE_DATA_ROOT"] = candidateDataRoot;
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("""
            $tokens = $null
            $errors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $env:PIPLAY_TEST_UI_SCRIPT, [ref]$tokens, [ref]$errors)
            if ($errors.Count -gt 0) { throw ($errors | Out-String) }
            . $env:PIPLAY_TEST_ROOT_HELPER
            $functionAst = $ast.Find({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                    $node.Name -eq 'Resolve-DeskCandidateDataRoot'
            }, $true)
            if (-not $functionAst) { throw 'Resolve-DeskCandidateDataRoot was not found.' }
            Set-Item -Path 'Function:script:Resolve-DeskCandidateDataRoot' `
                -Value $functionAst.Body.GetScriptBlock()
            try {
                Resolve-DeskCandidateDataRoot -SelectedRoot $env:PIPLAY_TEST_PAYLOAD_ROOT `
                    -SourceCommit ('a' * 40) | Out-Null
                [Console]::Error.WriteLine('Payload-overlapping candidate data root was accepted.')
                exit 5
            } catch {
                Write-Output 'CANDIDATE DATA ROOT REJECTED'
                exit 0
            }
            """);

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), "Candidate data-root rejection harness did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunCandidateEvidenceRootHarnessAsync(
        string payloadRoot, string mode, bool expectSuccess, string smokeMode = "DeskCandidate")
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "PiPlayCandidateEvidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string? junctionPath = null;
        var junctionRemoved = true;
        try
        {
            var useEnvironment = mode is "external-env" or "descendant-env";
            string evidenceRoot;
            switch (mode)
            {
                case "external-env":
                    evidenceRoot = Path.Combine(tempRoot, "evidence");
                    break;
                case "equal":
                    evidenceRoot = payloadRoot;
                    break;
                case "descendant-env":
                    evidenceRoot = Path.Combine(payloadRoot, "evidence");
                    break;
                case "ancestor":
                    evidenceRoot = Directory.GetParent(payloadRoot)?.FullName
                        ?? throw new InvalidOperationException("Candidate payload has no parent.");
                    break;
                case "canonical":
                    evidenceRoot = Path.Combine(payloadRoot, "not-created", "..");
                    break;
                case "device":
                    evidenceRoot = @"\\?\" + payloadRoot;
                    break;
                case "relative":
                    evidenceRoot = "relative-evidence";
                    break;
                case "junction":
                    junctionPath = Path.Combine(tempRoot, "payload-alias");
                    var junctionStart = new ProcessStartInfo
                    {
                        FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    junctionStart.ArgumentList.Add("/d");
                    junctionStart.ArgumentList.Add("/c");
                    junctionStart.ArgumentList.Add("mklink");
                    junctionStart.ArgumentList.Add("/J");
                    junctionStart.ArgumentList.Add(junctionPath);
                    junctionStart.ArgumentList.Add(payloadRoot);
                    using (var junctionProcess = new Process { StartInfo = junctionStart })
                    {
                        Assert.True(junctionProcess.Start(), "Junction fixture command did not start.");
                        var junctionOutputTask = junctionProcess.StandardOutput.ReadToEndAsync();
                        var junctionErrorTask = junctionProcess.StandardError.ReadToEndAsync();
                        await junctionProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                        Assert.True(junctionProcess.ExitCode == 0,
                            $"Junction fixture failed ({junctionProcess.ExitCode}).{Environment.NewLine}" +
                            await junctionErrorTask + Environment.NewLine + await junctionOutputTask);
                    }
                    Assert.True((File.GetAttributes(junctionPath) & FileAttributes.ReparsePoint) != 0,
                        "Junction fixture is not a reparse point.");
                    evidenceRoot = junctionPath;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown evidence-root mode.");
            }

            var powerShellPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell", "7", "pwsh.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = powerShellPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["PIPLAY_TEST_UI_SCRIPT"] = Path.Combine(RepoRoot, "scripts", "Test-UiSmoke.ps1");
            startInfo.Environment["PIPLAY_TEST_ROOT_HELPER"] = Path.Combine(RepoRoot, "scripts", "StableDeployRoot.ps1");
            startInfo.Environment["PIPLAY_TEST_PAYLOAD_ROOT"] = payloadRoot;
            startInfo.Environment["PIPLAY_TEST_REQUESTED_EVIDENCE"] = useEnvironment ? string.Empty : evidenceRoot;
            startInfo.Environment["PIPLAY_UI_EVIDENCE_ROOT"] = useEnvironment ? evidenceRoot : string.Empty;
            startInfo.Environment["PIPLAY_TEST_EXPECTED_EVIDENCE"] = evidenceRoot;
            startInfo.Environment["PIPLAY_TEST_EXPECT_SUCCESS"] = expectSuccess ? "true" : "false";
            startInfo.Environment["PIPLAY_TEST_SMOKE_MODE"] = smokeMode;
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("""
                $tokens = $null
                $errors = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                    $env:PIPLAY_TEST_UI_SCRIPT, [ref]$tokens, [ref]$errors)
                if ($errors.Count -gt 0) { throw ($errors | Out-String) }
                . $env:PIPLAY_TEST_ROOT_HELPER
                $functionAst = $ast.Find({
                    param($node)
                    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                        $node.Name -eq 'Resolve-SmokeEvidenceRoot'
                }, $true)
                if (-not $functionAst) { throw 'Resolve-SmokeEvidenceRoot was not found.' }
                Set-Item -Path 'Function:script:Resolve-SmokeEvidenceRoot' `
                    -Value $functionAst.Body.GetScriptBlock()

                $smokeMode = [string]$env:PIPLAY_TEST_SMOKE_MODE
                if ($smokeMode -eq 'Release') {
                    $script:releaseVerifierStubCalls = 0
                    function Assert-ReleasePreflight {
                        param([string]$SelectedRoot)
                        if (-not (Test-Path -LiteralPath (Join-Path $SelectedRoot 'PiPlay.exe') -PathType Leaf)) {
                            throw 'Release verifier stub received a root without PiPlay.exe.'
                        }
                        $script:releaseVerifierStubCalls++
                    }
                    Assert-ReleasePreflight -SelectedRoot $env:PIPLAY_TEST_PAYLOAD_ROOT
                    if ($script:releaseVerifierStubCalls -ne 1) {
                        throw 'Release verifier stub was not called exactly once.'
                    }
                    Write-Output 'RELEASE VERIFIER STUB PASS'
                }
                $verdictPrefix = if ($smokeMode -eq 'Release') { 'RELEASE' } else { 'CANDIDATE' }
                $requested = [string]$env:PIPLAY_TEST_REQUESTED_EVIDENCE
                if ($env:PIPLAY_TEST_EXPECT_SUCCESS -eq 'true') {
                    $resolved = Resolve-SmokeEvidenceRoot -SmokeMode $smokeMode `
                        -SelectedRoot $env:PIPLAY_TEST_PAYLOAD_ROOT `
                        -RequestedEvidenceRoot $requested
                    $expected = Resolve-FullyQualifiedFileSystemPath `
                        -Path $env:PIPLAY_TEST_EXPECTED_EVIDENCE -Name 'ExpectedEvidenceRoot'
                    if ($resolved -cne $expected) {
                        throw "Resolved evidence root '$resolved' did not equal '$expected'."
                    }
                    New-Item -ItemType Directory -Path $resolved -Force | Out-Null
                    Set-Content -LiteralPath (Join-Path $resolved 'fake-screenshot.png') `
                        -Value 'fake screenshot' -NoNewline
                    if (-not (Test-Path -LiteralPath (Join-Path $resolved 'fake-screenshot.png') -PathType Leaf)) {
                        throw 'External evidence file was not created.'
                    }
                    Write-Output "$verdictPrefix EVIDENCE ROOT ACCEPTED"
                    exit 0
                }

                try {
                    Resolve-SmokeEvidenceRoot -SmokeMode $smokeMode `
                        -SelectedRoot $env:PIPLAY_TEST_PAYLOAD_ROOT `
                        -RequestedEvidenceRoot $requested | Out-Null
                    [Console]::Error.WriteLine('Unsafe candidate evidence root was accepted.')
                    exit 5
                } catch {
                    Write-Output "$verdictPrefix EVIDENCE ROOT REJECTED"
                    exit 0
                }
                """);

            using var process = new Process { StartInfo = startInfo };
            Assert.True(process.Start(), "Candidate evidence-root harness did not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            return (process.ExitCode, await outputTask, await errorTask);
        }
        finally
        {
            if (junctionPath is not null && Directory.Exists(junctionPath))
            {
                try { Directory.Delete(junctionPath, recursive: false); }
                catch { junctionRemoved = false; }
            }
            if (junctionRemoved)
            {
                try { Directory.Delete(tempRoot, recursive: true); }
                catch { /* best-effort fixture cleanup */ }
            }
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunOptionalGitHarnessAsync(string mode)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "PiPlayOptionalGit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            if (mode == "success")
            {
                await File.WriteAllTextAsync(Path.Combine(tempRoot, "git.cmd"),
                    "@echo off\r\necho optional-success\r\nexit /b 0\r\n");
            }
            else if (mode == "nonzero")
            {
                await File.WriteAllTextAsync(Path.Combine(tempRoot, "git.cmd"),
                    "@echo off\r\necho discarded-output\r\nexit /b 31\r\n");
            }

            var powerShellPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell", "7", "pwsh.exe");
            Assert.True(File.Exists(powerShellPath), $"PowerShell 7 was not found at {powerShellPath}.");
            var startInfo = new ProcessStartInfo
            {
                FileName = powerShellPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["PATH"] = mode == "missing"
                ? tempRoot
                : tempRoot + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
            startInfo.Environment["PIPLAY_TEST_PROVENANCE_SCRIPT"] =
                Path.Combine(RepoRoot, "scripts", "Publish-Stable.ps1");
            startInfo.Environment["PIPLAY_TEST_NATIVE_SCRIPT"] =
                Path.Combine(RepoRoot, "scripts", "NativeCommand.ps1");
            startInfo.Environment["PIPLAY_TEST_PROVENANCE_ROOT"] = tempRoot;
            startInfo.Environment["PIPLAY_TEST_OPTIONAL_GIT_MODE"] = mode;
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("""
                $tokens = $null
                $errors = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                    $env:PIPLAY_TEST_PROVENANCE_SCRIPT, [ref]$tokens, [ref]$errors)
                if ($errors.Count -gt 0) { throw ($errors | Out-String) }
                . $env:PIPLAY_TEST_NATIVE_SCRIPT
                $functionAst = $ast.Find({
                    param($node)
                    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                        $node.Name -eq 'Invoke-Git'
                }, $true)
                if (-not $functionAst) { throw 'Invoke-Git was not found.' }
                $parameterText = @($functionAst.Parameters | ForEach-Object { $_.Extent.Text }) -join ','
                $bodyText = $functionAst.Body.Extent.Text
                $bodyText = $bodyText.Substring(1, $bodyText.Length - 2)
                $functionText = if ($parameterText) {
                    "param($parameterText)`n$bodyText"
                } else { $bodyText }
                Set-Item -Path 'Function:script:Invoke-Git' `
                    -Value ([scriptblock]::Create($functionText))
                $script:repoRoot = $env:PIPLAY_TEST_PROVENANCE_ROOT
                $global:LASTEXITCODE = 47
                try {
                    $result = Invoke-Git @('status', '--porcelain')
                } catch {
                    [Console]::Error.WriteLine("Optional Git probe threw: $($_.Exception.Message)")
                    exit 7
                }
                if ($env:PIPLAY_TEST_OPTIONAL_GIT_MODE -eq 'success') {
                    if ((@($result) -join '').Trim() -ne 'optional-success') {
                        [Console]::Error.WriteLine("Optional success output was '$($result -join '|')'.")
                        exit 6
                    }
                } elseif ($null -ne $result) {
                    [Console]::Error.WriteLine("Optional failure returned '$($result -join '|')'.")
                    exit 5
                }
                Write-Output 'OPTIONAL GIT PROBE PASS'
                exit 0
                """);

            using var process = new Process { StartInfo = startInfo };
            Assert.True(process.Start(), "Optional Git harness did not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            return (process.ExitCode, await outputTask, await errorTask);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* best-effort fixture cleanup */ }
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunGitProvenanceHarnessAsync(
        string relativeScript, string mode, bool statusFailure)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "PiPlayGitProvenance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            if (statusFailure)
            {
                await File.WriteAllTextAsync(Path.Combine(tempRoot, "git.cmd"), """
                    @echo off
                    for %%A in (%*) do if /I "%%~A"=="rev-parse" (
                      echo aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                      exit /b 0
                    )
                    for %%A in (%*) do if /I "%%~A"=="status" exit /b 23
                    exit /b 29
                    """);
            }

            var powerShellPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell", "7", "pwsh.exe");
            Assert.True(File.Exists(powerShellPath), $"PowerShell 7 was not found at {powerShellPath}.");
            var startInfo = new ProcessStartInfo
            {
                FileName = powerShellPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["PATH"] = statusFailure
                ? tempRoot + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                : tempRoot;
            startInfo.Environment["PIPLAY_TEST_PROVENANCE_SCRIPT"] = Path.Combine(RepoRoot,
                relativeScript.Replace('/', Path.DirectorySeparatorChar));
            startInfo.Environment["PIPLAY_TEST_NATIVE_SCRIPT"] = Path.Combine(RepoRoot, "scripts", "NativeCommand.ps1");
            startInfo.Environment["PIPLAY_TEST_PROVENANCE_MODE"] = mode;
            startInfo.Environment["PIPLAY_TEST_PROVENANCE_ROOT"] = tempRoot;
            startInfo.Environment["PIPLAY_TEST_EXPECTED_ERROR"] = statusFailure
                ? "Required git status query failed"
                : "Git is required to query source dirty state";
            startInfo.Environment["PIPLAY_TEST_SUCCESS_MARKER"] = statusFailure
                ? "REQUIRED STATUS FAILURE REJECTED"
                : "MISSING GIT REJECTED";
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("""
                $tokens = $null
                $errors = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                    $env:PIPLAY_TEST_PROVENANCE_SCRIPT, [ref]$tokens, [ref]$errors)
                if ($errors.Count -gt 0) { throw ($errors | Out-String) }
                . $env:PIPLAY_TEST_NATIVE_SCRIPT
                function Import-Function([string]$Name, [switch]$Optional) {
                    $functionAst = $ast.Find({
                        param($node)
                        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $node.Name -eq $Name
                    }, $true)
                    if (-not $functionAst) {
                        if ($Optional) { return }
                        throw "Function '$Name' was not found."
                    }
                    $parameterText = @($functionAst.Parameters | ForEach-Object { $_.Extent.Text }) -join ','
                    $bodyText = $functionAst.Body.Extent.Text
                    $bodyText = $bodyText.Substring(1, $bodyText.Length - 2)
                    $functionText = if ($parameterText) {
                        "param($parameterText)`n$bodyText"
                    } else { $bodyText }
                    Set-Item -Path ("Function:script:{0}" -f $Name) `
                        -Value ([scriptblock]::Create($functionText))
                }

                if ($env:PIPLAY_TEST_PROVENANCE_MODE -eq 'Build') {
                    Import-Function 'Get-SourceCommit'
                    Import-Function 'Get-SourceDirtyEntries'
                    if ($env:PIPLAY_TEST_EXPECTED_ERROR -like 'Required*') {
                        $commit = Get-SourceCommit -RepositoryRoot $env:PIPLAY_TEST_PROVENANCE_ROOT
                        if ($commit -ne ('a' * 40)) { throw "rev-parse did not succeed first: '$commit'" }
                    }
                    $action = { Get-SourceDirtyEntries -RepositoryRoot $env:PIPLAY_TEST_PROVENANCE_ROOT }
                } else {
                    Import-Function 'Invoke-Git'
                    Import-Function 'Invoke-GitRequired' -Optional
                    Import-Function 'Get-GitDirtyEntries'
                    $script:repoRoot = $env:PIPLAY_TEST_PROVENANCE_ROOT
                    if ($env:PIPLAY_TEST_PROVENANCE_MODE -eq 'Verify' -and
                        $env:PIPLAY_TEST_EXPECTED_ERROR -like 'Required*') {
                        $commit = Invoke-Git @('rev-parse', 'HEAD')
                        if ($commit -ne ('a' * 40)) { throw "rev-parse did not succeed first: '$commit'" }
                    }
                    $action = { Get-GitDirtyEntries }
                }

                try {
                    & $action | Out-Null
                    [Console]::Error.WriteLine('Required git status failure was treated as clean.')
                    exit 9
                } catch {
                    $message = $_.Exception.Message
                    if ($message -notlike "*$($env:PIPLAY_TEST_EXPECTED_ERROR)*") {
                        [Console]::Error.WriteLine("Unexpected error: $message")
                        exit 8
                    }
                    Write-Output $env:PIPLAY_TEST_SUCCESS_MARKER
                    exit 0
                }
                """);

            using var process = new Process { StartInfo = startInfo };
            Assert.True(process.Start(), "Git provenance harness did not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            return (process.ExitCode, await outputTask, await errorTask);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* best-effort fixture cleanup */ }
        }
    }

    private sealed class DeskCandidateFixture : IAsyncDisposable
    {
        private const string EvidenceReason =
            "GitHub Actions desk candidate; final interactive verification pending on SND-DESK";

        private DeskCandidateFixture(string root)
        {
            Root = root;
            EvidenceRoot = Path.Combine(root, "evidence");
        }

        public string Root { get; }
        public string EvidenceRoot { get; }

        public static async Task<DeskCandidateFixture> CreateAsync(string? mutation = null)
        {
            var root = Path.Combine(Path.GetTempPath(), "PiPlayDeskCandidate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "scripts"));
            var fixture = new DeskCandidateFixture(root);

            var sourceExe = FindVersionedExecutable();
            File.Copy(sourceExe, Path.Combine(root, "PiPlay.exe"));
            File.Copy(Path.Combine(RepoRoot, "scripts", "StableDeployRoot.ps1"),
                Path.Combine(root, "scripts", "StableDeployRoot.ps1"));
            File.Copy(Path.Combine(RepoRoot, "scripts", "Test-UiSmoke.ps1"),
                Path.Combine(root, "scripts", "Test-UiSmoke.ps1"));

            var fvi = FileVersionInfo.GetVersionInfo(sourceExe);
            var versionMatch = Regex.Match(fvi.FileVersion ?? string.Empty,
                @"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)\.(?<build>\d+)$",
                RegexOptions.CultureInvariant);
            Assert.True(versionMatch.Success, $"Fixture executable has unusable FileVersion '{fvi.FileVersion}'.");
            Assert.False(string.IsNullOrWhiteSpace(fvi.ProductVersion));

            await File.WriteAllTextAsync(Path.Combine(root, "Dependency.dll"), "fixture dependency");
            if (mutation == "unlisted-dll")
                await File.WriteAllTextAsync(Path.Combine(root, "Unlisted.dll"), "unlisted dll");
            else if (mutation == "unlisted-exe")
                await File.WriteAllTextAsync(Path.Combine(root, "Unlisted.exe"), "unlisted exe");
            else if (mutation == "unlisted-version-table")
                await File.WriteAllTextAsync(Path.Combine(root, "VERSION_TABLE.json"), "{}");
            else if (mutation == "listed-additional-published-exe")
                await File.WriteAllTextAsync(Path.Combine(root, "Helper.exe"), "listed helper exe");

            var hasNestedManifests = mutation is "listed-nested-manifests" or "unlisted-nested-manifests";
            if (hasNestedManifests)
            {
                var nestedRoot = Path.Combine(root, "nested");
                Directory.CreateDirectory(nestedRoot);
                await File.WriteAllTextAsync(Path.Combine(nestedRoot, "build-info.json"), "nested primary");
                await File.WriteAllTextAsync(Path.Combine(nestedRoot, "BUILDINFO.json"), "nested legacy");
            }
            if (mutation == "published-artifacts-nested")
            {
                var nestedRoot = Path.Combine(root, "nested");
                Directory.CreateDirectory(nestedRoot);
                await File.WriteAllTextAsync(Path.Combine(nestedRoot, "Ghost.exe"), "nested published claim");
            }

            var artifactPaths = new List<string>
            {
                "PiPlay.exe", "Dependency.dll",
                "scripts/StableDeployRoot.ps1", "scripts/Test-UiSmoke.ps1"
            };
            if (mutation == "removed-exe-entry")
                artifactPaths.Remove("PiPlay.exe");
            else if (mutation == "removed-dll-entry")
                artifactPaths.Remove("Dependency.dll");
            if (mutation == "listed-nested-manifests")
            {
                artifactPaths.Add("nested/build-info.json");
                artifactPaths.Add("nested/BUILDINFO.json");
            }
            else if (mutation == "published-artifacts-nested")
                artifactPaths.Add("nested/Ghost.exe");
            else if (mutation == "listed-additional-published-exe")
                artifactPaths.Add("Helper.exe");

            Dictionary<string, object?> NewArtifact(string relativePath)
            {
                var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                return new Dictionary<string, object?>
                {
                    ["path"] = relativePath,
                    ["size"] = new FileInfo(fullPath).Length,
                    ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath)))
                };
            }

            var artifacts = artifactPaths.Select(NewArtifact).ToList();
            if (mutation == "duplicate-exe-entry")
            {
                var duplicate = NewArtifact("PiPlay.exe");
                duplicate["path"] = @".\PiPlay.exe";
                artifacts.Add(duplicate);
            }

            var exePath = Path.Combine(root, "PiPlay.exe");
            var exeSize = new FileInfo(exePath).Length;
            var exeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exePath)));

            var manifest = new Dictionary<string, object?>
            {
                ["project"] = "PiPlay",
                ["version"] = $"{versionMatch.Groups["major"].Value}.{versionMatch.Groups["minor"].Value}.{versionMatch.Groups["patch"].Value}",
                ["buildNumber"] = int.Parse(versionMatch.Groups["build"].Value),
                ["publishLabel"] = "desk-candidate-test",
                ["channel"] = "Stable",
                ["configuration"] = "Release",
                ["sourceCommit"] = new string('a', 40),
                ["publishedArtifacts"] = new[] { "PiPlay.exe" },
                ["primaryArtifact"] = "PiPlay.exe",
                ["sha256"] = exeSha256,
                ["size"] = exeSize,
                ["artifactCount"] = artifacts.Count,
                ["artifactHashes"] = artifacts,
                ["releaseEvidence"] = false,
                ["releaseEvidenceReason"] = EvidenceReason,
                ["sourceDirty"] = false,
                ["sourceDirtyEntries"] = Array.Empty<string>(),
                ["fileVersion"] = fvi.FileVersion,
                ["productVersion"] = fvi.ProductVersion,
                ["builtUtc"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            if (mutation == "release-evidence-true")
                manifest["releaseEvidence"] = true;
            else if (mutation == "wrong-evidence-reason")
                manifest["releaseEvidenceReason"] = "interactive verification pending";
            else if (mutation == "missing-published-artifacts")
                manifest.Remove("publishedArtifacts");
            else if (mutation == "published-artifacts-missing-exe")
                manifest["publishedArtifacts"] = new[] { "Dependency.dll" };
            else if (mutation == "published-artifacts-extra-ghost")
                manifest["publishedArtifacts"] = new[] { "PiPlay.exe", "Ghost.exe" };
            else if (mutation == "published-artifacts-traversal")
                manifest["publishedArtifacts"] = new[] { "PiPlay.exe", "../Ghost.exe" };
            else if (mutation == "published-artifacts-nested")
                manifest["publishedArtifacts"] = new[] { "PiPlay.exe", "nested/Ghost.exe" };
            else if (mutation == "published-artifacts-duplicate")
                manifest["publishedArtifacts"] = new[] { "PiPlay.exe", "PiPlay.exe" };
            else if (mutation == "published-artifacts-case-alias")
                manifest["publishedArtifacts"] = new[] { "PiPlay.exe", "PIPLAY.EXE" };
            else if (mutation == "listed-additional-published-exe")
                manifest["publishedArtifacts"] = new[] { "PiPlay.exe", "Helper.exe" };
            else if (mutation == "missing-primary-sha")
                manifest.Remove("sha256");
            else if (mutation == "missing-primary-size")
                manifest.Remove("size");
            else if (mutation == "wrong-primary-sha")
                manifest["sha256"] = new string('0', 64);
            else if (mutation == "wrong-primary-size")
                manifest["size"] = exeSize + 1;

            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(root, "build-info.json"), json);
            await File.WriteAllTextAsync(Path.Combine(root, "BUILDINFO.json"), json);

            if (mutation == "tampered-artifact")
                await File.AppendAllTextAsync(Path.Combine(root, "scripts", "Test-UiSmoke.ps1"), "# tampered\n");

            return fixture;
        }

        public string[] SnapshotPayload() =>
            Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
                .Select(path =>
                {
                    var relative = Path.GetRelativePath(Root, path).Replace('\\', '/');
                    var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
                    return relative + ":" + hash;
                })
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        public async Task<(int ExitCode, string Output, string Error)> ValidateAsync(
            string? exePath = null, params string[] additionalArguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["PIPLAY_UI_EVIDENCE_ROOT"] = EvidenceRoot;
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(Path.Combine(Root, "scripts", "Test-UiSmoke.ps1"));
            startInfo.ArgumentList.Add("-Mode");
            startInfo.ArgumentList.Add("DeskCandidate");
            startInfo.ArgumentList.Add("-ValidateOnly");
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                startInfo.ArgumentList.Add("-ExePath");
                startInfo.ArgumentList.Add(exePath);
            }
            foreach (var argument in additionalArguments)
                startInfo.ArgumentList.Add(argument);

            using var process = new Process { StartInfo = startInfo };
            Assert.True(process.Start(), "Candidate validation process did not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            return (process.ExitCode, await outputTask, await errorTask);
        }

        public ValueTask DisposeAsync()
        {
            try { Directory.Delete(Root, recursive: true); } catch { /* best-effort fixture cleanup */ }
            return ValueTask.CompletedTask;
        }

        private static string FindVersionedExecutable()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "PowerShell", "7", "pwsh.exe"),
                Environment.ProcessPath,
                Process.GetCurrentProcess().MainModule?.FileName
            };
            foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
            {
                if (!File.Exists(candidate)) continue;
                var info = FileVersionInfo.GetVersionInfo(candidate);
                if (Regex.IsMatch(info.FileVersion ?? string.Empty, @"^\d+\.\d+\.\d+\.\d+$") &&
                    !string.IsNullOrWhiteSpace(info.ProductVersion))
                    return candidate;
            }

            throw new InvalidOperationException("Could not locate an executable with four-part FileVersion metadata.");
        }
    }
}

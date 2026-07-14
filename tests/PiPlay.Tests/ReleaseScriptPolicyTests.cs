using System.IO;

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
        Assert.Contains("Tag preflight (before tests, build, or deploy)", publish);
        Assert.Contains("already exists at $existingTagCommit, but HEAD is $headCommit", publish);

        var preflight = publish.IndexOf("Tag preflight (before tests, build, or deploy)", StringComparison.Ordinal);
        var testGate = publish.IndexOf("Running deterministic test lane (gate)", StringComparison.Ordinal);
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

        // Two publishes at once would interleave on bin\publish, on the deploy root mid-swap, and on
        // tag creation. Both the repo and the deploy root are locked, and the lock is taken before
        // any expensive or destructive step.
        Assert.Contains("function New-PublishLock", publish);
        Assert.Contains("$script:repoLock = New-PublishLock", publish);
        Assert.Contains("$script:deployLock = New-PublishLock", publish);
        Assert.Contains("Another PiPlay publish is already running against", publish);
        Assert.Contains("System.Threading.AbandonedMutexException", publish);   // a crashed publish leaves no stale lock

        var lockTaken = publish.IndexOf("$script:repoLock = New-PublishLock", StringComparison.Ordinal);
        var build = publish.IndexOf("Building + publishing the Stable channel Release", StringComparison.Ordinal);
        Assert.True(lockTaken >= 0 && lockTaken < build, "The publish lock must be taken before the build.");
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

    [Theory]
    [InlineData("scripts/Build-PiPlay.ps1")]
    [InlineData("scripts/Publish-Stable.ps1")]
    [InlineData("scripts/Verify-StableDeploy.ps1")]
    [InlineData("scripts/Preflight-SpecGate.ps1")]
    public void Git_helpers_use_shared_native_command_wrapper(string relativePath)
    {
        var script = Script(relativePath);

        Assert.Contains(". (Join-Path $PSScriptRoot \"NativeCommand.ps1\")", script);
        Assert.Contains("Invoke-NativeCommandQuiet", script);
        Assert.DoesNotContain("$previousErrorActionPreference = $ErrorActionPreference", script);
    }
}

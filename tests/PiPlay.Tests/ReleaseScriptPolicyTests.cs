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
}

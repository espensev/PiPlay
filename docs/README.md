# PiPlay — Documentation

PiPlay is a Windows desktop utility for watching YouTube in a movable, resizable
**Video Popout** window. It gives browser-style picture-in-picture behavior a
native PiPlay window, so move, resize, pin, fade, and monitor restore behave like
a real desktop tool.

**Status:** Beta candidate (v0.4.0-beta) | **Platform:** Windows | **Stack:** WPF on .NET 10 | Microsoft Edge WebView2 Evergreen Runtime

## Documents

| Doc | What it covers |
|---|---|
| [PiPlay_Product_Engineering_Spec.md](PiPlay_Product_Engineering_Spec.md) | The product & engineering spec - the source of truth. |
| [AGENTS.md](AGENTS.md) | Contributor guide and repository working rules. |
| [Feature_Workflow.md](Feature_Workflow.md) | How to add features, write change notes, run gates, and open PRs. |
| [CHANGELOG.md](CHANGELOG.md) | Product/app release notes. |
| [SPEC_GAPS_AND_OWNERSHIP.md](SPEC_GAPS_AND_OWNERSHIP.md) | Missing/unclear spec items and ownership boundaries to resolve. |
| [adr/](adr/) | Architecture Decision Records - why the big choices were made. |
| [reviews/](reviews/) | Completed review artifacts and provenance; not current source-of-truth docs. |
| [QA_Checklist.md](QA_Checklist.md) | Re-runnable manual release test checklist. |
| [YouTube_Compliance.md](YouTube_Compliance.md) | What PiPlay does and does not do with YouTube. |
| [Data_and_Privacy_Map.md](Data_and_Privacy_Map.md) | Every file PiPlay writes, where, and how to reset it. |

## Active Docs Vs Records

Active product/source docs are this index, the product spec, changelog, QA checklist, ADRs,
compliance policy, privacy map, feature workflow, and spec gaps/ownership notes.

Dated specs, plans, and worklogs under `docs/superpowers/` are immutable change-pass records.
Completed review artifacts live under `docs/reviews/`. Raw screenshots and release evidence live
under `docs/evidence/`; discovery folders are evidence, not current-state contracts.

## Requirements

- Windows 10/11 (x64).
- Microsoft Edge **WebView2 Evergreen Runtime** - PiPlay shows a friendly install prompt if it is missing.
- .NET 10 desktop runtime, unless you publish self-contained.

## Build & run

```powershell
# from the repo root (..\PiPlay)
dotnet build PiPlay.sln -c Debug
dotnet run --project src\PiPlay\PiPlay.csproj
```

> Repo builds (`dotnet run`, `bin\publish\...`) are for the **automated dev loop only**. All
> manual/human testing uses the deployed Stable copy at `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`
> — see *Stable publish* below, the root `CLAUDE.md`, and `AGENTS.md` Conventions.

Deterministic local gate, matching CI:

```powershell
dotnet test PiPlay.sln --configuration Debug
.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
```

Release pipeline:

```powershell
# Compile only, without changing VERSION or BUILD_NUMBER
.\Build-PiPlay.ps1 -Stage Build -NoVersionBump

# Versioned release publish, archive, build-info.json, and VERSION_TABLE.json
.\Build-PiPlay.ps1 -Stage Release -Version patch

# Validate release metadata and SHA256 hashes after a publish exists
.\scripts\Test-PublishMetadata.ps1
```

The pipeline uses `VERSION` as the semantic version source and `BUILD_NUMBER` as the monotonic build counter. Release outputs go under `bin\publish\<label>`, with `bin\publish\latest`, `bin\publish\archive`, `build-info.json`, `BUILDINFO.json`, and `VERSION_TABLE.json`.

Stable publish (a differentiable, runnable copy deployed for side-by-side test use):

```powershell
# Test-gate, build the Stable channel, validate metadata, and deploy a side-by-side copy.
# Bumps the patch version by default; pass -Version minor|major|<semver> or -NoVersionBump.
.\scripts\Publish-Stable.ps1

# If VERSION/BUILD_NUMBER were already committed for an exact source identity:
.\scripts\Publish-Stable.ps1 -NoVersionBump -NoBuildNumberBump

# Verify the deployed copy before testing it: re-hashes every artifact and prints the exact
# deployed version/build/commit, including how far the deploy lags HEAD.
.\scripts\Verify-StableDeploy.ps1
```

Normal publishes bump `VERSION`/`BUILD_NUMBER` but never commit them; after a deploy, commit the
bumped stamps (with the CHANGELOG entry) and tag the source commit `stable-vX.Y.Z-bN`. For an exact
current-HEAD deploy, pre-commit the stamps and publish with `-NoVersionBump -NoBuildNumberBump`.
When to move minor/major vs patch vs no-bump: [Feature_Workflow.md](Feature_Workflow.md).

A Stable build is **differentiable** from the dev app: its data lives beside the exe (`PiPlayData`, isolated from your dev profile), it has its own single-instance identity (so dev + stable run at once, each single-instance), and its title bar reads `PiPlay — Stable vX.Y.Z (bN)`. The channel is baked into the binary. See [adr/0007-stable-channel-and-portable-data.md](adr/0007-stable-channel-and-portable-data.md) and [Feature_Workflow.md](Feature_Workflow.md).

Signing is intentionally not part of the local pipeline yet. Treat unsigned outputs as local/internal builds until signing is added for distribution.

Per-monitor DPI awareness (PerMonitorV2) belongs in `src\PiPlay\app.manifest`.

## Quickstart

1. Launch PiPlay and open a YouTube video.
2. Click **Pop out video** - the video moves into a floating Popout Player.
3. Move, resize, **Pin** (always-on-top), or use **Fade** controls while you work.
4. Close the player to return playback to the main window.

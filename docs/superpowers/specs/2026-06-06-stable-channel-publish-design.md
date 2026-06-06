# Stable channel + differentiable stable publish — design

**Date:** 2026-06-06
**Status:** Implemented and verified.

## Goal
Publish a *stable* PiPlay copy to `E:\Dev_test_implemenations\PiPlay` that runs side by side with the
dev/installed app and is **differentiable** from it, without changing the normal app's behavior. Also
harden the current state ("make it stable") by fixing the concrete defects found in a pre-publish sweep.

## Requirements served
- **REQ-APP-01** (single-instance) — preserved, now scoped per channel.
- **REQ-WINDOW-01 / spec 16.4** (maximized-state preservation) — second-instance fix.
- **Spec 17** (broken URLs fail gracefully) — `ParseTime` overflow fix.
- **Spec 9.x** release/publish posture — `Publish-Stable.ps1` + pipeline hardening.

## Settled decisions (see ADR-0007)
- **Channel baked into the binary** (`PiPlayChannel` MSBuild property → `[AssemblyMetadata("PiPlay.Channel", …)]`),
  read at runtime by `AppChannel`; `PIPLAY_CHANNEL` is a test/diagnostic override. *(User choice: baked, not a marker.)*
- **Portable data beside the exe** for the Stable channel (`<exeDir>\PiPlayData`); Default unchanged
  (`%LOCALAPPDATA%\PiPlay`); `PIPLAY_DATA_ROOT` still wins. *(User choice: beside-exe, not a LOCALAPPDATA subfolder.)*
- **Per-channel single-instance identity**: Default keeps `…SingleInstance.v1`; Stable gets `…SingleInstance.Stable`.
- **Title** surfaces non-default channels (`PiPlay — Stable vX.Y.Z (bN)`); Default stays `"PiPlay"` (XAML title unchanged; set at runtime).

## Stability sweep (applied fixes)
A multi-agent, adversarially-verified review of `src/PiPlay` + the build pipeline surfaced **4 concrete,
verified defects** (all fixed; no speculative churn — a green 148-test suite was the regression oracle):
1. Second-instance activation forced `Normal`, dropping a maximized layout → `SystemCommands.RestoreWindow`.
2. `YouTubeUrlHelper.ParseTime` threw/overflowed on out-of-range `t=`/`start=` → `int.TryParse` + checked
   long math, degrade to "no offset".
3. `Build-PiPlay.ps1` pruned by lexical folder name → could delete the just-built publish → prune by
   recency + never delete the current label.
4. `Build-PiPlay.ps1` rolled back `VERSION`/`BUILD_NUMBER` after a successful publish → gate on
   `$artifactProduced`; clean partial folders only on a pre-publish failure.

## Testing approach
- Pure, parameterized resolvers unit-tested: `AppPaths.ResolveRoot`, `AppChannel.Parse/Resolve`,
  `AppInfo.FormatTitle`; plus a `YouTubeUrlHelper` overflow regression and a `TitleText` markup invariant.
- Full `dotnet test` lane: **148 → 173 green**.
- End-to-end: build the Stable channel, confirm `[AssemblyMetadata("PiPlay.Channel","Stable")]` is baked,
  deploy, and **launch** the deployed exe to confirm the Stable title + an isolated `PiPlayData` folder.

## Files
- **New:** `Services/AppChannel.cs`, `Services/AppInfo.cs`, `scripts/Publish-Stable.ps1`,
  `adr/0007-stable-channel-and-portable-data.md`, this note, and new test files
  (`AppChannelTests.cs`, `AppInfoTests.cs`) + cases in `AppPathsTests.cs` / `YouTubeUrlHelperTests.cs`.
- **Edited:** `Services/AppPaths.cs`, `App.xaml.cs`, `MainWindow.xaml(.cs)`, `PiPlay.csproj`,
  `Services/YouTubeUrlHelper.cs`, `scripts/Build-PiPlay.ps1` (channel + 2 hardening fixes),
  `Ui/XamlInvariantTests.cs`, and docs (README ×2, Feature_Workflow, CHANGELOG, Data_and_Privacy_Map, SPEC_GAPS).

## Result
Implemented and verified on 2026-06-06. `dotnet test` = **173 passing**. The Stable channel deploys a
runnable, differentiable copy to `E:\Dev_test_implemenations\PiPlay`, preserving its `PiPlayData` runtime
folder across redeploys.

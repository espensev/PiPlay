# Session worklog — stable channel publish + stability sweep (2026-06-06)

Saved record of the working session that produced PR #5 and the `stable-v0.3.0-b8` deploy.
(The full Claude Code transcript is also auto-persisted under the session directory; this is the
human-readable summary.)

## Request
> "Review the recent changes, then change code/harness/workflows etc. to allow for a stable copy to be
> published and differentiable in `E:\Dev_test_implemenations`. First do a sweep to make this 'stable'
> with all the fixes you see, then build and publish via our flow, then copy/deploy to that folder.
> Update all the docs/flows as needed." (ultracode session)
>
> Mid-session clarifications: keep data/logs where they are now (`%LOCALAPPDATA%\PiPlay`) for the normal
> app, or put them in the app dir? Make "two versions"? Keep the single-instance mutex.

## What was reviewed
- Recent work: Phase-2 profile edit/validation, the CI gate (`.github/workflows/ci.yml`), and pending
  doc housekeeping (removed duplicate `docs/piplay.ico` + an unlinked brand HTML snippet).
- Architecture: `App.xaml.cs` (hardcoded mutex `Local\PiPlay.SingleInstance.v1` + named pipe),
  `AppPaths` (`%LOCALAPPDATA%\PiPlay`, `PIPLAY_DATA_ROOT` override), `Build-PiPlay.ps1` pipeline,
  and the sibling **AppZone** convention on `E:` (runnable copy with WebView2 data beside the exe).

## Decisions (user choices)
- **Channel baked into the build** (not a deploy-time marker): `PiPlayChannel` MSBuild property →
  `[AssemblyMetadata("PiPlay.Channel", …)]`, read at runtime by `AppChannel`.
- **Portable data beside the exe** for the Stable channel (`<exeDir>\PiPlayData`), not a
  `%LOCALAPPDATA%\PiPlay-Stable` subfolder. Default channel unchanged.
- Single-instance kept, scoped **per channel**: Default keeps `…SingleInstance.v1`; Stable gets its own.
- Recorded in `adr/0007-stable-channel-and-portable-data.md`.

## Stability sweep (multi-agent, adversarially verified → 4 fixed)
1. `MainWindow.ActivateFromSecondInstance` forced `WindowState.Normal`, dropping a maximized layout →
   `SystemCommands.RestoreWindow` (REQ-WINDOW-01).
2. `YouTubeUrlHelper.ParseTime` threw/overflowed on out-of-range `t=`/`start=` → `int.TryParse` + checked
   `long` math; degrade to "no offset" (spec 17).
3. `Build-PiPlay.ps1` pruned by lexical folder name → could delete the just-built publish → prune by
   recency + never delete the current label.
4. `Build-PiPlay.ps1` rolled back `VERSION`/`BUILD_NUMBER` after a successful publish → gate on
   `$artifactProduced`; clean partial folders only on a pre-publish failure.

## Implementation
- New: `Services/AppChannel.cs`, `Services/AppInfo.cs`, `scripts/Publish-Stable.ps1`, ADR-0007,
  design note, worklog (this file), tests `AppChannelTests.cs` / `AppInfoTests.cs`.
- Edited: `AppPaths.cs` (pure `ResolveRoot`), `App.xaml.cs` (channel-scoped mutex/pipe),
  `MainWindow.xaml(.cs)` (`TitleText` + runtime channel title), `PiPlay.csproj` (channel property +
  `AssemblyMetadata`), `YouTubeUrlHelper.cs`, `Build-PiPlay.ps1` (`-Channel` + 2 fixes), tests, docs.

## Verification
- `dotnet test PiPlay.sln -c Debug` → **173 passing** (was 148).
- Channel baking confirmed: Stable build emits `[AssemblyMetadata("PiPlay.Channel","Stable")]`,
  FileVersion `0.3.0.8`.
- Deployed **v0.3.0 b8** to `E:\Dev_test_implemenations\PiPlay` (SHA256 `97285A06…`, sourceCommit
  `7fc66c4`); metadata validated.
- Launched the deployed exe: own window, title `PiPlay — Stable v0.3.0 (b8)`, isolated
  `PiPlayData\logs\piplay.log`.
- **Coexistence:** Default (`PiPlay`) + Stable (`PiPlay — Stable v0.3.0 (b8)`) alive simultaneously,
  distinct titles, each single-instance.

## Disposition
- Branch `feature/stable-channel-publish` pushed; **PR #5** opened to `main`
  (https://github.com/espensev/PiPlay/pull/5). CI "Build and test (Windows)" green on the branch HEAD.
- Annotated tag `stable-v0.3.0-b8` on the deployed commit `7fc66c4`, pushed.

## Commits
- `980c215` docs: finalize icon/brand housekeeping
- `5a4fe83` feat(release): stable channel publish + stability sweep
- `7fc66c4` fix(publish): hashtable splat  ← deployed sourceCommit / tagged
- `50c61bf` release: stable v0.3.0 build 8
- `e07ea1c` docs: "behaviorally unchanged" wording

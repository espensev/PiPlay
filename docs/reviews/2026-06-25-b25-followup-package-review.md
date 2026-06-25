# b25 follow-up package review - 2026-06-25

## Scope

Reviewed local `main` against `origin/main` after the stabilization commits:

- `cf73823` - dependency audit docs
- `068d970` - product spec alignment
- `fbe77c9` - P4 bring-back implementation and P3 opacity wording
- `4841842` - stabilization status docs

This was a static review focused on behavioral regressions in the return path and whether the docs now
overstate the implementation status. The previously recorded validation for `fbe77c9` was not rerun in
this review pass.

## Finding

### P2 - Returned-video navigation drops the captured playback state

When the popout ends on a different video, `ReturnPolicy.Decide(...)` returns `Navigate` before the
paused-state decision can matter. `MainWindow.ApplyReturnActionAsync(...)` then explicitly skips
`ApplyPlaybackSettingsAsync(...)` for `ReturnAction.Navigate` and only calls `NavigateInternal(...)` with
the returned video/timestamp.

That means this path correctly avoids seeking the original video, but it does not preserve the popout's
captured paused state, volume, mute, or playback rate after the source navigates to the returned video.
If the user pauses or changes audio/speed in the popout after moving to a recommendation/playlist item,
the source window falls back to YouTube's default behavior for that fresh navigation.

Evidence:

- `src/PiPlay/Services/ReturnPolicy.cs`: differing video ids return `ReturnAction.Navigate`
  unconditionally.
- `src/PiPlay/MainWindow.xaml.cs`: playback settings are applied only when the action is not
  `ReturnAction.Navigate`.
- `src/PiPlay/MainWindow.xaml.cs`: the navigate branch builds the returned watch URL but has no
  post-navigation replay for paused/volume/mute/rate.
- `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`: the different-video test asserts queued navigation and
  auto de-dup only; it does not assert any pending playback-state replay.

Impact:

- Same-video return is materially improved by `fbe77c9`.
- Different-video return still fails the full P4 acceptance wording: "timestamp, play/pause, volume,
  mute, and speed preserved."

Recommended fix:

- Add a small pending-return-state replay for `ReturnAction.Navigate`.
- After the source WebView navigates to the returned video and a video element is available, apply
  volume/mute/rate and then pause or play according to `PlayerReturnState.Paused`.
- Add tests around the policy/state handoff and a runtime seam that proves a different-video return
  queues both the target URL and the pending playback-state replay.

## QA Readiness Pass

2026-06-25 automated pre-commit QA was run at `011e8ee` before this docs-only evidence update:

- `dotnet clean PiPlay.sln --configuration Debug --nologo` passed with 0 warnings and 0 errors.
- `dotnet test PiPlay.sln --configuration Debug --nologo` passed: 683/683 tests.
- `./Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` passed: Release build, 0
  warnings, 0 errors, version `0.7.2`, build `25` unchanged.
- `git diff --check` passed with no output.
- `git status --short --branch` showed a clean tracked tree; `git ls-files --others
  --exclude-standard` returned no untracked commit candidates.

Manual Stable smoke was not run in this pass. Per `docs/AGENTS.md` and `tests/README.md`, that lane
must run against the deployed Stable copy after an explicit Stable publish/verify step, not against repo
build output.

## Non-Findings

- The source-window button and placeholder copy now correctly flip to `Bring video back`.
- The explicit `CaptureReturnStateNowAsync()` path avoids relying only on the last timer tick for
  same-video return.
- Whole-popout opacity wording is now honest about the current layered-window implementation.

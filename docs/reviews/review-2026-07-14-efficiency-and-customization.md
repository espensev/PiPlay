# Review - Efficiency, Customization, and Test-Build Flow

**Date:** 2026-07-14
**Surface:** branch delta `origin/main...HEAD` at `a9bb197`, with current-HEAD runtime and release
paths read as regression context for the requested application-wide audit
**Spec source:** current user request; `docs/PiPlay_Product_Engineering_Spec.md` sections 17, 20,
21, and 22.4
**Standards sources:** `CLAUDE.md`; `docs/AGENTS.md`; `docs/Feature_Workflow.md`;
`docs/adr/0007-stable-channel-and-portable-data.md`
**Verdict:** FAIL at the review snapshot; application findings remediated below, release-pipeline
findings remain open

## Findings

### High

- [axis: regression] `src/PiPlay/PlayerWindow.xaml.cs:141` - The normal-mode timestamp poll runs
  every 250 ms through an `async void` tick with no in-flight guard, is not stopped when a new
  navigation starts, and starts after failed navigation because `Core_NavigationCompleted` marks
  every result complete (`:230-241`, `:308-322`, `:461-475`).
  Evidence: each tick crosses WebView2 IPC, executes JavaScript, parses JSON, and allocates a
  `PlayerState` (`Services/YouTubeDomBridge.cs:41-60`, `:89-101`). Compact is disabled, so every
  current Popout Player uses this lane (`Services/PlaybackModePolicy.cs:69-83`).
  Impact: four DOM reads per second are certain; a read taking longer than 250 ms can overlap the
  next read, and stale/out-of-order results can overwrite return state during navigation.
  Recommendation: add an in-flight gate and navigation generation, stop on navigation start, and
  start only after successful navigation.

- [axis: standards] `scripts/Publish-Stable.ps1:278` - Stable deployment deletes the existing
  payload and then copies the new payload in place, with no staged swap or rollback.
  Evidence: `:278-284` removes every non-data item before `Copy-Item` completes.
  Impact: an interrupted or failed copy leaves the only sanctioned manual-test installation broken.
  Recommendation: stage and verify a sibling deployment, then swap with backup/rollback.

- [axis: standards] `scripts/Build-PiPlay.ps1:426` - A clean unsigned build can still be recorded as
  `releaseEvidence=true`, although `docs/AGENTS.md:49` requires signed release binaries.
  Evidence: release evidence depends on source cleanliness/non-release reason (`:426-441`), while
  signing is optional metadata (`:490-501`); the current deployed executable reports `NotSigned`,
  and the documented `scripts/Sign-PiPlay.ps1` example points to no existing file.
  Impact: the pipeline can label an artifact release evidence without satisfying the provenance
  requirement.
  Recommendation: make valid Authenticode status a release-evidence gate while retaining unsigned
  diagnostic builds.

### Medium

- [axis: regression] `src/PiPlay/MainWindow.xaml.cs:334` - Auto reads player state before rejecting
  non-watch pages, missing video IDs, and the already-handled video (`:346-356`).
  Impact: whenever Auto is enabled, those skip states still pay four WebView script/IPC/JSON calls
  per second indefinitely.
  Recommendation: expose a pure preflight policy and return before `ReadPlayerStateAsync`.

- [axis: regression] `src/PiPlay/MainWindow.xaml.cs:629` - Every valid accent-picker mouse update
  immediately replaces 16 app resource entries and calls the full Popout appearance path
  (`:642-648`, `:688-693`). That path also reapplies fade/probe state, opacity, DWM corners, and the
  native border (`PlayerWindow.xaml.cs:608-624`, `:705-739`) although only color changed.
  Impact: raw pointer frequency fans into app-wide WPF invalidation and unrelated native work; it
  can also restart idle behavior during a color drag.
  Recommendation: coalesce preview updates, skip duplicates, and add a Popout accent-only method.

- [axis: regression] `src/PiPlay/Controls/AccentColorPicker.xaml.cs:257` - The invariant 148-DIP
  hue/saturation disc is regenerated on both `Loaded` and `SizeChanged` (`:39-40`). Each pass
  allocates `int[pixelSize * pixelSize]`, a new bitmap, and performs per-pixel square-root/atan2/HSV
  work (`:257-293`).
  Impact: the pixel array alone is 87,616 bytes at 100% DPI and 197,136 bytes at 150% DPI, so even
  the 100% case crosses the large-object-heap threshold; reopening Settings repeats the work.
  Recommendation: cache the frozen bitmap by pixel size and DPI.

- [axis: regression] `src/PiPlay/App.xaml.cs:59` - Startup loads settings for the theme and
  `MainWindow` immediately loads them again (`MainWindow.xaml.cs:53-58`). Each load reads the file,
  parses it once in `HasThemeBlock`, and parses/deserializes it again
  (`Services/SettingsService.cs:26-49`, `:210-217`).
  Impact: the UI startup thread performs two reads, two cleanup scans, and four JSON traversals
  before the first window is shown.
  Recommendation: pass the boot settings into `MainWindow` and deserialize from the already-parsed
  `JsonDocument` root.

- [axis: regression] `src/PiPlay/PlayerWindow.xaml.cs:182` - Closing is not represented while
  asynchronous WebView initialization or polling is in flight, and app shutdown lets
  `Player_OnClosed` restore source playback and save before `MainWindow_Closing` saves again
  (`MainWindow.xaml.cs:943-965`, `:1045-1057`).
  Impact: close during cold initialization can surface a spurious failure against a disposed
  control; app shutdown performs unnecessary WebView work and duplicate durable settings writes.
  Recommendation: add closing state to both windows, suppress post-await work, and let the outer
  close path own the single shutdown save.

- [axis: spec] `src/PiPlay/Theme/ThemeColors.cs:175` - Very dark custom accents are lifted to the
  3:1 presentation floor, then the pressed state is darkened and lifted back to the same floor.
  Evidence: representative `#101010` produces identical primary/pressed RGB in all three presets;
  other dark colors land at only 1.00-1.03:1 state contrast.
  Impact: the customization remains visible, but the pressed affordance can disappear.
  Recommendation: keep the normal darker pressed state when distinct; otherwise use a small lighter
  fallback that preserves the 3:1 surface floor and at least a modest visible state delta.

- [axis: regression] `scripts/Publish-Stable.ps1:323` - A conflicting stable tag is detected only
  after build, destructive deploy, and pre-tag verification. Current stamps concretely collide:
  `stable-v0.7.2-b25` points to `9e602ed`, while HEAD is `a9bb197`.
  Impact: an exact-source run can replace Stable successfully and then fail late at tag creation.
  Recommendation: preflight the expected tag/commit before tests, build, or deployment.

### Low

- [axis: regression] `src/PiPlay/Services/WindowOpacityApplier.cs:47` - The test-observation
  `BorderSuppression` dictionary records every HWND and is never reclaimed, while only the main
  opacity state dictionary is cleared on `WM_NCDESTROY` (`:232-237`). The footprint is small but
  unbounded across Settings, prompt, and Popout windows.

- [axis: regression] `src/PiPlay/Services/LoggingService.cs:57` - Every log entry performs a file
  metadata check and synchronous `AppendAllText` under a lock. Normal volume is low; repeated DOM
  failures would turn this into recurring UI-thread disk I/O.

## Customization Impact

- Full theme application populates 59 resource keys. The XAML has 138 direct `DynamicResource`
  references, so presets materially change palette, density, radii, and popup elevation, but only at
  startup or confirmed Settings apply; there is no steady-state theme loop.
- Accent-only application replaces 16 brush/color entries feeding 14 direct XAML accent references,
  plus the imperative Source/Popout Pin and Fade brushes. The global accent therefore matters across
  chrome, but it intentionally does not recolor YouTube content.
- A profile color is intentionally identity-only: one 4-DIP leading rail in the 150-DIP profile
  selector/list (`MainWindow.xaml:96-117`). It does not replace the global app accent.
- Raw global/profile `#RRGGBB` values remain exact. Presentation colors alone are raised to at least
  3:1 against `SurfaceHover`; the title-bar wash targets a deliberately subtle 1.20:1.
- An isolated in-process probe (no realized WPF consumers) measured approximately 23.4 microseconds
  for derivation, 31.1 microseconds for accent apply, and 41.1 microseconds for full theme apply.
  These are lower-bound microbenchmarks, not end-user frame timings; WPF resource invalidation at raw
  pointer frequency is the relevant cost.

## Build/Test State

- Current Stable target: `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`, v0.7.2 build 25 from
  `00df3e7`, one commit behind HEAD and already marked non-release/dirty.
- The repo has four pre-existing untracked files. The safe requested outcome is therefore a fresh
  `-AllowDirty` diagnostic Stable deployment, preserving `PiPlayData`, not a release candidate.
- A release-verified copy additionally needs branch integration, new version/build stamps, a clean
  committed tree, a non-colliding tag, and valid signing.

## Verification

- `dotnet test PiPlay.sln --configuration Debug --nologo` - pass, 690/690 (baseline).
- `git diff --check origin/main...HEAD` - pass.
- Isolated theme/accent timing probe - completed; figures recorded above.
- `scripts/Verify-StableDeploy.ps1` - fail as expected, 5 checks: non-release evidence, dirty source
  manifest, one-commit drift, tag mismatch, and current dirty worktree.

## Coverage Notes

- Branch-delta files reviewed deeply: all 23 files reported by `git diff --name-status
  origin/main...HEAD`.
- Current-snapshot paths reviewed deeply: application startup/settings, Source and Popout lifecycle,
  WebView DOM polling, theme/accent derivation and resource application, profile identity wiring,
  color picker rendering, Stable build/publish/verify scripts, and their direct tests.
- Sampled/excluded: static image/icon assets and historical archive material; live YouTube playback
  and visual owner QA were not run during this read-only review phase.

## Open Questions

- The active accent branch is intentionally separate from a newer local `main` stack. This pass does
  not merge/rebase that unrelated work.
- Atomic deployment, signing enforcement, pipeline locking, and branch/upstream release policy need a
  dedicated release-engineering pass; they are not required to create an explicitly diagnostic copy.

## Remediation Status

Implemented in the working tree:

- Popout DOM polling is single-flight, navigation-generation-bound, watch-URL-gated, success-gated,
  and inert after close.
- Auto performs URL/lifecycle preflight before the player-state script call.
- Accent preview is coalesced to about 30 applies/second, duplicate values are skipped, the final
  accepted value is flushed, and the Popout receives an accent-only update.
- The invariant hue/saturation bitmap is frozen and cached by physical size/DPI.
- Production startup reuses one settings snapshot and `SettingsService` deserializes from one parsed
  JSON document.
- Source/Popout shutdown suppresses stale async UI work and duplicate return/save work.
- Very dark accents retain exact stored RGB while presentation adds a contrast-tested pressed state.

Final verification:

- `dotnet test PiPlay.sln --configuration Debug --nologo` - pass, 707/707.
- `Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` - pass, zero warnings/errors.
- `scripts/Preflight-SpecGate.ps1` and `git diff --check` - pass.
- Diagnostic Stable label `20260714-023107-v0.7.2-b25-stable` deployed and re-verified at the
  required manual-test path; all 21 artifacts hash clean and the executable SHA256 is
  `F1A4647E1D6D460FDA06ED6A04EAFCB581A46F7E11EC8C2AB5AC91758CC8BD69`.

Still open: atomic/rollback deployment, signed release-evidence enforcement, early tag-collision
preflight, publish locking/unique handoff, branch/upstream release policy, and the two low-severity
observation/logging items. These do not invalidate the diagnostics-only test copy but keep the overall
release-pipeline review verdict from passing.

# b25 full review follow-up - triage and implementation plan

**Input review:** `docs/reviews/2026-06-25-v0.7.2-b25-full-review.md`

**Evaluation:** `docs/reviews/2026-06-25-v0.7.2-b25-full-review-evaluation.md`

**Goal:** turn the b25 full review into an actionable queue without undoing settled current-product
decisions or reopening the already-reviewed v0.7.2-b25 release patch.

## Stabilization

- Commit `fbe77c9` landed the safe local follow-up package: Task 3 P4 `Bring video back`, Task 4 P3
  whole-popout opacity wording, the full-review evaluation, and this follow-up plan.
- Validation passed before commit: `dotnet test PiPlay.sln --configuration Debug --nologo`
  (`683/683`) and `git diff --check` with only line-ending normalization warnings.
- Branch merge check after fetch: the local `fix/p1-webview-inset-and-prompt` branch is already
  merged into `main`; no remote branch remains unmerged into `main`.
- Static review after stabilization is recorded in
  `docs/reviews/2026-06-25-b25-followup-package-review.md`.
- Automated QA readiness at `011e8ee` is recorded in the same review artifact: clean, full tests
  683/683, non-mutating Release build gate, whitespace check, and clean tracked/untracked status.
- Additional thorough PiPlay review is recorded in
  `docs/reviews/2026-06-25-piplay-b25-code-review-thorough.md`.
- Spec-tightening arbitration review is recorded in
  `docs/reviews/2026-06-25-piplay-b25-spec-tightening-arbitration.md`.
- Meta-review/severity calibration is recorded in
  `docs/reviews/2026-06-25-piplay-b25-meta-review-of-arbitration.md`.
- Review/audit prep docket is recorded in
  `docs/reviews/2026-06-25-piplay-b25-review-audit-prep.md`.
- Imported `docs/reviews/2026-06-25-spec-review-v2.md` appears to be a TerminalHQ/THQ review, not
  PiPlay release evidence, unless the owner confirms otherwise.
- Remaining open items are intentionally deferred gates: deployed visual QA, profile-accent owner
  decision, rounded-corner language, and video fit modes.

## Plan

- [x] **Task 1 - Evaluate the full review against current HEAD.**
  - Confirmed current `HEAD` is the review's source label `068d970`.
  - Confirmed the review's major technical claims against source and docs.
  - Saved the disposition in `docs/reviews/2026-06-25-v0.7.2-b25-full-review-evaluation.md`.

- [ ] **Task 2 - Visual QA the b25 border outcome before more P1 code.**
  - Use the deployed Stable copy, not repo build output.
  - Check main and popout at 100, 125, and 150 percent DPI.
  - Acceptance: the 4 DIP black WebView band reads as letterbox/canvas, not a grey or double app frame.
  - If it still reads as a frame, decide between a WebView2/windowing lift and explicit Cinema Padding.
  - Do not reduce the WebView margin to 0 without preserving native edge resize.

- [x] **Task 3 - Implement P4 Bring video back.**
  - Add a real `BringVideoBackAsync()` path in `MainWindow`.
  - When no popout exists, primary action remains `Pop out video`.
  - When a popout exists, primary action becomes `Bring back` / `Bring video back`, not focus-only
    `Show popout`.
  - Add `PlayerWindow.CaptureReturnStateNowAsync()` or equivalent so the command does not rely on the
    last timer tick.
  - Extend `PlayerReturnState` and `YouTubeDomBridge.PlayerState` for at least paused, volume, muted,
    and playback rate.
  - Update `ApplyReturnActionAsync(...)` so return decisions use popout-side paused state when known,
    not only `_sourceWasPlayingAtPopout`.
  - Update tests that currently pin focus-only copy and behavior.

- [x] **Task 3b - Finish P4 playback-state replay after returned-video navigation.**
  - Implemented pending returned-video playback-state replay: `ReturnAction.Navigate` queues the returned
    video/timestamp and a replay payload for paused/volume/mute/rate, then Source applies it after the
    returned video element is available.
  - Added headless coverage that different-video return queues both the URL and pending replay state.
  - Still requires Stable/WebView2 manual smoke because YouTube readiness is runtime-dependent.

- [x] **Task 3c - Reconcile the new resume rule with normative REQ-RETURN-01.**
  - Surfaced by the independent multi-agent review (`docs/reviews/2026-06-25-codex-p4-bring-video-back-review.md`);
    NOT caught by the parallel package self-review in `011e8ee`.
  - Resolved as **Option A**: the popout's live paused/playing state wins when known; the source launch
    state is fallback only.
  - Product spec, requirements matrix, `SPEC_GAPS`, and QA checklist now state the same rule.
  - Launch-from-paused intent is protected in code: PiPlay only nudges the popout into playback when the
    source was playing at popout launch.

- [ ] **Task 3d - Make final P4 capture authoritative and close paths equivalent.**
  - Surfaced by `docs/reviews/2026-06-25-piplay-b25-code-review-thorough.md`.
  - Done: explicit `Bring video back` now stops the sync timer before final capture and guards in-flight
    timer completions so stale reads cannot overwrite the final capture.
  - Make popout close button/system close either perform the same fresh capture as Source Window
    `Bring video back`, or document/test that X-close is timer-sampled fallback. Preferred: fresh
    async guarded capture for X-close too.
  - Remaining: X-close still uses the last timer-sampled media state plus fresh window placement capture;
    decide whether to add an async close-deferral for fresh DOM capture on X-close.

- [ ] **Task 3e - Preserve full returned target context.**
  - Current return state carries only `VideoId`, so different-video return reconstructs a plain watch
    URL and can drop playlist/list/index context.
  - During final capture, also re-read canonical/current URL and parse it into a richer return target
    before close.
  - Use `VideoId` for equality decisions, but navigate with a full validated `YouTubeTarget` or
    canonical YouTube watch URL.

- [x] **Task 3f - Reset media sampling across popout retarget/navigation.**
  - Surfaced by `docs/reviews/2026-06-25-piplay-b25-spec-tightening-arbitration.md`.
  - On retarget and allowed navigation start, media state is unknowned, the sync timer stops, and DOM
    sampling resumes only after successful navigation completion.

- [x] **Task 3g - Add app-shutdown guard for popout close.**
  - Main-window shutdown now marks `_mainWindowClosing`, stops source suppression, closes the player, and
    lets popout placement/settings persist while skipping source playback return and placeholder restore.

- [x] **Task 3h - Strengthen source suppression while popout is active.**
  - Popout launch now suppresses source playback with mute+pause and a short reassertion guard while the
    popout owns playback.
  - QA now covers duplicate-audio suppression through ads, autoplay-next, and SPA transitions.
  - Still requires Stable/WebView2 smoke because the failure mode is site/runtime-specific.

- [x] **Task 3i - Decide launch-from-paused behavior.**
  - Source launch intent is passed into `PlayerWindow`; PiPlay only nudges play when the source was
    playing at popout launch.
  - If the user manually presses play in the popout after launching from paused, Option A returns playing.

- [ ] **Task 8 - Review cleanup cluster (low/nit; from the 2026-06-25 multi-agent review).**
  - Tests (WebView2 boundary, so partly runtime-QA): the `ReturnAction.Seek` → `SeekAndPauseAsync`
    mapping and `ApplyPlaybackSettingsAsync` are unit-untested; the four new `Return*ForTests`
    accessors are unused dead scaffolding (add a `ApplyReturnPlaybackStateForTests(PlayerState)` seam +
    round-trip assert); extract a pure `BuildPlaybackSettingsScript(...)`/`ParsePlayerState(...)` and
    test clamp + `ToString("R", InvariantCulture)` (locale-regression surface) + parse guards; add a
    5-arg `Decide` `InlineData` row exercising the same/unknown-id fallback with a flipping
    `returnedPaused`; add a `0`-timestamp row to the paused-override Theory; pin `PopOutButton`'s
    `Click` attr in `XamlInvariantTests` (asymmetric with the placeholder).
  - Docs: stale double-audio row, `AGENTS.md` opacity wording, and stale `10 DIP` QA wording are fixed.
    The 4-arg `ReturnPolicy.Decide` overload remains as a tested compatibility wrapper.
  - Runtime QA: the new "X-close now propagates the popout's volume/mute/rate to the source" path now has
    a plain X-close/Alt-F4 row in `QA_Checklist.md`; still needs real WebView2 smoke.
  - Missed by earlier review: save popout placement/settings independently of source return scripting so
    a return-script failure cannot skip persistence.

- [x] **Task 9 - Address-pass review residuals (2026-06-26, `docs/reviews/2026-06-26-piplay-b25-address-pass-review.md`).**
  - **Source-returns-muted regression (from the new suppression mute):** fixed. Added pure
    `ReturnPolicy.ResolveReturnSettings(...)` (popout value → pre-suppression launch value → forced
    un-mute) so `ApplyPlaybackSettingsAsync` always writes `muted` and a no-sample return (e.g. X-close
    before the first sync tick) can never leave the source silent. Launch volume/mute/rate captured at
    popout start. New unit test `ResolveReturnSettings_prefers_popout_then_launch_and_forces_unmute`.
    The same fallback is now applied to the different-video pending replay payload, covered by
    `Return_to_a_different_video_without_popout_sample_replays_launch_settings`.
  - **Spec §13.4 race-prevention pseudocode:** updated from focus-only `_player.Activate()` to
    `await BringVideoBackAsync()`.
  - **Returned-video replay loop:** now re-validates its target every iteration (so a navigation mid-retry
    cannot replay stale state onto the wrong page) and clears the pending state on timeout.
  - Full gate green at 687/687. Still runtime-QA only: the un-mute-on-return DOM behavior (headless lane
    cannot drive WebView2) — confirm via `run-piplay` (pop out then immediately close; same on a paused
    source) and the deferred Stable smoke.

- [x] **Task 4 - Clean up P3 opacity semantics.**
  - Rename/copy-frame the current controls as whole-popout opacity if keeping the existing layered-HWND
    implementation.
  - Add a design note for the real transparency target: Off / Main / Popout / Both, chrome/background
    only by default, video opaque unless explicitly changed.
  - Do not promise video-safe transparency from the current whole-window alpha implementation.

- [ ] **Task 5 - Resolve the P2 profile-accent decision before code changes.**
  - Current product spec and code: profile color is an identity chip color; global app accent drives UI.
  - Priority roadmap text: profile color becomes the global accent.
  - If owner confirms the reversal, write a new spec/plan and migrate tests deliberately.
  - If owner keeps the current split, update roadmap copy so future agents stop re-raising this as a
    defect.

- [ ] **Task 6 - Rationalize P7 corner language.**
  - Short path: expose only native outer modes that DWM can actually render: Default / Square /
    Small / Round.
  - De-duplicate or clarify Soft/Round so they do not promise different outer silhouettes.
  - Long path: write an ADR for WebView2 airspace/composition lift if a large rounded floating-card
    silhouette is required.

- [ ] **Task 7 - Add P5 video fit modes after P4/P1.**
  - Spec explicit modes: Fit Window, Fill/Crop, Cinema Padding.
  - Keep Fit Window as default.
  - Validate behavior in normal YouTube page mode, since compact embed mode is dormant.

## Non-Actions

- Do not re-enable Compact player as part of this work.
- Do not reverse profile-color behavior without owner sign-off.
- Do not market whole-popout opacity as video-safe transparency.
- Do not change P1 windowing architecture until visual QA proves the 4 DIP compromise is still
  unacceptable.
- Do not RC-tag until the Stable publish/verify and real WebView2/YouTube smoke rows pass.

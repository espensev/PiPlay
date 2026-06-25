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

# PiPlay b25 review and audit prep - 2026-06-25

## Current Repo State

Base branch: `main`

Current committed head: `a4fe91c`

Remote relation: `main` is 6 commits ahead of `origin/main`.

Local state at prep time:

- Source and tests changed after the last recorded full automated QA pass; rerun automated QA before
  commit/push.
- Pending work now includes the address pass for return-rule alignment, different-video replay, source
  suppression, and docs drift.
- Do not RC-tag until automated QA is rerun and deployed Stable/WebView2 manual smoke passes.

## Local Commit Stack Not On Origin

- `cf73823` - dependency audit docs
- `068d970` - product spec alignment
- `fbe77c9` - P4 bring-back implementation and P3 opacity wording
- `4841842` - follow-up stabilization status docs
- `011e8ee` - follow-up review finding
- `a4fe91c` - QA readiness evidence

## Verified Automated QA

Recorded at `a4fe91c`:

- `dotnet clean PiPlay.sln --configuration Debug --nologo` - passed, 0 warnings/errors
- `dotnet test PiPlay.sln --configuration Debug --nologo` - passed, 683/683
- `./Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` - passed, Release build,
  0 warnings/errors, version `0.7.2`, build `25` unchanged
- `git diff --check` - clean

Not done:

- Stable publish/verify
- deployed Stable manual smoke
- real YouTube/WebView2 return-state QA

## Review Inputs To Audit

Primary PiPlay review docs:

- `docs/reviews/2026-06-25-v0.7.2-b25-full-review.md`
- `docs/reviews/2026-06-25-v0.7.2-b25-full-review-evaluation.md`
- `docs/reviews/2026-06-25-b25-followup-package-review.md`
- `docs/reviews/2026-06-25-codex-p4-bring-video-back-review.md`
- `docs/reviews/2026-06-25-codex-opinion-b25-return-rule.md`
- `docs/reviews/2026-06-25-piplay-b25-code-review-thorough.md`
- `docs/reviews/2026-06-25-piplay-b25-spec-tightening-arbitration.md`
- `docs/reviews/2026-06-25-piplay-b25-meta-review-of-arbitration.md`

Out-of-scope imported artifact:

- `docs/reviews/2026-06-25-spec-review-v2.md` appears to review TerminalHQ/THQ, not PiPlay.

## Calibrated Findings

### Confirmed Done

- P4 action is real in code: Source Window primary action and Source Placeholder now call
  `BringVideoBackAsync()` instead of focus-only `Show popout`.
- Same-video return now has fresh explicit capture on the Source Window bring-back path.
- Return state carries paused, volume, muted, and playback-rate fields.
- P3 copy is honest: the existing feature is whole-popout opacity, not video-safe transparency.

### Highest-Value Audit Gates

1. **Return rule docs/spec/QA mismatch**
   - Addressed after this prep note was first written: code/spec/QA now choose Option A, where the
     popout's live paused/playing state wins when known and source launch state is fallback only.
   - Audit should verify the patched normative wording and tests before push.

2. **Source suppression / duplicate audio**
   - Source suppression is still a single pause call when popout starts.
   - Stable smoke must verify no duplicate audio through ads, autoplay-next, and SPA transitions.
   - If reproduced, block release and add pause+mute/reassertion guard.

3. **Different-video return replay**
   - Addressed in code with pending replay after the Source Window navigates to the returned video.
   - Still requires real WebView2/YouTube smoke because video readiness is runtime-dependent.

4. **Launch-from-paused intent**
   - Addressed in code: source launch intent is passed into `PlayerWindow`, and PiPlay only nudges play
     when the source was already playing at popout launch.

### Real But Lower-Severity Cleanup

- Timer/final-capture race: guard timer writes after final capture, ideally with `_capturedReturn` or a
  capture generation.
- Retarget/navigation sampling: reset `_navCompleted`/sampling during retarget so old media state cannot
  pair with a new video id.
- Button state/return completion: avoid re-enabling popout action before return is settled.
- Settings persistence on return error: save placement/settings before or independently of source return
  scripting.
- App shutdown guard: useful cleanup, but meta-review classifies this as near non-issue for RC.
- QA/doc nits from the review were addressed for active docs: `10 DIP` to `4 DIP`,
  `whole-window opacity` to `whole-popout opacity`, and stale focus-only packet heading/copy cleanup.

## Recommended Audit Order

1. Read `2026-06-25-piplay-b25-meta-review-of-arbitration.md` first for severity calibration.
2. Read `2026-06-25-piplay-b25-spec-tightening-arbitration.md` for proposed requirement wording.
3. Verify current source around:
   - `MainWindow.ApplyReturnActionAsync`
   - `PlayerWindow.CaptureReturnStateNowAsync`
   - `PlayerWindow.SyncTimer_Tick`
   - `PlayerWindow.RetargetTo`
   - `MainWindow.StartVideoPopoutAsync`
4. Verify Option A wording across spec, QA, changelog, and gap docs.
5. Rerun automated QA.
6. Publish/verify Stable and run manual smoke before any RC tag.

## Packet Guidance

Use the newest full-source audit packet generated from the current working tree. Older packets were useful
but incomplete:

- `PiPlay-b25-opinion-packet-20260625-214126.zip` - docs/opinion only
- `PiPlay-b25-code-review-packet-20260625-215921.zip` - direct P4 code only; missing support files
- `PiPlay-b25-full-source-review-packet-20260625-221304.zip` - full tracked source at that time, but
  does not include the later arbitration/meta-review/audit-prep docs

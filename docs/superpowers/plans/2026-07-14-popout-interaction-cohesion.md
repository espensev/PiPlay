# Popout interaction cohesion — implementation plan

**Spec:** `docs/superpowers/specs/2026-07-14-popout-interaction-cohesion-design.md`

**Goal:** Remove the Auto return loop and appearance dead ends, make Fade reclaim the Normal-mode
bar by default, and make the existing themes/opacity controls give immediate coherent feedback while
leaving the approved accent curve unchanged.

**Result:** Implementation complete, independently reviewed, deterministically verified, and deployed
to the sanctioned diagnostics-only Stable path for owner testing.

## Tasks

- [x] **Task 1 — Lock the Auto identity and return latch.**
  - Add a pure source-first resolver, pass Auto's parsed target into launch, and arm the returned id
    before return scripting.
  - Verify with source-B/canonical-A selection plus same-video return/next-Auto-decision tests.
  - Commit: `fix(auto): prevent return from re-popping the current video`

- [x] **Task 2 — Reunite Popout Fade with Settings.**
  - Add the Popout Settings request affordance and extract one guarded Main-owned Settings workflow
    that uses the requesting window as owner.
  - Verify event cardinality, UIA/tooltip/style, and single-dialog activation seams.
  - Commit: `fix(ui): expose the shared settings from the popout`

- [x] **Task 3 — Make Fade reclaim the shipped Normal bar.**
  - Make preset-following auto-hide default on, retain the explicit override, and add the missing
    Normal-mode collapse/restore regression.
  - Verify Fade-off still pins the strip visible and hidden chrome remains non-hit-testable.
  - Commit: `fix(player): reclaim the faded top bar by default`

- [x] **Task 4 — Make opacity and presets visibly coherent.**
  - Apply Active opacity to the Source title-bar background, preview full theme resources live,
    strengthen stepped preset behavior defaults, and clarify the three preset roles in Settings.
  - Verify cancel reverts resources/opacity and the approved accent curve stays unchanged.
  - Commit: `feat(theme): preview purposeful presets across both windows`

- [x] **Task 5 — Validate and deliver for testing.**
  - Update current docs, run focused tests, the full deterministic suite, build, spec preflight, and
    diff review; publish diagnostics-only Stable to the sanctioned E: path.
  - Verify artifact hashes/source commit posture and report that the deploy is not release evidence.
  - Commit: `docs(qa): record the popout cohesion verification`

## Self-review

- Requirements → tasks: Auto/no-loop in Task 1; settings reachability in Task 2; P6/Q-8 in Task 3;
  P3/P8 and preset feedback in Task 4; full evidence in Task 5.
- Ownership: `MainWindow` retains settings/Auto/return policy; `PlayerWindow` owns only chrome and
  raises requests; target selection is pure service logic.
- Risk: return identity and modal dialog lifecycle carry the most risk; targeted WPF/logic tests pin
  both before the full gate.
- Verified: 781 deterministic tests pass; Release build succeeds with 0 warnings and 0 errors; spec
  preflight and independent review pass; the diagnostics-only Stable deploy re-hashes all 21 artifacts
  and is running from the sanctioned E: path.

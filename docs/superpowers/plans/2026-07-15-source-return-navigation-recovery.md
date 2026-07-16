# Source return and navigation recovery - implementation plan

**Spec:** `docs/superpowers/specs/2026-07-15-source-return-navigation-recovery-design.md`

**Goal:** Restore a usable, visible Source Window after Video Popout and make its compact control
surface truthful without changing playback architecture or merging Pin preferences.

**Result:** Complete in the working tree. Full tests/build/spec gate pass; deployed manual QA remains.

## Tasks

- [x] **Task 1 - Enforce the Source sizing floor.**
  - Preserve DPI-scaled `MinWidth` / `MinHeight` in native min/max handling and normalize saved Source
    placement before restore.
  - Verify focused placement/native-window tests.
  - Commit: `fix(window): enforce Source restore floor (Q-7)`

- [x] **Task 2 - Make return a coordinated transition.**
  - Restore/activate Source on user return, hold a return-in-progress guard through replay, suspend and
    restore Source Pin without changing preferences, and disable hidden Source navigation.
  - Verify minimized return, return-state, navigation availability, and existing return policy tests.
  - Commit: `fix(popout): make return visible and single-flight (Q-2)`

- [x] **Task 3 - Tighten Source recovery controls.**
  - Add separate Show Popout and Bring video back actions, consolidate profile CRUD into a dark menu,
    collapse transfer text at compact width, and synchronize Pin/Unpin copy.
  - Verify markup, WPF accessibility/state, and Focused overlay script tests.
  - Commit: `fix(ui): make Source recovery controls direct (REQ-UI-02)`

- [x] **Task 4 - Update living contracts and verify.**
  - Update spec, ADR clarification, QA, changelog, UI priority status, and worklog.
  - Run targeted suites, deterministic local CI, build, spec preflight, and diff review.
  - Commit: `docs: record Source return recovery contract`

## Self-review

- Requirements -> tasks: sizing/Q-7 -> Task 1; visible guarded return/Q-2/Q-6 -> Task 2; direct dark
  controls/REQ-UI-01/02 and ADR-0005 -> Task 3; durable evidence -> Task 4.
- Ownership: existing placement, Source lifecycle, Popout Pin, and shared theme seams only. YouTube DOM
  behavior and playback modes stay unchanged.
- Risk: focus/z-order and asynchronous different-video replay; WPF lifecycle tests plus existing return
  suites cover the state boundaries, and deployed manual QA remains required after a sanctioned publish.
- Verified: focused integrated suite 224/224; full Debug suite 959/959; no-bump Release build 0
  warnings/errors; spec gate and diff check pass. The Windows inspection helper did not expose the
  local Default-channel window, so deployed/manual UI rows remain explicit rather than claimed.

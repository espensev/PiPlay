# Accent reach default and profile-routing fixes — implementation plan

**Spec:** `docs/superpowers/specs/2026-07-14-accent-reach-default-and-routing-fixes-design.md`

**Goal:** Preserve the v0.9.0 appearance at reach 50 and make profile/global accent ownership survive
Settings applies and preset switches without weakening P2.

**Result:** Implemented and independently re-reviewed with no blocking findings. Full deterministic gate
passed with 769/769 tests, a clean Release build (0 warnings/errors), a passing spec preflight, and clean
diff/safety checks. Deployed-Stable visual QA remains a later release-candidate gate.

## Tasks

- [x] **Task 1 — Pin the reported failures.**
  - Add literal reach-default/curve tests and a profile/global Settings writer transaction test.
  - Add WPF coverage for the full-theme repaint and profile-owned preset-switch edge.
  - Verification: targeted tests fail for the reported behavior before production changes.
  - Commit: `test(ui): pin accent reach and profile routing regressions REQ-PROFILE-01`

- [x] **Task 2 — Repair reach and commit ownership.**
  - Split glyph/wash curves, correct startup seeds, route the writer through the profile-aware service,
    restore resolved resources after full theme apply, and preserve profile colors on preset changes.
  - Verification: focused theme/settings/profile/WPF tests pass.
  - Commit: `fix(ui): preserve shipped accent reach and profile ownership REQ-PROFILE-01`

- [x] **Task 3 — Reconcile live documentation.**
  - Update normative product/QA/current-code docs and explicitly supersede historical identity-only text.
  - Verification: spec preflight and documentation references agree with code.
  - Commit: `docs(ui): reconcile accent reach and profile routing contracts`

- [x] **Task 4 — Run the repository gate and final review.**
  - Run full Debug tests, the non-mutating build, spec preflight, and diff hygiene; audit every changed file.
  - Verification: all commands pass and the final review has no blocking findings.
  - Commit: included with the implementation unless the user requests different grouping.

## Self-review

- Requirements → tasks: reach fidelity and routing are pinned in Task 1, implemented in Task 2, folded
  into current truth in Task 3, and validated end to end in Task 4.
- Ownership: `ProfileAccentService` remains the sole profile-vs-global accent router; theme resources
  remain presentation-only and stored RGB stays exact.
- Risk: Settings apply order and full resource replacement are the concentrated seams; logic plus live
  WPF resource assertions cover both.
- Verified: Release build 0 warnings/errors; Debug tests 769/769; spec preflight and diff checks pass;
  independent final review has no blocking findings.

# Profile identity rail and Source Window accent wash - implementation plan

**Spec:** `docs/superpowers/specs/2026-07-11-profile-identity-rail-and-accent-wash-design.md`

**Goal:** keep every valid dark global/profile color visibly useful, carry the global accent into
Source Window chrome, and reduce profile identity to one rail without changing stored colors.

**Result:** Implemented. Focused UI/theme/profile tests passed (298), the full Debug suite passed
(690), the Release build completed with 0 warnings/errors, and the spec/diff gates passed. Manual
visual QA against deployed Stable remains the release-candidate lane.

## Tasks

- [x] **Task 1 - Lock down dark-color failures.**
  - Add red tests for surface-equal global accents and the profile rail.
  - Verify the tests fail for contrast below 3.0:1.

- [x] **Task 2 - Derive safe presentation colors.**
  - Add minimum contrast and adaptive shell-tint helpers.
  - Publish safe accent/tint resources while keeping persistence and picker swatches raw.

- [x] **Task 3 - Simplify and extend the chrome treatment.**
  - Remove the profile-colored selector frame, convert the single rail against live
    `SurfaceHover`, and add the title-bar gradient wash.
  - Keep Source/Popout imperative accent consumers aligned with the safe resources.

- [x] **Task 4 - Repair change records and traceability.**
  - Restore June history, update current product/changelog text, and attach requirement IDs to
    changed tests.

- [x] **Task 5 - Verify and self-review.**
  - Run focused tests, full tests, the repository build gate, spec preflight, and diff checks.
  - Review every changed file against the acceptance criteria.

## Self-review

- Requirements -> tasks: dark contrast is Tasks 1-2; one-rail/no-nested-control and the shell wash
  are Task 3; durable traceability is Task 4.
- Ownership: presentation tokens stay in `Theme`; profile persistence remains unchanged; no WebView2,
  playback, placement, or release-deploy seam changes.
- Risk: live WPF resource replacement and imperative accent consumers are the concentrated risks;
  logic, markup, and WPF tests cover them.
- Verified: 298 focused tests and 690 full-suite tests passed; Release build, spec preflight, and
  `git diff --check` passed.

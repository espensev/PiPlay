# Popout rounded-card region — implementation plan

**Spec:** `docs/superpowers/specs/2026-07-15-popout-rounded-card-region-design.md`

**Goal:** Give Soft Glass / `Round` a real 22 DIP Popout Player silhouette while preserving the
standard WebView2 playback and native window-quality paths for every other appearance.

**Result:** Native implementation, automated verification, and a post-remediation diagnostics
deployment are complete. Clean deployed/manual visual acceptance remains pending.

**Landing note:** Focused presentation, rounded regions, and audit remediation share the same
`MainWindow` / `PlayerWindow` / `YouTubeDomBridge` paths. They land as one consolidated feature commit
instead of reconstructing fragile intermediate commits from overlapping working-tree hunks.

## Tasks

- [x] **Task 1 — Add the native region policy and applier.**
  - Add pure geometry decisions plus ownership-safe `CreateRoundRectRgn` / `SetWindowRgn` plumbing.
  - Verify with focused policy tests.
  - Landing: consolidated Popout feature/stabilization commit.

- [x] **Task 2 — Wire the Popout lifecycle and live theme path.**
  - Pass the effective `PopoutFrame` radius at launch and preview/apply; refresh on size, DPI, and
    window-state transitions; clear while maximized or non-round.
  - Verify with real-HWND WPF tests plus existing resize/opacity tests.
  - Landing: consolidated Popout feature/stabilization commit.

- [ ] **Task 3 — Reconcile current docs and validate.**
  - [x] Update the ADR/spec/theme/QA/changelog surfaces provisionally for the implemented behavior.
  - [x] Run focused/full tests, build, spec gate, and `git diff --check`.
  - [x] Publish a post-remediation diagnostics-only Stable copy:
    `20260715-080621-v0.10.1-b33-stable` (`-AllowDirty`, not release evidence).
  - [ ] Complete and record manual Round/Square/System inspection at 100/125/150% DPI, including
    free resize, maximize/restore, Snap, live theme/radius changes, and repeated open/close cycles.
  - Landing: consolidated Popout feature/stabilization commit.

## Self-review

- Requirements → tasks: silhouette and WebView clipping → Tasks 1–2; DPI/maximize/resize → Task 2;
  durable architecture and manual acceptance → Task 3.
- Ownership: `ThemeRadii.PopoutFrame` remains the radius owner; DWM owns default/small/square window
  treatment; the new applier owns only the large Popout region.
- Risk: GDI region edge quality and snap/maximize transitions; covered by HWND tests and deployed
  visual acceptance with rollback as an explicit failure outcome.
- Verified: native policy/applier, lifecycle wiring, real-HWND coverage, full automated tests, Release
  build, spec gate, diff check, and diagnostics deployment passed in the implementation lane. Clean
  exact-source deployment and manual multi-DPI acceptance remain pending and keep Task 3 open.

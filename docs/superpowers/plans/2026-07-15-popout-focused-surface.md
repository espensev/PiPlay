# Focused Popout surface — implementation plan

Design: `docs/superpowers/specs/2026-07-15-popout-focused-surface-design.md`

**Result:** Core implementation, automated verification, deep-audit remediation, and a controlled
diagnostics deployment are complete. A clean exact-source candidate plus deployed/manual YouTube
acceptance remain pending; this plan is not release sign-off.

- [x] **Task 1 — Presentation model and settings/profile UI.** Add the pure policy, global default,
  profile override, persistence repair, Settings toggle, profile picker, and focused tests.
- [x] **Task 2 — Threshold surface drag.** Add the pre-navigation top-document bridge, closed
  nonce protocol, native move handoff, disposal, and logic/asset/WPF seams.
- [x] **Task 3 — Focused player and overlay.** Add no-crop full-viewport styling, accessible
  Opera-style controls, host action protocol, fade/activity behavior, live accent application, and
  selector-safe teardown.
- [x] **Task 4 — Reconcile current docs and regression coverage.** Update the living product spec,
  QA checklist, gaps, preset notes, changelog, and test index without disturbing the rounded-region
  pass already in the worktree.
- [x] **Task 5a — Verify and deliver for diagnostics.** Run targeted tests, full Debug tests, Release
  build, spec gate, diff check, independent review, then publish a labeled diagnostics-only Stable
  copy with `Publish-Stable.ps1 -AllowDirty`.
- [ ] **Task 5b — Complete deployed/manual YouTube acceptance.** After audit remediation and a fresh
  clean verification pass, validate Standard and Focused playback, real ad states, synthetic-event
  rejection, navigation/replay rejection, open/close cycling, and a sustained Focused soak. Record the
  evidence in the living QA surfaces before release sign-off.
- [x] **Task 6 — Runtime crash correction.** Reproduce the WebView2 `STATUS_BREAKPOINT`, remove
  unnecessary child-frame event wiring, make the native move handoff asynchronous, enlarge resize
  acquisition initially to 12/72 DIP, and re-run the deployed Stable crash/interaction loop.
- [x] **Task 7 — Owner interaction retune.** Keep the accepted 12 DIP edge band, extend corner reach
  to 96 DIP, enlarge the guaranteed native move handle to 44 DIP, and let unused YouTube
  chrome containers participate in threshold dragging while preserving real controls and captions.

## Open remediation gate

- [x] Re-review and remediate the code findings in
  `docs/reviews/review-2026-07-15-deep-technical-audit.md`.
- [ ] Complete the audit's live ad, current-document/trusted-input, navigation, cycling, and soak
  evidence against a clean exact-source deployment. Automated remediation alone does not complete
  Task 5b.

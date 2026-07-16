# Runtime failure hardening — implementation plan

**Spec:** `docs/superpowers/specs/2026-07-16-runtime-failure-hardening-design.md`

**Goal:** Bound verified retry/operation lifetimes, require acknowledged playback transfer, and remove deterministic live-preview duplicate work while preserving healthy polling and UI behavior.

**Result:** Implemented. The final focused gate passed 26/26, the full Debug solution passed 985/985, `scripts/Test-LocalCI.ps1` passed with a 0-warning/0-error Release build, and `scripts/Preflight-SpecGate.ps1` passed. Two independent final reviewers reported no remaining actionable findings. Live profiling items M-001 through M-004 remain deferred by authority/runtime boundary.

## Tasks

- [x] **Task 1 — Trace and classify every discovery.**
  - Complete Depth-4 static traces for D-001 through D-008, promote only supported findings, and serialize rejected and measurement-bound hypotheses.
  - Verify the trace-to-finding/rejection/measurement mapping has no orphan discovery.
  - Commit: `docs(audit): classify runtime efficiency discoveries`

- [x] **Task 2 — Fail closed on Source suppression and bound DOM failure logs.**
  - Add red-capable executor/gate tests, make suppression return an acknowledgement, route a negative outcome through launch rollback, and coalesce consecutive execution errors without changing timer cadence.
  - Verify the focused DOM failure test filter and existing Popout state-machine tests.
  - Commit: `fix(player): require acknowledged source suppression for Q-1`

- [x] **Task 3 — Own timed-out browser clears until completion.**
  - Add a single-flight clear coordinator, retain and observe late completion/fault, block only a second clear after foreground timeout, and surface a truthful disabled-action hint.
  - Verify coordinator logic tests plus Settings/MainWindow WPF seams.
  - Commit: `fix(privacy): retain timed-out clear operation ownership`

- [x] **Task 4 — Bound single-instance pipe failure behavior.**
  - Add session-qualified pipe naming and a testable cancellation-aware exponential retry loop with first-failure/recovery reporting.
  - Verify identity, delay cap/reset, and cancellation tests.
  - Commit: `fix(app): bound session pipe retry failures for REQ-APP-01`

- [x] **Task 5 — Skip unchanged accent resource writes.**
  - Add WPF identity/count tests and conditionally replace only missing, wrong-typed, or changed brush/color resources.
  - Verify derived values, frozen replacements, identical reapply identity, and intensity-only replacement bounds.
  - Commit: `perf(theme): retain unchanged accent resources`

- [x] **Task 6 — Close documentation and verification gates.**
  - Update the changelog, audit state/findings/verification records, this plan result, and the session worklog.
  - Run focused tests, `scripts/Test-LocalCI.ps1`, whitespace/diff checks, and an independent read-only remediation review.
  - Commit: `docs(audit): close runtime failure hardening pass`

## Self-review

- Requirements → tasks: Q-1/Q-3/Q-6 → Task 2; REQ-PRIVACY-02 → Task 3; REQ-APP-01 → Task 4; section 22.4 → Tasks 2, 4, and 5; audit traceability → Tasks 1 and 6.
- Ownership: DOM policy remains in `YouTubeDomBridge`; privacy orchestration remains in `MainWindow`; single-instance lifecycle remains in `App`; theme resource mutation remains in `ThemeResourceApplier`. Return persistence and WebView environment ownership remain untouched.
- Risk: async completion races and cancellation received injected-task/loop tests, including timeout-boundary completion and stray cancellation. UI resource/availability behavior received real WPF assertions.
- Verified: 26 focused tests; 985 full Debug tests; local CI PASS; Release build 0 warnings/0 errors; spec preflight PASS; two independent final reviews PASS.

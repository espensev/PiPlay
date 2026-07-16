# Adversarial Verifications

Schema: deep-audit/v1  
Audit slug: `piplay-runtime-2026-07-16`  
Repository/current revision: baseline commit `8015ba4` plus the focused uncommitted remediation working tree on `main`  
Source/runtime boundary and authority: safe automated source/build verification only; no deployed/live runtime was launched  
Dirty-state fingerprint/scope/exclusions/environment: see `STATE.md`  
Last updated: 2026-07-16 Europe/Berlin

### V-001 — Acknowledged suppression and DOM failure episodes

- Findings: F-003 and F-004.
- Before evidence: the first focused test run failed because the acknowledgement overload, launch policy, and failure gate did not exist.
- Adversarial cases: explicit true/false/null/empty results, executor exception, post-command JS contract, launch continuation blocked on false, launch continuation allowed on true, 100 consecutive failures, single-failure recovery, and healthy reset.
- Structural review: `MainWindow` awaits the tested precondition before the suppression guard, placeholder, environment/player construction, and success latch. Failure flows into the existing captured-state rollback; Auto uses a distinct failed-video latch.
- Result: PASS in the final 26-test focused filter, full suite, and two independent final reviews.
- Residual: host CPU under a persistent live renderer/controller failure remains M-001.

### V-002 — Timed-out browser clear ownership

- Finding: F-002.
- Adversarial cases: duplicate start while incomplete, late success, late fault, synchronous factory throw, already-completed task, 16 concurrent starts, true timeout, completion-at-timeout race, operation-owned `TimeoutException`, unavailable reason, and in-place Settings re-enable/re-disable.
- Structural review: coordinator stores the raw task under a lock and releases only the same terminal task; Main attaches one operational observer only after true foreground expiry and refreshes a live dialog on the dispatcher.
- Result: PASS in the final focused/full gates and independent final reviews.
- Residual: WebView2 internal disk scheduling is not claimed or measured.

### V-003 — Session pipe identity and bounded retry

- Finding: F-001.
- Adversarial cases: stable same-session identity, cross-session/channel distinction, exact exponential delay/cap, no retry before delay completes, reset after success, app-token cancellation, and unrelated `OperationCanceledException` treated as retryable failure.
- Structural review: actual server attempt owns construct/wait/read/dispatch; the policy logs first failure, delays cancellation-aware, summarizes recovery, and preserves normal async idle wait.
- Result: PASS in focused/full gates and independent final review.
- Residual: per-client payload/time limits and mixed-old/new pipe-name handoff are outside this finding.

### V-004 — Conditional accent resource updates

- Finding: F-005.
- Adversarial cases: identical apply preserves every brush/color entry identity, each intensity transition changes a brush if and only if its derived color changes, replacement values are exact/frozen, companions are exact, and missing/wrong-typed entries are repaired.
- Result: PASS using real WPF `ResourceDictionary` instances in the focused/full gates and independent review.
- Residual: realized frame/allocation delta remains optional M-003.

### V-005 — Repository verification loop

- Focused command: runtime-failure plus WPF filters; PASS 26/26.
- Full command: `dotnet test PiPlay.sln --configuration Debug --nologo`; PASS 985/985 (PASS-0 baseline 959/959).
- Canonical gate: `scripts/Test-LocalCI.ps1`; PASS with restore, 985 tests, and Release build 0 warnings/0 errors.
- Spec gate: `scripts/Preflight-SpecGate.ps1`; PASS with the dated design detected.
- Diff gate: `git diff --check`; PASS before final record closure and rerun in the final handoff check.

### V-006 — Independent remediation review

- Review lanes: independent production/concurrency/spec review and independent test/audit/docs review.
- First pass findings: timeout-boundary completion classification, stale Clear state in an already-open Settings dialog, and missing orchestration/race tests.
- Corrections: added `DidForegroundWaitExpire`, dispatcher-driven live availability refresh, `PopoutLaunchPolicy`, and focused branch/race/fault/WPF tests.
- Re-review result: both lanes PASS with no remaining actionable production or test findings; neither reviewer edited files or launched the app.

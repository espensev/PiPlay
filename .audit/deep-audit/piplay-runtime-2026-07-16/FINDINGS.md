# Findings

Record convention: finding bodies preserve the baseline/pre-remediation behavior at `8015ba4`; each `Result` and the ranked summary state the landed `e16c0f3` disposition.

Schema: deep-audit/v1  
Audit slug: `piplay-runtime-2026-07-16`  
Repository/current revision: baseline `8015ba4`; remediation `99f9834`; current/released `e16c0f3` on `main`
Source/runtime boundary and authority: all five fixes are landed/released; see `STATE.md`; no product-code writes in this closeout
Dirty-state fingerprint/scope/exclusions/environment: see `STATE.md`  
Last updated: 2026-07-18 Europe/Berlin

## Ranked summary

| Rank | ID | Severity | Confidence | Discovery/trace | Status | Summary |
|---:|---|---|---|---|---|---|
| 1 | F-001 | Medium | High mechanism | D-003 / T-003 | Fixed/verified: V-003/V-005/V-006 | Session-mismatched pipe identity could trigger an unbounded exception retry loop. |
| 2 | F-002 | Medium | High | D-002 / T-002 | Fixed/verified: V-002/V-005/V-006 | A timed-out browser clear lost ownership and permitted overlapping outstanding tasks. |
| 3 | F-003 | Medium | Medium-high | D-001 / T-001 | Fixed/verified: V-001/V-005/V-006 | Popout launch treated failed Source suppression as success, risking temporary duplicate playback. |
| 4 | F-004 | Low | High | D-001 / T-001 | Fixed/verified: V-001/V-005/V-006 | Persistent DOM execution failures enqueued equivalent errors at the fixed polling cadence. |
| 5 | F-005 | Low | High | D-007 / T-007 | Fixed/verified: V-004/V-005/V-006 | Accent preview replaced unchanged resource pairs, allocating and invalidating deterministically. |

## Records

### F-001 — Single-instance pipe failures retry without a bound

- Severity/confidence/depth: Medium; high mechanism confidence; Depth 4.
- Source revision/runtime boundary: baseline/pre-remediation `8015ba4`; no failure was induced live.
- Location: `src/PiPlay/App.xaml.cs` single-instance identity and server loop.
- Workload: cross-session same-channel primaries or persistent pipe namespace/ACL/resource failure.
- Evidence and multiplier: session-scoped mutex plus channel-only machine pipe; generic catch immediately loops. Cost is exception construction + log enqueue + another attempt multiplied by failure duration at an unbounded iteration rate.
- Correction: session-qualify the pipe name; await 250 ms exponential retry capped at 30 seconds; reset after success; log first failure and recovery summary.
- Verification target: deterministic injected loop/naming tests, canonical local CI, independent review.
- Result: implemented and verified by V-003, V-005, and V-006.

### F-002 — Timed-out browser clear can leave multiple outstanding clears

- Severity/confidence/depth: Medium; high; Depth 4.
- Source revision/runtime boundary: baseline/pre-remediation `8015ba4`; WebView2 internal disk scheduling unmeasured.
- Location: `MainWindow.PerformClearBrowserDataAsync`, `PrivacyService.ClearBrowserDataAsync`.
- Workload: an `AllProfile` clear longer than 30 seconds followed by another confirmed request.
- Evidence and multiplier: `WaitAsync` times out without canceling the underlying task; all gates reset and no task reference remains. Outstanding clear API tasks scale with repeated post-timeout confirmations.
- Correction: track the underlying task in a single-flight coordinator, observe late completion/fault, and keep only Clear disabled until terminal completion.
- Verification target: TCS-driven success/fault/duplicate/retry tests, WPF unavailable-hint test, canonical local CI.
- Result: implemented with timeout-boundary classification and live-dialog refresh; verified by V-002, V-005, and V-006.

### F-003 — Popout launch does not require acknowledged Source suppression

- Severity/confidence/depth: Medium correctness impact; medium-high; Depth 4.
- Source revision/runtime boundary: baseline/pre-remediation `8015ba4` Normal-mode Popout path; no WebView fault injected.
- Location: `MainWindow.StartVideoPopoutAsync`, `YouTubeDomBridge.SuppressPlaybackAsync`.
- Workload: initial Source suppression host/script failure during Popout launch.
- Evidence and multiplier: the bridge catches the exception and completes normally; launch then starts the safety guard and constructs/shows the Popout. Source audio can continue until a later 1 Hz guard attempt succeeds.
- Correction: return a true acknowledgement only when the DOM script found the video and issued mute/pause; route false through existing rollback before Popout construction.
- Verification target: injected executor outcome tests plus existing Popout state-machine coverage.
- Result: implemented through a tested launch precondition and post-command state check; verified by V-001, V-005, and V-006.

### F-004 — Consecutive DOM host failures repeat equivalent log work

- Severity/confidence/depth: Low; high for log work, measurement-bound for material CPU; Depth 4.
- Source revision/runtime boundary: baseline/pre-remediation `8015ba4`, one Normal Popout.
- Location: `YouTubeDomBridge.ExecuteAsync`/`ExecuteVoidAsync`, the 1 Hz Source and 4 Hz Popout timers.
- Workload/multiplier: up to five caught exceptions and log enqueues per second for persistent failure duration. Queue and disk retention are bounded; IPC cadence is the healthy cadence.
- Correction: log first consecutive query/command failure, suppress repeats, and report suppressed count on recovery. Preserve timers.
- Verification target: failure-gate count/reset tests and canonical local CI.
- Result: implemented per weakly held WebView and exact operation; verified by V-001, V-005, and V-006.

### F-005 — Accent preview rewrites unchanged WPF resources

- Severity/confidence/depth: Low deterministic efficiency defect; high; Depth 4.
- Source revision/runtime boundary: baseline/pre-remediation `8015ba4` WPF Settings preview.
- Location: `ThemeResourceApplier.ApplyAccentOnly`/`SetColorPair`.
- Workload/multiplier: up to 30 applies/s; 18 replacements/apply even when all or most values are identical. Intensity-only movement commonly changes one or two of nine pairs.
- Correction: preserve equal correctly typed brush/color entries; repair only missing, wrong-typed, or changed values.
- Verification target: real WPF dictionary identity/count/value/frozen-state tests, canonical local CI, optional M-003 profiling.
- Result: implemented and verified by V-004, V-005, and V-006; material end-user delta remains optional M-003.

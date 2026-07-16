# Final Deep Audit Report

Schema: deep-audit/v1
Audit slug: `piplay-runtime-2026-07-16`
Repository root: `D:\Development\DesktopApps\PiPlay`
Baseline/current revision: `8015ba4` on `main` (ahead 2 of `origin/main`); focused remediation lives uncommitted in the working tree
Source/runtime boundary: current source; deployed Stable at `E:\Dev_test_implemenations\PiPlay` is `d11eac5` (v0.11.0 b34), two commits stale, and was not used as evidence for current source
Report basis: D-001..D-008, T-001..T-008, F-001..F-005, V-001..V-006, M-001..M-004, R-001..R-003, COVERAGE.md
Current-truth revalidation for this report: canonical gate `scripts/Test-LocalCI.ps1` re-run 2026-07-16 → **LOCAL CI: PASS**, 985/985 tests (0 failed, 0 skipped), Release build 0 warnings/0 errors
Completion gate: met for the frozen scope; live-runtime measurements remain explicitly authority-blocked and are carried as M-001..M-004

## Verdict and confidence

- **Overall runtime assessment:** PiPlay's healthy steady state is disciplined. Every polling surface is bounded and single-flight (Auto 4/s with cheap preflight, Source suppression 1/s, Popout sync 4/s, accent preview ≤30/s coalesced, opacity probe 4/s only when needed), cardinality is capped by design (one primary instance, one Source, at most one Popout), and logging is a bounded-queue, batching, rotating pipeline. No unbounded hot loop exists on any healthy path.
- **Most consequential multiplier:** failure-duration multipliers, not healthy cadence. The two material mechanisms were the named-pipe server's immediate unbounded retry under persistent construction failure (F-001) and per-second equivalent-exception logging under persistent DOM execution failure (F-004). Both are now bounded/coalesced and verified.
- **Highest-risk scaling/lifecycle issue:** unvalidated live settling — repeated Popout open/close resource settling (M-004) and Focused-surface session cost in the renderer (M-002). Static ownership/teardown is thorough and no leak is confirmed; the risk is purely that live behavior is unmeasured.
- **Highest-value opportunity:** already realized in the working tree. F-001..F-005 (two Medium correctness/lifecycle defects, one Medium launch-acknowledgement defect, two Low deterministic-efficiency defects) are fixed, tested (26 focused tests; suite 959→985), and independently re-reviewed with no remaining actionable findings. Remaining value is measurement-gated.
- **Coverage and confidence:** 18 runtime areas mapped at Depth 2–4 (16 at Depth 3–4); static confidence is high across startup, steady state, failure, cancellation, return, and shutdown. Live operational cost (CPU/allocation/handles/renderer heap) is deliberately unmeasured pending a current-source Stable deployment and explicit measurement authority.

## Ranked findings

All five findings are **Fixed/verified in the working tree** at `8015ba4`+remediation. None reached Critical/High; each Medium was adversarially verified anyway.

| Rank | ID | Severity | Confidence | Depth | Verification | Claim and resolution |
|---:|---|---|---|---:|---|---|
| 1 | F-001 | Medium | High (mechanism) | 4 | V-003, V-005, V-006 | Single-instance pipe used a session-scoped mutex with a machine-scoped, channel-only pipe name, and any persistent pipe failure retried immediately without bound at exception speed. Fixed: session-qualified pipe identity (`SingleInstancePipePolicy`) + cancellation-aware 250 ms→30 s exponential backoff, first-failure/recovery-summary logging, reset on success. |
| 2 | F-002 | Medium | High | 4 | V-002, V-005, V-006 | A browser-data clear that outlived its 30 s foreground wait lost all ownership: gates reset, no task reference kept, so repeated confirmations could stack outstanding `AllProfile` clears. Fixed: `BrowserDataClearCoordinator` single-flight on the raw task, late completion/fault observed, Clear stays disabled until terminal completion, live Settings dialog refresh, timeout-boundary completion classified (`DidForegroundWaitExpire`). |
| 3 | F-003 | Medium (correctness) | Medium-high | 4 | V-001, V-005, V-006 | Popout launch treated failed Source suppression as success (bridge swallowed host exceptions), so Source audio could keep playing until the 1 Hz guard recovered — the double-audio window. Fixed: suppression returns a true acknowledgement only when the DOM script found the video and issued mute/pause; `PopoutLaunchPolicy` blocks Popout construction on anything else and routes into the existing rollback. |
| 4 | F-004 | Low | High (log work) | 4 | V-001, V-005, V-006 | Persistent DOM host failure formatted/enqueued up to five equivalent exceptions per second for the failure duration. Fixed: `ConsecutiveFailureGate` per weakly-held WebView and exact operation — log first failure, suppress repeats, report suppressed count on recovery. Timers/cadence deliberately preserved (see R-003). |
| 5 | F-005 | Low | High | 4 | V-004, V-005, V-006 | Accent preview replaced all 18 accent resource entries per apply (≤30/s) even when values were unchanged; intensity-only movement typically changes 1–2 of 9 pairs. Fixed: `ThemeResourceApplier` preserves equal, correctly typed brush/color entries and repairs only missing/wrong-typed/changed values, verified against real WPF `ResourceDictionary` identity/value/frozen-state tests. |

Validation criteria per finding live in `FINDINGS.md`; adversarial case inventories live in `VERIFICATIONS.md`.

## Runtime architecture and cost map

- **Startup and construction:** `App.OnStartup` loads theme/settings, elects one primary per session via a `Local\` mutex, starts the named-pipe handoff server on a thread-pool task, and constructs the single `MainWindow`. Shared WebView2 environment creation is lazy, effectively single-caller (R-002), and reused constant-time afterward.
- **Timers/workers/queues/loops:** Auto detector (250 ms, gated, single-flight), Source suppression guard (1 s while a Popout owns playback), Popout sync (250 ms, generation-guarded across navigation), Focused fallback (1 s, active-only), opacity/strip probe (250 ms, only under non-default settings), 15 ms alpha animator during fades, accent preview coalescer (33 ms max cadence), logging background thread over a bounded `BlockingCollection` (4,096 entries, 64 KiB batches, ~1 MB active + one backup), pipe server loop (async idle wait; capped backoff on failure).
- **Frequency and cardinality boundaries:** one instance/one Source/≤1 Popout; 4/s Auto; 1/s suppression; 4/s sync; ≤12×250 ms return-replay probes; ≤8/s Focused pointer reveal; ≤30/s preview applies; profile/settings cardinality is user-created and uncapped — every save serializes the full list (bounded by user behavior, flagged in coverage).
- **Threads/dispatchers/locks:** WPF dispatcher owns windows, all DispatcherTimers, WebView handlers, native helpers, and synchronous settings writes; pipe server dispatches handoff via `Dispatcher.Invoke`; Focused appearance uses a lock-protected latest-wins pump with ≤1 WebView call in flight; native opacity/resize dictionaries are UI-thread-only and clean through `WM_NCDESTROY`.
- **I/O and external dependencies:** WebView2 async IPC for all DOM work; durable atomic temp-flush-replace settings writes; batched log file writes with rotation; named-pipe IPC for single-instance handoff; no network I/O owned by the app beyond WebView2 itself.
- **Failure/retry/cancellation/recovery/shutdown:** DOM bridge failures are caught, classified per operation, and (post-fix) acknowledged at launch and coalesced in logs; privacy clear owns its task to terminal completion; pipe failures back off and recover; close/return invalidates generations, stops all four Popout timers, disposes bridges before WebView2, removes scripts/handlers/subclasses/regions; `App.OnExit` cancels the pipe server and drains logs.

## Hot-path table

| Rank | Entry/trigger | Frequency/cardinality | Dominant work | Allocation/retention | I/O/blocking | Amplification | Evidence | Action |
|---:|---|---|---|---|---|---|---|---|
| 1 | Popout sync timer | 4/s × 1 Popout, single-flight | WebView IPC state read + parse | small, transient | async IPC | navigation-generation bounded | T-001, V-001 | keep; M-001 if failure cost questioned |
| 2 | Focused DOM surface | media/mutation/pointer events + 1/s active-only fallback × session | renderer-side callbacks, conditional DOM writes | renderer heap unmeasured | in-renderer | session duration | T-004 | M-002 when authorized |
| 3 | Auto detector | 4/s while enabled | cheap URL preflight; DOM read only for unhandled watch video | negligible | async IPC | single-flight | COVERAGE | none |
| 4 | Source suppression guard | 1/s while Popout owns playback | WebView IPC mute/pause reassert | negligible | async IPC | failure-duration (log side fixed) | T-001, F-003/F-004 | keep 1 Hz (Q-1 guard); M-001 optional |
| 5 | Accent preview | ≤30/s during Settings drag only | derive 9 pairs; now conditional replacement + realized DynamicResource re-resolution | frozen brushes; churn now change-proportional | none | interaction-scoped | T-007, F-005, V-004 | M-003 optional |
| 6 | Popout close/return | per user close | 2 durable full-settings writes + ≤12 replay probes | serialize full settings ×2 | flush-to-disk ×2 | user-bounded | T-005, R-001 | preserve (spec §14) |
| 7 | Opacity/fade activity | 4/s probe + 15 ms animator, only when configured | native queries, alpha animation | negligible | none | feature-gated | COVERAGE | none |
| 8 | Logging pipeline | event-driven, bounded queue | format/enqueue/batch/rotate | 4,096-entry cap; ~2 MB disk | batched writes | fixed by F-004 coalescing | T-001/T-003 | preserve bounds |
| 9 | Pipe server | idle async wait; handoff per second instance | wait/read/dispatch | negligible | pipe IPC | failure now 250 ms→30 s backoff | T-003, F-001, V-003 | none |
| 10 | Browser-data clear | user-confirmed | WebView2 `AllProfile` clear | none retained post-terminal | WebView2-internal disk | single-flight (fixed) | T-002, F-002, V-002 | none |

## CPU, memory, and duplicate work

- **Deterministic duplicate work found and removed:** unchanged accent resource replacement (F-005: 18 replacements/apply → change-proportional, typically ≤2 for intensity movement) and equivalent-exception log formatting under persistent DOM failure (F-004: 5/s → first + recovery summary).
- **Suspected duplicate work that was not duplicate:** the dual Popout-return settings saves are spec-required durability checkpoints on either side of fallible Source scripting (R-001) — preserved.
- **Algorithmic CPU:** no super-linear growth found on any reachable path; per-tick work is constant-bounded and single-flight everywhere.
- **Allocation churn:** healthy-path churn is small and transient; the two deterministic churn sources are fixed. Realized WPF invalidation cost of preview applies remains unmeasured (M-003).
- **Retained growth/leaks:** none confirmed statically. Popout teardown removes timers, bridges, scripts, handlers, subclasses, and HRGNs deterministically (real-HWND tests cover native resize-state removal). Live settling slopes (private bytes, handles, GDI/USER, renderer processes) are M-004; renderer heap/node settling for Focused is M-002.

## I/O, concurrency, and lifecycle

- **I/O granularity/timeouts:** settings writes are atomic and durable (two per return, user-bounded); log writes are batched with bounded retention; the privacy clear's 30 s wait now bounds only the foreground UX while ownership persists to terminal completion (F-002).
- **Overlap and reentrancy:** every timer path carries an in-progress flag; the clear path is single-flight under a lock; Focused appearance IPC is latest-wins with ≤1 call in flight; environment creation is single-caller by wiring (R-002 records the reopen condition: multiple Sources or reentrant Retry).
- **Failure amplification:** pipe failure amplification is now bounded (F-001); DOM failure amplifies neither IPC cadence (R-003) nor, post-fix, log volume (F-004); launch no longer proceeds on unacknowledged suppression (F-003).
- **Cancellation/restart/shutdown:** app exit cancels the pipe loop cleanly; Popout navigation/close invalidates work by generation token; `WM_NCDESTROY` clears native state; log queue drains on exit. Static shutdown paths are sound; repeated-cycle settling is the remaining live question (M-004).

## Measurement requirements

Execution of all four is **blocked** on (a) a current-source Stable deployment via `Publish-Stable.ps1` — which itself requires committing the remediation and version stamps — and (b) explicit owner authority for fault injection/profiling/soak. Full designs with thresholds are in `MEASUREMENTS.md`.

- **M-001 — persistent DOM failure host cost** (D-001/F-004): is 5 failed single-flight `ExecuteScriptAsync` calls/s material after log coalescing? Confirm ≥1 CPU point or ≥5 ms CPU/s; reject <0.5 point and <2 ms/s. Only a confirm result would justify considering polling backoff.
- **M-002 — Focused DOM incremental session cost** (D-004): matched Standard/Focused ABBA, 3×20–30 min runs; renderer CPU, callback families, heap/node settling. Confirm ≥5 ms CPU/s callbacks, ≥1 renderer CPU point, or post-warmup heap growth.
- **M-003 — realized WPF accent-preview cost + F-005 delta** (D-007, optional): p95 apply time, dispatcher lateness, allocation/apply across three workloads; correction success = replacements 18→≤4 below intensity 50 (≤2 above) plus ≥30% lower allocation or apply time.
- **M-004 — Popout lifecycle settling** (D-008): 5 warmup + 50 Standard + 50 Focused cycles with 5/15/30/60 s post-close samples plus a 30–60 min navigation/resize/DPI soak; confirm retention at ≥0.5 MB/cycle + 25 MB cumulative, ≥1 handle/cycle + 25, sustained GDI/USER growth, or ≥25% cycle-latency degradation.

## Preserve list and rejected hypotheses

Preserve (correctness- or efficiency-critical; do not "optimize" away):

- Both durable settings checkpoints around Source return (spec §14; R-001).
- The 1 Hz Source suppression guard cadence — it is the Q-1 double-audio recovery mechanism, not waste (T-001, R-003).
- Single-flight in-progress flags on all four Popout/Source timers and the Auto detector.
- The bounded logging pipeline (queue cap, batching, rotation, drop-on-full accounting).
- Acknowledged-suppression launch precondition and captured-state rollback (F-003 fix).
- Atomic temp-flush-replace settings persistence.
- Generation tokens across Popout navigation/close and `WM_NCDESTROY`-driven native-state cleanup.

Rejected hypotheses (do not rediscover unless the recorded condition changes):

- **R-001** — dual return saves are duplicate work → rejected; spec-required durability. Reconsider technique only if slow-storage measurement proves material UI latency, keeping both boundaries.
- **R-002** — shared WebView environment creation is a concurrent hot path → rejected for current wiring (one cold caller; Retry post-terminal-failure; Popout gated on `_browserReady`). Reopen if multiple Sources/callers or reentrant Retry appear.
- **R-003** — persistent DOM failure amplifies IPC above healthy cadence → rejected; failure amplified only exception/log work (fixed by F-004), never call count.

## Staged optimization plan

1. **Stage 1 — done, pending landing:** F-001..F-005 remediation is complete, tested (985/985), gate-clean, spec-preflighted, and independently re-reviewed in the working tree. Remaining steps are owner decisions, per repo rules: review → commit (with `VERSION`/`BUILD_NUMBER`/changelog stamps for a release) → `Publish-Stable.ps1` → push. No further static product work is open.
2. **Stage 2 — measurable validation:** after a current-source Stable deploy and explicit authority, execute M-004 and M-002 first (lifecycle settling and Focused session cost are the only unbounded-in-time unknowns), then M-001/M-003 opportunistically. Act only on threshold-crossing results; every plan carries confirm/reject criteria.
3. **Stage 3 — architecture:** none justified. Current evidence supports no architecture change; the one recorded conditional is re-opening D-006 (environment creation single-flight) if Source/window ownership ever multiplies.

## Coverage and uncertainty

- **Inspected:** startup/single-instance, shared WebView environment, Source init/navigation, Auto, Popout launch/suppression/sync, Focused DOM surface, appearance preview, fade/opacity, native corners/regions, return/replay, settings persistence, browser-data clear, logging, shutdown, failure/recovery — 18 areas, Depth 2–4 (see `COVERAGE.md`).
- **Excluded by frozen scope:** build/publish/CI efficiency, security assessment, UI style, YouTube/network internals, manual QA, production load, live fault injection/profiling, deployment.
- **Partially inspected:** compact shell (Depth 2 — feature-gated dormant since the v0.6.0 kill-switch; P2 if ever re-enabled); deployed Stable runtime (verifier-clean but two commits stale — explicitly not evidence for current source).
- **Evidence freshness:** no drift — revision `8015ba4` unchanged since baseline; dirty tree matches the recorded remediation fingerprint; canonical gate re-run PASS 985/985 at report time.
- **Environment/measurement limits:** no live PiPlay process was launched at any point; all runtime-cost claims are static or test-backed; live cost/settling claims are deliberately absent and carried as M-001..M-004.
- **Exact next actions:** (1) owner review/ship decision on the working-tree remediation (separate explicit yes required for pushing protected `main`); (2) after ship, current-source Stable deploy + explicit authority → `profile M-004`, `profile M-002`, then M-001/M-003.

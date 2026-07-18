# Interim Deep Audit Report

- Schema: deep-audit/v1
- Audit slug: `piplay-runtime-2026-07-16`
- Repository: `D:\Development\DesktopApps\PiPlay`; baseline/remediation/current `8015ba4` / `99f9834` / `e16c0f3` on `main`, aligned with `origin/main`
- Runtime boundary: exact-source deployed Stable `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`, `v0.12.0 b35`, source `e16c0f3`; verified from a clean temporary checkout
- Report basis: D-001..D-008, T-001..T-008, F-001..F-005, V-001..V-006, M-001..M-005, R-001..R-003, and [COVERAGE.md](COVERAGE.md)
- Reading map: this report is the synthesis; [STATE.md](STATE.md) is resume truth; [MEASUREMENTS.md](MEASUREMENTS.md) owns measurement detail
- Last updated: 2026-07-18 Europe/Berlin
- Completion gate: static audit/remediation is complete; this remains interim while M-005 gathers passive natural-session evidence

## Verdict and confidence

- **Overall runtime assessment:** the inspected healthy paths are statically bounded and single-flight across timers, WebView bridges, logging, and one-Source/one-Popout ownership. Five supported Medium/Low findings were fixed, verified, landed, and released; no Critical/High finding remains. One mostly-idle Standard session plateaued after settling.
- **Most consequential multiplier:** persistent failure duration, not healthy cadence. The named-pipe retry loop and equivalent DOM-failure logging were the strongest multipliers; both are now bounded/coalesced.
- **M-002 conclusion:** the old 24-hour A/B logger is ended and closed. It failed after one valid four-hour Standard block because the sampler mishandled a transient WebView2 child. PiPlay and the later reboots did not cause the failure.
- **What the valid block says:** Standard used 0.3713% mean attributed process CPU and 0.1428% renderer CPU on 32 logical processors. Private bytes settled after the first hour (post-hour slope -0.0709 MiB/min); working set, handles, threads, GDI/USER, and process topology did not show retained growth. This is useful counter-evidence to an unbounded healthy Standard-session leak.
- **What it cannot say:** no Focused block produced accepted samples, active playback was not continuous, and callback duration/long tasks/JS heap/DOM nodes were unavailable. A Focused-minus-Standard conclusion would be invented.
- **Highest-value next evidence:** M-005 observes natural process trends every two minutes without forcing mode, playback, windows, or restarts. It is process-presence-gated and identity-split; no product behavior is changed.
- **Confidence:** high for the static findings/fixes and the one Standard session's plateau; low for Focused incremental cost. M-005 can surface operational anomalies but cannot establish actual Focused DOM activity or causal effect.

## Ranked findings

All findings are landed at/after `99f9834`, released in `e16c0f3`, and covered by V-001..V-006.

| Rank | ID | Severity | Confidence | Depth | Verification | Claim and resolution |
|---:|---|---|---|---:|---|---|
| 1 | F-001 | Medium | High mechanism | 4 | V-003/V-005/V-006 | Session-mismatched pipe identity plus immediate persistent-failure retry could loop at exception speed. Fixed with session-qualified identity, cancellation-aware 250 ms→30 s capped backoff, and recovery-summary logging. |
| 2 | F-002 | Medium | High | 4 | V-002/V-005/V-006 | A browser-data clear that exceeded the foreground timeout lost task ownership and allowed overlap. Fixed with raw-task single-flight ownership through terminal completion. |
| 3 | F-003 | Medium correctness | Medium-high | 4 | V-001/V-005/V-006 | Popout launch could proceed without acknowledged Source suppression, creating a temporary duplicate-playback window. Fixed with an explicit suppression acknowledgement and rollback on failure. |
| 4 | F-004 | Low | High | 4 | V-001/V-005/V-006 | Persistent DOM failure formatted/enqueued equivalent errors at polling cadence. Fixed with per-WebView/per-operation first-failure suppression and recovery summaries. |
| 5 | F-005 | Low | High | 4 | V-004/V-005/V-006 | Accent preview replaced unchanged resource pairs. Fixed with type/value/identity-preserving change-only replacement. |

No M-002 result supports a new finding. D-004 remains classified. M-005 supplies contextual trend/anomaly evidence only; a matched active measurement is still required for a Focused-cost conclusion.

## Runtime architecture and cost map

- **Startup/construction:** one primary per session via a `Local\` mutex; background named-pipe handoff; one Source Window; lazy shared WebView2 environment.
- **Timers/workers/queues:** Auto 250 ms and single-flight; Source suppression 1 s while Popout owns playback; Popout sync 250 ms and generation-guarded; Focused fallback 1 s and active-only; opacity probe 250 ms only under non-default settings; accent preview ≤30/s and coalesced; bounded 4,096-entry logging queue with batching/rotation.
- **Cardinality:** one app instance, one Source, at most one Popout. Healthy hot-path work is constant-bounded.
- **Concurrency:** WPF dispatcher owns window/native state; bridge calls use in-progress/generation guards; Focused appearance is latest-wins with at most one WebView call in flight; browser-data clear is task-owned single-flight.
- **I/O:** WebView2 IPC, atomic temp-flush-replace settings writes, batched local logs, and named-pipe IPC. PiPlay owns no network telemetry.
- **Lifecycle:** close/return invalidates generations, stops timers, disposes bridges before WebView2, removes handlers/scripts/subclasses/regions, cancels pipe work, and drains logs.

## Hot-path table

| Rank | Entry/trigger | Frequency/cardinality | Dominant work | Allocation/retention | I/O/blocking | Amplification | Evidence | Action |
|---:|---|---|---|---|---|---|---|---|
| 1 | Popout sync | 4/s × one Popout, single-flight | WebView state read/parse | transient | async IPC | generation bounded | T-001/V-001 | preserve |
| 2 | Focused DOM surface | events + active 1/s fallback × session | renderer callbacks/conditional DOM writes | heap/nodes unknown | renderer | session duration | T-004/M-002 | M-005 process subset |
| 3 | Auto detector | 4/s when enabled | URL preflight; conditional DOM read | negligible | async IPC | single-flight | coverage | none |
| 4 | Source suppression | 1/s while Popout owns playback | mute/pause reassert | negligible | async IPC | failure log side fixed | T-001/F-003/F-004 | preserve Q-1 guard |
| 5 | Accent preview | ≤30/s during drag | derive pairs/change-only apply | change-proportional | none | interaction scoped | T-007/F-005 | M-003 optional |
| 6 | Close/return | per user close | two required durable saves + bounded replay | full settings ×2 | disk flush ×2 | user bounded | T-005/R-001 | preserve |
| 7 | Pipe server | async idle; handoff/second instance | wait/read/dispatch | negligible | pipe IPC | failure capped | T-003/F-001 | none |
| 8 | Browser-data clear | user confirmed | WebView2 AllProfile clear | task owned | WebView2 disk | single-flight | T-002/F-002 | none |

## CPU, memory, and duplicate work

- Deterministic duplicate work removed: unchanged accent resource replacement (F-005) and equivalent failure-log formatting (F-004).
- Suspected duplicate work rejected: dual return saves are spec-required durability boundaries (R-001).
- No reachable super-linear CPU path was found.
- No leak is confirmed statically or by M-002. The Standard block's full-window private-byte rise was consistent with startup settling: 30-minute means rose to about 1,239–1,244 MiB, then settled around 1,217 MiB; the post-first-hour slope was negative.
- M-002 topology stayed exactly 10 processes/3 renderers across all 2,815 accepted rows; handles 8081→7916, threads 447→433, GDI 59→58, USER 42→42.
- M-005 distinguishes per-session settling from cross-session drift and never stitches across root identity, configured presentation, boot, or Stable identity. Configured presentation remains context, not proof of active Popout presentation.

## I/O, concurrency, and lifecycle

- Settings writes remain atomic/durable and user-bounded; the two return checkpoints are correctness-critical.
- Log storage is bounded and failure log volume is coalesced. The existing LibreHardwareMonitor feed is separate machine telemetry and remains untouched.
- M-002 failed in the sampler's churn-unsafe thread read, not in PiPlay. No M-002 task/process remains; the stale abort helper was avoided because it could overwrite newer settings. The first later unexpected reboot was about eight hours after the harness failure.
- M-005 hardens per-process reads and identity boundaries; its CPU remains a sampled-survivor lower bound. Full instrumentation and cleanup details are in [MEASUREMENTS.md](MEASUREMENTS.md).

## Measurement requirements

- **M-001 — persistent DOM failure host cost:** planned; still needs explicit fault-injection authority.
- **M-002 — Focused DOM incremental session cost:** closed partial/inconclusive. One Standard baseline accepted; zero Focused comparator rows.
- **M-003 — realized WPF accent-preview cost:** optional/planned.
- **M-004 — Popout lifecycle settling:** planned; no safe unattended open/close driver exists.
- **M-005 — natural-session trend/anomaly logging:** installed and last live-verified healthy on 2026-07-18. It samples exact Stable every 120 seconds only while PiPlay exists and retains 45 days. Acceptance requires three long sessions with at least two post-settling hours each; [MEASUREMENTS.md](MEASUREMENTS.md) owns the operational, privacy, and escalation contract.

M-005 does not replace renderer instrumentation for callback duration, long tasks, JS heap, DOM nodes, or actual Popout state. It can narrow general long-session process retention and surface associations; it cannot confirm or reject D-004.

## Preserve list and rejected hypotheses

Preserve:

- both durable settings checkpoints around Source return;
- the 1 Hz Source suppression guard, which enforces Q-1 recovery;
- single-flight flags/generation tokens on timers and bridge work;
- bounded logging queue, batching, rotation, and drop accounting;
- acknowledged suppression before Popout construction;
- atomic settings persistence and deterministic close/disposal order.

Rejected:

- R-001: dual return saves are duplicate work — rejected as required durability.
- R-002: shared WebView environment creation is concurrently hot — rejected for current one-Source wiring.
- R-003: DOM failure increases IPC call count above healthy cadence — rejected; only log work amplified, and F-004 fixed it.

## Staged optimization plan

1. **Done/released:** F-001..F-005 at `99f9834`, audit state at `19bcfe8`, exact-source Stable `e16c0f3` / `v0.12.0 b35`.
2. **Passive validation:** leave M-005 running without forced workload. Analyze only after the long-session acceptance gate. Treat threshold crossings as anomaly signals; promote nothing until a targeted matched measurement and the finding gate are satisfied.
3. **Optional focused measurements:** M-004, then M-001/M-003, only if their specific operational questions become valuable and authority is explicit.
4. **Architecture:** none justified by current evidence.

## Coverage and uncertainty

- Covered at Depth 3–5: startup/single-instance, Source initialization/navigation, Auto, Popout launch/suppression/sync, Focused static lifecycle plus Standard process baseline, appearance, native window state, return, persistence, privacy clear, logging, failure/recovery, and shutdown.
- Partially covered: Focused operational delta, callback/heap/node/frame behavior, repeated lifecycle settling, and compact shell (feature-gated/dormant).
- Remaining measurement limits: M-002 has no accepted Focused comparator and mostly idle playback; M-005 has uncontrolled workload and no actual Popout/playback state or callback/heap/node/frame attribution. Host correlation is contextual, not causal.
- Exact next action: continue `profile M-005` passively. At the acceptance gate, inspect per-session post-settling trends. If anomalous, `deepen D-004`/`deepen D-008` and design a targeted measurement; otherwise narrow general retention concern only and refresh this report.

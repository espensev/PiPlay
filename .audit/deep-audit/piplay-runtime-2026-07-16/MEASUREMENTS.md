# Measurements

Schema: deep-audit/v1  
Audit slug: `piplay-runtime-2026-07-16`  
Repository/current revision: baseline `8015ba4` on `main`; rerun plans against the eventual verified remediation revision  
Source/runtime boundary and authority: current source; measurement execution not authorized  
Dirty-state fingerprint/scope/exclusions/environment: see `STATE.md`  
Last updated: 2026-07-16 Europe/Berlin

### M-001 — Persistent DOM execution failure host cost

- Related: D-001 / T-001 / F-004 / R-003.
- Question: is five single-flight failed `ExecuteScriptAsync` calls/s materially expensive after repetitive logging is coalesced?
- Workload: current-source Stable, one Normal Popout, controlled persistent controller/renderer execution failure versus healthy matched playback.
- Metrics: PiPlay/WebView process-tree CPU, host IPC/task duration, allocation rate, log enqueue/write rate, UI dispatcher delay, recovery latency.
- Confirm materiality: sustained incremental process-group CPU at least 1 percentage point or failed-call work at least 5 ms CPU/s with non-overlapping repeated-run intervals.
- Reject materiality: incremental CPU below 0.5 points, failed-call work below 2 ms/s, stable memory, and no increased dispatcher lateness.
- Authority/blocker: requires current-source Stable deployment and explicit fault/profile authorization.

### M-002 — Focused DOM incremental session cost

- Related: D-004 / T-004.
- Design: matched Standard/Focused ABBA, three runs each, 5-minute warmup plus 20–30 minutes playback with controlled pointer and SPA watch/off-watch transitions.
- Metrics: renderer/process-group CPU, script/task duration by event/timer/mutation callback, long tasks, frame intervals, JS heap/nodes, and settling after navigation.
- Confirm materiality: Focused callbacks at least 5 ms CPU/s, at least 1 percentage point sustained renderer CPU, or heap/nodes continue growing after warmup/navigation.
- Reject materiality: under 0.5 CPU points, no callback family above 2 ms/s, no increased long-task/frame-loss rate, and heap/nodes plateau in every run.
- Authority/blocker: current-source Stable plus explicit profiling authority.

### M-003 — Realized WPF accent-preview cost and correction delta

- Related: D-007 / T-007 / F-005.
- Design: Source-only, Source + Standard, Source + Focused; five fixed 15-second accent and intensity sweeps per workload.
- Metrics: queued inputs/applies, resource replacements/apply, UI inclusive duration, dispatcher lateness, visible-preview latency, render intervals, allocated bytes/apply, Gen0 collections, Focused appearance IPC count.
- Confirm material baseline: p95 apply above 8.3 ms, p95 preview latency above 100 ms, or over-33.3-ms frames exceed idle by at least 3 points and 5% absolute.
- Reject material baseline: p95 apply below 4 ms, preview latency below 50 ms, slow-frame increase under 1 point, allocation below 0.5 MB/s, no correlated GC.
- Correction success: intensity replacement count falls from 18 to at most 4 below intensity 50 and at most 2 above it, plus at least 30% lower allocation or apply time.
- Authority/blocker: optional after static correction; current-source Stable and explicit interactive profiling authority.

### M-004 — Popout lifecycle settling

- Related: D-008 / T-008.
- Design: fresh process per variant; five warmups, 50 Standard cycles, 50 Focused cycles; sample before open and 5/15/30/60 seconds after close; separate 30–60 minute navigation/resize/DPI soak.
- Metrics: attributed process-tree private/working bytes, managed/JS heap, handles, GDI/USER objects, HWNDs, threads, child-process count/type, post-close CPU, and open/close latency.
- Confirm retention: cycles 11–50 settled slope lower bound above zero and at least 0.5 MB/cycle plus 25 MB cumulative, at least one handle/cycle plus 25, sustained GDI/USER growth, growing child-process count, or cycles 41–50 at least 25% slower than 6–15.
- Reject retention: 60-second slope below 0.2 MB/cycle and under 20 MB cumulative, handle drift under 10, zero GDI/USER net drift, stable process plateau, and latency degradation under 10%.
- Authority/blocker: deployed Stable is stale; requires current-source Stable and explicit repeated-lifecycle/soak authority.

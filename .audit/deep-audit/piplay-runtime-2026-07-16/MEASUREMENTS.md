# Measurements

Reading map: [REPORT.md](REPORT.md) is the synthesis; [STATE.md](STATE.md) is resume truth; this file owns measurement contracts, raw locations, and interpretations.

Schema: deep-audit/v1  
Audit slug: `piplay-runtime-2026-07-16`  
Repository/current revision: baseline `8015ba4`; verified remediation `99f9834`; current/released `e16c0f3` on `main`
Source/runtime boundary and authority: exact-source Stable `v0.12.0 b35` at `e16c0f3` passed verification from clean temporary checkout `D:\tmp\piplay-audit-e16c0f3-20260716`; M-002 and low-rate M-005 runtime profiling were explicitly authorized against the deployed Stable boundary
Dirty-state fingerprint/scope/exclusions/environment: see `STATE.md`  
Last updated: 2026-07-19 Europe/Berlin

| ID | Related item | Hypothesis | Status | Metrics | Thresholds | Result |
|---|---|---|---|---|---|---|
| M-001 | D-001 / F-004 | Persistent DOM failure may retain material host cost after log coalescing | Planned | process CPU, IPC/task duration, allocation, log rate, dispatcher delay | confirm >=1 CPU point or >=5 ms CPU/s | Not run |
| M-002 | D-004 / T-004 | Focused presentation may add material session CPU or retention | Partial, closed | attributed process/renderer CPU and resource settling | matched Focused comparator required | Inconclusive; Standard plateau only |
| M-003 | D-007 / F-005 | Realized WPF preview cost may remain material | Planned | apply latency, dispatcher delay, allocation, GC, replacement counts | see record | Not run |
| M-004 | D-008 / T-008 | Repeated Popout lifecycle may retain resources | Planned | post-close memory/handles/GDI/USER/processes/latency | see record | No safe driver |
| M-005 | D-004 / D-008 / M-002 | Natural long sessions can surface process-level drift or anomalies passively | Running | two-minute attributed CPU/resource samples by session; presentation is context only | >=3 long sessions; escalation rules below | Pending |

### M-001 — Persistent DOM execution failure host cost

- Related item: D-001 / T-001 / F-004 / R-003.
- Hypothesis: five single-flight failed `ExecuteScriptAsync` calls per second may remain materially expensive after repetitive logging is coalesced.
- Why static inspection is insufficient: WebView2 controller/renderer IPC and failure construction cost are implementation/runtime dependent.
- Workload: exact-source Stable, one Normal Popout, controlled persistent controller/renderer execution failure versus healthy matched playback.
- Metrics: PiPlay/WebView process-tree CPU, host IPC/task duration, allocation rate, log enqueue/write rate, UI dispatcher delay, and recovery latency.
- Confirmation threshold: sustained incremental process-group CPU at least 1 percentage point or failed-call work at least 5 ms CPU/s with non-overlapping repeated-run intervals.
- Rejection threshold: incremental CPU below 0.5 points, failed-call work below 2 ms/s, stable memory, and no increased dispatcher lateness.
- Authority/status: planned only; explicit fault-injection authority is still absent.

### M-002 — Focused DOM incremental session cost

- Related item: D-004 / T-004.
- Hypothesis: Focused document-lifetime observers, events, and fallback work materially increase renderer/process cost or prevent resource settling relative to Standard.
- Environment and provenance: deployed Stable `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`, `v0.12.0 b35`, source `e16c0f351a1e8f76a6ab396aa1eba079a521bf54`; 32 logical processors.
- Authorized plan: campaign `m002-24h-ab-20260716`; Standard, Focused, Focused, Standard, Standard, Focused; six four-hour blocks; five-minute warmup plus 14,100 seconds accepted per block; five-second samples.
- Metrics: attributed process-tree and renderer CPU; working/private bytes; handles; threads; process/renderer counts; root GDI/USER; sample gaps; LibreHardwareMonitor timestamp/cursor correlation.
- Instrumentation limit: callback-family duration, long tasks/frame intervals, JS heap, and DOM node counts were not exposed. This profile could only evaluate the process/renderer CPU and process-retention subset.
- Original full-profile thresholds, not evaluable by this harness: Focused callbacks at least 5 ms CPU/s, at least 1 percentage point sustained renderer CPU above Standard, or heap/nodes continuing to grow after warmup/navigation; rejection required a Focused effect below 0.5 CPU points plus quiet callback, long-task, frame-loss, heap, and node metrics.
- Executed process-level decision rule: a matched accepted Focused block was required before estimating any Focused-minus-Standard CPU or retention effect. No such block exists, so the result is inconclusive regardless of the Standard plateau.
- Raw artifacts: `D:\tmp\piplay-metrics-e16c0f3\m002-24h-ab-20260716`; authoritative control files are `campaign-manifest.json`, `campaign-result.json`, `campaign-error.txt`, and `m002-24h-r1-standard.samples.csv`.
- Execution status: **partial, failed, closed**. Campaign ran 2026-07-16 14:11:20.167Z–18:12:40.397Z and completed 1/6 planned blocks.
- Accepted Standard block:
  - Run/PID: `m002-24h-r1-standard`, exact Stable PID 64176, initialized Standard at local 16:11:43.898.
  - Accepted window: 2026-07-16 14:16:45.374Z–18:11:44.501Z; 2,815 measured rows over 3h54m59.127s, plus 60 warmup rows.
  - Continuity: maximum sample gap 5.029s; zero gaps above the ten-second rejection boundary; `processExitedEarly=false`.
  - CPU: process-group mean 0.3713%, p50 0.31%, p95 0.7509%, p99 0.89%, max 1.7513%; renderer mean 0.1428%, p95 0.4296%. Thirty-minute group means were stable (0.3814% first segment, 0.3544% last).
  - Topology: all accepted rows had 10 attributed processes and 3 renderers.
  - Private bytes: mean 1,199.924 MiB; first/last 1,000.625/1,212.242 MiB. Thirty-minute means were 1064.9, 1161.6, 1223.4, 1239.0, 1235.3, 1244.3, 1216.5, and 1217.6 MiB. The full-window +0.5901 MiB/min slope is dominated by startup settling; OLS after the first hour is -0.0709 MiB/min.
  - Other resources: working set 1309.16 -> 1054.195 MiB; handles 8081 -> 7916; threads 447 -> 433; GDI 59 -> 58; USER 42 -> 42. No retained-growth signal survived settling.
- Rejected Focused block: `m002-24h-r2-focused` initialized Focused at local 20:12:13.737, but produced only sample zero during warmup and no summary. It contributes no accepted comparator.
- Failure: `Measure-PiPlaySession.ps1:166` evaluated `$_.Threads.Count` after process discovery. A transient WebView2 child exited or became inaccessible, and strict mode raised `The property 'Count' cannot be found on this object`. This was a harness failure, not a PiPlay exit or reboot.
- Whole-system correlation: the accepted join window used `LibreHardwareMonitorLog-2026-07-16-2.csv`, local 16:16:45.624–20:11:44.159, with 14,092 roughly one-second rows, maximum gap 2.134s, none above five seconds. Host CPU/GPU/RAM load varied materially, so this is continuity/context evidence rather than PiPlay attribution.
- Workload confounder: PiPlay's logged `lastKnownSeconds` advanced only about 360 seconds across the four-hour block, so continuous active playback was not established. CPU represents mostly healthy session/idle steady state.
- Restart boundaries: M-002 failed at local 20:12:40 on July 16. The first later unexpected reboot began at 04:17:50 on July 17, roughly eight hours later; restarts did not cause the measurement failure and no samples are stitched across them.
- Cleanup: the result records restored/original settings SHA256 `772B84016EF8C50FBC2B74967A3B09ED397273BEB8845BA63547F8D22B1896D9` and relaunched PID 80488. No M-002 process or scheduled task remains. Newer live settings were preserved during closeout.
- Interpretation: **narrowed/inconclusive**. This Standard session is counter-evidence to unbounded healthy-session resource growth after settling. With no accepted Focused block, no Focused-minus-Standard effect or callback-level conclusion is valid.
- Next action: do not rerun the forced campaign. Use M-005 natural-session evidence for the process-level subset; use purpose-built renderer instrumentation only if callback-level attribution becomes necessary.

### M-003 — Realized WPF accent-preview cost and correction delta

- Related item: D-007 / T-007 / F-005.
- Hypothesis: realized DynamicResource consumers and frozen-brush creation may still make high-rate preview material despite change-proportional replacement.
- Workloads: 30 seconds hue-only at intensity 100; 30 seconds intensity sweep at fixed hue; 30 seconds mixed hue/intensity, each on the old and corrected implementations after warmup.
- Metrics: apply-duration p50/p95/p99, dispatcher delay, UI-thread CPU, allocation rate/bytes per apply, Gen0/1/2 counts, realized frame time, resource replacement count.
- Confirmation threshold: p95 apply above 8.3 ms, p95 preview latency above 100 ms, or over-33.3-ms frames exceed idle by at least 3 points and 5% absolute.
- Rejection threshold: p95 apply below 4 ms, preview latency below 50 ms, slow-frame increase under 1 point, allocation below 0.5 MB/s, and no correlated GC.
- Correction success: replacement count falls from 18 to at most 4 below intensity 50 and at most 2 above it, plus at least 30% lower allocation or apply time.
- Authority/status: optional and planned; explicit interactive profiling authority remains absent.

### M-004 — Popout lifecycle settling

- Related item: D-008 / T-008.
- Hypothesis: repeated Popout open/close or prolonged navigation/resize/DPI activity may leave process, WebView2, handle, or native resources unsettled.
- Design: fresh process per variant; five warmups, 50 Standard cycles, 50 Focused cycles; sample before open and 5/15/30/60 seconds after close; separate 30–60 minute navigation/resize/DPI soak.
- Metrics: attributed private/working bytes, managed/JS heap, handles, GDI/USER, HWNDs, threads, child-process count/type, post-close CPU, and open/close latency.
- Confirmation threshold: cycles 11–50 settled slope lower bound above zero and at least 0.5 MB/cycle plus 25 MB cumulative, at least one handle/cycle plus 25, sustained GDI/USER growth, growing child-process count, or cycles 41–50 at least 25% slower than 6–15.
- Rejection threshold: 60-second slope below 0.2 MB/cycle and under 20 MB cumulative, handle drift under 10, zero GDI/USER net drift, stable process plateau, and latency degradation under 10%.
- Authority/status: planned. `scripts/Test-UiSmoke.ps1` does not drive Popout lifecycle, and Windows foreground activation was unavailable; no unsafe alternate driver was created.

### M-005 — Low-rate natural-session process trend and anomaly logging

- Related item: D-004 / T-004 / D-008 / M-002.
- Hypothesis: natural long-running PiPlay sessions may expose post-settling process/resource drift or recurring CPU anomalies worth a narrower targeted measurement.
- Why M-002 is insufficient: it produced only one Standard block; later natural sessions were not sampled. Static inspection cannot establish renderer cost or day-scale settling.
- Environment/revision: task accepts only `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`; each newly detected session records Stable version, build, source commit, boot identity, and root PID(s). Never combine different boots or executable identities.
- Workload: natural user sessions. No URL, playback content, UI input, restart, or mode is forced. Configured Standard/Focused presentation is a contextual boolean only; the logger does not establish that a Popout, active playback, or the Focused DOM surface was actually active.
- Historical instrumentation: `C:\ProgramData\PiPlayPassiveRuntime\PiPlayPassiveLogger.ps1`, main scheduled task `PiPlay-Passive-Runtime-Logger`, independent 15-minute `PiPlay-Passive-Runtime-Watchdog`, 120-second interval, process-presence-gated rows, daily rotation, and 45-day retention. Both tasks were removed on 2026-07-19 after the interactive PowerShell action opened a visible Windows Terminal at sign-in.
- Metrics/units: attributed process-group and renderer CPU percentage normalized across 32 logical processors; private/working MiB; handles; threads; root/process/renderer/GPU counts; configured presentation; timestamps and boot/build/process-creation identity. CPU is a sampled-survivor lower bound; children created after the prior sample contribute their observed lifetime CPU, while children that start and exit entirely between samples are absent.
- Baseline: M-002 Standard mean group CPU 0.3713%, mean renderer CPU 0.1428%; post-first-hour private slope -0.0709 MiB/min; 10 processes/3 renderers.
- Acceptance: at least three sessions with at least two accepted hours after the first settling hour. Split sessions on boot, root identity, configured presentation, or Stable identity change. Do not treat short sessions as rejection evidence.
- Escalation threshold: post-settling private slope above 0.2 MiB/min with at least 50 MiB net growth in two long sessions, handle growth at least 25 in two sessions, topology growth, or recurring process/renderer CPU at least 1 percentage point above the M-002 Standard context. A crossing is an anomaly/association, not confirmation of D-004 or a leak.
- Narrowing threshold: post-settling private slope at or below 0.1 MiB/min with under 25 MiB net drift, handle drift under 10, stable topology, and no recurring CPU anomaly in every accepted session. This can narrow general process-retention concern only; it cannot reject Focused callback, heap/node, frame, or causality hypotheses.
- Confounders: actual Popout/activity state is unknown; configured presentation may not equal active presentation. Natural content/playback state, host load, page/network activity, settings changes, time of day, and Stable updates are uncontrolled. Use LibreHardwareMonitor timestamps only for context; do not causally attribute host sensor changes.
- Safety/cost/cleanup: retired. The task-based collector is no longer authorized because an interactive console-host action can surface a Terminal window even when PowerShell receives `-WindowStyle Hidden`. Scripts and logs remain protected in place as evidence, but no task may relaunch them.
- Execution status: **retired on verified machine `snd-desk` on 2026-07-19 Europe/Berlin**. `PiPlay-Passive-Runtime-Logger` and `PiPlay-Passive-Runtime-Watchdog` were stopped and removed, the exact blank Terminal process was closed, and verification found zero remaining PiPlay logger tasks or processes.
- Raw artifact location: `C:\ProgramData\PiPlayPassiveRuntime\logs`.
- Observed data: no PiPlay session has occurred since installation; only logger start/idle self-test evidence exists.
- Interpretation/next action: no further passive collection is expected. Existing rows may be inspected as historical context only. Any future measurement must use a non-interactive, non-console-host design and receive fresh authorization before installation.

# Execution Traces

Schema: deep-audit/v1  
Audit slug: `piplay-runtime-2026-07-16`  
Repository/current revision: `D:\Development\DesktopApps\PiPlay` at baseline `8015ba4` on `main`  
Source/runtime boundary and authority: current source; user authorized focused product remediation after the read-only discovery pass; runtime measurement remains plan-only  
Dirty-state fingerprint/scope/exclusions/environment: see `STATE.md`  
Last updated: 2026-07-16 Europe/Berlin

### T-001 — Popout suppression and persistent DOM execution failure

- Discovery: D-001.
- Entry/workload: launch one current Normal Popout and keep it open while Source and/or Popout `ExecuteScriptAsync` calls fail persistently.
- Path: `MainWindow.StartVideoPopoutAsync` reads state and resolves a target, awaits `SuppressPlaybackAsync`, starts the 1 Hz Source guard, and constructs the Popout; successful Popout navigation starts `PlayerWindow`'s 250 ms sync timer. Both bridge helpers catch host exceptions and return `null`/a completed `Task`, so the launch caller cannot distinguish suppression failure from success.
- Frequency/cardinality: one Source command/s plus four Popout reads/s, one Popout maximum, and single-flight guards on both timers. Slow calls reduce the attempt rate; no call overlap occurs.
- Failure/lifecycle boundary: navigation stops/restarts Popout sync by generation; return/close stops both timers; logging is queued and disk retention is bounded. No WebView `ProcessFailed` owner converts a persistent controller/renderer failure into an explicit recovery state.
- Cost/error propagation: failure does not raise IPC cadence above the healthy design, but it can allocate/format/enqueue up to five equivalent exceptions per second. More importantly, the first swallowed suppression failure lets Popout construction proceed before Source playback ownership was transferred.
- Counter-evidence: 1 Hz suppression is an intentional Q-1 recovery guard; one-player and bounded-log policies cap fan-out/retention.
- Result: promotes F-003 (suppression acknowledgement, Medium) and F-004 (repeated failure logging, Low). Material host IPC/CPU cost remains M-001.

### T-002 — Browser-data clear timeout loses operation ownership

- Discovery: D-002.
- Entry/workload: confirm Clear browser data when the WebView2 `AllProfile` task takes longer than 30 seconds, then reopen Settings and confirm again.
- Path: `SettingsWindow` records the action and closes; `MainWindow` starts `PerformClearBrowserDataAsync` fire-and-forget; `PrivacyService` returns the profile clear task; `WaitAsync` bounds only the foreground wait. On timeout, the copy says the clear continues, but `finally` resets both action flags and re-enables commands without retaining the task.
- Frequency/cardinality: foreground attempts are user-confirmed and at least timeout/prompt bounded, but outstanding underlying tasks have no static cap during a persistent slow/hung dependency.
- Failure/lifecycle boundary: there is no completion observer, late-fault owner, task identity check, cancellation API, or deduplication. WebView2 documentation exposes a `Task`-returning clear API without a cancellation-token overload; downstream internal serialization is undocumented.
- Counter-evidence: each foreground wait is bounded and normal clears are expected to be short; actual disk concurrency may be serialized inside WebView2.
- Result: promotes F-002 (Medium). The supported claim is multiple outstanding API tasks, not unmeasured concurrent disk I/O.

### T-003 — Single-instance pipe identity and retry loop

- Discovery: D-003.
- Entry/workload: start a primary in more than one Windows logon session for the same channel, or otherwise make named-pipe construction/wait/read fail persistently.
- Path: a `Local\` mutex elects one primary per session; the pipe name contains only the channel. Each primary starts a background loop which constructs one machine-namespace pipe, awaits one connection/read, dispatches synchronously to WPF, and repeats. Every non-cancellation exception is logged and retried immediately.
- Frequency/cardinality: idle cost is negligible because wait is asynchronous. A synchronous persistent failure has no delay, retry cap, or scheduler-yield boundary and can iterate at exception speed in one ThreadPool task.
- Failure/lifecycle boundary: app exit cancels wait/read cleanly; the loop task is not joined. Cross-session collision is a credible inferred constructor-failure trigger because mutex and pipe scopes differ; it was not induced.
- Counter-evidence: one loop exists per primary and logging storage is bounded, but formatting/enqueue/drop accounting remains per iteration.
- Result: promotes F-001 (Medium). Session-qualified naming removes the credible boundary mismatch; cancellation-aware capped backoff contains squatting/ACL/resource failures.

### T-004 — Focused presentation document-lifetime work

- Discovery: D-004.
- Entry/workload: long Standard-versus-Focused playback with pointer activity, ads, and YouTube SPA watch/off-watch transitions.
- Path: Focused installs media listeners, a player-class observer, a whole-document child-list observer, global input listeners, and an active-only 1-second fallback; host appearance/configuration is single-flight/latest-wins.
- Safeguards: media/player observers detach on replacement/deactivation, writes are conditional, controls are cached, pointer reveal is throttled to 8/s, fallback stops off-watch, and document tokens/generations prevent stale host work.
- Unknown cost: mutation/event rates, callback CPU, renderer allocation, heap/node settling, and ad/SPA distributions require renderer-attributed measurement.
- Result: M-002 only; no static correction.

### T-005 — Dual Popout-return durable settings checkpoints

- Discovery: D-005.
- Entry/workload: close/return the single Popout.
- Path: `Player_OnClosed` copies placement/preferences, performs a durable settings save, awaits fallible Source return scripting/navigation, then saves again. Each save serializes and atomically flushes/replaces the file.
- Frequency/cardinality: exactly two user-bounded checkpoints per normal return; no retry loop or overlap.
- Authority/counter-evidence: product spec section 14 explicitly requires saving before and after source-return scripting so placement survives script failure. Reentrant Pin/Auto changes may also occur across the await.
- Result: rejected as duplicate work in R-001; preserve both writes.

### T-006 — Shared WebView environment creation reachability

- Discovery: D-006.
- Local mechanism: `EnsureCreatedAsync` assigns `_environment` only after `CreateAsync`, so genuinely concurrent callers would create more than one task.
- Current reachability: App owns one service/Source; the Loaded path is the cold caller; Popout is gated by `_browserReady` and consumes the completed environment; Retry is exposed only after a completed failure and collapses its panel before awaiting.
- Frequency/cardinality: one cold creation, sequential retry after terminal failure, then constant-time reuse. No credible current parallel caller was found.
- Result: rejected for the current runtime in R-002. Reopen if ownership/callers expand.

### T-007 — Accent preview resource replacement

- Discovery: D-007.
- Entry/workload: Settings color/intensity drag, coalesced to at most one apply every 33 ms.
- Path: `ThemeColors.DeriveAccentSet` computes nine pairs; `ApplyAccentOnly` unconditionally replaces 18 dictionary entries with nine new frozen brushes and nine colors; realized `DynamicResource` consumers re-resolve each replacement.
- Deterministic duplicate work: an identical apply changes no semantic value. Intensity-only movement changes only ShellTint and ChromeGlyph; above intensity 50 the glyph is saturated, so normally only ShellTint changes, while the other 14–16 entries are still replaced.
- Counter-evidence: preview is bounded to about 30 Hz, deduplicated before apply, and user-interaction scoped; actual frame/user impact is not measured.
- Result: promotes F-005 (Low deterministic allocation/invalidation). End-user materiality and improvement size remain M-003.

### T-008 — Popout lifecycle resource settling

- Discovery: D-008.
- Entry/workload: repeated Standard/Focused open-close cycles and 30–60 minute navigation/resize/DPI soak.
- Static ownership: close invalidates initialization generations, stops all four timers, disposes bridges before WebView, removes scripts/handlers/subclasses/native regions, and observes one-player cardinality. Real-HWND tests cover native resize-state removal.
- Unknown cost: process-tree private bytes, JS/managed heaps, handles/GDI/USER objects, renderer reuse, threads, and close-settled slopes were not measured on current source.
- Result: M-004 only; no static leak correction.

# Discovery Queue

Schema: deep-audit/v1  
Audit slug: `piplay-runtime-2026-07-16`  
Repository root: `D:\Development\DesktopApps\PiPlay`  
Audited source/runtime boundary: current source `8015ba4`; deployed Stable `d11eac5` is stale for current-source testing  
Baseline/current revision: `8015ba4`  
Branch/worktree: `main`, primary worktree, ahead 2  
Authority: focused product remediation authorized; audit-state writes allowed; runtime measurement plan-only  
Dirty-state fingerprint: pre-existing untracked parked-tree review plus this audit state and dated remediation design/plan  
Scope/exclusions/environment: see `STATE.md`  
Last updated: 2026-07-16 Europe/Berlin

| Priority | ID | Categories | Location | Suspected multiplier | Depth | Status | Next mode |
|---|---|---|---|---|---:|---|---|
| P1 | D-001 | CPU, WebView IPC, retry, logging, failure | `MainWindow.SourceSuppressionTimer_Tick`; `PlayerWindow.SyncTimer_Tick`; `YouTubeDomBridge.Execute*Async` | 1/s + 4/s x failure duration | 4 | Fixed/verified: F-003/F-004, V-001/V-005/V-006; M-001 remains | profile only if authorized |
| P1 | D-002 | I/O, concurrency, slow dependency, lifecycle | `MainWindow.PerformClearBrowserDataAsync`; `PrivacyService.ClearBrowserDataAsync` | one new clear per timeout while prior clear may still run | 4 | Fixed/verified: F-002, V-002/V-005/V-006 | none |
| P1 | D-003 | CPU, logging, retry, background task | `App.StartPipeServer` | immediate retry x persistent pipe failure duration | 4 | Fixed/verified: F-001, V-003/V-005/V-006 | none |
| P1 | D-004 | CPU, browser DOM, events, retained listeners | `YouTubeDomBridge.BuildPlayerFirstSurfaceScript` | document mutations + media events + 1/s fallback + pointer activity x session duration | 4 | Classified: M-002 | `profile` when authorized |
| P1 | D-008 | memory, handles, native/WebView lifecycle | Popout open/close ownership across `PlayerWindow`, bridges, HWND helpers, WebView2 | resources per cycle x repeated cycles/soak duration | 4 | Classified: M-004 | `profile` when authorized |
| P2 | D-005 | filesystem I/O, UI latency, duplicate work | `MainWindow.Player_OnClosed`; `SettingsService.AtomicWrite` | two durable full-settings writes per Popout close | 4 | Rejected: R-001 | none |
| P2 | D-006 | startup, concurrency, allocation, WebView profile | `WebViewEnvironmentService.EnsureCreatedAsync`; retry path in `MainWindow` | concurrent retries x environment creation cost | 4 | Rejected current reachability: R-002 | reopen on ownership change |
| P2 | D-007 | CPU, allocation, WPF invalidation, WebView IPC | accent preview pipeline and `ThemeResourceApplier.ApplyAccentOnly` | up to ~30 frames/s x 18 resource replacements + realized consumers | 4 | Fixed/verified: F-005, V-004/V-005/V-006; M-003 remains | optional profile |

## Records

### D-001 - Persistent DOM failure keeps host polling and logging hot

- Location: `src/PiPlay/MainWindow.xaml.cs:1671` `StartSourceSuppressionGuard`; `:1690` `SourceSuppressionTimer_Tick`; `src/PiPlay/PlayerWindow.xaml.cs:171` sync timer construction; `:708` `SyncTimer_Tick`; `src/PiPlay/Services/YouTubeDomBridge.cs:906` `ExecuteAsync`; `:922` `ExecuteVoidAsync`; `src/PiPlay/Services/LoggingService.cs:29-33` queue/file bounds.
- Runtime entry or suspected path: open a normal-mode Popout -> Source suppression calls WebView script once per second while Popout sync reads player state four times per second.
- FACT evidence: both timers are reachable while a Popout is open; both are single-flight; WebView execution exceptions are caught and logged, after which the timer remains enabled; logging is asynchronous, bounded to 4,096 queued entries, batches writes, and rotates at about 1 MB plus one backup.
- Cost/amplification model: `5 host IPC attempts/s x duration of persistent renderer/document failure`, plus up to five formatted/enqueued log entries per second and recurring batched file writes.
- Safeguards/counter-evidence: no call overlap inside either timer; navigation stops Popout sync until completion; URL preflight skips non-watch pages; log queue and on-disk retention are bounded; one-player policy caps fan-out.
- Current depth: 4 - callers, operation-specific failure semantics, timer lifecycles, logging bounds, launch rollback, and independent review were inspected; live persistent-failure cost was not measured.
- Unknowns: which WebView failure modes return quickly and persist; actual CPU/IPC/log-write cost; whether navigation/renderer recovery naturally stops the state; acceptable retry cadence.
- Disposition: F-003/F-004 fixed and verified by V-001/V-005/V-006; broad IPC amplification rejected by R-003.
- Recommended next mode: M-001 profile only after current-source deployment and explicit live fault/profile authority.

### D-002 - Timed-out profile clears can overlap

- Location: `src/PiPlay/MainWindow.xaml.cs:1269-1331` `PerformClearBrowserDataAsync`; `src/PiPlay/Services/PrivacyService.cs:62-66`.
- Runtime entry or suspected path: user confirms Clear browser data -> `AllProfile` clear -> a 30-second `WaitAsync` timeout returns control while the underlying WebView2 clear may continue -> `finally` re-enables the action.
- FACT evidence: the timeout does not cancel the underlying task; the UI explicitly says it may finish in the background; `_privacyActionInProgress` and `_clearingBrowserData` are reset after timeout; a later user action can start another clear on the same profile.
- Cost/amplification model: `number of repeated post-timeout confirmations x concurrent AllProfile clears x profile/cache size and storage latency`.
- Safeguards/counter-evidence: each foreground wait is bounded to 30 seconds; re-entry is blocked while that wait is active; initiation requires explicit user confirmation; normal clears are expected to finish in 1-2 seconds.
- Current depth: 4 - UI owner, concrete WebView2 call, foreground/background lifetime, completion races, live Settings state, and independent review inspected; WebView2 internal scheduling remains unmeasured.
- Unknowns: whether WebView2 internally serializes/deduplicates clears; whether a late first completion races navigation or a second clear; shutdown ownership of late tasks.
- Disposition: F-002 fixed and verified by V-002/V-005/V-006; no further static product work.
- Recommended next mode: none; measure internal WebView scheduling only if a separate operational question requires it.

### D-003 - Named-pipe server has an immediate persistent-failure retry loop

- Location: `src/PiPlay/App.xaml.cs:117-148` `StartPipeServer`; `src/PiPlay/Services/LoggingService.cs`.
- Runtime entry or suspected path: primary instance startup -> background `Task.Run` -> create/wait/read pipe in a loop -> any non-cancellation exception is logged and retried immediately.
- FACT evidence: the catch block contains no delay, backoff, failure-state latch, or retry cap; normal operation blocks in `WaitForConnectionAsync`; cancellation exits cleanly; the mutex limits the app to one primary server.
- Cost/amplification model: `exception rate permitted by pipe construction/wait failure x failure duration`, with one log enqueue per iteration.
- Safeguards/counter-evidence: ordinary idle cost is near zero because wait is asynchronous; only one server loop exists per app instance; log queue and file retention are bounded.
- Current depth: 4 - startup registration, session namespace, attempt loop, cancellation variants, retry/reset, logging sink, and independent review inspected; no live failure was induced.
- Unknowns: practical Windows failures that repeat synchronously; scheduler yield characteristics of the thrown async path; whether a failed pipe server should disable handoff or retry slowly.
- Disposition: F-001 fixed and verified by V-003/V-005/V-006.
- Recommended next mode: none; payload/time-limit hardening is a separate residual.

### D-004 - Focused surface still has session-long DOM observation costs

- Location: `src/PiPlay/Services/YouTubeDomBridge.cs:601-633`, `:667-832`; `src/PiPlay/Services/PlayerFirstSurfaceBridge.cs`.
- Runtime entry or suspected path: Focused Popout document -> media event listeners, player-class observer, whole-document child-list observer, global input listeners, active-only one-second fallback timer, and host appearance/configuration bridge.
- FACT evidence: remediation caches controls, performs conditional DOM writes, binds media events, narrows the ad observer, throttles pointer reveal to 125 ms, keeps the fallback timer active-only at one second, and uses single-flight/latest-wins host appearance IPC. The whole-document observer and global input listeners remain for the document lifetime by design.
- Cost/amplification model: `YouTube mutation batches + media timeupdate/state events + pointer events + 1 fallback tick/s`, multiplied by watch-session duration; callbacks are mostly guarded but not free.
- Safeguards/counter-evidence: one document-created script; child frames exit; overlay mutations are ignored; inactive state avoids most update work; no unconditional `innerHTML` churn; navigation tokens and disposal are guarded.
- Current depth: 4 - document triggers, listener/observer teardown, SPA/off-watch behavior, active gating, host pump, and adversarial counter-evidence inspected; real event rates and renderer CPU are unmeasured.
- Unknowns: YouTube mutation-batch rate in Standard versus Focused, callback CPU distribution, renderer allocation/retention, behavior over SPA navigation and ads.
- Disposition: no static finding; M-002 records the matched Standard-vs-Focused measurement.
- Recommended next mode: `profile M-002` only when authorized.

### D-005 - Popout return performs the same durable settings write twice

- Location: `src/PiPlay/MainWindow.xaml.cs:1708-1748` `Player_OnClosed`; `src/PiPlay/Services/SettingsService.cs:60-71`, `:112-125`.
- Runtime entry or suspected path: Popout close -> copy placement/preferences into `_settings` -> synchronous atomic save with flush-to-disk -> await Source return scripting/navigation -> save `_settings` again.
- FACT evidence: both saves sanitize and serialize the complete settings object, create/flush a temp file durably, and replace the live file; no `_settings` mutation is visible between the two saves inside `Player_OnClosed`/`ApplyReturnActionAsync`.
- Cost/amplification model: `2 full JSON serializations + 2 durable temp-file flush/replace operations per close`; excess is one full durable write per lifecycle.
- Safeguards/counter-evidence: close frequency is user-bounded; the first save deliberately precedes fallible Source scripting so placement survives; Source commands and Settings are restricted during return, reducing legitimate mid-await mutation.
- Current depth: 4 - caller, downstream durable write, intervening/reentrant return behavior, shutdown, and authoritative spec requirement inspected.
- Unknowns: whether any dispatcher-reentrant path can legitimately mutate settings between saves; actual slow-storage latency.
- Disposition: rejected by R-001; preserve both durability checkpoints.
- Recommended next mode: none.

### D-006 - Shared WebView environment creation is not single-flight

- Location: `src/PiPlay/Services/WebViewEnvironmentService.cs:50-69`; `src/PiPlay/MainWindow.xaml.cs:174-248`.
- Runtime entry or suspected path: Source load or Retry -> `EnsureCreatedAsync`; `_environment` is assigned only after `CreateAsync` completes; concurrent retry invocations can each pass the null check.
- FACT evidence: there is no cached in-flight task, semaphore, or UI initialization flag; Retry is an `async void` click and remains a possible repeated user event; the normal Player path reuses the already-created environment.
- Cost/amplification model: `concurrent initialization calls x WebView environment startup/profile initialization cost`, potentially followed by duplicate handler attachment/navigation in the caller.
- Safeguards/counter-evidence: ordinary startup has one caller; Retry is visible after a failure; Popout creation is gated on `_browserReady`; successful `_environment` reuse is constant-time.
- Current depth: 4 - service, every caller, Retry visibility, Source/Popout gates, and current ownership/cardinality inspected.
- Unknowns: whether WPF input can re-enter before Retry awaits; whether WebView2 internally coalesces or rejects same-folder creation; partial-initialization handler duplication.
- Disposition: rejected for current reachability by R-002; reopen if parallel ownership is introduced.
- Recommended next mode: none.

### D-007 - Accent preview remains a realized-WPF measurement gap

- Location: `src/PiPlay/MainWindow.xaml.cs:65-72`, `:1041-1126`; `src/PiPlay/Theme/ThemeResourceApplier.cs:54-76`; Focused appearance pump.
- Runtime entry or suspected path: color wheel/intensity drag -> latest pair coalesced by a 33 ms Dispatcher timer -> replace 18 accent resource entries -> realized DynamicResource consumers update -> Source appearance and optional Popout appearance refresh.
- FACT evidence: input is deduplicated and coalesced to about 30 frames/s; accent-only application replaces nine brush/color pairs; brushes are frozen; Focused WebView appearance IPC is separately deduplicated and latest-wins. A prior isolated probe excluded realized WPF consumers and therefore did not measure the dominant invalidation cost.
- Cost/amplification model: `up to 30 preview frames/s x 18 dictionary replacements x realized DynamicResource dependency fan-out`, plus bounded Source/Popout imperative work.
- Safeguards/counter-evidence: no full palette/radii/elevation apply per pointer event; duplicate values are skipped; final value is flushed; preview exists only while Settings interaction is active.
- Current depth: 4 - trigger, cadence, exact per-pair derivation, consumer mechanism, conditional-write correction, WPF identity/value tests, and downstream Popout pump inspected; realized frame time/allocation are unmeasured.
- Unknowns: UI-thread frame delay, allocation rate, invalidation fan-out with both windows open, and whether 30 Hz is perceptibly or energetically material.
- Disposition: F-005 fixed and verified by V-004/V-005/V-006; M-003 records optional realized-WPF profiling.
- Recommended next mode: `profile M-003` only when authorized.

### D-008 - Repeated Popout lifecycle settling has not been validated

- Location: `PlayerWindow` timer/bridge/WebView cleanup; `PlayerFirstSurfaceBridge.Dispose`; `PlayerSurfaceDragBridge.Dispose`; `WindowOpacityApplier` and `BorderlessWindowHelper` HWND cleanup.
- Runtime entry or suspected path: create/show/navigate/configure Popout -> use timers, document scripts, native subclasses/regions, renderer processes -> close/dispose -> repeat.
- FACT evidence: code stops all four Popout timers, disposes bridges before WebView2, removes document scripts/handlers, removes native subclass state on `WM_NCDESTROY`, and observes the single-player invariant. The prior audit only sampled a short Standard run and explicitly left 50-cycle and 30-60 minute Focused/SPA/resize settling unmeasured.
- Cost/amplification model: `(retained managed/native/WebView resource per cycle, if any) x open/close cycles`, plus steady renderer CPU/memory over soak duration.
- Safeguards/counter-evidence: deterministic cleanup is extensive and 959 current tests pass; no confirmed leak exists; only one Popout can be live.
- Current depth: 4 - ownership, disposal, failure, navigation generation, native cleanup tests, and prior passive sample inspected; live settling validation is absent.
- Unknowns: process-tree private bytes/working set settling, handles/GDI/USER counts, renderer process reuse, delayed COM/WebView reclamation, SPA and DPI/resize effects.
- Disposition: no static finding; M-004 records the current-source cycle/soak measurement.
- Recommended next mode: `profile M-004` only after current-source deployment and explicit runtime authority.

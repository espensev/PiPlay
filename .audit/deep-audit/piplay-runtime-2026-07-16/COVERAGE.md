# Coverage Map

Schema: deep-audit/v1  
Audit slug: `piplay-runtime-2026-07-16`  
Repository/current revision: `D:\Development\DesktopApps\PiPlay` at `8015ba4` on `main`  
Source/runtime boundary: current source; deployed Stable `d11eac5` is two commits stale  
Authority: focused product remediation authorized; audit-state writes allowed; measurement plan-only  
Dirty-state fingerprint/scope/exclusions/environment: see `STATE.md`  
Last updated: 2026-07-16 Europe/Berlin

| Runtime area | Entry point | Evidence inspected | Depth | Status | Gap | Next mode |
|---|---|---|---:|---|---|---|
| Process startup | `App.OnStartup` | theme/settings load, mutex, pipe server, MainWindow creation | 3 | Mapped | pipe failure behavior | `trace D-003` |
| Single-instance handoff | `StartPipeServer`, `TrySendToExistingInstance` | wait/read/dispatcher/cancel/error loop, session namespace | 4 | F-001 fixed; V-003/V-005/V-006 | live failure not induced | none |
| Shared WebView environment | `EnsureCreatedAsync` | directory/options/create/reuse and every caller | 4 | R-002, current path bounded | future ownership expansion | reopen on change |
| Source initialization/navigation | `InitializeBrowserAsync`, Core events | awaits, handlers, navigation, close guards, Retry visibility | 4 | Mapped/guarded | live runtime failure | optional fault test |
| Auto detector | `_autoTimer`, `AutoTimer_Tick` | 250 ms cadence, cheap preflight, single-flight DOM read, lifecycle gates | 3 | Guarded | live cadence cost not measured; expected low | revisit after P1 |
| Popout launch | `StartVideoPopoutAsync` | state read, target resolution, suppression, environment, Player creation, rollback | 3 | Mapped | live latency distribution | optional profile |
| Source suppression | launch + `_sourceSuppressionTimer` | acknowledged launch precondition, 1-second command, per-operation failure gate, stop paths | 4 | F-003/F-004 fixed; V-001/V-005/V-006 | live failure cost M-001 | profile only if authorized |
| Popout state sync | `_syncTimer`, `SyncTimer_Tick` | 250 ms cadence, URL/nav/final-state/single-flight and per-operation log gates | 4 | F-004 fixed; M-001 remains | live failure cost | profile only if authorized |
| Compact shell | shell bridge and `player-shell.js` | 250 ms shell state, watchdog, fallback, disposal | 2 | Feature-gated/dormant | current kill-switch reachability not fully traced | P2 later |
| Focused DOM surface | generated script + bridge | observer/listener/timer/media events, auth, appearance pump, disposal | 4 | M-002, no static defect | renderer CPU/event rates/soak | profile when authorized |
| Appearance preview | accent coalescer/resources | 33 ms max cadence, dedupe, conditional exact derived-pair deltas, Popout pump | 4 | F-005 fixed; V-004/V-005/V-006; M-003 remains | realized WPF frame/allocation data | optional profile |
| Fade/opacity activity | idle timer, 250 ms cursor probe, 15 ms alpha animator | feature gates, native queries, animation teardown | 3 | Mapped/guarded | live power cost under non-default settings | optional profile |
| Native corners/regions | size/DPI/move lifecycle | eligibility short-circuit, snap classification, region replacement/clear | 3 | Mapped/guarded | sustained resize/DPI soak | `profile D-008` |
| Return/navigation replay | `Player_OnClosed`, `ApplyReturnActionAsync`, replay loop | return decision, up to 12 x 250 ms probes, stale-state guards, spec-required dual saves | 4 | R-001; bounded/required | optional slow-disk latency | preserve |
| Settings/profile persistence | `SettingsService` | load/sanitize, full JSON save, durable temp flush/replace, call sites | 3 | Mapped | slow-disk UI latency, profile cardinality | P2 profile |
| Browser-data clear | `PerformClearBrowserDataAsync` | confirmation, single-flight AllProfile task, 30 s foreground wait, late observer/live UI refresh | 4 | F-002 fixed; V-002/V-005/V-006 | WebView2 internal disk scheduling | none |
| Logging | `Log` | bounded queue, background thread, batching, retry retention, rotation, drain | 4 | Preserve-worthy bounds; feeds D-001/D-003 | sustained sink-failure behavior | trace with candidates |
| Shutdown | `MainWindow_Closing`, `PlayerWindow_Closing/Closed`, `App.OnExit` | timer stops, generations, state capture, bridge/WebView dispose, pipe cancel, log drain | 4 | Static path sound | repeated-cycle settling | `profile D-008` |
| Failure/recovery | WebView init, DOM bridge, Popout rollback, privacy timeout, pipe loop | catches, acknowledged outcomes, retry/lifecycle gates, cross-session identity | 4 | F-001 through F-004 fixed; V-001/V-002/V-003/V-005/V-006 | live fault cost M-001 | profile only if authorized |
| Automated tests | focused/full/local-CI lanes | current Debug suite, Release build, spec gate, independent reviews | 4 | 26/26 focused; 985/985 full; CI/reviews PASS | tests are not end-to-end resource measurements | profile plans |
| Deployed runtime | Stable verifier | 21 artifacts hash clean; exact-source release at `d11eac5` | 2 | Stale for audit HEAD | current-source deploy absent | no live testing |

## Frequency and cardinality boundaries

- One primary app instance per channel, one Source Window, and at most one Popout Player.
- Auto dispatcher wake-up: 4/s while enabled; WebView state read only for a current, unhandled watch video.
- Source suppression: 1 WebView command/s while a Popout owns playback.
- Normal Popout state sync: 4 WebView reads/s while navigation is complete; single-flight.
- Return replay after different-video navigation: at most 12 reads at 250 ms spacing, bounded to roughly 3 seconds.
- Focused surface: media-driven updates plus active-only 1/s fallback, one player-class observer, one document child-list observer, and throttled pointer reveal (at most one reveal per 125 ms).
- Opacity/strip activity probe: 4/s only when non-default idle opacity or strip auto-hide needs it.
- Accent preview: latest pair at most once per 33 ms; no full-theme apply.
- Log queue: 4,096 entries; batch target 64 KiB; retained failed batch capped near 512 KiB; active log plus one rotated backup near 1 MB each.
- Profile/settings cardinality is user-created and not explicitly capped; every save serializes the full list.

## Thread, task, dispatcher, lock, and queue boundaries

- WPF UI dispatcher owns window state, all DispatcherTimers, WebView event handlers, native-window helpers, and synchronous settings writes.
- Named-pipe server runs on a thread-pool task and synchronously dispatches handoff to the UI with `Dispatcher.Invoke`.
- Logging uses one dedicated background thread and a bounded `BlockingCollection` drop-on-full queue.
- WebView2 calls are asynchronous IPC; callers generally resume on the UI context. Timer paths use in-progress flags to prevent overlap.
- Focused appearance uses a lock-protected latest-value pump with at most one WebView call in flight.
- Native opacity and resize state dictionaries are documented UI-thread-only and clean through `WM_NCDESTROY`.

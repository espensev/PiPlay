# Open issues and ownership

Only unresolved or deferred work belongs here. Requirements and accepted architecture live in `PiPlay_Product_Engineering_Spec.md` and `DECISIONS.md`.

## Open defects and hardening

| Priority | Issue | Required next step |
|---|---|---|
| Runtime smoke | Q-1 still lacks deployed Stable confirmation across ads, autoplay-next, SPA rerenders, and `start_radio=1`. Suppression reasserts about once per second, so check for brief transition leaks, not only steady audio. | Run the exact `QA_Checklist.md` rows on verified Stable. |
| Medium | `PlayerShellBridge.OnWebMessageReceived` validates shape but not `e.Source`; a foreign Compact child frame can send `close`, `pinToggle`, `fullscreenToggle`, state, or error messages. Compact is dormant, so current reach is bounded. | Require `e.Source.StartsWith(WebViewEnvironmentService.ShellOrigin, StringComparison.OrdinalIgnoreCase)` and test a forged foreign source before Compact revival. |
| Medium | `YouTubeDomBridge.ExecuteRawAsync` has no timeout around `ExecuteScriptAsync`; a stalled renderer can wedge launch/return `_popoutInProgress` or sync `_syncTickInProgress`. | Add a bounded `WaitAsync` routed through `ConsecutiveFailureGate`, with a never-completing-executor test. |
| Low | Named-pipe `ReadToEndAsync` has no per-connection deadline; one local client can occupy the only server slot. | Add an approximately 5 s linked cancellation around one connection. |
| Low | Global dispatcher exceptions are always marked handled; repeated faults can spam uncoalesced modals and leave partially mutated state alive. | Define recoverable classes and coalesce/one-shot the dialog. |
| Low | Top-level `data:` navigation is allowed without a demonstrated product need. | Verify a need or remove it. |
| Low | Dormant shell `getCurrentTime()` failure can write `0` over a good return timestamp. | Preserve the last good positive value on transient failure. |
| Low | Command-line URL sniffing accepts any argument beginning `youtu`; downstream parsing is safe but the boundary is too broad. | Tighten the initial shape check. |
| Deferred reach | `WebViewEnvironmentService.EnsureCreatedAsync` is not single-flight, but current one-Source wiring has no credible concurrent caller. | Reopen only if environment ownership/callers expand. |
| Test seam | Playback-settings script construction and player-state parsing remain inline; no locale-regression test pins invariant-culture volume/rate serialization into JavaScript. | Extract and test pure `BuildPlaybackSettingsScript` and `ParsePlayerState` seams, including `ToString("R", InvariantCulture)`. |
| Deferred structure | `MainWindow.xaml.cs` owns browser initialization, navigation, profiles, privacy, Settings-dialog lifecycle, Popout lifecycle, Auto, and accent preview. | Before its next growth pass, extract Popout-lifecycle and accent-preview coordination. Retain deliberate internal `*ForTests` seams; they enable WPF behavior tests without public API expansion. |

## Product and architecture decisions needed

| Topic | Current constraint / unresolved choice |
|---|---|
| Direct profile launch | Decide whether Source remains visible, starts minimized/hidden, or becomes optional. |
| X-close freshness | Native X/Alt+F4 returns the last timer sample; only Bring-back performs a fresh read. Decide whether close deferral is worth the complexity after script awaits are bounded. |
| Playlist position | Video/list context now returns, but exact queue index/position and interruption-free transfer remain aspirational. Decide the state contract before expanding it. |
| Transparency | Current Popout opacity fades video too. A future video-opaque design needs Off/Main/Popout/Both scope and chrome/background/panel/header/overlay targets; no click-through. |
| Outer silhouette | ADR-0008 clips Round Popouts but guarantees no curve-following border/shadow. Decide whether composition hosting trade-offs justify border/shadow controls. |
| Main-window modes | Browse/Cinema/Compact/Popout layout modes do not exist and must not be confused with dormant Compact playback. Decide names, persistence, and profile precedence. |
| Video fit | Current default is no-crop `contain`. Decide whether optional Fill Window crop and Cinema Padding are wanted. |
| Source chrome auto-hide | Decide Disabled/Main/Popout/Both behavior, reveal rules, and a 2–3 s default. Popout Fade already exists. |
| Corner UX | Current `theme/square/small/round` works. Owner direction also names None/Small/Medium/Large with Medium default; reconcile before changing tokens/UI. |
| Accent-derived tokens | `AccentMuted`, `AccentSubtle`, and `AccentGlow` are unwired. Their safe consumer pairing is unresolved; exact contrast warnings are in `Theme_Preset_Differences.md`. |
| Blackout preset | Owner direction requires a fourth **Blackout** preset with 0% transparency; no superseding decision or implementation exists. |
| Compact revival | `PlaybackModePolicy.CompactPlayerEnabled=false`. Revival requires source validation, RD-list behavior, shell timestamp hardening, error/fallback QA, and 480 x 270 minimum acceptance. |
| Self-hosted CI | Keep `PIPLAY_WINDOWS_RUNNER` unset until a separate, low-privilege, disposable PiPlay VM exists. It must hold no user data, deploy directories, signing material, SSH keys, PATs, or unrelated drive access. Never use interactive `SND-DESK` or reuse the SQ-Control runner. The scalar supports one label; an array needs `fromJSON`. Public contributed workflows can target labels directly. |

Future runner prerequisites: Windows x64, .NET SDK `10.0.300`, Node `24`, PowerShell, outbound GitHub/NuGet access, and writable work/temp/tool-cache paths.

## Current branch manual gates

- Playlist page/mix launch and return context: verify PL/RD queue advance, malformed-list note, and return on deployed Stable.
- Profile-driven backgrounds: owner by-eye signoff for `AccentLetterbox`, `AppBackgroundWash`, profile-row wash, and 1 px Popout edge at intensity 0/50/100 across all presets/DPI. Add no Settings dial; strength constants remain code literals and may be tuned only after deployed-Stable signoff.
- Rounded region: inspect floating/maximized/snap restore at 100%, 125%, 150%, and mixed DPI; outer shadow remains unresolved.

## Deferred requirements and maintenance

- **REQ-RELEASE-01:** signing remains optional/non-gating until public distribution uses a real certificate. Do not add Authenticode to release evidence; provenance is commit + `stable-vX.Y.Z-bN` + verifier.
- **M-001 — persistent DOM execution-failure host cost:** planned; fault-injection authority is absent. Hypothesis: five single-flight failed `ExecuteScriptAsync` calls/s may remain material after repetitive-log coalescing. Use exact-source Stable (`E:\Dev_test_implemenations\PiPlay\PiPlay.exe`), one Normal Popout, and controlled persistent controller/renderer execution failure versus matched healthy playback. Measure PiPlay/WebView process-tree CPU, host IPC/task duration, allocation rate, log enqueue/write rate, UI-dispatcher delay, and recovery latency. Confirm at sustained incremental process-group CPU `>=1` percentage point or failed-call work `>=5 ms CPU/s`, with non-overlapping repeated-run intervals. Reject only if CPU increment `<0.5` points, failed-call work `<2 ms/s`, memory is stable, and dispatcher lateness does not increase.
- **M-002 — partial/inconclusive:** one mostly idle four-hour Standard block was accepted; there is no accepted Focused comparator. Do not rerun the forced campaign.
- **M-003 — realized WPF accent-preview cost/correction delta:** optional/planned; interactive profiling authority is absent. After warmup, compare old and corrected implementations for 30 s hue-only at intensity 100, 30 s intensity sweep at fixed hue, and 30 s mixed hue/intensity. Measure apply-duration p50/p95/p99, dispatcher delay, UI-thread CPU, allocation rate/bytes per apply, Gen0/1/2 counts, realized frame time, and resource-replacement count. Confirm if p95 apply `>8.3 ms`, p95 preview latency `>100 ms`, or frames over `33.3 ms` exceed idle by both `>=3` percentage points and `>=5%` absolute. Reject only if p95 apply `<4 ms`, preview latency `<50 ms`, slow-frame increase `<1` point, allocation `<0.5 MB/s`, and no correlated GC. Correction success also requires replacements to drop from 18 to `<=4` below intensity 50 and `<=2` above it, plus `>=30%` lower allocation or apply time.
- **M-004 — Popout lifecycle settling:** planned; no safe driver exists. Use a fresh process per variant, five warmups, 50 Standard cycles, 50 Focused cycles, samples before open and 5/15/30/60 s after close, and a separate 30–60 min navigation/resize/DPI soak. Measure attributed private/working bytes, managed/JS heap, handles, GDI/USER, HWNDs, threads, child-process count/type, post-close CPU, and open/close latency. Confirm if cycles 11–50 have a settled-slope lower bound above zero and `>=0.5 MB/cycle` plus `>=25 MB` cumulative; or `>=1 handle/cycle` plus `>=25`; or sustained GDI/USER growth; or growing child-process count; or cycles 41–50 are `>=25%` slower than cycles 6–15. Reject only if 60-s slope `<0.2 MB/cycle` and `<20 MB` cumulative, handle drift `<10`, zero GDI/USER net drift, stable process plateau, and latency degradation `<10%`. `scripts/Test-UiSmoke.ps1` does not drive Popout lifecycle; Windows foreground activation was unavailable, and no unsafe alternate driver was created.
- These measurements require fresh authority. Configured presentation is context only; it does not prove that a Popout or Focused DOM surface was active.
- M-005 was retired on verified machine `snd-desk` on 2026-07-19 because its scheduled PowerShell action opened a visible Windows Terminal at sign-in. Tasks `PiPlay-Passive-Runtime-Logger` and `PiPlay-Passive-Runtime-Watchdog` were removed; do not reinstall a scheduled shell-based logger. Historical scripts/logs remain under `C:\ProgramData\PiPlayPassiveRuntime\`. Any future measurement needs fresh authorization and a non-interactive, non-console-host design.
- Dependency maintenance commands:

  ```powershell
  dotnet list PiPlay.sln package --vulnerable --include-transitive
  dotnet list PiPlay.sln package --deprecated --include-transitive
  dotnet list PiPlay.sln package --outdated --include-transitive
  ```

- Evaluate xUnit v3 and `Xunit.StaFact` 3.x together. WebView2 freshness does not justify Fixed Runtime. Add `packages.lock.json` only if locked restore becomes a requirement.

## Ownership boundaries

| Owner | Owns | Must not own |
|---|---|---|
| `MainWindow` | Source browser, commands, profiles, launch/return, shared Settings dialog. | URL parsing, raw scripts, direct settings-file writes. |
| `PlayerWindow` | Popout chrome/playback, placement, state polling, close report. | Source navigation, profiles, global Settings transaction. |
| `YouTubeUrlHelper` / navigation policies | URL parsing/building and allowed destinations. | UI/WebView lifecycle. |
| `YouTubeDomBridge` | All YouTube selectors/scripts. | Native action ownership or arbitrary host capabilities. |
| Page bridges/protocols | Install/remove scripts; validate source/schema/version/nonce/document token; raise closed requests. | YouTube selector ownership, child-frame trust, coordinates, arbitrary actions/data. |
| `SettingsService` / `ProfileService` | Atomic settings and profile validation/persistence. | Product meaning of settings. |
| Placement/native helpers | DPI bounds, hit testing, regions, opacity. | Profiles or playback policy. |
| `LoggingService` | Bounded redacted local diagnostics. | Telemetry, analytics, credentials. |

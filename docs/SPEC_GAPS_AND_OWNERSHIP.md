# Open issues and ownership

Only unresolved or deferred work belongs here. Requirements and accepted architecture live in `PiPlay_Product_Engineering_Spec.md` and `DECISIONS.md`.

## Open defects and hardening

| Priority | Issue | Required next step |
|---|---|---|
| Runtime smoke | Q-1 still lacks deployed Stable confirmation across ads, autoplay-next, SPA rerenders, and `start_radio=1`. Suppression reasserts about once per second, so check for brief transition leaks, not only steady audio. | Run the exact `QA_Checklist.md` rows on verified Stable. |
| Medium | `PlayerShellBridge.OnWebMessageReceived` validates shape but not `e.Source`; a foreign Compact child frame can send `close`, `pinToggle`, `fullscreenToggle`, state, or error messages. Compact is dormant, so current reach is bounded. | Parse `e.Source` as an absolute URI and require its `GetLeftPart(UriPartial.Authority)` to equal `WebViewEnvironmentService.ShellOrigin` with `StringComparison.OrdinalIgnoreCase`. Before Compact revival, test valid shell, YouTube, lookalike-host, and alternate-port sources. |
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
| Playlist position | Video/list context returns; exact queue index/position and interruption-free transfer are neither specified nor implemented. Define the state contract before expanding it. |
| Transparency | Current Popout opacity fades video too. A future video-opaque design needs Off/Main/Popout/Both scope and chrome/background/panel/header/overlay targets; no click-through. |
| Outer silhouette | ADR-0008 clips Round Popouts but guarantees no curve-following border/shadow. Decide whether composition hosting trade-offs justify border/shadow controls. |
| Main-window modes | Browse/Cinema/Compact/Popout layout modes do not exist and must not be confused with dormant Compact playback. Decide names, persistence, and profile precedence. |
| Video fit | Current default is no-crop `contain`. Decide whether optional Fill Window crop and Cinema Padding are wanted. |
| Source chrome auto-hide | Decide Disabled/Main/Popout/Both behavior, reveal rules, and a 2–3 s default. Popout Fade already exists. |
| Corner UX | Implemented values are `theme/square/small/round`. No accepted requirement defines None/Small/Medium/Large or a Medium default; decide before changing tokens/UI. |
| Accent-derived tokens | `AccentMuted`, `AccentSubtle`, and `AccentGlow` are unwired. Their safe consumer pairing is unresolved; exact contrast warnings are in `Theme_Preset_Differences.md`. |
| Blackout preset | Three presets are implemented. **Blackout** has no accepted requirement or implementation; add it only through a new product decision. |
| Compact revival | `PlaybackModePolicy.CompactPlayerEnabled=false`. Revival requires source validation, RD-list behavior, shell timestamp hardening, error/fallback QA, and 480 x 270 minimum acceptance. |
| Self-hosted CI | Keep `PIPLAY_WINDOWS_RUNNER` unset until a separate, low-privilege, disposable PiPlay VM exists. It must hold no user data, deploy directories, signing material, SSH keys, PATs, or unrelated drive access. Never use interactive `SND-DESK` or reuse the SQ-Control runner. The scalar supports one label; an array needs `fromJSON`. Public contributed workflows can target labels directly. |

Future runner prerequisites: Windows x64, .NET SDK `10.0.300`, Node `24`, PowerShell, outbound GitHub/NuGet access, and writable work/temp/tool-cache paths.

## Manual gates for unreleased changes

- Playlist page/mix launch and return context: verify PL/RD queue advance, malformed-list note, and return on deployed Stable.
- Profile-driven backgrounds: on deployed Stable, inspect `AccentLetterbox`, `AppBackgroundWash`, profile-row wash, and the 1 px Popout edge at intensity 0/50/100 across all presets/DPI. Add no Settings dial; tune code constants only after this check.
- Rounded region: inspect floating/maximized/snap restore at 100%, 125%, 150%, and mixed DPI; outer shadow remains unresolved.

## Deferred requirements and maintenance

- **REQ-RELEASE-01:** signing remains optional/non-gating until public distribution uses a real certificate. Do not add Authenticode to release evidence; provenance is commit + `stable-vX.Y.Z-bN` + verifier.
- **M-001 — unmeasured DOM execution-failure cost:** no controlled result exists. With fresh authorization, compare exact-source Stable using one Normal Popout and five failed `ExecuteScriptAsync` calls/s against matched healthy playback. Record process-tree CPU, failed-call CPU, memory, log rate, dispatcher delay, and recovery latency over repeated non-overlapping intervals. Confirm at sustained CPU increase `>=1` percentage point or failed-call work `>=5 ms CPU/s`; reject only below `0.5` points and `2 ms/s`, with stable memory and dispatcher delay.
- **M-002 — partial/inconclusive:** one mostly idle four-hour Standard block was accepted; there is no accepted Focused comparator. Do not rerun the forced campaign.
- **M-003 — unmeasured WPF accent-preview cost:** no authorized interactive profile exists. With fresh authorization and warmup, run three 30 s sweeps: hue-only at intensity 100, intensity-only at fixed hue, and mixed. Record apply p50/p95/p99, preview latency, UI CPU, allocation/GC, frame time, and replacements. Confirm if p95 apply exceeds `8.3 ms`, p95 preview exceeds `100 ms`, or frames over `33.3 ms` exceed idle by both `>=3` percentage points and `>=5%` absolute; reject only below `4 ms`, `50 ms`, `1` point, and `0.5 MB/s`, with no correlated GC. Correction also requires replacements to fall from `18` to `<=4` below intensity 50 and `<=2` above it, plus `>=30%` lower allocation or apply time.
- **M-004 — unmeasured Popout lifecycle settling:** no safe lifecycle driver exists; `scripts/Test-UiSmoke.ps1` does not drive Popout cycles. With fresh authorization and a safe driver, use a fresh process per variant, five warmups, 50 Standard cycles, 50 Focused cycles, samples before open and 5/15/30/60 s after close, plus a 30–60 min navigation/resize/DPI soak. Record memory, handles, GDI/USER, HWNDs, threads, child processes, post-close CPU, and latency. Confirm when cycles 11–50 have a positive settled-slope lower bound and `>=0.5 MB/cycle` plus `>=25 MB`, `>=1 handle/cycle` plus `>=25`, sustained GDI/USER or child-process growth, or cycles 41–50 are `>=25%` slower than cycles 6–15. Reject only at 60 s below `0.2 MB/cycle`, `20 MB`, `10` handles, and `10%` latency growth, with zero GDI/USER drift and a stable process plateau.
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

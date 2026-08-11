# Open issues and ownership

Only unresolved or deferred work belongs here. Requirements and accepted architecture live in `PiPlay_Product_Engineering_Spec.md` and `DECISIONS.md`.

## Open defects and hardening

| Priority | Issue | Required next step |
|---|---|---|
| Runtime smoke | Q-1 double-audio fix shipped in v0.8.0 b29 but still lacks deployed Stable confirmation across ads, autoplay-next, SPA rerenders, and `start_radio=1`. Suppression reasserts about once per second, so check for brief transition leaks, not only steady audio. | Run the exact `QA_Checklist.md` rows on verified Stable. |
| Medium | `PlayerShellBridge.OnWebMessageReceived` validates message shape but not `e.Source`; a foreign child frame can send Compact shell state/actions. Compact is dormant, so current reach is bounded. | Accept only `WebViewEnvironmentService.ShellOrigin` and test a forged foreign source before Compact revival. |
| Medium | `YouTubeDomBridge.ExecuteRawAsync` has no timeout around `ExecuteScriptAsync`; a stalled renderer can wedge launch/return/sync flags. | Add a bounded `WaitAsync` routed through `ConsecutiveFailureGate`, with never-completing-task tests. |
| Low | Named-pipe `ReadToEndAsync` has no per-connection deadline; one local client can occupy the only server slot. | Add an approximately 5 s linked cancellation around one connection. |
| Low | Global dispatcher exceptions are always marked handled and show an uncoalesced modal; repeated faults can spam dialogs and leave partial state alive. | Define recoverable classes and coalesce/one-shot the dialog. |
| Low | Top-level `data:` navigation is allowed without a demonstrated product need. | Verify a need or remove it. |
| Low | Dormant shell `getCurrentTime()` failure can write `0` over a good return timestamp. | Preserve the last good positive value on transient failure. |
| Low | Command-line URL sniffing accepts any argument beginning `youtu`; downstream parsing is safe but the boundary is too broad. | Tighten the initial shape check. |
| Deferred reach | `WebViewEnvironmentService.EnsureCreatedAsync` is not single-flight, but current one-Source wiring has no credible concurrent caller. | Reopen only if environment ownership/callers expand. |
| Test seam | Playback-settings script construction and player-state parsing remain inline; no locale-regression test pins invariant-culture volume/rate serialization into JavaScript. | Extract and test pure `BuildPlaybackSettingsScript` and `ParsePlayerState` seams, including `ToString("R", InvariantCulture)`. |

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
- Profile-driven backgrounds: owner by-eye signoff for `AccentLetterbox`, `AppBackgroundWash`, profile-row wash, and 1 px Popout edge at intensity 0/50/100 across all presets/DPI.
- Rounded region: inspect floating/maximized/snap restore at 100%, 125%, 150%, and mixed DPI; outer shadow remains unresolved.

## Deferred requirements and maintenance

- **REQ-RELEASE-01:** signing remains optional/non-gating until public distribution uses a real certificate. Do not add Authenticode to release evidence; provenance is commit + `stable-vX.Y.Z-bN` + verifier.
- M-001 persistent-DOM-failure cost, M-003 realized accent-preview cost, and M-004 repeated-Popout settling require fresh measurement authority; see `.audit/deep-audit/piplay-runtime-2026-07-16/REPORT.md`.
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

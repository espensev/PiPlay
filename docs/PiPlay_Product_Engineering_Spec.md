# PiPlay product and architecture contract

**Status:** living contract and current implementation map. `VERSION` and `BUILD_NUMBER` own the build stamp. Code and tests cited below are the authority for actual behavior.

## Product boundary

PiPlay hosts YouTube in a native Windows Source Window and transfers playback to one borderless Popout Player. **Video Popout**, **Popout Player**, **Source Window**, **Pin**, **Fade**, **Auto**, **Standard**, and **Focused** are user-facing terms. `MainWindow`, `PlayerWindow`, and Compact are internal names.

The durable quality rules are:

- **Q-1 — no duplicate playback.** A concrete Source video requires acknowledged mute/pause before Popout construction, while a playlist-only Source page has no guaranteed video and its tested suppression attempt is best-effort, so launch may continue unacknowledged. A required-path failure restores the Source; a suppression guard remains active while the Popout owns playback (`MainWindow.StartVideoPopoutAsync`; `PopoutLaunchPolicy.RequiresAcknowledgedSuppression`; `RuntimeFailurePolicyTests.Only_video_targets_require_acknowledged_suppression`).
- **Q-2 — preserve available context.** Return uses the Popout's reported video/list, nullable timestamp, play state, volume, mute, and rate; zero is valid. Popout state wins over launch state when known (`PlayerReturnState`; `ReturnPolicy.Decide`; `ReturnPolicyTests`).
- **Q-3 — contain page automation.** `YouTubeDomBridge` builds YouTube scripts. Focused and drag bridges bind them to the current navigation, nonce, document token, trusted top-level source, and closed action schemas (`PlayerFirstSurfaceBridge`; `PlayerSurfaceDragBridge`; `PlayerSurfaceProtocolTests`; `YouTubeDomBehaviorTests`).
- **Q-4 — use WebView2 Evergreen.** The app creates a shared `CoreWebView2Environment` without a Fixed Runtime folder (`PiPlay.csproj`; `WebViewEnvironmentService.EnsureCreatedAsync`; [Microsoft WebView2 distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)).
- **Q-5 — remain non-invasive.** Do not download, proxy, extract, or re-host media; alter ads or monetization; bypass DRM, age, region, login, or playback restrictions; inspect credentials; or remove required controls and branding. Focused ad actions fail closed in `YouTubeDomBehaviorTests`. Recheck the current [YouTube Terms](https://www.youtube.com/static?template=terms) before public distribution.
- **Q-6 — recover visibly.** Invalid settings are sanitized or quarantined; failed launch restores the Source; navigation/runtime failures keep a retry path; repeated DOM failures are bounded (`SettingsService.Load`; `MainWindow.StartVideoPopoutAsync`; `ConsecutiveFailureGate`; corresponding tests).
- **Q-7 — native window behavior.** Placement, monitor clamping, DPI, resize, Pin, maximize/restore, and rounded-region decisions stay in native/window policy seams (`WindowPlacementService`; `BorderlessResizeHitTestPolicy`; `RoundedWindowRegionPolicy`; their tests).
- **Q-8 — visible means interactable.** Fade and opacity never enable `WS_EX_TRANSPARENT`; top-level WebView windows keep `AllowsTransparency="False"` (`WindowOpacityApplier`; `PlayerWindow.xaml`; `XamlInvariantTests`; `WpfRuntimeTests`).

## Current functional contract

- Auto is off by default, applies only to a playing `/watch` target, shares the one-player launch gate, and de-duplicates the returned identity. Manual launch remains available (`AppSettings.AutoPopout`; `AutoPopoutPolicy.Decide`; `AutoPopoutPolicyTests`).
- Normal mode uses real YouTube watch URLs and retains valid playlist and `RD...` mix identifiers. Playlist-only launch may resolve the first playable item. Malformed list identifiers degrade to a single video with a note (`YouTubeUrlHelper`; `PopoutTargetResolver`; their tests).
- Compact playback data and shell code remain, but `PlaybackModePolicy.CompactPlayerEnabled=false`; every new Popout resolves to Normal. Standard is default; Focused is a reversible no-crop presentation of the same watch page (`PlaybackModePolicyTests`; `PopoutPresentationPolicyTests`; `YouTubeDomBehaviorTests`).
- **Show Popout** restores/focuses the existing window. **Bring video back** captures fresh state, closes the Popout, and replays the return state. Both paths retain one-player ownership (`MainWindow.ActivateExistingPlayer`; `MainWindow.BringVideoBackAsync`; `MainWindowLifecycleTests`).
- Source and Popout Pin values are independent. Fade hides Popout chrome only after its configured idle condition; pointer/focus/drag recovery reveals it. Whole-window opacity never disables input (`FadePolicy`; `WindowOpacityPolicy`; `FadePolicyTests`; `WindowOpacityPolicyTests`).
- Exact themes, colors, radii, density, elevation, fade presets, and opacity defaults are code-owned in `ThemeCatalog`, `ThemeColors`, and `ThemePreferenceResolver`, with literal and contrast coverage in `ThemeCatalogTests`, `ThemeColorsTests`, and `XamlInvariantTests`. Do not duplicate those tables in prose.
- The Source minimum is declared in `MainWindow.xaml`; Normal Popout minimum is `320 x 180` DIP. Per-monitor awareness is `PerMonitorV2` (`PlaybackModePolicy.NormalMinWidth/NormalMinHeight`; `app.manifest`; `PlaybackModePolicyTests`; `XamlInvariantTests`).

## Data, navigation, and security

- Data-root precedence is `PIPLAY_DATA_ROOT`, then portable Stable `PiPlayData`, then the operating system's Local Application Data folder. Stable and Default have separate channel identities (`AppPaths.ResolveRoot`; `AppChannel.Resolve`; `AppPathsTests`; `AppChannelTests`).
- Settings schema `4` is written through a flushed temporary file and same-volume move/replace. Reset preserves `WebView2UserData`; the separate Clear browser data action uses WebView2 `AllProfile` (`AppSettings.CurrentSchemaVersion`; `SettingsService.AtomicWrite`; `PrivacyService`; tests).
- Source and Popout allow only the implemented YouTube/Google authentication surface in-app; unrelated HTTP(S) destinations open externally. The allowlist is a navigation boundary, not a defense against a compromised allowed page (`NavigationPolicy`; `PopoutNavigationPolicy`; their tests).
- Logs are local, redacted, queued, rotated, and bounded. Never add cookies, authorization headers, credential URLs, unsanitized searches, telemetry, or uploads (`src/PiPlay/Services/LoggingService.cs:Log`; `LoggingServiceTests`).

## Stable requirement IDs

| ID | Contract | Evidence |
|---|---|---|
| REQ-APP-01 | One instance per channel and Windows logon session | `App.MutexName`, `SingleInstancePipePolicy.BuildPipeName`, `RuntimeFailurePolicyTests` |
| REQ-NAV-02 | Popout navigation stays on its allowlisted surface or opens externally | `PopoutNavigationPolicy.DecideNewWindow`, `PopoutNavigationPolicyTests` |
| REQ-PRIVACY-01 | Reset replaces settings only and preserves WebView2 browser data | `SettingsService.Reset`, `SettingsServiceTests` |
| REQ-PRIVACY-02 | Clear browser data remains a separate confirmed action and clears WebView2 `AllProfile` data | `PrivacyService`, `PrivacyServiceTests` |
| REQ-PROFILE-01 | Each nullable profile mode or presentation value overrides its global value only when set | `PlaybackModePolicy.ResolveEffectiveMode`, `PopoutPresentationPolicy.ResolveEffectivePresentation`, their tests |
| REQ-PROFILE-02 | Saved placement prefers its recorded monitor when available and otherwise clamps to a visible work area | `WindowPlacementService`, `PlacementMath`, their tests |
| REQ-RETURN-01 | Current Popout media and playback state wins when known; timestamp zero remains valid | `ReturnPolicy`, `PlayerReturnState`, `ReturnPolicyTests` |
| REQ-RETURN-07 | A Popout launched from a paused Source does not auto-start playback | `MainWindow.StartVideoPopoutAsync`, `PlayerWindow.SyncTimer_Tick` |
| REQ-UI-01 | Secondary controls, popups, scrollbars, and tooltips retain the dark interaction surface | `ControlStyles.xaml`, `ThemeColors`, `XamlInvariantTests` |
| REQ-UI-02 | Icon-only controls expose accessible names and render through the icon font | `ControlStyles.xaml`, `Prompt`, `XamlInvariantTests`, `WpfRuntimeTests` |
| REQ-WINDOW-01 | Windows use PerMonitorV2 placement and restore into a visible monitor work area | `app.manifest`, `WindowPlacementService`, `PlacementMath`, their tests |
| REQ-WINDOW-02 | Borderless WebView windows preserve native edge and corner resize hit targets | `BorderlessResizeHitTestPolicy`, `BorderlessWindowHelper`, `XamlInvariantTests` |

## Decisions in force

| ID | Decision | Implementation evidence |
|---|---|---|
| ADR-0001 | Windows WPF shell with native custom chrome | `PiPlay.csproj`, `MainWindow.xaml`, `PlayerWindow.xaml` |
| ADR-0002 | `net10.0-windows`; nullable/implicit usings; no trimming, single-file publish, or NativeAOT | `PiPlay.csproj:PropertyGroup` |
| ADR-0003 | Shared WebView2 Evergreen runtime/environment | `PiPlay.csproj:Microsoft.Web.WebView2`, `WebViewEnvironmentService` |
| ADR-0004 | Native Popout owns a second WebView; Source is suppressed and hidden during transfer | `MainWindow.StartVideoPopoutAsync`, `PlayerWindow` |
| ADR-0005 | Exactly one Popout Player | `MainWindow._player`, `MainWindow.CanStartVideoPopout` |
| ADR-0006 | No click-through | `WindowOpacityApplier`, `WpfRuntimeTests` |
| ADR-0007 | Stable channel has portable data and a separate per-session identity | `AppChannel`, `AppPaths`, `App.MutexName`, `SingleInstancePipePolicy.BuildPipeName` |
| ADR-0008 | A floating effective-Round Popout may use a DPI-scaled native region; snap/maximize clears it | `RoundedWindowRegionPolicy`, `RoundedWindowRegionPolicyTests` |

Change an ADR explicitly; do not silently contradict it.

## Verification boundary

`scripts/Test-LocalCI.ps1` is the required source gate. It includes repository policy checks, the complete deterministic test suite, executable DOM scenarios, and a Release build.

One release cycle proceeds in this order: clean committed source; a GitHub-produced non-release desk candidate; automated packaged preflight and desktop smoke; one SND-DESK human pass limited to **Unresolved verification**; then, only after acceptance, exact-source Stable publish, verification, and tag. Commands live only in `README.md`. Desk acceptance is release-decision evidence only; it is never release provenance or tested-byte identity for later Stable output. Human testing is this one end-of-cycle gate, never a per-change requirement.

## Unresolved verification

- The one SND-DESK live pass remains unresolved until it verifies YouTube/ad/autoplay/SPA audio ownership, real account/login behavior, end-to-end return playback, and fractional/mixed-DPI rendering.
- Compact is disabled. `PlayerShellBridge.OnWebMessageReceived` parses message shape but does not validate `CoreWebView2WebMessageReceivedEventArgs.Source`; add and test that boundary before enabling Compact.
- `YouTubeDomBridge.ExecuteRawAsync` awaits `ExecuteScriptAsync` without an explicit timeout. A permanently stalled renderer can therefore retain the caller's operation guard; add a bounded, tested timeout before claiming that failure mode is contained.

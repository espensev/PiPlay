# PiPlay product and engineering specification

**Status:** approved product intent. This file is not implementation proof: actual behavior is established by the source and tests named beside each contract. Architecture is in [`DECISIONS.md`](DECISIONS.md); verified open work is in [`SPEC_GAPS_AND_OWNERSHIP.md`](SPEC_GAPS_AND_OWNERSHIP.md).

## 1. Product

PiPlay is a Windows desktop utility that plays real YouTube pages in one movable, resizable native Video Popout. It is not a media host or replacement player.

## 2. Terminology

| Term | Meaning |
|---|---|
| Video Popout | Transfer current YouTube playback to the floating player. |
| Popout Player | Floating borderless playback window. |
| Source Window | Main PiPlay browser window. |
| Source Placeholder | Near-black surface shown while playback is popped out. |
| Pin | Keep the active surface topmost. |
| Fade | Idle/hover chrome fading; never click-through. |
| Auto | Automatic `/watch` popout; off by default. |

Use **Pop out video**, **Bring video back**, and **Show Popout** in user-facing copy. `MainWindow`, `PlayerWindow`, `Detach`, and `fake PiP` are implementation terms.

## 3. Quality requirements

| ID | Contract | Evidence anchor |
|---|---|---|
| Q-1 | Source audio stays muted/paused while the Popout owns playback. | `YouTubeDomBridge.SuppressPlaybackAsync`; `MainWindowLifecycleTests`; live listening remains open. |
| Q-2 | Return preserves current video, playlist/mix context, timestamp, play state, volume/mute/rate, and window state where available. | `PlayerReturnState`, `ReturnPolicy`, `PlayerWindow`; `ReturnPolicyTests`. |
| Q-3 | YouTube DOM work is centralized, best-effort, schema-safe, and non-essential to app survival. | `YouTubeDomBridge`; `YouTubeDomBehaviorTests`, `RuntimeFailurePolicyTests`. |
| Q-4 | Use WebView2 Evergreen unless a future decision changes the requirement. | `src/PiPlay/PiPlay.csproj`, `WebViewEnvironmentService`. |
| Q-5 | Do not inspect credentials, download media, alter ads/monetization, bypass restrictions, or remove required YouTube controls/branding. | `YouTube_Compliance.md`; `NavigationPolicy`, `PlayerSurfaceProtocol`. |
| Q-6 | URL/settings/navigation/network/runtime/script failures recover without a crash or stuck duplicate work. | `SettingsService`, `ConsecutiveFailureGate`; `RuntimeFailurePolicyTests`, `SettingsServiceTests`. |
| Q-7 | Move, resize, Pin, close/return, monitor restore, and DPI behavior are native and recoverable. | `WindowPlacementService`, `BorderlessResizeHitTestPolicy`, `RoundedWindowRegionPolicy`; WPF tests. |
| Q-8 | A visible player remains directly interactable at every fade or opacity setting. | `WindowOpacityPolicy`, `PlayerSurfaceDragProtocol`; `WindowOpacityPolicyTests`, `PlayerSurfaceProtocolTests`. |

## 4. Target experience

Open a YouTube video, playlist, or mix; select **Pop out video**; use the native Popout; then return or close. **Show Popout** activates the existing player. **Bring video back** transfers playback and closes it. The Source Placeholder hides the Source WebView while the Popout owns playback. (`MainWindow.xaml.cs`, `PlayerWindow.xaml.cs`, `PopoutLaunchPolicyTests`, `ReturnPolicyTests`.)

## 5. Visual and interaction identity

Source controls include navigation, URL/search, profiles, Pin, Auto, Settings, transfer, and Show Popout. Popout controls include Settings, Fade, Pin, Expand/Restore, Close, and a move handle. Every opened popup, menu, dropdown, item container, and tooltip uses the dark theme and every icon-only control has an accessible name. (`MainWindow.xaml`, `PlayerWindow.xaml`, `ControlStyles.xaml`, UI tests.)

Current palettes, radii, density, elevation, opacity, and accent derivation are only in [`Theme_Preset_Differences.md`](Theme_Preset_Differences.md).

## 6. Auto, Fade, and Pin

### 6.1 Auto

Auto is off by default and eligible only for a playing `/watch` video. Shorts, embeds, home, search, settings, history, and login do not auto-pop. Launch/return uses one identity gate so the returned video is not immediately re-popped. (`AutoPopoutPolicy`, `YouTubeUrlHelper`, `AutoPopoutPolicyTests`.)

### 6.2 Fade

Fade changes Popout chrome visibility only. Delay presets are Short `1500 ms`, Normal `2500 ms`, and Long `4000 ms`; opacity never implies click-through. (`FadePolicy`, `PlayerAppearancePolicy`, `FadePolicyTests`.)

### 6.3 Pin

Source and Popout Pin values are independent. Source Pin is suspended while the Popout owns playback and restored on return. (`MainWindow.xaml.cs`, `PlayerWindow.xaml.cs`, `PlayerPinAffordanceTests`.)

## 7. Opacity and transparency

### 7.1 Controls fade

Pointer, keyboard, focus, pause, and movement recovery apply to the Popout chrome; the strip never becomes a second input mode.

### 7.2 Chrome auto-hide

When enabled, the idle-hidden strip collapses so the video fills the window; top-edge reveal restores it. Fade off keeps it visible. (`PlayerWindow`, `XamlInvariantTests`.)

### 7.3 Whole-window opacity

Active and idle opacity apply to the whole Popout; the Source title backdrop may also use the active wash. The UI floor is `0.45`; hand-edited persisted values from `0.10` through below `0.45` remain honored. Idle never exceeds active. (`WindowOpacityPolicy`, `WindowOpacityPolicyTests`.)

### 7.4 Click-through

Never set `WS_EX_TRANSPARENT`, transparent hit testing, or a transparent WebView. Chrome-only/video-opaque transparency requires a new decision. (`WindowOpacityApplier`, `DECISIONS.md` ADR-0006.)

## 8. Non-goals

No media download/re-hosting, ad blocking, restriction bypass, multiple Popouts, browser-native PiP, click-through, transparent WebView, default crop, required global hotkeys, cross-platform build, public telemetry, installer/auto-update, or credential inspection.

## 9. Technical direction

The app targets `net10.0-windows` with WPF, nullable reference types, implicit usings, and no trimming, NativeAOT, or single-file publish. WebView2 package/runtime choices are in `src/PiPlay/PiPlay.csproj`; SDK selection is in `global.json`. `PerMonitorV2` is declared in `src/PiPlay/app.manifest`. (`DECISIONS.md` ADR-0001–0003.)

`PIPLAY_DATA_ROOT` overrides the data root. Otherwise Stable uses `<exeDir>\PiPlayData` and Default uses `%LOCALAPPDATA%\PiPlay`. Source and Popout share one `CoreWebView2Environment`. (`AppPaths`, `AppChannel`, `AppPathsTests`.)

## 10. Playback modes and presentation

### 10.1 Normal page mode

Normal is the default and uses a real YouTube watch page. Valid `PL...` and `RD...` list IDs remain in Normal watch URLs and return context. (`YouTubeUrlHelper`, `YouTubeUrlHelperTests`.)

### 10.2 Compact mode

Compact is dormant: `PlaybackModePolicy.CompactPlayerEnabled` is `false`. If revived, its minimum is `480 x 270` DIP; Normal is `320 x 180` DIP. Compact builders omit auto-generated `RD...` lists. (`PlaybackModePolicy`, `PlaybackModePolicyTests`.)

### 10.3 Local Compact shell

The shell is served from `https://piplay.local/player.html`, uses a versioned host protocol, and must remain isolated from Source navigation. Shell failure falls back to Normal without creating another player. (`PlayerShell*`, `WebViewEnvironmentService`, `PlayerShellAssetTests`.)

### 10.4 Standard and Focused

Standard is default. Focused is a reversible best-effort overlay on the real top-level HTTPS YouTube `/watch` page; it uses `contain`, not default `cover`. Empty overlay pixels pass input; native YouTube controls, ads, branding, settings, quality, captions, fullscreen, links, and Skip controls remain reachable. (`YouTubeDomBridge`, `PlayerFirstSurfaceBridge`, `PlayerSurfaceProtocolTests`.)

## 11. Runtime coordination

One WPF dispatcher owns native/window state. Launch, return, navigation, and page calls are generation- or single-flight-guarded. Normal Popout DOM sync is `250 ms`; Source suppression is `1 s`; normal-page DOM execution has a `5 s` deadline; and a connected single-instance client has `2 s` to finish its pipe payload. Timers stop on close/navigation. Logging is local and bounded. (`MainWindow.xaml.cs`, `PlayerWindow.xaml.cs`, `YouTubeDomBridge`, `SingleInstancePipePolicy`, `RuntimeFailurePolicyTests`.)

## 12. Component contracts

### 12.1 MainWindow / Source Window

Owns Source browsing, navigation/profile commands, launch/return, placeholder, and shared Settings. It does not parse URLs, embed raw JavaScript, or write settings directly.

### 12.2 PlayerWindow / Popout Player

Owns Popout chrome/playback, Pin/Fade/appearance, placement, state polling, and the close report. It does not own Source navigation, profiles, or the global Settings transaction.

### 12.3 WebViewEnvironmentService

Owns one environment, data folder, missing-runtime recovery, and virtual-host mapping.

### 12.4 YouTubeUrlHelper

Parses supported YouTube video/share/Shorts/embed/watch-playlist URLs and builds normalized watch or shell targets. Valid list IDs are retained; malformed IDs produce a non-blocking fallback reason. (`YouTubeUrlHelperTests`.) A command-line launch argument is accepted only when `YouTubeUrlHelper.TryParse` recognises it, and is otherwise ignored. (`App.ExtractUrlArg`, `AppStartupArgumentTests`.)

### 12.5 YouTubeDomBridge and host protocols

All normal-page JavaScript belongs in `YouTubeDomBridge`. Host requests are exact-schema, versioned, nonce/document-token checked, source checked, and trusted-input gated. Focused actions are limited to `close`, `pinToggle`, `fullscreenToggle`, and `settings`; drag requests contain no coordinates.

### 12.6 SettingsService

Owns schema `4`, sanitization/migration/recovery, and atomic persistence. (`SettingsServiceTests`.)

## 13. Video Popout lifecycle

### 13.1 Preconditions

Browser ready; no launch, return, clear, or shutdown in progress; no player exists; target is a supported video or playlist page. A playlist-only page may use its first rendered playable item. (`PopoutTargetResolverTests`.)

### 13.2 Launch

Capture Source state and target, acknowledge mute+pause, hide Source WebView, show the placeholder, and create/navigate the single Popout with the shared environment. A first item adopted from a playlist-only page starts at zero; an unrelated miniplayer/preview timestamp is never carried into it. A failure restores Source visibility and captured playback state. (`MainWindow.xaml.cs`, `PopoutTargetResolverTests`, `PopoutLaunchPolicyTests`.)

### 13.3 Source Placeholder

While the placeholder is active, disable Source navigation, URL, profile, and profile-action commands. Auto-off and both recovery actions remain available. **Show Popout** and **Bring video back** remain separate actions.

### 13.4 Race gate

The launch path returns when the browser is not ready, launch/return/clear/shutdown is active, or a Popout already exists. (`MainWindow.xaml.cs`, `MainWindowLifecycleTests`.)

### 13.5 Failure

Hide the placeholder, restore Source, restore captured state where possible, report a concise error, and suppress repeated failure noise. (`ConsecutiveFailureGate`, `RuntimeFailurePolicyTests`.)

## 14. Return and close

Return state is nullable; zero is a valid timestamp. Known live Popout state wins over launch fallback. Same-video return seeks; a different video/list navigates with context before replaying timestamp, play state, volume/mute, and rate where YouTube permits. Return restores Source placement/Pin and arms Auto de-dup before asynchronous replay. (`PlayerReturnState`, `ReturnPolicy`, `ReturnPolicyTests`, `MainWindow.xaml.cs`.)

## 15. WebView and navigation

### 15.1 Shared environment

Source and Popout use the environment and data root from sections 9 and 12.3.

### 15.2 Navigation and new windows

Source and Popout allow YouTube plus the supported Google sign-in/account hosts. Unrelated HTTP(S) targets open externally or are blocked by surface policy. The allowlist is a navigation guardrail, not a malicious-page security boundary. (`NavigationPolicy`, `NavigationPolicyTests`.)

### 15.3 Navigation failure

Failure keeps a safe URL and exposes retry behavior. (`MainWindow.xaml.cs`.)

### 15.4 Runtime failure

Missing or failed WebView2 exposes install/retry recovery. (`WebViewEnvironmentService`, `RuntimeFailurePolicyTests`.)

## 16. Window behavior

### 16.1 Size and DPI

Normal Popout minimum is `320 x 180` DIP; dormant Compact is `480 x 270` DIP. Keep `UseLayoutRounding=False` and `AllowsTransparency=False` on top-level windows. (`PlaybackModePolicy`, XAML tests.)

### 16.2 Dragging

The Popout top handle is `44 DIP`. Trusted primary mouse/pen drag on passive surface pixels begins only after the system threshold; below it the event remains a click. Interactive controls, ads, menus, captions, timelines, links, and overlays are excluded. (`PlayerSurfaceDragProtocol`, `PlayerSurfaceScriptTests`.)

### 16.3 Resizing

Borderless windows expose a native `12 DIP` resize band and `96 DIP` diagonal reach; the latter is not a `96 x 96` content-stealing square. (`BorderlessResizeHitTestPolicy`, tests.)

### 16.4 Multi-monitor behavior

`PerMonitorV2` is required; restore the prior monitor when available, otherwise clamp to visible work area. (`WindowPlacementService`, `PlacementMathTests`, WPF tests.)

## 17. Profiles and appearance ownership

Profiles contain a name, URL, optional mode/presentation/accent/Pin/Fade values, and placement. Non-null profile fields override global values; unset fields inherit. Duplicate names prompt overwrite/rename, URLs validate, and broken values fail gracefully. (`ProfileService`, `ProfileServiceTests`.)

## 18. Logging

Logs are local, redacted, queued, and bounded. Never log cookies, authorization headers, credential-bearing URLs, secrets, or unsanitized search text. (`LoggingService`, `LoggingServiceTests`.)

## 19. Security and privacy

**Reset app state** clears settings/profiles/placement and retains WebView2 browser data. **Clear browser data** is separate, confirmed, closes Popout, clears `AllProfile`, and signs the user out; timeout does not allow a second clear while the first is unresolved. (`PrivacyService`, `SettingsService`, `PrivacyServiceTests`.) Full data locations are in [`Data_and_Privacy_Map.md`](Data_and_Privacy_Map.md); page-script boundaries are in [`YouTube_Compliance.md`](YouTube_Compliance.md).

## 20. Accessibility and usability

Every icon-only control has an accessible name and keyboard focus. Pin/transfer names state the next action. `Ctrl+L` and `F6` focus the Source URL while Source commands are available. (`MainWindow.xaml`, `PlayerWindow.xaml`, UI tests.)

## 21. Packaging and release

Development and release scripts own build/publish behavior. Exact-source Stable publishing requires committed `VERSION`, `BUILD_NUMBER`, a clean tree, and a Stable deployment root supplied by `PIPLAY_STABLE_ROOT` or `-DeployRoot`. The verifier checks manifest hashes and source identity before final acceptance. Signing is optional through `-SignScript` and runs before hashes. (`Build-PiPlay.ps1`, `Publish-Stable.ps1`, `Verify-StableDeploy.ps1`.)

## 22. Quality gates

### 22.1 Automated contract coverage

`scripts/Test-LocalCI.ps1` checks Node, restore, the Debug suite with temporary data, and the non-mutating Release build. The test suite covers URL/navigation, playlist/mix target resolution, return policy, settings/privacy, protocol/DOM behavior, theme resources, and WPF invariants. (`tests/PiPlay.Tests`.)

### 22.2 Deployed UI smoke

`scripts/Test-UiSmoke.ps1` checks the five named Source controls against the deployed executable, uses an isolated data root, foregrounds the real HWND in a per-monitor-DPI-aware capture context, and rejects blank/uniform frames. It needs an interactive desktop and WebView2; it is a final deployed smoke, not a substitute for code tests.

### 22.3 End-user acceptance

Only the result-dependent checks remain manual: listen for duplicate audio through Popout launch/return and exercise one real playlist/mix return when available. Record unavailable account/ad/profile states as not run. See [`QA_Checklist.md`](QA_Checklist.md).

## 23. Deferred scope

Compact revival, alternate main-window layouts, chrome-only/video-opaque transparency, curve-following outer border/shadow styling, exact playlist queue position, tray mode, optional global hotkeys, import/export, installer/auto-update, multiple players, cross-platform work, and public telemetry are deferred.

## 24. Unresolved work

`SPEC_GAPS_AND_OWNERSHIP.md` is the sole current backlog/ownership surface. Do not add historical status here.

## 25. Resolved defaults

`DECISIONS.md` owns ADR-0001 through ADR-0008. This contract remains normative until a decision or requirements change supersedes it.

## 26. Implementation constraints

### 26.1 Source Placeholder

The Source WebView is hidden while the visible Source Placeholder is shown; the helper must not leave WebView content bleeding through. (`MainWindow.xaml.cs`, XAML tests.)

### 26.2 Nullable timestamp

Return timestamps are nullable; `0` is a known value distinct from unknown. (`PlayerReturnState`, `ReturnPolicyTests`.)

### 26.3 Video selector

Normal-page scripts use this ordered selector in `YouTubeDomBridge`:

```js
document.querySelector('#movie_player video.html5-main-video') ||
document.querySelector('video.html5-main-video') ||
document.querySelector('video')
```

### 26.4 Atomic settings save

Write and flush a temporary file, then use same-volume `File.Replace` or `File.Move`; never overwrite live settings with `File.Copy`. (`SettingsService`.)

### 26.5 Borderless resize and transparency

The `12 DIP` band and `96 DIP` corner reach are native hit-test dimensions, not content padding. WebView2 remains an opaque child HWND; keep `AllowsTransparency=False` and the no-click-through rule. (`BorderlessResizeHitTestPolicy`, ADR-0006.)

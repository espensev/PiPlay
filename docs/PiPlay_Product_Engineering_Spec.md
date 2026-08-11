# PiPlay product and engineering specification

**Status:** Living contract. `VERSION` and `BUILD_NUMBER` own the current build stamp.

`must`, `must not`, `should`, and `may` are normative. Stable requirements use `Q-*` and `REQ-*` IDs. Architecture choices live in `DECISIONS.md`; unresolved work lives in `SPEC_GAPS_AND_OWNERSHIP.md`.

## 1. Product

PiPlay is a Windows desktop utility for playing YouTube in a movable, resizable native Video Popout. It is a high-quality playback/window host, not a replacement media platform.

## 2. Terminology

| Term | Meaning |
|---|---|
| PiPlay | App/product. |
| Video Popout | Transfer of current playback to the floating player. |
| Popout Player | Floating borderless playback window. |
| Source Window | Main PiPlay browsing window. |
| Source Placeholder | Accent-derived near-black letterbox surface shown while Source playback is popped out. |
| Pin | Keep the active PiPlay surface topmost. |
| Fade | Idle/hover fading; never click-through. |
| Auto | Automatic `/watch` popout; off by default. |

Use **Pop out video**, **Bring video back**, and **Show Popout** in UI. `MainWindow`, `PlayerWindow`, `Detach`, and `fake PiP` are internal terms.

## 3. Quality requirements

1. **Q-1 No duplicate playback:** Source audio must remain muted/paused while Popout owns playback.
2. **Q-2 No lost context:** return preserves video, playlist context, timestamp, playback state, settings, and window state where available.
3. **Q-3 Isolated best-effort DOM work:** all YouTube scripts are centralized, tested, failure-contained, and never required to keep the app alive.
4. **Q-4 Evergreen runtime:** use platform WebView2 Evergreen unless an explicit future requirement changes it.
5. **Q-5 Non-invasive YouTube behavior:** no credential interception, downloads, ad/monetization changes, restriction bypass, or removal of required controls/branding.
6. **Q-6 Recovery:** bad URLs/settings, navigation/network/runtime/script failures, and popup/login edges produce bounded recovery, not crashes or stuck duplicate work.
7. **Q-7 Native window quality:** moving, resizing, Pin, close/return, monitor restore, and DPI are intentional native behaviors.
8. **Q-8 Visible means interactable:** opacity/fade never makes a visible player click-through.

## 4. Target experience

### 4.1 Core user story

- Open a YouTube video, playlist, or mix; select **Pop out video**; move, resize, Pin, or change presentation; then return or close.

### 4.2 Expected behavior

- Source playback stops before Popout construction. Warm Popout playback starts within 2 s of expected source timestamp plus elapsed time; target is ≤1 s.
- **Show Popout** restores/focuses the existing player. **Bring video back** transfers playback and closes it. Neither creates a second player.
- The Popout feels like a native media surface: no address bar, tabs, OS frame, crop-by-default, or inaccessible controls.

## 5. Visual and interaction identity

### 5.1 Visual identity

- All appearance changes preview live on open windows. **Done** commits; title-bar close, Escape, or any other non-affirmative dismissal restores the complete prior appearance.

### 5.2 Color tokens

- Current palette, opacity, derived accents, and persistence values are in `Theme_Preset_Differences.md`.

### 5.3 Shape tokens

- Current radii, density, elevation, and native corner modes are in `Theme_Preset_Differences.md`.

### 5.4 Icon style

- **REQ-UI-02:** glyphs use one coherent icon family and always resolve; `.notdef`/empty boxes are defects. Use `Segoe Fluent Icons` with `Segoe MDL2 Assets` fallback or a readable text label.

### 5.5 Window taxonomy

- Source controls: Back, Reload, Home, URL/search, profile selection/actions, Pin, Auto, Settings, transfer action, and Show Popout when applicable. At the 760 x 480 DIP minimum only the transfer label may collapse; icon, tooltip, and accessible name remain.
- Popout controls: Settings, Fade, Pin, Expand/Restore, Close, and a guaranteed move handle. Standard and Focused presentations share the same native strip and recovery path.

### 5.6 Dark-theme completeness

- **REQ-UI-01:** every control and secondary surface it opens—dropdown, item container, menu, popup, tooltip—uses the dark theme. Empty states remain intentional and tooltips do not cover their control.

## 6. Auto, Fade, and Pin

### 6.1 Auto

- Off by default; `/watch` only. Shorts, embeds, home, search, settings, history, and login never auto-pop.
- Trigger when the resolved current video is playing. Enabling Auto on an already-playing `/watch` video pops it immediately.
- Resolve one Source-first target identity; use canonical URL only when Source URL has no video identity. Detection and launch carry the same target.
- Playlist autoplay-next may pop the next video. Every return arms the de-dup latch with the returned identity before navigation/seek/resume; the returned video does not loop, while a different playing video remains eligible.
- Auto and manual launch use the same one-player/race gate. A DOM failure skips the attempt and never crashes.

### 6.2 Fade

- Fade controls Popout chrome only; mouse, pen, keyboard, focus, or pause reveals it. Fade off keeps the strip visible.
- Delay presets are Short `1500 ms`, Normal `2500 ms`, and Long `4000 ms`.
- Strip auto-hide engages only while Fade is enabled. It does not imply opacity or click-through.

### 6.3 Pin

- Source and Popout Pin values persist independently. Popout exposes a direct Pin control.
- While Popout owns playback, Source topmost is suspended; return restores the actual pre-popout Source state, including profile-derived Pin.
- Tooltips/accessibility names state the next action: `Pin` or `Unpin`.

## 7. Fade, opacity, and transparency

### 7.1 Controls fade

- Pointer, keyboard, focus, pause, and movement recovery rules are in section 6.2.

### 7.2 Chrome fade

- Strip auto-hide follows Fade and never reserves a second input or transparency mode.

### 7.3 Whole Popout opacity

- Active opacity affects the Source title-bar backdrop only and the entire active Popout. Idle opacity affects only the entire Popout. Hosted video follows Popout alpha.
- Movement restores the configured active value, not necessarily 100%. Idle never exceeds active.
- Normal Settings sliders stop at `0.45`; hand-edited persisted values down to `0.10` are honored. Values outside `0.10–1.00` normalize to `1.00`.

### 7.4 Click-through transparency

- Fade/opacity preserve input. Never set `WS_EX_TRANSPARENT`, transparent hit testing, or a transparent WebView.
- Chrome-only/video-opaque transparency requires a new scope/architecture decision.

## 8. Non-goals

- Downloading or re-hosting media; ad blocking/skipping; changing monetization; bypassing DRM, region, age, login, or playback restrictions.
- Multiple Popout Players, browser-native PiP, a custom decoder, a required YouTube API key, credential inspection/storage, or required global hotkeys.
- Cross-platform builds, click-through/mouse pass-through, transparent WebView2, or cropping video by default.

## 9. Technical direction

- Windows-only WPF, `net10.0-windows`, `UseWPF=true`, `Nullable=enable`, `ImplicitUsings=enable`, `SelfContained=false` by default.
- `PublishTrimmed=false`, `PublishSingleFile=false`, no NativeAOT.
- WebView2 Evergreen via `Microsoft.Web.WebView2` `1.0.3967.48`; one shared `CoreWebView2Environment` and channel-resolved user-data root.
- **REQ-APP-01:** one instance per channel/session. A second launch activates the existing instance and hands off a URL/profile where applicable; it never contends for the same WebView2 root.

## 10. Playback modes and presentation

### 10.1 Normal page mode

Default and release-facing playback uses a real YouTube watch page:

```text
https://www.youtube.com/watch?v=VIDEO_ID&t=123s
https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID&t=123s
```

Regular playlists and `RD...` mix/radio queues stay attached in Normal mode and survive return.

### 10.2 Compact mode

Compact embed/shell plumbing is dormant: `PlaybackModePolicy.CompactPlayerEnabled=false`, Settings exposes no Compact option, and new Popouts force Normal. Reserved data remains `PlayerSettings.CompactMode` plus `Profile.Mode`: `null` = global, `normal`, `compact`; legacy `embed` normalizes to `compact`.

If deliberately re-enabled, Compact minimum is 480 x 270 DIP, regular playlists remain supported, and `RD...` auto-generated lists degrade to one video. Normal minimum remains 320 x 180 DIP.

### 10.3 Local Compact shell

The dormant shell is served as `https://piplay.local/player.html` from `src\PiPlay\PlayerShell\`. It uses the YouTube IFrame API and versioned host messages. It must remain isolated from the Source navigation surface and fail back to Normal without opening a second player.

### 10.4 Standard and Focused presentation

- Standard is default. Optional Focused still uses the real Normal `/watch` page; it is independent from Compact.
- Focused uses `object-fit: contain`; free resize may letterbox and must never silently use `cover` or crop.
- Overlay controls: Mute, Captions, Settings, Pin, Expand/Restore, Close, Play/Pause, Next, progress, and time. Captions/Next hand off to YouTube and do nothing when unavailable.
- Empty overlay pixels are pointer-transparent; real controls are named, keyboard-focusable, and follow Fade.
- During ads, custom seek/Next or any skip-capable action is hidden/disabled and rechecks fail-closed. Required YouTube controls, ads, disclosures, links, branding, quality, and captions remain reachable.
- Selector failure restores ordinary page layout and leaves the native strip usable.

## 11. Architecture

One WPF dispatcher owns window/native state. Timers and page calls are single-flight/generation-guarded. Logging uses one bounded background queue. Close invalidates generations, stops timers, removes handlers/scripts/native hooks, disposes bridges before WebView, and drains logs.

Auto and Popout sync run at 250 ms with single-flight guards; Source suppression runs at 1 s while Popout owns playback; Focused fallback runs at 1 s only while active. Single-instance pipe retry is cancellation-aware, backs off from 250 ms to 30 s, and summarizes recovery once.

## 12. Component contracts

### 12.1 MainWindow / Source Window

- Owns the Source browser, navigation/profile commands, launch/return, placeholder, and shared Settings dialog. It must not parse URLs, embed raw JavaScript, or write settings directly.

### 12.2 PlayerWindow / Popout Player

- Owns Popout chrome/playback, Pin/Fade/appearance, placement, state polling, and the close report. It must not own Source navigation, profiles, or global Settings transactions.

### 12.3 WebViewEnvironmentService

- Owns one environment and data folder, missing-runtime errors, and virtual-host mapping.

### 12.4 YouTubeUrlHelper

- Supports `youtube.com/watch`, `youtu.be`, `/shorts/`, `/embed/`, watch playlists/mixes, and `/playlist?list=`. `ApplyList` retains charset-valid list IDs; `BuildWatchUrl` carries valid `RD...` with no `FallbackReason` or unsupported-mix note, while `BuildShellUrl` and `BuildEmbedUrl` omit it because the YouTube IFrame API cannot load auto-generated lists. Malformed list IDs set non-blocking `FallbackReason` instead of failing the video.

### 12.5 YouTubeDomBridge

All normal-page JavaScript is owned by `YouTubeDomBridge`:

```csharp
static Task<PlayerState?> ReadPlayerStateAsync(CoreWebView2 webView);
static Task PauseAsync(CoreWebView2 webView);
static Task<bool> SuppressPlaybackAsync(CoreWebView2 webView);
static Task PlayAsync(CoreWebView2 webView);
static Task SeekAsync(CoreWebView2 webView, int seconds);
static Task SeekAndPauseAsync(CoreWebView2 webView, int seconds);
static Task SeekAndPlayAsync(CoreWebView2 webView, int seconds);
static Task ApplyPlaybackSettingsAsync(CoreWebView2 webView, double? volume, bool? muted, double? playbackRate);
static Task<string?> ReadCanonicalUrlAsync(CoreWebView2 webView);
```

Page-to-host requests are exact-schema, versioned, nonce/current-document-token checked, top-document/source checked, and trusted-event gated. Focused may request only `close`, `pinToggle`, `fullscreenToggle`, or `settings`. Drag messages contain no coordinates.

### 12.6 SettingsService

- Owns schema `4`, sanitize/migrate/recover behavior, and atomic persistence.

### 12.7 WindowPlacementService

- Owns DPI-aware bounds/monitor restore and visible-work-area clamping.

## 13. Video Popout lifecycle

### 13.1 Preconditions

Browser initialized; no launch/return/clear/shutdown in progress; no player exists; current target is a supported video or playlist page. A playlist-only page may launch the first playable item.

### 13.2 Launch

1. Set the in-progress guard; read Source URL and player state.
2. Capture timestamp and `sourceWasPlayingAtPopout` before suppression.
3. Resolve video/list/start and presentation/profile precedence.
4. Require acknowledged mute+pause. On failure, restore captured state and do not construct Popout.
5. Hide Source WebView; show `Playing in Video Popout`; create/navigate Popout with the shared environment; start bounded state/suppression timers.

### 13.3 Source Placeholder

**Show Popout** and **Bring video back** have separate handlers. While the Tier-1 placeholder hides YouTube, disable Back/Reload/Home, URL, profile selection, and Save/Edit/Delete. Auto may be turned off and both recovery actions remain available.

### 13.4 Race gate

```csharp
if (!_browserReady || _popoutInProgress || _returnInProgress ||
    _player is not null || _clearingBrowserData || _mainWindowClosing)
    return;
```

### 13.5 Failure

Hide the placeholder, show Source WebView, restore captured mute/play state where possible, report a concise error, and coalesce repeated logs.

## 14. Return and close

Return state is nullable where unknown; zero is a valid timestamp:

```csharp
int? LastKnownSeconds;
bool? Paused;
double? Volume;
bool? Muted;
double? PlaybackRate;
string? VideoId;
string? PlaylistId;
```

- **REQ-RETURN-01:** live Popout paused/playing state wins when known; Source launch state is fallback only.
- **REQ-RETURN-07:** launching from paused must not auto-nudge Popout into playing. A later playing return must result from user action in Popout.
- Same-video return seeks; different-video/playlist return navigates with video/list context, then replays timestamp, paused state, volume/mute, and rate where YouTube permits.
- Return restores/activates a minimized Source without changing a previously maximized state, restores pre-popout Pin, and blocks Auto/manual re-entry until replay settles/fails/times out. Shutdown must not reopen or steal focus.
- Save placement/settings before and after fallible return scripting. Both durable checkpoints are required.

## 15. WebView and navigation

### 15.1 Shared environment

- Source and Popout use the single shared environment and channel-resolved user-data root from sections 9 and 12.3.

### 15.2 Navigation and new windows

- **REQ-NAV-01 Source:** allow YouTube (`youtube.com` subdomains, `youtu.be`, `youtube-nocookie.com`) and legitimate regional Google sign-in/account domains. Open unrelated top-level/new-window HTTP(S) targets in the system browser without a per-link prompt.
- **REQ-NAV-02 Popout:** remain on YouTube plus the same Google sign-in surface. Retarget playable YouTube links in the existing player; block or externally open unrelated targets.
- The allowlist prevents accidental drift, not malicious page compromise. Log domains only; never full credential-bearing URLs.

### 15.3 Navigation failure

- Navigation failure shows a compact retry state and preserves a safe URL.

### 15.4 Runtime failure

- Missing or failed WebView2 shows install/retry guidance.

## 16. Window behavior

### 16.1 Popout Player behavior

- Source minimum is 760 x 480 DIP. Normal Popout minimum is 320 x 180; dormant Compact is 480 x 270.
- Keep `UseLayoutRounding=False` on top-level windows and `AllowsTransparency=False`.

### 16.2 Dragging behavior

- Popout top handle is 44 DIP. Trusted primary mouse/pen drag on passive video/unused YouTube chrome begins only after system drag threshold. Below threshold it remains a click.
- Never arm drag over buttons, links, inputs, menus, captions, timeline/progress/volume, settings/fullscreen, overlay controls, end cards, or ads with actions. No child-frame wiring, touch drag, coordinates, global hooks, or drag while maximized.

### 16.3 Resize behavior

- **REQ-WINDOW-02:** borderless windows expose a 12 DIP native resize band and 96 DIP diagonal corner length along that band; it is not a 96 x 96 content-stealing square. Optional outline is 0–2 px.

### 16.4 Multi-monitor behavior

- **REQ-WINDOW-01:** `PerMonitorV2` in `src\PiPlay\app.manifest`; restore same monitor when available, otherwise clamp to visible work area; remain crisp at 100%, 125%, 150%, and mixed DPI.

### 16.5 Keyboard behavior

- Keyboard focus and shortcuts must remain available as specified in section 20.

## 17. Profiles and appearance ownership

- Profiles contain `name`, `url`, optional `mode`, `presentation`, `accentColor`, `topmost`, `fadeEnabled`, and bounds/monitor identity.
- **REQ-PROFILE-01:** an applicable profile overrides global settings per non-null field; unset fields inherit global values. Presentation applies only to that profile target.
- **REQ-PROFILE-02:** profiles store bounds and monitor identity; restore that monitor or clamp visibly.
- Duplicate names prompt overwrite/rename; URLs validate; broken values fail gracefully.
- A valid active profile color drives the app accent; a colorless profile inherits `theme.accentColor`. Stored colors remain exact; presentation colors may be minimally contrast-corrected.
- Accent intensity is `0–100`: 0 removes shared shell/background reach and the Popout edge. `AccentChromeGlyph` reaches full strength at 50; `AccentLetterbox`, `AppBackgroundWash`, `PopoutAccentEdge`, and shell tint continue scaling through 100. Primary controls stay accented; profile-row identity remains at preset `SubtleAlpha`, independent of intensity. Profile/accent changes apply live.
- Background derivation: letterbox = `Mix(#000000, Primary, 0.06 × reach)`; room tone = `Mix(AppBackground, Primary, 0.04 × reach)`. Profile rows use their own accent at `ProfileRowWashAlpha` (theme `SubtleAlpha`); Popout root uses a 1 px inset `PopoutAccentEdge`. Settings keeps plain `AppBackground`.

## 18. Logging

Default log: `%LOCALAPPDATA%\PiPlay\logs\piplay.log` (Stable substitutes `<exeDir>\PiPlayData`). Log startup/shutdown, WebView initialization, navigation failure, Popout launch/return, settings/runtime failures, and bounded recovery summaries.

Never log cookies, authorization headers, full credential URLs, command lines containing secrets, or unsanitized search text. Queue, batches, failed-batch retention, and files must stay bounded.

## 19. Security and privacy

- **REQ-PRIVACY-01:** **Reset app state** clears `settings.json` state/profiles/placement but keeps WebView2 browser data and YouTube login.
- **REQ-PRIVACY-02:** **Clear browser data** is a separate confirmed action that closes Popout, clears WebView2 `AllProfile`, and logs the user out. A timed-out clear remains single-flight until its underlying task reaches terminal completion.
- Treat `WebView2UserData\` as private browser data. No telemetry or uploads.
- Full locations and uninstall behavior are in `Data_and_Privacy_Map.md`; YouTube restrictions are in `YouTube_Compliance.md`.

## 20. Accessibility and usability

- Every icon-only control has an accessible name and visible keyboard focus. Pin/transfer names follow current state.
- `Ctrl+L` and `F6` select the Source URL while navigation is available. Standard YouTube shortcuts remain available when WebView is focused.
- Dark surfaces meet contrast requirements; arbitrary stored accents retain readable presentation tokens. Popout is always recoverable at every opacity.

## 21. Packaging and release

### 21.1 Development builds

Development publish:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

### 21.2 Shareable builds

Shareable self-contained publish:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

- Release candidates use committed `VERSION`, `BUILD_NUMBER`, and `docs/CHANGELOG.md`, then `.\scripts\Publish-Stable.ps1` and `.\scripts\Verify-StableDeploy.ps1` from a clean tree.
- **REQ-RELEASE-01:** code signing is deferred and not a release gate. Current provenance is exact-source commit, `stable-vX.Y.Z-bN`, and verifier hashes. Optional `-SignScript <path>` runs before hashes. Public distribution under a real certificate revives the requirement.
- No trimming/single-file/AOT. Release notes must name WebView2 Evergreen; formal installer/auto-update remain deferred.

## 22. Quality gates

### 22.1 Functional acceptance

- Navigation/watch/playlist-page launch; timestamp tolerance; no duplicate audio; placeholder; one player/instance; all return-state variants including zero and paused launch.
- `watch?v=X&list=PL...` and playlist-only pages retain list context. `watch?v=X&list=RD...` carries the mix in Normal, advances, and returns with the mix; dormant Compact omits RD. Malformed list IDs degrade to one video with a non-blocking note.
- Source/Popout external-link and Google-login policies; Auto eligibility/de-dup; Settings single-dialog behavior; privacy actions; runtime/network/corrupt-settings recovery.
- Standard/Focused precedence, no-crop layout, trusted-current-document actions, ad fail-closed behavior, passive-drag threshold/exclusions, and close/return recovery.

### 22.2 Chrome acceptance

- `UI-CHK-1`: all icons resolve; zero empty boxes. `UI-CHK-2/3`: closed/open profile controls are dark. `UI-CHK-4`: tooltips are dark and non-occluding. `UI-CHK-5`: URL text is legible at fractional DPI. `UI-CHK-6`: icon style/state is coherent.
- `UI-CHK-7`: icon-only controls expose accurate names. `UI-CHK-8`: 12 DIP band reads as letterbox/canvas, not a second frame. `UI-CHK-9`: accent wash stays restrained and profile rail remains visible without row shifts.
- `UI-CHK-10`: active profile retints toolbar, primary action, title/background/letterbox, and Popout identity without accenting the caption row or changing Close-red hover. `UI-CHK-11`: accent editing targets the named profile or global fallback without overwriting the other. `UI-CHK-12`: preset/corner preview commits only on Done and otherwise restores exactly.

### 22.3 Reliability

Run two hours of repeated Popout/return, 20 app restarts, logged-in/out, autoplay allowed/blocked, Standard/Focused, mixed-DPI movement, resize, snap, and round-region restore. No unbounded log/settings/resource growth.

### 22.4 Performance

- Warm Popout video visible in about 1.5 s; cold first-run environment initialization exempt.
- CPU/GPU comparable to a normal browser WebView for the same playback.
- Static runtime audit preserves bounded/single-flight hot paths; unresolved measurements live in `SPEC_GAPS_AND_OWNERSHIP.md`.

### 22.5 Definition of Done

Automated tests, deployed functional/manual checks, and true-render chrome checks are equal release gates. Use `QA_Checklist.md`; diagnostic/dirty deployments are not release evidence.

## 23. Deferred scope

Deferred: re-enabling Compact, main-window Browse/Cinema/Compact layouts, chrome-only/video-opaque transparency, wallpapers/imagery or any surface under/over video pending an architecture lift, tray mode, optional global hotkeys, import/export, installer/auto-update, multiple players, cross-platform work, high-contrast-specific behavior, configurable title-bar size, and larger touch targets.

## 24. Unresolved work

`SPEC_GAPS_AND_OWNERSHIP.md` is the sole current backlog/ownership surface. Do not add historical status here.

## 25. Resolved defaults

`DECISIONS.md` owns ADR-0001 through ADR-0008. Requirements in sections 3–22 remain normative unless explicitly superseded there.

## 26. Implementation constraints

### 26.1 Source Placeholder helper

Source Placeholder fallback remains:

```csharp
Browser.Visibility = visible ? Visibility.Hidden : Visibility.Visible;
SourcePlaceholder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
```

### 26.2 Nullable timestamp

Return timestamps remain nullable; zero is a valid value, as specified in section 14.

### 26.3 Safer video selector

Video selector remains centralized and ordered:

```js
document.querySelector('#movie_player video.html5-main-video') ||
document.querySelector('video.html5-main-video') ||
document.querySelector('video')
```

### 26.4 Atomic settings save

Settings persistence must write a temp file, flush writer and stream with `flushToDisk: true`, then use same-volume `File.Move(tempPath, FilePath, overwrite: true)` or `File.Replace`. Never overwrite live settings with `File.Copy`.

### 26.5 Borderless resize contract

The 12 DIP band and 96 DIP corner reach in section 16.3 are native hit-test dimensions, not content padding.

### 26.6 Transparency caution

WebView2 remains an opaque child HWND; keep `AllowsTransparency=False` and the no-click-through rule in section 7.4.

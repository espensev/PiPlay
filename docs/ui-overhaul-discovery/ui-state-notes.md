# UI State Notes

Purpose: document each visible PiPlay state against the actual source names, so design/change notes can point back to the code without translation.

## State 01 - Source Window, Logged In YouTube Home

Capture:

- Screenshot: `screenshots/piplay-current-20260610-180854.png`
- Captured: 2026-06-10 18:08 local time
- Running window title: `PiPlay - Stable v0.4.0-beta (b13)`
- User-visible situation: app is open, user is logged into YouTube, the YouTube home feed is visible, and YouTube's own in-page mini-player is visible in the lower-right of the WebView.

Primary code surface:

- `MainWindow` / Source Window: `src/PiPlay/MainWindow.xaml`
- Source behavior and events: `src/PiPlay/MainWindow.xaml.cs`
- Shared visual styling: `src/PiPlay/Theme/ControlStyles.xaml`
- Settings model affecting this state: `src/PiPlay/Models/AppSettings.cs`

PiPlay-owned UI elements visible in this state:

| Area | Code name | XAML / handler | Current visual role |
|---|---|---|---|
| Title bar | app icon image | `MainWindow.xaml`, `Assets/piplay.ico` | Left app identity icon. |
| Title bar | `TitleText` | `MainWindow.xaml`; text updated by `ApplyChannelTitle()` | Shows `PiPlay - Stable v0.4.0-beta (b13)` for the Stable channel. |
| Title bar | `PinnedHint` | `MainWindow.xaml`; toggled by `ApplyTopmost()` | Hidden in this capture; appears when main window is pinned on top. |
| Title bar | `SettingsButton` | `SettingsButton_Click()` | Gear button opens `SettingsWindow`. |
| Title bar | `MinimizeButton` | `MinimizeButton_Click()` | Minimizes the main window. |
| Title bar | `MaximizeButton` | `MaximizeButton_Click()` | Toggles normal/maximized; glyph is updated on `StateChanged`. |
| Title bar | `CloseButton` | `CloseButton_Click()` | Closes the app and also closes an active popout if present. |
| Toolbar navigation | `BackButton` | `BackButton_Click()` | Goes back if `Browser.CoreWebView2.CanGoBack` is true. |
| Toolbar navigation | `ReloadButton` | `ReloadButton_Click()` | Reloads the current WebView page. |
| Toolbar navigation | `HomeButton` | `HomeButton_Click()` | Navigates to `https://www.youtube.com/`. |
| Toolbar URL/search | `UrlBox` | `UrlBox_KeyDown()`; updated by `Core_SourceChanged()` | Shows the current WebView URL and accepts a YouTube URL or search text. |
| Toolbar profiles | `ProfilesCombo` | `ProfilesCombo_SelectionChanged()`; populated by `LoadProfilesIntoCombo()` | Shows saved profiles; placeholder is `Profiles` when none is selected. |
| Toolbar profiles | `SaveProfileButton` | `SaveProfileButton_Click()` | Saves the current URL as a profile after validation and naming. |
| Toolbar profiles | `EditProfileButton` | `EditProfileButton_Click()` | Disabled unless `ProfilesCombo.SelectedItem is Profile`. |
| Toolbar profiles | `DeleteProfileButton` | `DeleteProfileButton_Click()` | Disabled unless `ProfilesCombo.SelectedItem is Profile`. |
| Toolbar state | `PinToggle` | `PinToggle_Click()` | Pins the main `MainWindow` on top and persists `MainWindow.Topmost`. |
| Toolbar state | `AutoToggle` | `AutoToggle_Click()` | Enables/disables auto-popout detection. |
| Toolbar action | `PopOutButton` | `PopOutButton_Click()` -> `StartVideoPopoutAsync()` | Starts the Video Popout flow; enabled after WebView2 initialization. |
| Content | `Browser` | `InitializeBrowserAsync()` | The main WebView2 host for YouTube. |
| Content overlay | `SourcePlaceholder` | `ShowSourcePlaceholder()` | Hidden here; shown while PiPlay-owned `PlayerWindow` owns playback. |
| Content overlay | `RuntimeErrorPanel` / `RuntimeErrorText` | `ShowRuntimeError()` | Hidden here; shown when WebView2 runtime/init fails. |

External YouTube/WebView content visible in this state:

- YouTube's header, sidebar, topic chips, feed cards, and account avatar are not PiPlay-owned controls. They are rendered inside `Browser`.
- The lower-right YouTube mini-player is also inside `Browser`. It is not `PlayerWindow`, not `SourcePlaceholder`, and not PiPlay's popout surface.
- Because the WebView URL in the capture is `https://www.youtube.com/`, code that keys off the current/canonical URL may treat this as the home page even while YouTube's own mini-player is visible.

Relevant behavior notes from source:

- `InitializeBrowserAsync()` creates the shared WebView2 environment, wires navigation handlers, enables `PopOutButton`, then navigates to `_settings.LastUrl`.
- `Core_SourceChanged()` copies `Browser.CoreWebView2.Source` into `UrlBox`.
- `ResolveNavigationUrl()` accepts YouTube URLs/video IDs, otherwise converts typed text into a YouTube search URL.
- `LoadProfilesIntoCombo()` always clears selection after binding `_settings.Profiles`, so edit/delete remain disabled until a profile is selected.
- `StartVideoPopoutAsync()` reads the video state, resolves the popout target using `ReadCanonicalUrlAsync()` first and `core.Source` second, pauses source playback, shows `SourcePlaceholder`, then creates `PlayerWindow`.
- `AutoTimer_Tick()` only auto-pops when `AutoPopoutPolicy` sees playback on a watch/share URL; the home page URL is explicitly not a watch URL.

Current change candidates for this state:

- `PopOutButton` is enabled whenever WebView2 is ready, even on YouTube home. In this capture, a YouTube mini-player is visible, but `ResolvePopoutTargetAsync()` may still fail because the page URL is home. We may want a clearer disabled/empty state, or a DOM-based mini-player target resolver.
- `AutoToggle` is icon-only. Its off/on state may not be obvious without the tooltip, especially next to `PinToggle` and the primary `PopOutButton`.
- The toolbar is dense at this width. `ProfilesCombo`, three profile buttons, `PinToggle`, `AutoToggle`, and `PopOutButton` all compete with `UrlBox`; narrower windows should be checked separately.
- The YouTube mini-player can visually overlap the main content, but PiPlay cannot restyle it directly because it is external page UI inside `Browser`.
- If this state is meant to represent a simple "logged in home" baseline, the persisted YouTube mini-player makes it less clean as a baseline; consider a separate capture with no YouTube mini-player active.

## State 02 - Selected, Source Watch Page in YouTube Mix

Capture:

- Screenshot: `screenshots/piplay-state02-video-selected-20260610-181406.png`
- Captured: 2026-06-10 18:14 local time
- Running window title: `PiPlay - Stable v0.4.0-beta (b13)`
- UI Automation confirmed `UrlBox.Value`: `https://www.youtube.com/watch?v=ICgUdkLkYZ4&list=RDICgUdkLkYZ4&start_radio=1`
- User-visible situation: a YouTube watch page is open and the selected/playing video is `AMARANTHE - Interference (OFFICIAL MUSIC VIDEO)`.

Primary code surface:

- `MainWindow` / Source Window: `src/PiPlay/MainWindow.xaml`
- Video popout flow: `StartVideoPopoutAsync()` and `ResolvePopoutTargetAsync()` in `src/PiPlay/MainWindow.xaml.cs`
- URL parsing and mix/radio fallback: `YouTubeUrlHelper.TryParse()` and `ApplyList()` in `src/PiPlay/Services/YouTubeUrlHelper.cs`
- Auto-popout gate: `AutoTimer_Tick()` in `src/PiPlay/MainWindow.xaml.cs`, plus `AutoPopoutPolicy.Decide()`
- Playback mode resolution: `PlaybackModePolicy.ResolveEffectiveMode()` and `BuildPopoutUrl()`

PiPlay-owned UI element status in this state:

| Code name | Current status | Notes |
|---|---|---|
| `TitleText` | `PiPlay - Stable v0.4.0-beta (b13)` | Stable channel title is applied by `ApplyChannelTitle()`. |
| `SettingsButton` | Enabled | Opens `SettingsWindow`. UIA name currently reports the glyph, not `Settings`. |
| `MinimizeButton` | Enabled | UIA name currently reports the glyph, not `Minimize`. |
| `MaximizeButton` | Enabled | UIA name currently reports the glyph, not `Maximize`. |
| `CloseButton` | Enabled | UIA name currently reports the glyph, not `Close`. |
| `BackButton` | Enabled | UIA name currently reports the glyph, not `Back`. |
| `ReloadButton` | Enabled | UIA name currently reports the glyph, not `Reload`. |
| `HomeButton` | Enabled | UIA name currently reports the glyph, not `YouTube home`. |
| `UrlBox` | Enabled, populated with a `/watch` URL | Current URL includes `list=RD...` and `start_radio=1`, meaning this is a YouTube mix/radio watch URL. |
| `ProfilesCombo` | Enabled, no selected profile | Placeholder visible as `Profiles`; UIA name is empty. |
| `SaveProfileButton` | Enabled | Can save the current watch URL as a profile. UIA name currently reports the glyph. |
| `EditProfileButton` | Disabled | Disabled because no profile is selected. |
| `DeleteProfileButton` | Disabled | Disabled because no profile is selected. |
| `PinToggle` | Off | Main window is not pinned on top; `PinnedHint` is hidden. UIA name currently reports the glyph. |
| `AutoToggle` | Off | Auto-popout detector is not running. UIA name currently reports the glyph. |
| `PopOutButton` | Enabled | Pressing it should enter `StartVideoPopoutAsync()` and attempt a PiPlay popout. UIA name is empty. |
| `Browser` | Visible | Hosts the YouTube watch page. |
| `SourcePlaceholder` | Hidden | Will be shown only after PiPlay starts `PlayerWindow`. |
| `RuntimeErrorPanel` | Hidden | WebView2 runtime is initialized successfully. |

External YouTube/WebView content visible in this state:

- The main YouTube player is visible and appears to be playing or hover-focused; the player overlay shows `Pause`.
- The right side shows YouTube's mix playlist panel headed `Mix - AMARANTHE - Interference...`.
- YouTube's watch title, channel row, engagement buttons, playlist queue, and recommendations are external page UI inside `Browser`.

Relevant behavior notes from source:

- `ResolvePopoutTargetAsync()` first reads the page canonical URL through `YouTubeDomBridge.ReadCanonicalUrlAsync()`, then falls back to `core.Source`.
- `YouTubeUrlHelper.TryParse()` accepts this as a valid watch video because it has `v=ICgUdkLkYZ4`.
- `YouTubeUrlHelper.ApplyList()` treats list ids starting with `RD` as mix/radio playlists, drops the playlist id, and records `FallbackReason = "Mix/radio playlists aren't supported in Video Popout - popped out the current video."`
- `StartVideoPopoutAsync()` logs `target.FallbackReason` but does not currently show that note in the UI.
- If `AutoToggle` were on, `AutoTimer_Tick()` would see this as a watch URL via `YouTubeUrlHelper.IsWatchUrl()` and could auto-pop once playback is detected.

Current change candidates for this state:

- Add explicit `AutomationProperties.Name` values for icon-only and templated controls. Tooltips are present visually, but UIA currently exposes several controls as glyphs and exposes `PopOutButton` / `ProfilesCombo` with empty names.
- Decide whether the `RD...` mix/radio fallback should be user-visible. Today the app silently pops only the current video and logs the reason.
- Consider whether saving a profile from an `RD...` mix URL should preserve the original mix URL, normalize to the current watch video, or warn that the playlist portion will not drive popout.
- If the intended product behavior is "pop out the selected YouTube queue item," this state is the baseline for testing that `PopOutButton` resolves the current video and does not accidentally attempt to preserve the unsupported mix playlist.

## State 03 - Source Fullscreen, YouTube Expanded Player

Capture:

- Screenshot: `screenshots/piplay-state03-youtube-fullscreen-20260610-181731.png`
- Captured: 2026-06-10 18:17 local time
- Running window title: `PiPlay - Stable v0.4.0-beta (b13)`
- UI Automation confirmed `WindowVisualState`: `Normal`
- UI Automation confirmed `UrlBox.Value`: `https://www.youtube.com/watch?v=80zlpkqZrgs&list=RDICgUdkLkYZ4&index=2`
- User-visible situation: from the selected YouTube mix/watch page, YouTube's player is expanded into a fullscreen-like view within the PiPlay source window.

Primary code surface:

- `MainWindow` / Source Window: `src/PiPlay/MainWindow.xaml`
- `Browser` WebView2 host: `src/PiPlay/MainWindow.xaml`
- Source navigation and current URL mirroring: `Core_SourceChanged()` in `src/PiPlay/MainWindow.xaml.cs`
- Main-window maximize button only: `MaximizeButton_Click()` in `src/PiPlay/MainWindow.xaml.cs`
- Separate compact-player fullscreen action, not this state: `PlayerShellProtocol.ActionFullscreenToggle` and `PlayerWindow` request handling.

PiPlay-owned UI element status in this state:

| Code name | Current status | Notes |
|---|---|---|
| `TitleText` | Visible | PiPlay title bar is still present, so this is not an OS-level borderless video fullscreen. |
| `SettingsButton` / `MinimizeButton` / `MaximizeButton` / `CloseButton` | Visible and enabled | Native PiPlay chrome remains accessible while YouTube is expanded. |
| `BackButton` / `ReloadButton` / `HomeButton` | Visible and enabled | PiPlay toolbar remains above the player. |
| `UrlBox` | Visible, populated with a `/watch` URL | The selected item changed from State 02 to `v=80zlpkqZrgs`, still within the `RDICgUdkLkYZ4` mix. |
| `ProfilesCombo` | Visible, no selected profile | Same profile state as State 02. |
| `PinToggle` | Off | Main window is not pinned on top. |
| `AutoToggle` | Off | Auto-popout detector is not running. |
| `PopOutButton` | Visible and enabled | Popout can still be started from this state. |
| `Browser` | Visible | YouTube's expanded player is rendered inside the WebView. |
| `SourcePlaceholder` | Hidden/collapsed | This is not the PiPlay popout placeholder state. |
| `RuntimeErrorPanel` | Hidden/collapsed | WebView2 is running normally. |

External YouTube/WebView content visible in this state:

- YouTube's player content fills nearly all of the `Browser` content area below PiPlay's toolbar.
- The YouTube watch-page title, channel row, playlist panel, and recommendations are hidden by YouTube's expanded player view.
- Any fullscreen controls, overlays, or keyboard behavior seen in the video area belong to YouTube/WebView content, not WPF controls.

Relevant behavior notes from source:

- There is no source-window-specific fullscreen handler in the current code search. PiPlay does not currently subscribe to a WebView2 fullscreen event for `Browser`.
- `MainWindow` remains `WindowVisualState=Normal`; this is distinct from PiPlay's `MaximizeButton_Click()` path.
- `PlayerWindow` has a compact-shell fullscreen request path that toggles its own `WindowState`, but this state is still the source `MainWindow`, not `PlayerWindow`.
- `PopOutButton` should still call the same `StartVideoPopoutAsync()` path as State 02, resolving the current video from the canonical/current URL.

Current change candidates for this state:

- Decide whether YouTube fullscreen/expanded mode should hide PiPlay's title bar and toolbar, or whether keeping PiPlay chrome visible is intentional.
- If true fullscreen is desired, investigate WebView2 fullscreen support/events for the source `Browser` and a reversible chrome-hidden state in `MainWindow`.
- If PiPlay chrome should remain visible, consider whether this state should be named in product copy as "expanded player" rather than "fullscreen" to avoid expectation drift.
- The accessible-name cleanup from State 02 still applies because all chrome controls remain visible in this state.

## State 04 - Popout Standard, Two Windows

Capture:

- Combined screenshot: `screenshots/piplay-state04-popout-two-windows-20260610-182343.png`
- Source-only screenshot: `screenshots/piplay-state04-popout-source-window-20260610-182343.png`
- Player-only screenshot, controls idle/hidden: `screenshots/piplay-state04-popout-player-window-20260610-182343.png`
- Player top-right detail, controls visible/non-collapsed: `screenshots/piplay-state04-popout-top-edge-reveal-attempt-20260610-182634.png`
- Fade-on two-window screenshot: `screenshots/piplay-state04-popout-standard-fade-on-two-windows-20260610-183000.png`
- Fade-on player screenshot: `screenshots/piplay-state04-popout-standard-fade-on-player-20260610-183000.png`
- Fade-on hovered player screenshot: `screenshots/piplay-state04-popout-standard-fade-on-hovered-player-20260610-183245.png`
- Fade-on hovered controls detail: `screenshots/piplay-state04-popout-standard-fade-on-hovered-controls-20260610-183245.png`
- Captured: 2026-06-10 18:23-18:26 local time
- Top-level windows detected: `PiPlay - Stable v0.4.0-beta (b13)` and `PiPlay Video Popout`
- User-visible situation: the app has started Video Popout. The source window is on the left with the placeholder visible, and the standard popout player is on the right in normal-page mode.

Primary code surface:

- Source placeholder: `SourcePlaceholder` and `ShowSourcePlaceholder()` in `src/PiPlay/MainWindow.xaml` / `src/PiPlay/MainWindow.xaml.cs`
- Popout creation: `StartVideoPopoutAsync()` in `src/PiPlay/MainWindow.xaml.cs`
- Popout window: `PlayerWindow` in `src/PiPlay/PlayerWindow.xaml` / `src/PiPlay/PlayerWindow.xaml.cs`
- Popout controls strip: `ChromeStrip`, `FadeToggle`, `PinToggle`, `CloseButton`
- Controls fade/collapse policy: `FadePolicy` and `PlayerWindow.ApplyFadeState()`, `ShowControls()`, `HideControls()`
- Whole-window opacity/fade: `WindowOpacityPolicy` and `WindowOpacityApplier`

PiPlay-owned UI element status in this state:

| Surface | Code name | Current status | Notes |
|---|---|---|---|
| Source window | `SourcePlaceholder` | Visible | Shows `Playing in Video Popout` while the popout owns playback. |
| Source window | `Browser` | Hidden | `ShowSourcePlaceholder(true)` hides the WebView so there is no duplicate playback in the source window. |
| Source window | `PopOutButton` | Visible and enabled | Code re-enables it after popout creation; if `_player` exists, pressing it activates the existing popout instead of creating another. |
| Source window | `UrlBox` | Visible | Still shows the selected `/watch` URL from the source context. |
| Player window | `PlayerWindow` | Visible, normal window state | Separate top-level WPF window titled `PiPlay Video Popout`. |
| Player window | `Player` | Visible | WebView2 host for the popped-out YouTube page. This is normal-page mode, not compact shell mode. |
| Player window | `ChromeStrip` | Visible in non-collapsed detail; hidden/collapsed in idle capture | The strip is the native PiPlay popout chrome row above `Player`. |
| Player window | `FadeToggle` | Visible in non-collapsed and hovered details; UIA sampled `On` after the user enabled fade controls | Eye icon; toggles whether popout controls fade when idle. |
| Player window | `PinToggle` | Visible in non-collapsed and hovered details | Pin icon; toggles `PlayerWindow.Topmost`. UIA sampled it as `Off` in the captured moment. |
| Player window | `CloseButton` | Visible in non-collapsed and hovered details | Closes the popout and returns playback to the source window. |
| Player window | `ErrorBar` | Hidden | No compact-shell error/fallback state is active. |

External YouTube/WebView content visible in this state:

- The right `Player` WebView shows the normal YouTube page UI, including the YouTube header/search/account controls, video area, and mix/next panel.
- YouTube content is translucent because the whole `PlayerWindow` appears to be in an opacity/fade state; desktop wallpaper is visible through the popout.
- The YouTube scrollbar and all YouTube page controls belong to the `Player` WebView, not to PiPlay WPF.
- In the fade-on capture, the native PiPlay strip is not visually present; UI Automation confirms `FadeToggle=On` and `PinToggle=Off`, so this is the pre-fullscreen fade-on standard state.
- In the fade-on hovered capture, the `ChromeStrip` is visible again: `FadeToggle` is highlighted/on, `PinToggle` is available, and `CloseButton` is available.

Relevant behavior notes from source:

- `StartVideoPopoutAsync()` pauses source playback, calls `ShowSourcePlaceholder(true)`, then creates exactly one `PlayerWindow`.
- `ShowSourcePlaceholder(true)` sets `Browser.Visibility = Hidden` and `SourcePlaceholder.Visibility = Visible`.
- `PlayerWindow` starts with `PinToggle.IsChecked = topmost` and `FadeToggle.IsChecked = fadeEnabled`; those values come from `_settings.Player`.
- `FadeToggle_Click()` changes `_fadeEnabled`, updates `_returnState.FadeEnabled`, and calls `ApplyFadeState()`.
- `PinToggle_Click()` maps directly to `Topmost = PinToggle.IsChecked == true`.
- `HideControls()` fades `ChromeStrip` to opacity 0; when strip auto-hide is enabled, `OnHideFadeCompleted()` also collapses it so the video gets the row height back.
- `OpacityHoverPoll_Tick()` watches the top-edge band (`FadePolicy.TopEdgeRevealBandDip`) to reveal a collapsed `ChromeStrip`.

Current change candidates for this state:

- Clarify the product vocabulary: call this **Popout Standard** or **standard popout**, reserving **fullscreen** for the later maximized/fullscreen popout state.
- The source placeholder is clear, but the source toolbar still offers `PopOutButton`; consider whether its label/state should change to `Show popout` or otherwise indicate it will activate the existing `PlayerWindow`.
- Add explicit `AutomationProperties.Name` values for `FadeToggle`, `PinToggle`, and `CloseButton`; UIA currently reports glyph names for those controls.
- Decide whether the standard popout should default to translucent/idle opacity. The current capture is visually striking but can make YouTube content compete with the desktop wallpaper.
- The controls-visible state is transient and a little hard to capture reliably; if this is a core feature, consider making top-edge reveal easier to discover or giving users a clearer non-collapsed mode.

Resize and scroll notes from code:

- Both `MainWindow` and `PlayerWindow` are borderless WPF windows with `WindowStyle="None"`, `ResizeMode="CanResize"`, `AllowsTransparency="False"`, and `WindowChrome.ResizeBorderThickness="10"`.
- `PlayerWindow` has `CaptionHeight="0"`; dragging is handled by `ChromeStrip_MouseLeftButtonDown()` on the visible strip instead of a native titlebar/caption region.
- `PlayerWindow` calls `BorderlessWindowHelper.EnableExpandedResizeZones(this)` in its constructor, so the popout gets the same `WM_NCHITTEST` resize helper as the source window.
- `BorderlessResizeHitTestPolicy` defines an invisible 10 DIP resize band on each edge and a 32 DIP corner length for diagonal resize. It returns Win32 hit-test values like `HTLEFT`, `HTRIGHT`, `HTTOPLEFT`, and `HTBOTTOMRIGHT`.
- Resize zones are active only when the window is resizable and `WindowState == Normal`; maximized/fullscreen-like popout states should not report resize zones.
- There is no visible lower-right resize grip in the code. The resize handles are invisible edge/corner hit zones.
- When the resize helper sees the cursor over an enabled WPF `ButtonBase`, the button wins over resize. This protects PiPlay controls such as `FadeToggle`, `PinToggle`, and `CloseButton`.
- WebView/YouTube controls are not WPF `ButtonBase` controls. The code does not add special precedence for YouTube's own scrollbar or in-page controls at the outer resize band.
- `PlayerWindow.Player` is a WebView2 control filling the remaining grid row. PiPlay does not implement custom scrolling for it; the visible page scrollbar in the popout is YouTube/WebView content.
- The app-wide WPF `ScrollBar` style in `ControlStyles.xaml` applies to WPF controls such as dropdowns, not to the HTML scrollbar rendered inside YouTube's WebView.
- The `ChromeStrip` row is `Auto`, and the strip itself has `Height="32"`. Tests pin this so when strip auto-hide collapses `ChromeStrip`, the video/WebView row gets that height back instead of leaving a dead band.
- ADR-0006 says opacity/fade are visual states only: the visible popout should remain draggable, resizable, and clickable; there is no click-through/pass-through mode.

Live observation to verify/fix:

- User observation in Popout Standard: resize appears to work only from the upper-left corner, upper-right corner, and the top segment between them.
- User observation: left edge, right edge, bottom edge, and lower corners do not appear to resize from this state.
- User observation: scroll is not working from this state.
- This does not match the code's intended resize contract, which expects all four edges plus all four corners while `PlayerWindow.WindowState == Normal`.
- Likely investigation path: the top edge is WPF `ChromeStrip`, while the side/bottom resize bands are over the `Player` WebView2 child HWND. WebView2/HwndHost airspace may be preventing the parent window's `WM_NCHITTEST` resize helper from seeing those side/bottom points.
- Scroll investigation path: PiPlay has no custom WebView scrolling logic, so determine whether mouse wheel, touchpad scroll, and dragging YouTube's own scrollbar all fail, or whether only the outer edge/scrollbar area is blocked by resize/airspace behavior.

## State 05 - Popout Fullview Faded

Capture:

- Player screenshot: `screenshots/piplay-state05-fullview-faded-player-window-20260610-183844.png`
- Source screenshot: `screenshots/piplay-state05-fullview-faded-source-window-20260610-183844.png`
- Combined screenshot: `screenshots/piplay-state05-fullview-faded-two-windows-20260610-183844.png`
- Preferred post-song-swap player screenshot: `screenshots/piplay-state05-fullview-faded-song-swapped-player-20260610-184017.png`
- Post-song-swap combined screenshot: `screenshots/piplay-state05-fullview-faded-song-swapped-two-windows-20260610-184017.png`
- Hovered player screenshot: `screenshots/piplay-state05-fullview-faded-hovered-player-20260610-184219.png`
- Captured: 2026-06-10 18:38 local time
- UI Automation confirmed `PlayerWindow.WindowVisualState`: `Normal`
- UI Automation confirmed `FadeToggle=On`, `PinToggle=Off`, and `ErrorBar` not present.

User-visible situation:

- The popout is in a fullview/faded presentation with YouTube's in-player controls visible.
- The window remains translucent enough for the desktop wallpaper to show through the video/page.
- This is still the normal-page `PlayerWindow` path with the `Player` WebView showing YouTube content.
- After the song swap, the preferred capture shows a cleaner faded fullview: the video fills the visible player area without the YouTube control overlay.
- Hovering over the fullview faded state brings back the same basic interaction layer: the PiPlay top strip is visible and YouTube's player overlay controls appear. The resize/scroll observation remains unchanged.

Relevant behavior notes from source:

- Because UIA reports `WindowVisualState=Normal`, `BorderlessResizeHitTestPolicy` would still consider resize zones active according to code.
- This differs from a true maximized/fullscreen window state, where `BorderlessResizeHitTestPolicy` would return no resize zones.
- `FadeToggle=On` means the shared fade/idle behavior still applies; hover/activity should restore controls and opacity according to `PlayerWindow.OnUserActivity()`.
- `ErrorBar` is absent, so this is not the compact-shell fallback/error path.

Live observation carried forward:

- User says resize/scroll behavior stays the same here as State 04.
- Expected by code: all four edges and four corners resize while the window is normal.
- Observed by user: only the upper-left corner, upper-right corner, and top segment resize; no scroll.
- Same likely investigation path as State 04: side/bottom hit testing and scroll input are happening over `Player` WebView2/HwndHost content rather than WPF chrome.

## Transition - Enable Compact Mode While Popout Is Open

User-visible transition:

- From the standard/fullview popout, opening Settings and choosing `Compact player` does not change the already-open `PlayerWindow`.
- The current popout stays visually and behaviorally the same.
- To see compact mode, the user has to close the existing popout and create/open the popout again.
- The intermediate transition state is the original video back in the old parent/source window: the `Browser` page that was playing when the user clicked `PopOutButton` the first time. `MainWindow` hides `SourcePlaceholder`, reveals that same `Browser`, and returns playback there before the next popout is launched.

What the code says:

- `SettingsWindow.CompactModeToggle` updates `SettingsWindow.CompactMode`.
- `MainWindow.ApplyPlayerPreferences(...)` persists `_settings.Player.CompactMode = compactMode`.
- The code comment in `ApplyPlayerPreferences(...)` is explicit: global compact-mode default takes effect on the next popout; an open player keeps its mode.
- The live `_player` only receives `_player?.ApplyAppearance(...)` and `_player?.ApplyWindowOpacity(...)`; it does not receive a mode switch or navigation rebuild.
- `StartVideoPopoutAsync()` resolves the effective mode only when creating a new player via `PlaybackModePolicy.ResolveEffectiveMode(...)`, then builds the URL with `PlaybackModePolicy.BuildPopoutUrl(...)`.
- If `_player is not null`, `StartVideoPopoutAsync()` just activates the existing popout and returns, so pressing `PopOutButton` again while the old popout is open will not create a compact replacement.
- `Player_OnClosed()` is the close/reopen bridge: it sets `_player = null`, persists the old popout state, calls `ShowSourcePlaceholder(false)`, and uses `ReturnPolicy.Decide(...)` to seek/play the source `Browser`.
- `ShowSourcePlaceholder(false)` makes `Browser.Visibility = Visible` and `SourcePlaceholder.Visibility = Collapsed`, so the old parent window becomes the visible playback surface again.
- `PlayerReturnState` carries `LastKnownSeconds`, `Topmost`, `FadeEnabled`, and `Placement`, but not a video id or URL. That means closing the popout resumes/seeks the original source `Browser`; it does not navigate the parent window to a later song if the popout advanced while open.

Change candidate:

- If this is intended behavior, the Settings UI should make it clear that `Compact player` applies to new popouts only.
- If live switching is desired, code would need an explicit `PlayerWindow` mode transition path or a close/reopen affordance, because the current implementation intentionally treats mode as a launch-time choice.

## State 06 - Compact Popout

Capture:

- Player screenshot: `screenshots/piplay-state06-compact-player-window-20260610-184928.png`
- Source screenshot: `screenshots/piplay-state06-compact-source-window-20260610-184928.png`
- Combined screenshot: `screenshots/piplay-state06-compact-two-windows-20260610-184928.png`
- User-provided detail crop: compact YouTube in-player expand/fullscreen icon visible in the lower-right control area.
- Captured: 2026-06-10 18:49 local time
- UI Automation confirmed `PlayerWindow.WindowVisualState`: `Normal`
- UI Automation confirmed `FadeToggle=On`, `PinToggle=Off`, and `ErrorBar` not present.

User-visible situation:

- Compact mode is active after closing/reopening the popout.
- The right `PlayerWindow` shows the YouTube embedded/compact player surface rather than the full YouTube watch page.
- Per-video playback mostly works as expected for YouTube compact/embed behavior.
- Source `MainWindow` remains in the same `SourcePlaceholder` state on the left.

Primary code surface:

- Compact URL generation: `PlaybackModePolicy.BuildPopoutUrl(...)` and `YouTubeUrlHelper.BuildShellUrl(...)`
- Compact shell host: `src/PiPlay/PlayerShell/player.html`
- Compact shell bridge: `src/PiPlay/PlayerShell/player-shell.js` and `PlayerShellBridge`
- Popout navigation/new-window handling: `PlayerWindow.Core_NavigationStarting(...)` and `PlayerWindow.Core_NewWindowRequested(...)`
- Allowlist: `NavigationPolicy.IsAllowed(..., NavigationSurface.Player)`

What the code says:

- Compact mode navigates `PlayerWindow.Player` to the local shell host (`https://piplay.local/player.html?...`).
- The shell creates a YouTube IFrame API player inside `#player`, with `html, body { overflow: hidden; }`.
- The shell carries only target params (`v`, optional `list`, optional `start`) and sends playback state back to the host.
- The shell action channel only allows window-level requests: `close`, `pinToggle`, and `fullscreenToggle`.
- If the shell sends `fullscreenToggle`, `PlayerWindow.ShellBridge_RequestReceived(...)` maps it to the native WPF window state: normal <-> maximized.
- The current shell code does not map YouTube's native embedded-player expand/fullscreen button to `postRequest("fullscreenToggle")`.
- The local shell does not call `requestFullscreen()` or define its own PiPlay expand button; it simply creates the `YT.Player` iframe and lets YouTube render its own controls.
- `PlayerWindow.Core_NewWindowRequested(...)` currently handles every new-window request by setting `e.Handled = true` and calling `OpenExternal(e.Uri)`.

Issue / desired behavior:

- Current observed behavior: clicking a video/suggestion from the compact YouTube UI opens it in another app/system browser.
- Current observed behavior: the YouTube in-player expand/fullscreen icon is visible in compact mode but does nothing when clicked.
- Desired behavior: normal left-clicking a video inside compact mode should stay in PiPlay, ideally in the compact player flow.
- Desired external behavior: opening another app/browser should happen only for explicit new-window intent, such as right-click -> open in new window.
- Desired fullscreen behavior: compact mode should have a reliable PiPlay-owned expand/fullview affordance, or the visible YouTube affordance should not imply a supported action if WebView2/YouTube embed behavior prevents it.
- Current code is too broad for that desired behavior because every `NewWindowRequested` from `PlayerWindow` is treated as external.
- Current code has a host-side fullscreen/maximize path, but that path is only used when the compact shell sends PiPlay's allowlisted `fullscreenToggle` request. YouTube's native iframe control does not currently trigger that path.

Rework candidates:

- Distinguish default left-click navigation from explicit new-window intent if WebView2 exposes enough event context for this surface.
- For allowed YouTube watch URLs from compact recommendations, consider rebuilding/navigating the compact shell URL in the same `PlayerWindow` instead of calling `OpenExternal(...)`.
- Keep truly external/non-YouTube URLs and explicit new-window actions routed to the system browser.
- Add a PiPlay-owned compact expand/fullview button wired to `PlayerShellProtocol.ActionFullscreenToggle`, or wire an equivalent host action outside the YouTube iframe.
- Investigate whether YouTube iframe fullscreen permission / WebView2 fullscreen plumbing can make the native YouTube expand icon work; if not, avoid relying on that native control.
- Add tests around `PlayerWindow.Core_NewWindowRequested(...)` policy once the desired event distinction is chosen.

Potential follow-up states to capture:

- Source Window on a normal non-mix `/watch` page before popout.
- Popout fullscreen/maximized state, if distinct from this normal-window fullview.
- `PlayerWindow` idle with fade/auto-hide active.

## State 07 - Settings Window, Player Customization Visible

Capture:

- Screenshot: `screenshots/piplay-state07-settings-window-20260610-185634.png`
- Captured: 2026-06-10 18:56 local time
- Running window title: `PiPlay settings`
- User-visible situation: the Settings dialog is open over the app, showing Privacy, Appearance, and Playback controls in one tall dialog.

Primary code surface:

- Settings dialog view: `src/PiPlay/SettingsWindow.xaml`
- Settings dialog behavior: `src/PiPlay/SettingsWindow.xaml.cs`
- Dialog launch/apply path: `MainWindow.SettingsButton_Click(...)` and `ApplyPlayerPreferences(...)` in `src/PiPlay/MainWindow.xaml.cs`
- Persisted settings model: `PlayerSettings` in `src/PiPlay/Models/AppSettings.cs`
- Privacy wording/actions: `PrivacyService` in `src/PiPlay/Services/PrivacyService.cs`

PiPlay-owned UI element status in this state:

| Section | Code name | Current visual status | Notes |
|---|---|---|---|
| Title bar | `CloseButton` | Visible | Closes the dialog. If `AppearanceChanged` is true, `CloseButton_Click()` completes the dialog so changes are applied. |
| Privacy | `ResetDescriptionText` | Visible | Text comes from `PrivacyService.ResetDescription`; promises the YouTube sign-in is kept. |
| Privacy | `ResetAppStateButton` | Enabled | Opens a confirmation, then sets `RequestedAction = ResetAppState`. |
| Privacy | `ClearDescriptionText` | Visible | Text comes from `PrivacyService.ClearDescription`; warns that YouTube sign-in will be cleared. |
| Privacy | `ClearBrowserDataButton` | Enabled in this capture | Enabled when `MainWindow.CanClearBrowserData` is true; otherwise disabled with `PrivacyService.ClearNotReadyHint`. |
| Appearance | `PinAccentCyanSwatch` / `PinAccentVioletSwatch` / `PinAccentGreenSwatch` / `PinAccentAmberSwatch` | Visible; Violet appears selected | Changes `PinAccent` and sets `AppearanceChanged = true`. |
| Appearance | `FadeAccentCyanSwatch` / `FadeAccentVioletSwatch` / `FadeAccentGreenSwatch` / `FadeAccentAmberSwatch` | Visible; Cyan appears selected | Changes `FadeAccent` and sets `AppearanceChanged = true`. |
| Appearance | `FadeDelayShortPreset` / `FadeDelayNormalPreset` / `FadeDelayLongPreset` | Visible; Short appears selected | Tags map to 1500, 2500, and 4000 ms. |
| Appearance | `ActiveOpacitySlider` / `ActiveOpacityValueText` | Visible at `85%` | Slider range is 45-100 in the UI. Moves live-preview on an open popout. |
| Appearance | `IdleOpacitySlider` / `IdleOpacityValueText` | Visible at `78%` | Shares the same 45-100 UI range and live-preview path. |
| Appearance | `StripAutoHideToggle` | Visible; appears off | Controls whether the popout top bar collapses while idle. |
| Playback | `CompactModeToggle` | Visible; appears on | Sets the global compact-player default for new popouts. |

Relevant behavior notes from source:

- `SettingsWindow` is borderless (`WindowStyle="None"`), non-resizable (`ResizeMode="NoResize"`), hidden from the taskbar (`ShowInTaskbar="False"`), and starts centered on its owner.
- `SettingsButton_Click()` constructs `SettingsWindow` from `_settings.Player`, sets the owner to `MainWindow`, and makes the dialog topmost when the owner is topmost.
- Opacity slider movement is the one live-previewed setting: `OpacityPreviewChanged` calls `_player?.ApplyWindowOpacity(..., animate: false)` while the dialog is still open.
- If the dialog is dismissed without applying, `MainWindow` restores the open popout to the persisted opacity values.
- When appearance settings are applied, `ApplyPlayerPreferences(...)` persists pin color, fade color, fade delay, compact mode, active/idle opacity, and strip auto-hide to `_settings.Player`.
- Open popouts receive live appearance updates for accents, fade delay, strip auto-hide, and opacity. Compact mode is explicitly different: the code comment says it takes effect on the next popout and an open player keeps its mode.
- Reset app state is handled after the modal closes by `PerformResetAppState()` / `ApplyResetState()`; the settings dialog itself only records `RequestedAction`.
- Clear browser data is handled after the modal closes by `PerformClearBrowserDataAsync()` and `PrivacyService.ClearBrowserDataAsync(...)`.

Current change candidates for this state:

- The dialog is now tall: the captured window is 720 x 1249 physical pixels. The XAML uses `SizeToContent="Height"`, `ResizeMode="NoResize"`, and no `ScrollViewer`, so shorter displays may clip the bottom controls.
- The visible `Compact player` copy does not say "new popouts only" on the button itself. The tooltip does, but the earlier transition showed this is easy to miss.
- Consider grouping opacity and top-bar controls into a denser layout if more player settings are added.
- `CloseButton` likely needs an explicit `AutomationProperties.Name`, matching the accessibility cleanup noted for other icon-only controls.

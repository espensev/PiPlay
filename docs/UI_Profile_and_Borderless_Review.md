# UI, Profile Refresh & Borderless Blending Review and Implementation Guide (Refined)

> **Scope:** Synthesized engineering findings and adversarially refined implementation specifications from 11 focused narrow review passes, 10 adversarial cross-review passes, 6 dedicated follow-up design passes, 8 narrow plan-refinement passes, and analysis of the ambient desktop Popout Player example-of-use scenario.  
> **Status:** Implemented, Cross-Reviewed & Verified in `v0.14.0 (build 40)` (`1,045 passed, 0 failed`).

---

## 1. Ambient Desktop "Example of Use" Analysis

In the ambient desktop centerpiece workflow, the **Popout Player** floats over a near-black/deep-indigo desktop (`~#05050F`) between desktop widgets (clock and audio spectrum visualizer) while playing dark-frame 16:9 video. Four specific implementation mechanics currently prevent the Popout Player from disappearing seamlessly into the desktop background:

### 1.1 Static `PopoutEdgeBorder` (`BorderThickness="1"`, `CornerRadius="0"`)
- **Location:** `src/PiPlay/PlayerWindow.xaml:29-30`, `src/PiPlay/PlayerWindow.xaml.cs:203-206, 559-583, 853-857, 976-984, 1189-1227, 1302-1338`.
- **Mechanic:** `PopoutEdgeBorder` hardcodes `BorderThickness="1"` and omits `CornerRadius` (defaulting to `0`—which bypassed `No_hardcoded_corner_radii_outside_the_token_dictionary` in `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:778-811` because that test only checks elements that explicitly declare a `CornerRadius` attribute).
- **Visual Defect:**
  - `PopoutEdgeBorder.BorderThickness` is never collapsed in `PlayerWindow.xaml.cs`. It remains `1` in `Focused` presentation (`_focusedSurfaceActive`), when `ChromeStrip` auto-hides on idle (`OnHideFadeCompleted`), when `WindowState == Maximized`, when `IsSnapLike` is `true`, and when `AccentIntensity == 0` (`PopoutAccentEdge` alpha `0` still reserves a 1 DIP layout inset on all 4 sides).
  - When `RoundedWindowRegionApplier.Apply` (`src/PiPlay/Services/RoundedWindowRegionApplier.cs:19-33`) clips the HWND to a 22 DIP `CreateRoundRectRgn(0, 0, g.WidthPx, g.HeightPx, ...)`, all four square corners of `PopoutEdgeBorder` are severed by the curved clip region, and Win32 GDI's exclusive `(right, bottom)` coordinates shave 1 physical pixel off the right and bottom edges—leaving an asymmetric 1px top/left border line.

### 1.2 12 DIP `AccentLetterbox` Resize Mat Around `#000000` Video
- **Location:** `src/PiPlay/PlayerWindow.xaml:11, 117`, `src/PiPlay/PlayerWindow.xaml.cs:941-952`, `src/PiPlay/Theme/ThemeColors.cs:23, 234`, `src/PiPlay/Services/YouTubeDomBridge.cs:416-437`.
- **Mechanic:** Because `WebView2` is a child HWND (airspace) that swallows mouse input, `Player` uses `Margin="12,0,12,12"` in `Standard` mode (`12,12,12,12` in `Focused` mode) when `WindowState == Normal` so the parent WPF window has a 12 DIP perimeter for `WM_NCHITTEST` resize hit-testing (`tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:62-86`).
- **Visual Defect:** `PlayerWindow.Background` binds to `{DynamicResource AccentLetterbox}`, which mixes up to 6% of the accent color into `#000000` (`LetterboxMixCeiling = 0.06` in `src/PiPlay/Theme/ThemeColors.cs:23`). Inside `Player` (`WebView2`), `.piplay-focused-active` and `#movie_player` force `#000000`. Whenever `AccentIntensity > 0`, the 12 DIP resize band renders as a visible tinted frame around the pure-black video surface instead of blending into `#000000`.

### 1.3 Missing Top 12 DIP Resize Inset When `ChromeStrip` Auto-Hides in `Standard` Mode
- **Location:** `src/PiPlay/PlayerWindow.xaml.cs:941-952, 1302-1318, 1331-1338`.
- **Mechanic:** `ApplyFocusedResizeInset()` (`line 943`) returns early when `_presentation != PopoutPresentation.Focused`.
- **Visual & Interaction Defect:** When `_stripAutoHide` collapses `ChromeStrip` on idle in `Standard` mode (`OnHideFadeCompleted` at `line 1337`), `Grid.Row="0"` (`Height="Auto"`) collapses to `0` height, but `Player.Margin` remains `12,0,12,12` (`Top = 0`). The `WebView2` child HWND shifts up to `Y = 1` DIP (just inside the 1 DIP `PopoutEdgeBorder`), swallowing `WM_NCHITTEST` along the top edge (`HTTOP` resize is lost) and leaving a 3-sided 12 DIP mat on left/right/bottom with a 1px line at the top.

### 1.4 Saved Profile Window Bounds, Fade & Popout Topmost Not Applied on Launch
- **Location:** `src/PiPlay/Models/Profile.cs:33-35`, `src/PiPlay/Services/ProfileService.cs:35-45`, `src/PiPlay/MainWindow.xaml.cs:779, 801-806, 848-851, 1516-1573`.
- **Mechanic:** `Profile` persists `Topmost`, `FadeEnabled`, and `Bounds` (`PlacementData?`). However:
  - `Profile.FadeEnabled` and `Profile.Bounds` have zero readers anywhere in `src/PiPlay/`.
  - `Profile.Topmost` is applied only to `MainWindow` via `ApplyTopmost` on profile selection (`src/PiPlay/MainWindow.xaml.cs:779`), never when constructing `PlayerWindow` (`src/PiPlay/MainWindow.xaml.cs:1566`: `new PlayerWindow(env, popoutUrl, _settings.Player.Topmost, ...)`).
  - Consequently, selecting a saved ambient desktop profile does not position or size the Popout Player to the profile's `Bounds` (`PlacementData`) or apply its `FadeEnabled`/`Topmost` state.

---

## 2. Multi-Pass & Cross-Review Findings Ledger

### 2.1 Theme Presets & Token Pipeline (`ThemeCatalog`, `ThemeColors`, `Colors.xaml`, `ThemeResourceApplier`)
| ID | Severity | Location | Verified Finding |
|---|---|---|---|
| **THM-01** | **P1** | `src/PiPlay/Theme/ThemeCatalog.cs:255-268`, `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:1122-1134` | **`soft-glass` has the highest border and secondary-text contrast of all three presets**, contradicting `ThemeCatalog.cs:255-256` (*"quieted borders and secondary text"*). `soft-glass` `BorderSubtle` (`#2A3A52` on `#121A26` = **`1.52:1`**), `BorderStrong` (`#3A4D6A` = **`2.04:1`**), and `TextSecondary` (`#C4CEDC` = **`10.99:1`**) all exceed `sharp-dark` (`1.17:1`, `1.43:1`, `7.50:1`) because `sharp-dark` borders were darkened in a later pass while `soft-glass` was left untouched. |
| **THM-02** | **P1** | `src/PiPlay/Theme/ThemeCatalog.cs:197-268` | **Near-duplicate surface palettes across presets:** `soft-glass` (`#0B1018`, `#121A26`, `#1B2738`) is nearly identical in luminance and RGB to `sharp-dark` shifted up by one tier (`#0B0E12`, `#131820`, `#1E2630`). `minimal` (`#14120F`, `#1C1A16`, `#26231E`) has almost no warm chroma ($R - B \le 8$) and the exact same `SurfaceBase` luminance ($L = 0.0104$) as `soft-glass` ($L = 0.0101$). |
| **THM-03** | **P2** | `src/PiPlay/Theme/Colors.xaml:38-99, 148`, `src/PiPlay/Theme/ThemeResourceApplier.cs:55-80, 162-164` | **Orphaned elevation & accent tokens:** `ElevationPanel` is defined in `ThemeCatalog` and written by `ThemeResourceApplier.Apply`, but has zero `{DynamicResource ElevationPanel}` consumers in XAML. `set.Subtle` (`AccentSubtle` / `AccentSubtleColor`) is computed in `ThemeColors.DeriveAccentSet` (`SubtleAlpha = 0x22` on `sharp-dark`) but neither seeded in `Colors.xaml` nor published as a `Color`/`SolidColorBrush` pair by `ThemeResourceApplier.ApplyAccentOnly`. |

### 2.2 User-Saved Profiles & Selector Identity (`Profile`, `ProfileService`, `ProfileAccentService`, `MainWindow`)
| ID | Severity | Location | Verified Finding |
|---|---|---|---|
| **PRF-01** | **P1** | `src/PiPlay/MainWindow.xaml:117-141`, `src/PiPlay/Theme/ControlStyles.xaml:360-370, 415-420`, `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:335-359` | **`ProfileIdentityRow` paints an unblended inner `<Grid.Background>` patch:** `MainWindow.xaml:118-120` sets `<Grid.Background>` via `AccentWashBrushConverter` (`ProfileRowWashAlpha`), bypassing `XamlInvariantTests.cs:353` (`Assert.Null(row.Attribute("Background"))`, which only checks the XML attribute while `WpfRuntimeTests.cs:2253-2293` asserts `row.Background` on `ContentSite`). Because `ContentSite` (`ControlStyles.xaml:420`) has `HorizontalAlignment="Left"` and `Margin="10,0,28,0"`, selecting a colored profile draws a clipped, sharp-cornered rectangular patch behind only the text width inside the closed `DarkComboBox`. |
| **PRF-02** | **P1** | `src/PiPlay/MainWindow.xaml.cs:783-811`, `src/PiPlay/Services/ProfileService.cs:35-45` | **Quick-saved profiles have no visual identity and are not activated:** `SaveProfileButton_Click` creates new profiles with `AccentColor = null` and calls `LoadProfilesIntoCombo()` without selecting the newly saved profile (`ProfileAccentService.SetActiveProfile` is not called). Colorless profiles render a transparent rail inside a fixed `Width="12"` column (`MainWindow.xaml:122`), leaving a 12px blank indent. |
| **PRF-03** | **P1** | `src/PiPlay/MainWindow.xaml:109-174`, `src/PiPlay/MainWindow.xaml.cs:764-781, 1068` | **No UI mechanism to deselect an active profile:** Once any profile is selected in `ProfilesCombo`, the user cannot return to "no active profile", directly contradicting `AccentEditContext` (`MainWindow.xaml.cs:1068`: *"Deselect the profile to edit the default accent instead"*). Moreover, when `ProfilesCombo.SelectedItem` becomes `null` or switches from a profile with `Topmost = true` to a profile with `Topmost = null`, `ApplyTopmost(_settings.MainWindow.Topmost)` is not called. |
| **PRF-04** | **P1** | `src/PiPlay/Models/Profile.cs:33-35`, `src/PiPlay/MainWindow.xaml.cs:779, 1516-1573` | **`Profile.FadeEnabled` and `Profile.Bounds` (`PlacementData?`) are dead fields; `Profile.Topmost` is ignored by `PlayerWindow`:** Neither `FadeEnabled` nor `Bounds` is ever read when activating a profile or launching `PlayerWindow`, and `PlayerWindow` always receives `_settings.Player.Topmost` (`line 1566`). |
| **PRF-05** | **P2** | `src/PiPlay/Prompt.cs:109-137, 214-315`, `src/PiPlay/MainWindow.xaml.cs:817, 845` | **`Prompt.EditProfile` UX flaws:** (1) Unconditionally renders the `"Playback mode"` dropdown (`BuildModePicker`, `lines 239-246`) with a single `"Standard"` item when `PlaybackModePolicy.CompactPlayerEnabled` is `false`. (2) `MainWindow.EditProfileButton_Click` (`line 817`) omits `accentPreview: QueueAccentPreview`, so editing a profile's accent has no live preview. (3) `AccentColorPicker` has no visual dimming (`Opacity`) when `IsEnabled="False"` (`useAccent` unchecked). |

### 2.3 Window Borders, Resize Bands & Background Blending (`PlayerWindow`, `MainWindow`, `RoundedWindowRegionApplier`)
| ID | Severity | Location | Verified Finding |
|---|---|---|---|
| **BRD-01** | **P1** | `src/PiPlay/PlayerWindow.xaml:29-30`, `src/PiPlay/Services/RoundedWindowRegionApplier.cs:19-33` | **`PopoutEdgeBorder` never collapses and clashes with `CreateRoundRectRgn`:** Always `BorderThickness="1"` and `CornerRadius="0"` across `Focused`, auto-hidden `Standard`, `Maximized`, `IsSnapLike`, and `Round` 22 DIP region modes. |
| **BRD-02** | **P1** | `src/PiPlay/PlayerWindow.xaml:11, 117`, `src/PiPlay/PlayerWindow.xaml.cs:941-952, 1309, 1337` | **12 DIP `AccentLetterbox` mat & missing top resize inset on `Standard` auto-hide:** `AccentLetterbox` creates a visible 12 DIP tinted frame around `#000000` video in `Focused` / auto-hidden `Standard` modes; `ApplyFocusedResizeInset()` fails to add the top 12 DIP margin when `ChromeStrip` auto-hides in `Standard` mode. |
| **BRD-03** | **P2** | `src/PiPlay/MainWindow.xaml:216, 225, 237` | **`SourceLetterbox` outer `AccentLetterbox` border around YouTube browse view:** `SourceLetterbox` (`line 216`) paints `AccentLetterbox` behind `Browser` (`Margin="12,0,12,12"`), creating a dark/tinted step below `SourceToolbar` (`AppBackgroundWash`) even though `SourcePlaceholder` (`line 237`) already sets its own `Background="{DynamicResource AccentLetterbox}"`. |
| **BRD-04** | **P2** | `src/PiPlay/Theme/ToggleAccent.cs:65-75`, `src/PiPlay/Theme/ControlStyles.xaml:204-236, 381`, `src/PiPlay/SettingsWindow.xaml:17-80` | **Residual control and card borders:** `ToggleAccent.Apply` sets `toggle.BorderBrush = brush` on checked `PinToggle`/`AutoToggle` instead of using a soft tonal fill; `ControlStyles.xaml:221` hardcodes `BorderThickness="1"` instead of `{TemplateBinding BorderThickness}`; `ControlStyles.xaml:381` has a dead `BorderBrush="{DynamicResource BorderSubtle}"` setter on `DarkComboBox`; `PresetToggle` (`SettingsWindow.xaml:52-53`) draws a 1px resting `BorderSubtle` wireframe around every preset card/chip; `SwatchToggle` (`SettingsWindow.xaml:17-44`) is dead markup. |

### 2.4 Source Window, Settings, Shared Controls, Focused Overlay & Native Windowing
| ID | Severity | Location | Verified Finding |
|---|---|---|---|
| **UI-01** | **P1** | `src/PiPlay/SettingsWindow.xaml:198, 290, 306`, `src/PiPlay/Controls/AccentColorPicker.xaml:45`, `src/PiPlay/Prompt.cs:257` | **Unstyled WPF `Slider` (4 instances) and `CheckBox` (1 instance):** Render default Windows Aero light-theme tracks/thumbs/checkmarks against PiPlay's dark surfaces. |
| **UI-02** | **P1** | `src/PiPlay/Controls/AccentColorPicker.xaml.cs:66, 99, 141-180, 221-230` | **`AccentColorPicker` `UseNearestReadableButton` no-op bug & mid-typing layout jump:** Invalid RGB input calls `SetReadability(false)` without updating `SelectedColor`. Clicking `UseNearestReadableButton` (`UseNearestReadable()`) computes `AccentReadabilityPolicy.NearestReadable(SelectedColor)` which equals `SelectedColor`, so `SelectedColor = target` no-ops on the `DependencyProperty` and leaves the warning stuck and **Done** disabled. Additionally, `ReadabilityRow` (`Grid.Row="0"`, `Height="Auto"`) shifts the entire picker down 28px on every intermediate keystroke in `HexInput`. |
| **UI-03** | **P1** | `src/PiPlay/MainWindow.xaml.cs:481-490, 465-470` | **Keyboard shortcut defect (`AltGr+L` hijack):** `MainWindow_PreviewKeyDown` checks `(Keyboard.Modifiers & ModifierKeys.Control) != 0` instead of `Keyboard.Modifiers == ModifierKeys.Control`, intercepting `Ctrl+Alt+L` (`AltGr+L` on Polish/German/international layouts, preventing typing `ł` or `@` inside YouTube search/comments) and `Ctrl+Shift+L`. `Key.F6` also lacks a `ModifierKeys.None` check, and `UrlBox` lacks `Escape` handling. |
| **UI-04** | **P1** | `src/PiPlay/MainWindow.xaml:181-184`, `src/PiPlay/MainWindow.xaml.cs:606-633` | **`AutoToggle` broken checked visual & ambiguous Play icon:** `AutoToggle` uses `Style="{StaticResource PinToggle}"` and Play glyph `&#xE768;`, but `ApplySourceAppearance` (`line 614`) only calls `ToggleAccent.SetCheckedBrush(PinToggle, pinBrush)` and omits `AutoToggle`. At low accent intensity or before `ApplyAuto` runs, `AutoToggle` lacks the contrast-safe checked brush, and its Play glyph looks like a media Play button rather than Auto Popout. |
| **UI-05** | **P2** | `src/PiPlay/MainWindow.xaml:63-74, 92, 188-198, 248, 269, 275`, `src/PiPlay/MainWindow.xaml.cs:132, 191, 214, 224-252, 495-500, 736-746`, `src/PiPlay/Services/BorderlessResizeHitTestPolicy.cs:10` | **Source Window ergonomics:** (1) `CornerLengthDip = 96` overlaps `SourceToolbar` (`y = 42..92`), so `SourceNavigationGroup` (`Margin="8,0,4,0"`) and `PopOutButton` (`Margin="6,6,10,6"`) sit 2–4 DIP inside the 12 DIP resize zone when disabled. (2) `SourceCommandsAvailable` (`line 736`) does not check `RuntimeErrorPanel.Visibility != Visibility.Visible`, and `RuntimeErrorPanel` (`line 269`) hardcodes `"WebView2 Runtime is required"` even on generic browser init exceptions. (3) `BackButton` never subscribes to `CoreWebView2.HistoryChanged` to track `CanGoBack`. (4) `ShowPopoutButton` (`line 189`) duplicates `PopOutButton`'s `&#xE8A7;` icon; `PopOutButton` keeps wide `DensityButtonPadding` when its label collapses below `940` DIP; `PlaceholderShowPopoutButton` (`line 248`) reads `"Show popout"` instead of `docs/AGENTS.md:25` `"Show Popout"`; `SettingsButton` (`line 63`) sits flush inside the window-caption cluster without a separator; `MaximizeButton.ToolTip` never updates on `StateChanged`. |
| **UI-06** | **P2** | `src/PiPlay/Theme/ControlStyles.xaml:105-236, 363-370, 400-467, 520-572`, `src/PiPlay/SettingsWindow.xaml:46-80, 112-175` | **Shared controls & Settings polish:** (1) Missing `IsKeyboardFocused` / `IsKeyboardFocusWithin` focus-ring triggers on `DarkButton`, `AccentButton`, `DangerButton`, `IconButton`, `CloseIconButton`, `PinToggle`, `DarkComboBox`, and `PresetToggle`. (2) `DarkComboBoxItem` (`lines 363-370`) places `IsHighlighted` before `IsSelected`, so hovering an unselected item looks identical to the selected item. (3) `CloseIconButton` and `PinToggle` lack `IsEnabled="False"` triggers. (4) `DropDownBorder` (`line 436`) `Margin="0,4,20,20"` clips the left/top of `ElevationPopup` (`BlurRadius="16"`), while `DarkContextMenu` and `ToolTip` omit `Effect="{DynamicResource ElevationPopup}"`. (5) `SettingsWindow` places `Privacy` above `Appearance`, `UniformGrid Columns="3"` (`line 141`) makes the 3rd theme card 8px wider than the first two, `PresetToggle` overwrites the card's preview `Background` on hover and colors the subtitle in `AccentPrimary` when checked. |
| **DOM-01** | **P1** | `src/PiPlay/Services/YouTubeDomBridge.cs:662, 720, 728-733, 824` | **Focused DOM overlay focus trap & frozen seek bar:** Clicking any Focused overlay button or dragging `controls.progress` leaves `document.activeElement` inside `root`. `hasInteractiveAttention()` (`line 720`: `root.contains(document.activeElement)`) stays `true` after mouse release, causing `hideControlsWhenIdle()` (`lines 728-733`) to loop every 250ms forever without hiding the overlay, and causing `updateProgress()` (`line 662`: `if (document.activeElement !== controls.progress)`) to permanently stop updating the seek thumb and `--piplay-progress` fill. `handleSeek()` (`line 824`) also omits updating `--piplay-progress` while scrubbing. |
| **DOM-02** | **P2** | `src/PiPlay/Services/YouTubeDomBridge.cs:401, 404, 740, 766-772, 792, 807-808` | **Focused overlay controls & state sync:** (1) `updateControls()` (`line 792`) forces `.is-visible` permanently on every 1000ms poll tick while paused instead of revealing on pause transition and fading after `fadeDelayMs`. (2) `mute` button has no `icons.muted` glyph and `media.muted = !media.muted` (`lines 807-808`) fails to produce audio when `media.volume === 0`. (3) `fullscreenToggle` never updates its glyph or title when `PlayerWindow` is maximized. |
| **WIN-01** | **P1** | `src/PiPlay/PlayerWindow.xaml.cs:133, 205, 832, 1223-1226`, `src/PiPlay/Services/BorderlessWindowHelper.cs:28, 106-150` | **`PlayerWindow` maximize overhang on multi-monitor setups:** `PlayerWindow` does not call `BorderlessWindowHelper.EnableProperMaximize` (`WM_GETMINMAXINFO`), and `StateChanged` calls `RoundedWindowRegionApplier.Clear(hwnd)` (`SetWindowRgn(hwnd, IntPtr.Zero, true)`) when maximized, exposing the unclipped `WS_THICKFRAME` border overhang (`~8–12px`) onto adjacent monitors and stopping at `rcWork` instead of full-monitor `rcMonitor`. |
| **WIN-02** | **P1** | `src/PiPlay/Services/WindowPlacementService.cs:26-45, 63-78`, `src/PiPlay/MainWindow.xaml.cs:1855-1857`, `src/PiPlay/Services/BorderlessWindowHelper.cs:78` | **Placement coordinate & DPI bugs + swapped mouse buttons:** (1) `WindowPlacementService` mixes workspace coordinates (`WINDOWPLACEMENT.rcNormalPosition`) with virtual-screen coordinates (`MONITORINFO.rcWork`) when the taskbar is docked top or left. (2) `MainWindow.Player_OnClosed` (`lines 1855-1857`) stores physical-pixel `state.Placement.Width`/`Height` into `_settings.Player.LastWidth`/`LastHeight`, which `PlayerWindow` (`lines 160-161`) assigns directly to DIP `Width`/`Height` (inflating window size by `DpiScale` on every fallback launch at >100% DPI). (3) `BorderlessWindowHelper.IsLeftButtonDown()` (`line 78`) checks `VK_LBUTTON` without checking `SystemParameters.SwapButtons`. |
| **POL-01** | **P2** | `src/PiPlay/Services/PopoutTargetResolver.cs:12-23`, `src/PiPlay/Services/YouTubeUrlHelper.cs:39, 55-59`, `src/PiPlay/Services/PlaybackModePolicy.cs:126`, `src/PiPlay/Services/SettingsService.cs:216`, `src/PiPlay/App.xaml.cs:186-188, 203` | **Policy & persistence edge cases:** (1) `PopoutTargetResolver.Resolve` lets a stale SPA `canonicalTarget` override a live `fromSource.IsPlaylistOnly` page. (2) `YouTubeUrlHelper.TryParse` leaves `IsPlaylistOnly = false` for `/watch?list=PL...` without `v=`. (3) `PlaybackModePolicy.ResolveProfileOverride` compares raw `profileMode` before normalizing. (4) `SettingsService.HasThemeBlock` uses case-sensitive `TryGetProperty("theme", ...)`. (5) `NamedPipeServerStream` (`App.xaml.cs:186-188`) and `NamedPipeClientStream` (`line 203`) omit `PipeOptions.CurrentUserOnly`. |

---

## 3. Theme Presets Refresh Blueprint

### 3.1 Design Goals for the Three Built-In Presets
1. **`sharp-dark` (`Crisp · 100%` — Carbon Studio Monitor):** High-precision carbon-black (`#050609` -> `#0B0E12` -> `#12161D`) with crisp 1px hairline borders (`#1F2733` at `1.29:1` vs `SurfaceBase`) and zero blur.
2. **`minimal` (`Quiet · 94%` — Warm Espresso / Obsidian Editorial Surface):** Unmistakable warm espresso/umber chroma ($R - B = +18$ on `SurfaceHover`) and **borderless tonal blending**: `BorderSubtle` (`#231C15`) is matched **`1.00:1`** to `SurfaceRaised` (`#231C15`) so any resting element using `BorderSubtle` disappears completely into its raised surface, relying on tonal steps (`#120E0B` -> `#1B1510` -> `#231C15`) instead of wireframes.
3. **`soft-glass` (`Glass · 82%` — Midnight Sapphire Atmospheric Overlay):** Deep sapphire chroma ($B - R = +46$ on `SurfaceHover`) that genuinely honors `ThemeCatalog.cs:255-256` (*"quieted borders and secondary text"*): `BorderSubtle` (`#18263C`) is quieted from `1.52:1` down to **`1.18:1`** vs `SurfaceBase` (**`1.04:1`** vs `SurfaceRaised`), `BorderStrong` (`#263B59`) is quieted from `2.04:1` down to **`1.58:1`**, and `TextSecondary` (`#94A6C0`) is quieted from `10.99:1` down to **`7.25:1`**.

### 3.2 WCAG-Verified `ThemePalette` Matrix (`src/PiPlay/Theme/ThemeCatalog.cs:197-268`)

All 27 tokens below have been verified against `src/PiPlay/Theme/ThemeColors.cs:89-94, 140-147` and `tests/PiPlay.Tests/Infrastructure/Wcag.cs`, and pass every shipping gate in `tests/PiPlay.Tests/ThemeCatalogTests.cs:183-208, 372-397` and `tests/PiPlay.Tests/ThemeColorsTests.cs`:

| Token | `sharp-dark` (Current -> Proposed) | `minimal` (Current -> Proposed) | `soft-glass` (Current -> Proposed) |
|---|---|---|---|
| `AppBackground` | `#050609` -> **`#050609`** ($L=0.0018$) | `#14120F` -> **`#120E0B`** ($L=0.0047$) | `#0B1018` -> **`#070C14`** ($L=0.0036$) |
| `SurfaceBase` | `#0B0E12` -> **`#0B0E12`** ($L=0.0043$) | `#1C1A16` -> **`#1B1510`** ($L=0.0081$) | `#121A26` -> **`#0E1726`** ($L=0.0085$) |
| `SurfaceRaised` | `#131820` -> **`#12161D`** ($L=0.0079$) | `#26231E` -> **`#231C15`** ($L=0.0124$) | `#1B2738` -> **`#162338`** ($L=0.0166$) |
| `SurfaceHover` | `#1E2630` -> **`#1B212C`** ($L=0.0150$) | `#312D27` -> **`#2E241C`** ($L=0.0193$) | `#26354B` -> **`#243652`** ($L=0.0362$) |
| `BorderSubtle` | `#181F29` -> **`#1F2733`** (`1.29:1` vs Base) | `#2E2A23` -> **`#231C15`** (**`1.00:1` vs Raised**) | `#2A3A52` -> **`#18263C`** (**`1.18:1` vs Base / `1.04:1` vs Raised**) |
| `BorderStrong` | `#262F3D` -> **`#2E394A`** (`1.66:1` vs Base) | `#3C362D` -> **`#3E3226`** (`1.45:1` vs Base) | `#3A4D6A` -> **`#263B59`** (**`1.58:1` vs Base**) |
| `TextPrimary` | `#F4F7FA` -> **`#F4F7FA`** (`17.99:1` vs Base) | `#F4F1EC` -> **`#F5EFE6`** (`15.82:1` vs Base) | `#F6F8FC` -> **`#F2F6FC`** (`16.56:1` vs Base) |
| `TextSecondary` | `#9AA2AD` -> **`#9AA2AD`** (`7.50:1` vs Base) | `#B0A99E` -> **`#B5A895`** (`7.75:1` vs Base) | `#C4CEDC` -> **`#94A6C0`** (**`7.25:1` vs Base**) |
| `Danger` | `#E45D75` -> **`#E45D75`** (`3.43:1` w/ `#FFF`) | `#E8564C` -> **`#E8564C`** (`3.58:1` w/ `#FFF`) | `#E45D75` -> **`#E45D75`** (`3.43:1` w/ `#FFF`) |

#### Curated `AccentOptions` Contrast on Proposed `SurfaceHover` (Minimum Gate: `3.00:1`)
| Accent Option | Hex | On `sharp-dark` (`#1B212C`) | On `minimal` (`#2E241C`) | On `soft-glass` (`#243652`) |
|---|---:|---:|---:|---:|
| Cyan | `#2BAED0` | `6.20:1` | `5.82:1` | `4.68:1` |
| Steel Blue | `#3F84C0` | `4.05:1` | `3.81:1` | **`3.06:1`** |
| Steel | `#4A8FAB` | `4.47:1` | `4.19:1` | `3.37:1` |
| Violet | `#9E84F0` | `5.39:1` | `5.06:1` | `4.07:1` |
| Green | `#2DB57F` | `6.17:1` | `5.79:1` | `4.65:1` |
| Amber | `#D69A2E` | `6.55:1` | `6.15:1` | `4.94:1` |

### 3.3 `Colors.xaml` Seeds, `AccentSubtle` & `ElevationPanel`
1. **Update `src/PiPlay/Theme/Colors.xaml` Seeds for `sharp-dark`:**
   - `SurfaceRaisedColor` (`line 20`): `#FF131820` -> **`#FF12161D`**
   - `SurfaceHoverColor` (`line 21`): `#FF1E2630` -> **`#FF1B212C`**
   - `BorderSubtleColor` (`line 22`): `#FF181F29` -> **`#FF1F2733`**
   - `BorderStrongColor` (`line 23`): `#FF262F3D` -> **`#FF2E394A`**
   - Add `<Color x:Key="AccentSubtleColor">#222BAED0</Color>` (`line 47`, matching `WithAlpha(#2BAED0, 0x22)`) and `<SolidColorBrush x:Key="AccentSubtle" Color="{StaticResource AccentSubtleColor}" />` (`line 91`).
2. **Publish `AccentSubtle` in `ThemeResourceApplier.ApplyAccentOnly` (`src/PiPlay/Theme/ThemeResourceApplier.cs:64`):**
   ```csharp
   SetColorPair(resources, "AccentBorder", set.Border);
   SetColorPair(resources, "AccentSubtle", set.Subtle);
   SetColorPair(resources, "AccentShellTint", set.ShellTint);
   ```
3. **Consume `{DynamicResource ElevationPanel}` on non-airspace panels:**
   - Apply `Effect="{DynamicResource ElevationPanel}"` on the `ThemeCardToggle` style (`src/PiPlay/SettingsWindow.xaml`) and the inner status `StackPanel`/`Border` of `SourcePlaceholder` (`src/PiPlay/MainWindow.xaml:239`).

---

## 4. User-Saved Profiles Refresh Blueprint

### 4.1 Auto-Seed Distinct Profile Accents & Activate on Quick-Save
- **Problem:** Clicking `Save` (`SaveProfileButton_Click` in `src/PiPlay/MainWindow.xaml.cs:783-811`) creates a profile with `AccentColor = null` and leaves it unselected in `ProfilesCombo`.
- **Implementation Specification:**
  1. In `src/PiPlay/Services/ProfileAccentService.cs`, add `PickDistinctProfileAccent(AppSettings settings, string? globalAccent = null)`:
     ```csharp
     public static string PickDistinctProfileAccent(AppSettings settings, string? globalAccent = null)
     {
         ArgumentNullException.ThrowIfNull(settings);
         var baseline = ProfileService.NormalizeAccentForStorage(globalAccent)
             ?? ProfileService.NormalizeAccentForStorage(settings.Theme?.AccentColor)
             ?? ThemeCatalog.DefaultAccentColor;

         var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         if (settings.Profiles is not null)
         {
             foreach (var p in settings.Profiles)
             {
                 if (ProfileService.NormalizeAccentForStorage(p?.AccentColor) is { } norm)
                 {
                     used.Add(norm);
                 }
             }
         }

         foreach (var option in ThemeCatalog.AccentOptions)
         {
             if (!string.Equals(option.HexColor, baseline, StringComparison.OrdinalIgnoreCase) &&
                 !used.Contains(option.HexColor))
             {
                 return option.HexColor;
             }
         }

         var nonBaseline = ThemeCatalog.AccentOptions
             .Where(o => !string.Equals(o.HexColor, baseline, StringComparison.OrdinalIgnoreCase))
             .ToArray();
         if (nonBaseline.Length > 0)
         {
             var count = settings.Profiles?.Count ?? 0;
             return nonBaseline[count % nonBaseline.Length].HexColor;
         }

         return ThemeCatalog.DefaultAccentColor;
     }
     ```
  2. In `src/PiPlay/Services/ProfileService.cs:35-45`, add optional `string? defaultAccentColor = null` to `CreateQuickSaveProfile` so brand-new profiles (`existing is null`) get `NormalizeAccentForStorage(defaultAccentColor)` while overwriting an existing profile preserves `existing.AccentColor` (including `null`):
     ```csharp
     public static Profile CreateQuickSaveProfile(
         Profile? existing, string name, string url, bool topmost, string? defaultAccentColor = null) => new()
     {
         Name = name,
         Url = url,
         Mode = existing?.Mode,
         Presentation = existing?.Presentation,
         AccentColor = existing is not null
             ? existing.AccentColor
             : NormalizeAccentForStorage(defaultAccentColor),
         Topmost = topmost,
         FadeEnabled = existing?.FadeEnabled,
         Bounds = existing?.Bounds,
     };
     ```
  3. In `src/PiPlay/MainWindow.xaml.cs:807-810` (`SaveProfileButton_Click`), pass `ProfileAccentService.PickDistinctProfileAccent(_settings, EffectiveAccentColor)` and immediately activate the saved profile before reloading the combo:
     ```csharp
     var defaultAccent = ProfileAccentService.PickDistinctProfileAccent(_settings, EffectiveAccentColor);
     var saved = ProfileService.CreateQuickSaveProfile(existing, name, currentUrl, Topmost, defaultAccent);
     ProfileService.Save(_settings, saved);
     ProfileAccentService.SetActiveProfile(_settings, saved);
     _settingsService.Save(_settings);
     LoadProfilesIntoCombo();
     ```

### 4.2 Seamless Selector Blending (`ProfileIdentityRow` & `DarkComboBox`)
- **Location:** `src/PiPlay/MainWindow.xaml:114-141`, `src/PiPlay/Theme/ControlStyles.xaml:381, 415-420`.
- **Implementation Specification:**
  1. Change `<Setter Property="BorderBrush" Value="{DynamicResource BorderSubtle}" />` on `DarkComboBox` (`src/PiPlay/Theme/ControlStyles.xaml:381`) to `<Setter Property="BorderBrush" Value="Transparent" />`.
  2. Change `ContentSite` (`src/PiPlay/Theme/ControlStyles.xaml:420`) from `HorizontalAlignment="Left"` to `HorizontalAlignment="Stretch"`.
  3. In `src/PiPlay/MainWindow.xaml:114-141` (`ProfileIdentityRow`), remove `<Grid.Background>` (`AccentWashBrushConverter`) and use a `<Border.Style>` `DataTrigger` on `ProfileIdentityColorRail` (`ColumnDefinition Width="Auto"`) so colorless profiles collapse the rail and its margin cleanly without local-value DP precedence override:
     ```xml
     <Grid x:Name="ProfileIdentityRow" MinWidth="0">
       <Grid.ColumnDefinitions>
         <ColumnDefinition Width="Auto" />
         <ColumnDefinition Width="*" />
       </Grid.ColumnDefinitions>
       <Border x:Name="ProfileIdentityColorRail" Width="4" MinHeight="16"
               CornerRadius="{DynamicResource RadiusSwatch}"
               Tag="{DynamicResource SurfaceHover}">
         <Border.Style>
           <Style TargetType="Border">
             <Setter Property="Margin" Value="0,2,8,2" />
             <Style.Triggers>
               <DataTrigger Binding="{Binding AccentColor}" Value="{x:Null}">
                 <Setter Property="Visibility" Value="Collapsed" />
                 <Setter Property="Margin" Value="0" />
               </DataTrigger>
             </Style.Triggers>
           </Style>
         </Border.Style>
         <Border.Background>
           <MultiBinding Converter="{StaticResource ContrastBrushConverter}">
             <Binding Path="AccentColor" />
             <Binding Path="Tag" RelativeSource="{RelativeSource Self}" />
           </MultiBinding>
         </Border.Background>
       </Border>
       <TextBlock x:Name="ProfileIdentityName" Grid.Column="1" Text="{Binding Name}" TextTrimming="CharacterEllipsis"
                  Foreground="{DynamicResource TextPrimary}" VerticalAlignment="Center" />
     </Grid>
     ```

### 4.3 Explicit Profile Deselection (`ClearProfileMenuItem`)
- **Location:** `src/PiPlay/MainWindow.xaml:149-172`, `src/PiPlay/MainWindow.xaml.cs:754-782`.
- **Implementation Specification:**
  - Add `ClearProfileMenuItem` to `ProfileActionsMenu` (`src/PiPlay/MainWindow.xaml:165`):
    ```xml
    <MenuItem x:Name="ClearProfileMenuItem" Style="{StaticResource DarkMenuItem}"
              Header="_Clear active profile" Click="ClearProfileButton_Click" IsEnabled="False"
              AutomationProperties.Name="Clear active profile">
      <MenuItem.Icon>
        <TextBlock Text="&#xE894;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" />
      </MenuItem.Icon>
    </MenuItem>
    ```
  - In `UpdateSourceCommandAvailability()` (`src/PiPlay/MainWindow.xaml.cs:754-756`), set `ClearProfileMenuItem.IsEnabled = hasProfile;`.
  - In `ProfilesCombo_SelectionChanged` (`src/PiPlay/MainWindow.xaml.cs:764-781`), call `ApplyTopmost(_settings.MainWindow.Topmost);` when `ProfilesCombo.SelectedItem is not Profile profile`, and `ApplyTopmost(profile.Topmost ?? _settings.MainWindow.Topmost);` when a profile is selected.
  - Add `private void ClearProfileButton_Click(object sender, RoutedEventArgs e) => ProfilesCombo.SelectedItem = null;`.

### 4.4 Wire `Profile.Topmost`, `Profile.FadeEnabled` & `Profile.Bounds` (`PlacementData?`) on Popout Launch
- **Location:** `src/PiPlay/Services/ProfileService.cs`, `src/PiPlay/MainWindow.xaml.cs:1566-1573`.
- **Implementation Specification:**
  - Add `ProfileService.ResolveLaunchOverrides(Profile? activeProfile, string? targetVideoId, PlayerSettings player, string? targetPlaylistId = null)` returning `(bool Topmost, bool FadeEnabled, PlacementData? Placement)` when `activeProfile`'s parsed `YouTubeUrlHelper.TryParse` video ID or playlist ID matches the launch target (and falling back to `player` defaults otherwise).
  - Use `(popoutTopmost, popoutFadeEnabled, popoutPlacement)` in `MainWindow.StartVideoPopoutAsync` (`src/PiPlay/MainWindow.xaml.cs:1566`).

### 4.5 Clean Up `Prompt.EditProfile` (`src/PiPlay/Prompt.cs:239-246`, `src/PiPlay/MainWindow.xaml.cs:817-855`, `src/PiPlay/Controls/AccentColorPicker.xaml:6`)
1. In `Prompt.EditProfile` (`src/PiPlay/Prompt.cs:239-246`), gate adding `"Playback mode"` + `modePicker` to `body.Children` via `var showModePicker = PlaybackModePolicy.CompactPlayerEnabled; if (showModePicker)` (avoiding `CS0162` while keeping `BuildModePicker` intact for `WpfRuntimeTests.cs:2639-2657`).
2. In `MainWindow.EditProfileButton_Click` (`src/PiPlay/MainWindow.xaml.cs:817`), pass `accentPreview: QueueAccentPreview`, call `CancelQueuedAccentPreview();` immediately after `Prompt.EditProfile` returns, and call `ApplyResolvedAccent();` before returning on cancel, duplicate-name conflict abort (`line 845`), or non-`Updated` outcome.
3. In `src/PiPlay/Controls/AccentColorPicker.xaml:6`, add a `<UserControl.Style>` trigger setting `Opacity="0.4"` when `IsEnabled="False"`.

---

## 5. Border Removal & Seamless Background Blending Specification

### 5.1 Dynamic `PopoutEdgeBorder` Suppression (`RoundedWindowRegionPolicy.cs` & `PlayerWindow.xaml.cs`)
- **Policy Helper (`src/PiPlay/Services/RoundedWindowRegionPolicy.cs`):**
  ```csharp
  public static bool ShouldShowPopoutEdgeBorder(
      bool isNormalWindowState,
      bool isSnapLike,
      bool focusedSurfaceActive,
      bool stripCollapsed,
      bool customWindowRegionApplied,
      DwmCornerMode cornerMode,
      byte edgeAlpha)
  {
      if (!isNormalWindowState || isSnapLike)
          return false;
      if (focusedSurfaceActive || stripCollapsed)
          return false;
      if (customWindowRegionApplied)
          return false;
      if (cornerMode is not (DwmCornerMode.Default or DwmCornerMode.Square))
          return false;
      return edgeAlpha > 0;
  }
  ```
- **`PlayerWindow` Wiring (`src/PiPlay/PlayerWindow.xaml.cs`):**
  - Add `private bool _isSnapLike;` and `UpdatePopoutEdgeBorder()`.
  - Call `UpdatePopoutEdgeBorder()` from `ApplyFocusedSurfaceActive(bool active)` (`line 582`), `HandleWindowStateChanged()` (`line 856`), `ApplyAccentVisuals(string? accentColor)` (`line 984`), `ApplyCornerModeToHwnd(...)` (both before `if (hwnd == IntPtr.Zero) return;` at `line 1193` for headless tests and after updating `_isSnapLike` and `_customWindowRegionApplied` at `line 1227`), `ShowControls()` (`lines 1309, 1313`), and `OnHideFadeCompleted()` (`line 1337`).
  - Also call `HandleWindowStateChanged()` (instead of only `UpdateExpandAffordance()`) inside `ToggleExpandedState()` (`line 840`), `ApplyFullScreenElementState()` (`lines 883, 891`), and `TryRestoreFromEscape()` (`line 909`).

### 5.2 Seamless `#000000` Blending of the 12 DIP Resize Band & Auto-Hide Top Resize Fix (`PlayerWindow.xaml.cs`)
- **Unify Resize Inset (`src/PiPlay/PlayerWindow.xaml.cs:941-952`):**
  ```csharp
  private void UpdatePlayerResizeInset()
  {
      var stripHidden = _focusedSurfaceActive || ChromeStrip.Visibility == Visibility.Collapsed;
      if (stripHidden && WindowState == WindowState.Normal)
      {
          Player.Margin = new Thickness(BorderlessResizeHitTestPolicy.ResizeBorderDip);
      }
      else
      {
          Player.ClearValue(FrameworkElement.MarginProperty);
      }
  }
  ```
- **Match Window Mat to `#000000` When Borderless (`src/PiPlay/PlayerWindow.xaml.cs`):**
  ```csharp
  private void UpdatePlayerCanvasBackground()
  {
      var borderlessVideo = _focusedSurfaceActive || ChromeStrip.Visibility == Visibility.Collapsed;
      if (borderlessVideo)
      {
          Background = Brushes.Black;
      }
      else
      {
          SetResourceReference(Control.BackgroundProperty, "AccentLetterbox");
      }
  }
  ```
- Call `UpdatePlayerResizeInset()` and `UpdatePlayerCanvasBackground()` from `ApplyFocusedSurfaceActive(bool active)`, `ShowControls()`, `OnHideFadeCompleted()`, and `HandleWindowStateChanged()`.

### 5.3 Source Window `SourceLetterbox` Blending (`src/PiPlay/MainWindow.xaml:216`)
- Change `SourceLetterbox` (`src/PiPlay/MainWindow.xaml:216`) from `Background="{DynamicResource AccentLetterbox}"` to `Background="{DynamicResource AppBackgroundWash}"` (and update `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:2232` in lockstep). `SourcePlaceholder` (`src/PiPlay/MainWindow.xaml:237`) retains `Background="{DynamicResource AccentLetterbox}"`.

### 5.4 Borderless Resting Preset Cards & Toggles (`SettingsWindow.xaml`, `ControlStyles.xaml`, `ToggleAccent.cs`, `AccentColorPicker.xaml`)
1. **`PresetToggle` & `ThemeCardToggle` (`src/PiPlay/SettingsWindow.xaml:17-80`):**
   - Delete dead `SwatchToggle` (`lines 17-44`).
   - Set `PresetToggle` resting `BorderBrush="Transparent"` and `BorderThickness="{DynamicResource BorderThicknessDefault}"`; on `IsMouseOver` set `bd.Background = SurfaceHover` and `bd.BorderBrush = BorderStrong`; on `IsChecked` set `Foreground = AccentPrimary`, `bd.Background = AccentSubtle`, and `bd.BorderBrush = AccentBorder`; on `IsKeyboardFocused` set `bd.BorderBrush = AccentBorder`.
   - Define `ThemeCardToggle` (`BasedOn="{StaticResource PresetToggle}"`, `Height="58"`, `Margin="0"`, `Effect="{DynamicResource ElevationPanel}"`) with its own `ControlTemplate` that preserves each card's preview `Background` (`#0B0E12`, `#1B1510`, `#0E1726`) on hover and checked states.
2. **`PinToggle` & `ToggleAccent` (`src/PiPlay/Theme/ControlStyles.xaml:204-236`, `src/PiPlay/Theme/ToggleAccent.cs:63-75`):**
   - Add `<Setter Property="BorderThickness" Value="{DynamicResource BorderThicknessDefault}" />` to `PinToggle` (`ControlStyles.xaml:210`) and bind `BorderThickness="{TemplateBinding BorderThickness}"` on `bd` (`line 221`).
   - Place `<Trigger Property="IsChecked" Value="True"><Setter TargetName="bd" Property="Background" Value="{DynamicResource SurfaceRaised}" /></Trigger>` **before** `<Trigger Property="IsMouseOver" Value="True">` in `PinToggle`.
   - In `ToggleAccent.Apply` (`src/PiPlay/Theme/ToggleAccent.cs:65-74`), stop setting or clearing `Control.BorderBrushProperty`; only set `toggle.Foreground = brush` when checked and `toggle.ClearValue(Control.ForegroundProperty)` when unchecked.
3. **`AccentColorPicker` Swatches (`src/PiPlay/Controls/AccentColorPicker.xaml:95-97`):**
   - Remove `BorderBrush="{DynamicResource BorderStrong}" BorderThickness="1"` from the 22×22 `PresetRow` swatch borders.

---

## 6. UI Appearance, Ergonomics, Focused Overlay & Windowing Implementation Guide

### 6.1 Shared Control Styles (`src/PiPlay/Theme/ControlStyles.xaml`)
1. **Add `DarkSlider` (+ implicit `Slider` style) and `DarkCheckBox` (+ implicit `CheckBox` style):**
   - In `DarkSlider`, use `CornerRadius="{DynamicResource RadiusScrollbarThumb}"` on the 4 DIP track and active-fill `Track.DecreaseRepeatButton` border (with a collapsed `PART_SelectionRange` fallback), and a 16×16 `<Ellipse>` thumb with `IsKeyboardFocused` setting `Thumb.BorderBrush = {DynamicResource AccentBorder}`.
   - In `DarkCheckBox`, use an 18×18 `<Border x:Name="bd" CornerRadius="{DynamicResource RadiusInput}">` containing a vector checkmark `<Path x:Name="CheckMark" Stroke="{DynamicResource OnAccent}" ... />`, with `IsMouseOver`, `IsChecked`, and a `MultiTrigger` (`IsChecked="True"` + `IsMouseOver="True"` -> `AccentHover`).
2. **Add Keyboard Focus Rings Across All Interactive Styles:**
   - Add `FocusVisualStyle="{x:Null}"` and `IsKeyboardFocused` (`TargetName="bd"`, `BorderBrush="{DynamicResource AccentBorder}"`) to `DarkButton`, `AccentButton`, `DangerButton`, `IconButton`, `CloseIconButton`, `PinToggle`, `PresetToggle`, and `ThemeCardToggle`.
   - On `DangerButton`, `IconButton`, and `CloseIconButton`, bind `BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"` on `bd` (`BorderBrush="Transparent"`, `BorderThickness="{DynamicResource BorderThicknessDefault}"`), leaving `AccentIconButton` with its single `Foreground` setter (`tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:323-331`).
   - On `DarkComboBox`, add a `<DataTrigger Binding="{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource AncestorType=ComboBox}}" Value="True">` inside the inner `ToggleButton.Template.Triggers` setting `TargetName="bd" Property="BorderBrush" Value="{DynamicResource AccentBorder}"`.
3. **Fix `DarkComboBoxItem`, `DropDownBorder`, `DarkContextMenu`, `ToolTip`, and Disabled Triggers:**
   - In `DarkComboBoxItem` (`lines 363-370`), place `IsSelected` (`SurfaceRaised` + `AccentPrimary`) before `IsHighlighted` (`SurfaceHover`).
   - Add `<Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.35" /></Trigger>` to `CloseIconButton` and `PinToggle`.
   - In `DarkComboBox` (`lines 433-444`), set `HorizontalOffset="-12"` on `Popup` and `Margin="12,8,20,20"` on `DropDownBorder`.
   - On `DarkContextMenu` (`line 520`) and `ToolTip` (`line 550`), add `HorizontalOffset="-12"`, `VerticalOffset="-4"`, `Margin="12,8,20,20"`, and `Effect="{DynamicResource ElevationPopup}"`.

### 6.2 Source Window (`src/PiPlay/MainWindow.xaml` & `src/PiPlay/MainWindow.xaml.cs`)
1. **Fix `MainWindow_PreviewKeyDown` & `UrlBox_KeyDown` (`src/PiPlay/MainWindow.xaml.cs:465-490`):**
   - Require exact modifiers (`Keyboard.Modifiers == ModifierKeys.Control` for `Key.L`, `ModifierKeys.None` for `Key.F6`, `ModifierKeys.Alt` for `e.SystemKey == Key.D`) while keeping `if (!focusAddress || !SourceCommandsAvailable) return;`.
   - In `UrlBox_KeyDown` (`line 465`), handle `Key.Escape` by restoring `UrlBox.Text = Browser.CoreWebView2?.Source ?? _settings.LastUrl ?? string.Empty; Keyboard.ClearFocus(); e.Handled = true;`.
2. **Fix `AutoToggle` Icon, Checked Brush, Dynamic Tooltip & Availability:**
   - Change `AutoToggle` (`src/PiPlay/MainWindow.xaml:181`) glyph from `&#xE768;` to `&#xE945;`.
   - In `ApplySourceAppearance` (`src/PiPlay/MainWindow.xaml.cs:614`), call `ToggleAccent.SetCheckedBrush(AutoToggle, pinBrush);`.
   - Add `UpdateSourceAutoAffordance(bool on)` called from `ApplyAuto(bool on)` and `AutoToggle_Click`, and set `AutoToggle.IsEnabled = enabled;` in `UpdateSourceCommandAvailability()` (`line 741`).
3. **Clear the 12 DIP Resize Zone & Responsive Toolbar Polish:**
   - Change `SourceNavigationGroup` (`src/PiPlay/MainWindow.xaml:92`) to `Margin="12,0,4,0"` and `PopOutButton` (`line 194`) to `Margin="6,6,12,6"`.
   - Change `ShowPopoutButton` (`line 189`) glyph from `&#xE8A7;` to `&#xE78B;`.
   - In `ApplySourceToolbarLayout(double width)` (`src/PiPlay/MainWindow.xaml.cs:495`), set `PopOutButton.Padding = new Thickness(8, 0, 8, 0)` when `compact` and `PopOutButton.ClearValue(Control.PaddingProperty)` when `!compact`.
   - Add `<Border Width="1" Height="14" Background="{DynamicResource BorderSubtle}" Margin="4,0" VerticalAlignment="Center" IsHitTestVisible="False" />` (without `CornerRadius`) between `SettingsButton` and `MinimizeButton` (`src/PiPlay/MainWindow.xaml:67`), update `MaximizeButton.ToolTip` in `StateChanged` (`line 132`), and change `PlaceholderShowPopoutButton` (`line 248`) to `Content="Show Popout"`.
4. **Gate `SourceCommandsAvailable` on `RuntimeErrorPanel` & Track `CanGoBack`:**
   - Add `&& RuntimeErrorPanel.Visibility != Visibility.Visible` to `SourceCommandsAvailable` (`src/PiPlay/MainWindow.xaml.cs:736`), and call `UpdateSourceCommandAvailability()` in `ShowRuntimeError()` (`line 251`) and `InitializeBrowserAsync` (`lines 191, 216`).
   - Add `x:Name="RuntimeErrorTitle"` (`MainWindow.xaml:269`) and `x:Name="DownloadRuntimeButton"` (`line 275`) so `ShowRuntimeError` distinguishes missing WebView2 Runtime from generic browser initialization failures.
   - Subscribe `core.HistoryChanged += (_, _) => UpdateSourceCommandAvailability();`, add `internal void SetCanGoBackForTests(bool? canGoBack)`, and set `BackButton.IsEnabled = enabled && (_canGoBackForTests ?? (Browser.CoreWebView2?.CanGoBack ?? false));` (`line 746`).

### 6.3 Settings Window & `AccentColorPicker` (`src/PiPlay/SettingsWindow.xaml`, `src/PiPlay/Controls/AccentColorPicker.xaml.cs`)
1. **Reorder Settings Sections (`src/PiPlay/SettingsWindow.xaml:110-324`):**
   - Order sections inside `SettingsScroll` as `Appearance` -> `<Border Height="1" ... Margin="0,18" />` -> `Advanced` -> `<Border Height="1" ... Margin="0,18" />` -> `Privacy` (removing the trailing divider after `ClearBrowserDataButton`).
2. **Equal-Width Theme Cards (`src/PiPlay/SettingsWindow.xaml:141-175`):**
   - Replace `<UniformGrid Columns="3" Margin="0,0,0,14">` with a 5-column `Grid` (`*`, `8`, `*`, `8`, `*`), apply `Style="{StaticResource ThemeCardToggle}"`, update preview `Background` to `#1B1510` (`ThemeMinimalPreset`) and `#0E1726` (`ThemeSoftGlassPreset`), and set `Foreground="{DynamicResource TextSecondary}"` on the three card subtitle `TextBlock`s.
3. **Fix `AccentColorPicker` No-Op Recovery & Layout Stability (`src/PiPlay/Controls/AccentColorPicker.xaml` & `.cs`):**
   - In `UseNearestReadable()` (`src/PiPlay/Controls/AccentColorPicker.xaml.cs:66`), compute `var target = AccentReadabilityPolicy.NearestReadable(SelectedColor);` and call `RefreshFromSelectedColor(target, recomputeHsv: true)` when `SelectedColor == target` (with matching guards in `RgbInput_TextChanged` at `line 160` and `HexInput_TextChanged` at `line 174`).
   - Differentiate `ReadabilityWarning.Text` between invalid RGB (`"Enter RGB values from 0 to 255."`) and invalid Hex (`"Enter a valid 6-digit hex color (#RRGGBB)."`).
   - Move `ReadabilityRow` to the bottom of `AccentColorPicker.xaml` with `MinHeight="28"` and `Visibility.Hidden` when valid (eliminating layout jumps during typing), add a `<DropShadowEffect BlurRadius="3" ShadowDepth="0" Opacity="0.85" Color="#000000" />` on `HueSatThumb`, and add 1px premultiplied-alpha (`Pbgra32`) edge feathering in `CreateDisc` (`lines 319-331`).

### 6.4 Focused DOM Overlay, Native Windowing & Policies
1. **Fix Focused Overlay Focus Trap, Pause Fade, Mute & Maximize Sync (`src/PiPlay/Services/YouTubeDomBridge.cs`, `src/PiPlay/Services/PlayerFirstSurfaceBridge.cs`):**
   - In `hasInteractiveAttention()` (`src/PiPlay/Services/YouTubeDomBridge.cs:718-723`), keep the exact substrings pinned by `PlayerSurfaceScriptTests.cs:297-300` while restricting `focused` to `:focus-visible,:active`:
     ```javascript
     function hasInteractiveAttention() {
       if (!root) return false;
       const focused = document.activeElement && root.contains(document.activeElement) &&
         typeof document.activeElement.matches === "function" &&
         document.activeElement.matches(":focus-visible,:active");
       const hovered = root.querySelector(".piplay-focused-button:hover,.piplay-focused-progress:hover");
       return !!focused || !!hovered;
     }
     ```
   - Guard `.blur()` with `if (event.detail && typeof control.blur === "function") control.blur();` in `handleAction` (`line 821`) and on `change` for `seek`.
   - In `updateProgress()` (`line 662`), remove `&& document.activeElement !== controls.progress`, and update `--piplay-progress` live inside `handleSeek()` (`line 824`).
   - In `revealControls()` (`lines 735-742`) and `updateControls()` (`lines 790-794`), arm `hideTimer` whenever `config.fadeEnabled && media` and trigger `revealControls()` on pause transitions while preserving `else if (pausedChangedToPlaying) revealControls();` verbatim (`PlayerSurfaceScriptTests.cs:301-302`).
   - Add `icons.muted`, restore `media.volume = 0.5` when unmuting from `media.volume === 0`, and add optional `bool Maximized = false` (5th parameter) to `PlayerFirstSurfaceAppearance` to sync `fullscreenToggle`'s icon and title.
2. **Fix `PlayerWindow` Maximize, `SwapButtons`, `WindowPlacementService` & `LastWidth`/`LastHeight` DPI:**
   - Add `bool useWorkArea = true` to `BorderlessWindowHelper.EnableProperMaximize` and `CalculateMaximizedBounds` (`src/PiPlay/Services/BorderlessWindowHelper.cs:28, 139, 188`), and call `BorderlessWindowHelper.EnableProperMaximize(this, useWorkArea: false);` in `PlayerWindow`'s constructor (`src/PiPlay/PlayerWindow.xaml.cs:134`).
   - Add `PrimaryMouseButtonVirtualKey(bool swapButtons)` in `BorderlessWindowHelper.cs:78` checking `SystemParameters.SwapButtons`.
   - Add `PlacementMath.WorkspaceToScreen`, `PlacementMath.ScreenToWorkspace`, and `PlacementMath.ToDipSize` in `src/PiPlay/Services/PlacementMath.cs` and wire them in `WindowPlacementService.cs:26-45, 63-78` and `MainWindow.Player_OnClosed` (`src/PiPlay/MainWindow.xaml.cs:1855-1858`).
3. **Fix Popout Target, URL, PlaybackMode, Settings & Pipe Policies:**
   - In `PopoutTargetResolver.Resolve` (`src/PiPlay/Services/PopoutTargetResolver.cs:15`), return `fromSource` when `hasSource && (!string.IsNullOrEmpty(fromSource.VideoId) || fromSource.IsPlaylistOnly)`.
   - In `YouTubeUrlHelper.TryParse` (`src/PiPlay/Services/YouTubeUrlHelper.cs:39, 58`), set `target.IsPlaylistOnly = target.VideoId is null && !string.IsNullOrEmpty(target.PlaylistId);` after `ApplyList`.
   - Normalize `profileMode` in `PlaybackModePolicy.ResolveProfileOverride` (`src/PiPlay/Services/PlaybackModePolicy.cs:126`).
   - Make `SettingsService.HasThemeBlock` (`src/PiPlay/Services/SettingsService.cs:213-218`) case-insensitive.
   - Add `SingleInstancePipePolicy.ServerPipeOptions` (`PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly`) and `ClientPipeOptions` (`PipeOptions.CurrentUserOnly`) in `src/PiPlay/Services/SingleInstancePipePolicy.cs` and use them in `src/PiPlay/App.xaml.cs:186-188, 203`.

---

## 7. Test Suite Invariants Checklist (Exact Lockstep Updates)

| Test File | Test Method & Lines | Exact Lockstep Update |
|---|---|---|
| `tests/PiPlay.Tests/ThemeCatalogTests.cs` | `Sharp_dark_matches_the_v2_spec_literals` (`lines 233-243`), `Minimal_matches_the_v2_spec_literals` (`lines 245-255`), `Soft_glass_matches_the_v2_spec_literals` (`lines 257-267`) | Update `ThemePalette` hex literals to match the Section 3.2 matrix. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | `Grey_border_tokens_are_quieted_to_a_faint_hairline` (`lines 1122-1134`) | Update `BorderSubtleColor` to `"#FF1F2733"` and `BorderStrongColor` to `"#FF2E394A"`. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | `Colors_xaml_accent_seeds_match_the_default_derived_set_REQ_UI_01` (`lines 1000-1025`) | Add `Assert.Equal(Hex(set.Subtle), t["AccentSubtleColor"]);`. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | `Settings_sections_live_inside_the_scroll_viewer_in_order` (`lines 150-170`) | Update expected order to `["AppearanceSectionHeader", "AdvancedSectionHeader", "PrivacySectionHeader"]`. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | `Source_placeholder_has_bring_video_back_action` (`line 284`) | Update `PlaceholderShowPopoutButton` `Content` assertion from `"Show popout"` to `"Show Popout"`. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | `RequiredNames` (`line 121`) & `RequiredAccessibleNames` (`line 531`) | Add `"ClearProfileMenuItem"`; add `"RuntimeErrorTitle"` and `"DownloadRuntimeButton"` to `RequiredNames`. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | `Profile_selector_uses_one_live_contrast_rail_not_nested_fill_REQ_PROFILE_01` (`lines 335-359`) | Add `Assert.Empty(row.Elements(XamlTestFiles.Pres + "Grid.Background"));`. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | `MainWindow_full_theme_preview_and_cancel_restore_palette_and_bar_opacity` (`line 828`) | Update `soft-glass` `AppBackground` hex assertion from `"#0B1018"` to `"#070C14"`. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | `ThemeResourceApplier_*` (`lines 78-146, 219-249`) | Include `"AccentSubtle"` in the asserted accent token sets. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | `Backgrounds_wear_the_accent_room_tones` (`line 2232`) | Update `SourceLetterbox` assertion from `letterbox` to `wash` (`AppBackgroundWash`). |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | `Profile_rows_wear_their_own_accent_as_a_wash` (`lines 2253-2293`) | Update to assert `Assert.Null(row.Background)` and `ProfileIdentityColorRail` `Visible` vs `Collapsed` (`Margin="0"`). |
| `tests/PiPlay.Tests/Ui/MainWindowLifecycleTests.cs` | `Source_navigation_commands_are_disabled_while_the_placeholder_hides_the_browser` (`lines 177-238`) | Seed `window.SetCanGoBackForTests(true)` and include `ClearProfileMenuItem` in the profile menu assertions. |
| `docs/Theme_Preset_Differences.md` | `Palette` table (`lines 21-30`) & `Accent` notes (`lines 45-53`) | Update palette hexes and `ElevationPanel` / `AccentSubtle` documentation in lockstep. |

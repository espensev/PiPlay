# Chrome UI Spec Review

Goal: review the current PiPlay chrome UI findings against the current product spec and docs, using current verifiable evidence. This is a report only. No implementation fixes are included.

Review date: 2026-05-30  
Workspace: `D:\Development\DesktopApps\PiPlay`  
Build exercised for screenshots: `bin\publish\latest\PiPlay.exe`

## Evidence Sources

Current screenshot evidence captured from the running app:

| Evidence | File | What it verifies |
|---|---|---|
| E-SHOT-1 | `docs/evidence/chrome-current-base.png` | Current main-window chrome: icon buttons render as empty boxes; profile ComboBox is light; address-bar text is illegible; `Pop out video` icon renders. |
| E-SHOT-2 | `docs/evidence/chrome-current-back-tooltip-active.png` | Back button hover: button glyph is an empty box and tooltip is light themed. |
| E-SHOT-3 | `docs/evidence/chrome-current-minimize-tooltip-active.png` | Minimize hover: caption-button glyphs are empty boxes; tooltip is light themed and overlaps the caption-button area. |
| E-SHOT-4 | `docs/evidence/chrome-current-profiles-combo-open-active.png` | Profile ComboBox popup opens as a light, empty popup over the dark toolbar/content. |

Current source evidence:

| Evidence | Location | Current fact |
|---|---|---|
| E-SRC-1 | `src\PiPlay\PiPlay.csproj:6` | The app is WPF: `<UseWPF>true</UseWPF>`. |
| E-SRC-2 | `src\PiPlay\MainWindow.xaml:18` | The main window uses WPF `WindowChrome`. |
| E-SRC-3 | `src\PiPlay\Theme\ControlStyles.xaml:83`, `:88` | `IconButton` is a WPF `Button` style and sets `FontFamily` to `Segoe MDL2 Assets`. |
| E-SRC-4 | `src\PiPlay\MainWindow.xaml:51-56` | Caption buttons use PUA glyph content: `E921`, `E922`, `E8BB`. |
| E-SRC-5 | `src\PiPlay\MainWindow.xaml:72-77` | Current nav cluster is `BackButton`, `ReloadButton`, and `HomeButton`; there is no current `ForwardButton` in source. |
| E-SRC-6 | `src\PiPlay\MainWindow.xaml:89-95` | Save-profile and Pin controls also use PUA glyph content. |
| E-SRC-7 | `src\PiPlay\MainWindow.xaml:100`, `:114` | `Pop out video` and placeholder icons are explicit `TextBlock FontFamily="Segoe MDL2 Assets"` glyphs. |
| E-SRC-8 | `src\PiPlay\MainWindow.xaml:86-88` | `ProfilesCombo` is a WPF `ComboBox`, not a WebView2 page `<select>`. |
| E-SRC-9 | `src\PiPlay\Theme\ControlStyles.xaml:193-200` | `DarkComboBox` sets only basic closed-control properties. |
| E-SRC-10 | `src\PiPlay\Theme\ControlStyles.xaml:33-187`, `:193` | `ControlTemplate` definitions exist for other controls, but no ComboBox popup/list template is defined before or inside `DarkComboBox`. |
| E-SRC-11 | `src\PiPlay\Models\AppSettings.cs:16` | `Profiles` defaults to an empty list. |
| E-SRC-12 | `src\PiPlay\MainWindow.xaml.cs:246-250` | `LoadProfilesIntoCombo()` sets `ProfilesCombo.ItemsSource` from `_settings.Profiles.ToList()` and clears selection. |
| E-SRC-13 | `src\PiPlay\MainWindow.xaml:80-83` | `UrlBox` uses `DarkTextBox`. |
| E-SRC-14 | `src\PiPlay\Theme\ControlStyles.xaml:163-187` | `DarkTextBox` sets foreground/background through app tokens. |
| E-SRC-15 | `src\PiPlay\Theme\Colors.xaml:11`, `:14` | `SurfaceRaisedColor` is `#FF1C2025`; `TextPrimaryColor` is `#FFF3F5F7`. |
| E-SRC-16 | current contrast calculation | `#F3F5F7` on `#1C2025` calculates to `14.98:1`. |
| E-SRC-17 | `src\PiPlay\MainWindow.xaml:52-95`, `src\PiPlay\PlayerWindow.xaml:47-49` | Tooltips are plain string `ToolTip` values on controls. |
| E-SRC-18 | `src\PiPlay\Theme\ControlStyles.xaml` search | No app-level `ToolTip` style, placement, or template is defined. |

Current spec/doc evidence:

| Evidence | Location | Current requirement |
|---|---|---|
| E-SPEC-1 | `docs\PiPlay_Product_Engineering_Spec.md:129` | PiPlay should feel quiet, dark, polished, and utility-first. |
| E-SPEC-2 | `docs\PiPlay_Product_Engineering_Spec.md:142-153` | The spec defines dark color tokens including `SurfaceRaised` and `TextPrimary`. |
| E-SPEC-3 | `docs\PiPlay_Product_Engineering_Spec.md:171-180` | Icon style requires coherent action/title-bar icons compatible with the app icon family. |
| E-SPEC-4 | `docs\PiPlay_Product_Engineering_Spec.md:188-195` | Source Window is a dark native WPF host and includes window controls plus Pin/profile save/load. |
| E-SPEC-5 | `docs\PiPlay_Product_Engineering_Spec.md:1076-1086` | Accessibility/usability requires tooltips or labels and sufficient dark-UI contrast. |
| E-SPEC-6 | `docs\PiPlay_Product_Engineering_Spec.md:1165-1176` | UX gate expects icons to share stroke weight, corner style, and active color behavior. |
| E-SPEC-7 | `docs\PiPlay_Product_Engineering_Spec.md:1198-1218` | MVP includes WPF shell, basic profile save/load, and basic visual identity with dark shell and coherent icons. |
| E-SPEC-8 | `docs\SPEC_GAPS_AND_OWNERSHIP.md:17` | MVP profile scope is Pin plus basic profile save/load; profile edit/validation is Phase 2. |
| E-SPEC-9 | `docs\adr\0001-app-shell-wpf.md:14` | Custom/borderless WPF chrome requires explicit accessibility attention. |

External primary references checked:

- Microsoft Learn, Segoe MDL2 Assets icons: PUA glyphs need the intended font specified; without the font, glyphs are not available through normal fallback. The current doc also says Windows 11 recommends Segoe Fluent Icons over Segoe MDL2 Assets.  
  https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-ui-symbol-font
- W3C WCAG 2.2 Understanding SC 1.4.3: normal text needs at least `4.5:1` contrast, and contrast should be evaluated from specified foreground/background colors.  
  https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html

## Corrections From the Earlier Draft

- The app is WPF, not WinUI. Wording about `RequestedTheme` / `ElementTheme.Dark` does not apply to the current profile ComboBox issue.
- The current icon font is `Segoe MDL2 Assets`, not Segoe Fluent Icons.
- The current toolbar source has Back, Reload, and Home buttons. It does not currently define a Forward button.
- The `Pop out video` icon is not inline SVG in current source. It is also a Segoe MDL2 glyph, but it is hosted in an explicit `TextBlock` with its own `FontFamily`.
- The address-bar source color tokens do not fail contrast. The visible runtime problem is that the URL text is illegible in the current screenshot; the current declared color pair itself calculates to `14.98:1`.

## Findings

### 1. Caption, Navigation, Save, and Pin Icons Render as `.notdef` Boxes

Issue title: Caption/nav toolbar icon buttons render as `.notdef` boxes - Segoe MDL2 PUA glyphs not resolving in styled button content

Current visual evidence:

- `docs/evidence/chrome-current-base.png` shows empty-box glyphs for caption buttons, the toolbar icon buttons, Save, and Pin.
- `docs/evidence/chrome-current-back-tooltip-active.png` confirms the hovered Back button is an empty box.
- `docs/evidence/chrome-current-minimize-tooltip-active.png` confirms caption-button glyphs are empty boxes.
- The `Pop out video` icon renders in the same current run.

Current source evidence:

- `IconButton` sets `FontFamily="Segoe MDL2 Assets"` in `src\PiPlay\Theme\ControlStyles.xaml:88`.
- The affected buttons set glyphs as string `Content` values in `src\PiPlay\MainWindow.xaml:51-56`, `:72-77`, and `:89-95`.
- `Pop out video` uses an explicit `TextBlock FontFamily="Segoe MDL2 Assets"` in `src\PiPlay\MainWindow.xaml:100`.

Spec assessment:

- Out of spec.
- This violates the MVP visual-identity bar for coherent icons and polished native chrome (`E-SPEC-1`, `E-SPEC-3`, `E-SPEC-6`, `E-SPEC-7`).
- Tooltips make some targets discoverable, but discoverability does not satisfy the coherent-icon requirement when the visible button glyphs are broken.

Confidence: high. The current screenshots and current source agree.

### 2. Profiles ComboBox Renders as a Light Native Control and Opens a Light Empty Popup

Issue title: Profiles ComboBox popup renders light on dark chrome and opens empty

Current visual evidence:

- `docs/evidence/chrome-current-base.png` shows the closed ComboBox as a white/light control in the dark toolbar.
- `docs/evidence/chrome-current-profiles-combo-open-active.png` shows the open popup as a large white empty region.

Current source evidence:

- `ProfilesCombo` is a WPF `ComboBox` in `src\PiPlay\MainWindow.xaml:86-88`.
- `DarkComboBox` sets only basic properties in `src\PiPlay\Theme\ControlStyles.xaml:193-200`.
- There is no ComboBox popup/list template or `ComboBoxItem` style in `src\PiPlay\Theme\ControlStyles.xaml`.
- Profiles default to an empty list in `src\PiPlay\Models\AppSettings.cs:16`.
- `LoadProfilesIntoCombo()` binds the current settings profile list in `src\PiPlay\MainWindow.xaml.cs:246-250`.

Spec assessment:

- Light theme: out of spec.
- Empty state: usability/spec gap, not necessarily a data bug.
- The light control and popup conflict with the Source Window being a dark WPF host and with the MVP dark-shell visual identity (`E-SPEC-1`, `E-SPEC-4`, `E-SPEC-7`).
- The empty popup may be valid if no profiles exist, but the current presentation gives no empty-state affordance and looks broken. MVP requires basic profile save/load, so the empty load state should still look intentional.

Confidence: high for theme leak; medium for whether "empty" is incorrect data, because the source allows no saved profiles.

### 3. Address-Bar URL Text Is Illegible in the Current Runtime Capture

Issue title: Address-bar URL text is illegible in current UI - runtime rendering does not match expected readable dark textbox

Current visual evidence:

- `docs/evidence/chrome-current-base.png` shows the URL field text as mostly clipped/faint marks rather than readable URL text.

Current source evidence:

- `UrlBox` uses `DarkTextBox` in `src\PiPlay\MainWindow.xaml:80-83`.
- `DarkTextBox` sets `Foreground` to `TextPrimary` and `Background` to `SurfaceRaised` in `src\PiPlay\Theme\ControlStyles.xaml:163-187`.
- The current tokens are `#F3F5F7` on `#1C2025` (`src\PiPlay\Theme\Colors.xaml:11`, `:14`), which calculate to `14.98:1`.

Spec assessment:

- Runtime UI readability: out of spec if the captured state is representative.
- Declared color-token contrast: currently within spec.
- The right framing is not "the current XAML color pair fails WCAG." The current evidence says the rendered address-bar text is unreadable despite source tokens that should be readable. That points to runtime text rendering, clipping, resource application, or build-state mismatch, not a confirmed color-token contrast failure.
- This still conflicts with the accessibility/usability requirement that dark UI have sufficient contrast and readable controls (`E-SPEC-5`).

Confidence: high that the current screenshot is illegible; high that the current declared color pair passes contrast; medium on root cause.

### 4. ToolTips Render Light on Dark Chrome and Minimize Tooltip Occludes Caption Area

Issue title: ToolTips render light on dark chrome; Minimize tooltip overlaps caption buttons

Current visual evidence:

- `docs/evidence/chrome-current-back-tooltip-active.png` shows a light Back tooltip on dark chrome.
- `docs/evidence/chrome-current-minimize-tooltip-active.png` shows a light Minimize tooltip overlapping the caption-button area.

Current source evidence:

- Tooltips are plain string `ToolTip` values in `src\PiPlay\MainWindow.xaml:52-95` and `src\PiPlay\PlayerWindow.xaml:47-49`.
- `src\PiPlay\Theme\ControlStyles.xaml` does not define an app-level `ToolTip` style, dark template, or placement policy.

Spec assessment:

- Partially out of spec.
- The presence of tooltip text satisfies part of the minimum "labels or tooltips" accessibility requirement.
- The light theme and placement conflict with the dark, polished chrome goal and with the ADR note that custom/borderless WPF chrome needs explicit accessibility attention (`E-SPEC-1`, `E-SPEC-5`, `E-SPEC-9`).

Confidence: high. The current screenshot and source agree.

## Overall Spec Verdict

| Area | Verdict | Reason |
|---|---|---|
| MVP dark shell / visual identity | Fails current spec | Broken icon glyphs and light profile ComboBox are directly visible in the current app capture. |
| Coherent icons | Fails current spec | Multiple chrome/toolbar glyphs render as empty boxes while the `Pop out video` glyph renders, producing inconsistent icon behavior. |
| Basic profile save/load UI | Partially fails current spec | The control exists and is wired, but the load popup appears light, blank, and unintentionally empty. |
| Address-bar readability | Fails current runtime expectation; token contrast passes | The screenshot is unreadable, but current source colors are high contrast. |
| Tooltip accessibility/discoverability | Partially passes, partially fails | Tooltip text exists, but theme and placement are inconsistent with the custom dark chrome. |

Bottom line: based on current screenshots and current source/spec evidence, the chrome does not currently meet the spec's MVP visual-identity bar. The strongest evidence-backed failures are the `.notdef` icon boxes and the light-themed profile ComboBox/popup. The address-bar issue is also visible in the current capture, but should be reported as a runtime readability/rendering mismatch rather than a confirmed source-token contrast failure.

## Prior Related Report

This document supersedes and consolidates the earlier notes in `superseded-chrome-ui-issue-report.md` with current screenshots and refreshed source/spec evidence. The earlier report remains useful as the first-pass wording record.

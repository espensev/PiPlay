# Chrome UI Issue Report

Status: report only. No implementation changes have been made.

Scope: main-window chrome and toolbar issues visible in the referenced screenshots, cross-checked against the current WPF source.

## Fact-Check Notes

- PiPlay is a WPF app, not a WinUI app: `src/PiPlay/PiPlay.csproj` has `<UseWPF>true</UseWPF>`, and `src/PiPlay/MainWindow.xaml` uses `WindowChrome`.
- The current icon button style uses `Segoe MDL2 Assets`, not Segoe Fluent Icons: see `src/PiPlay/Theme/ControlStyles.xaml`.
- The profile selector is a WPF `ComboBox` in the toolbar, not a WebView2 page `<select>`: see `src/PiPlay/MainWindow.xaml`.
- The current source color tokens for the URL box are high contrast: `TextPrimary` is `#FFF3F5F7`, `SurfaceRaised` is `#FF1C2025`, and that pair calculates to about `14.98:1`. If the screenshot build shows low-contrast URL text, the runtime state or build output does not match the expected source styling.

## 1. Caption and Navigation Icon Buttons Render as `.notdef` Boxes

Issue title: Caption/nav icon buttons render as `.notdef` boxes - Segoe MDL2 PUA glyphs not resolving in button content

Observed behavior:

- The caption buttons on the right side of the chrome render as empty boxes instead of icons. The `Minimize` tooltip confirms at least one affected hit target.
- The navigation buttons in the toolbar also render as empty boxes. The `Back` tooltip confirms at least one affected hit target.
- Empty boxes in this context are consistent with the font `.notdef` glyph, often called "tofu".

Source evidence:

- `IconButton` sets `FontFamily` to `Segoe MDL2 Assets` in `src/PiPlay/Theme/ControlStyles.xaml`.
- The affected buttons use Private Use Area icon-code content in `src/PiPlay/MainWindow.xaml`, including:
  - `MinimizeButton`: `&#xE921;`
  - `MaximizeButton`: `&#xE922;`
  - `CloseButton`: `&#xE8BB;`
  - `BackButton`: `&#xE72B;`
  - `ReloadButton`: `&#xE72C;`
  - `HomeButton`: `&#xE80F;`
- The `Pop out video` icon is not inline SVG in the current source. It is also a Segoe MDL2 glyph, but it is hosted in an explicit `TextBlock FontFamily="Segoe MDL2 Assets"`. That makes the inconsistency more precise: string content inside styled icon buttons is failing, while an explicitly fonted `TextBlock` glyph appears to render.

Why this is a bug:

- These buttons are discoverable only by tooltip when the icon glyph fails.
- Segoe MDL2 icon values live in the Unicode Private Use Area. If the intended icon font is not applied or not available, normal text fallback will not provide equivalent symbols, so WPF can render `.notdef` boxes instead.

## 2. Profiles ComboBox Popup Uses the Default Light Theme

Issue title: Profiles ComboBox popup renders light on dark chrome - WPF ComboBox popup is not themed

Observed behavior:

- The profile dropdown opens as a stark white native-looking popup against the dark PiPlay toolbar.
- This is a theme leak: the dark app chrome does not carry through to the framework popup/list portion of the control.

Source evidence:

- `ProfilesCombo` is declared in `src/PiPlay/MainWindow.xaml`.
- `DarkComboBox` in `src/PiPlay/Theme/ControlStyles.xaml` sets foreground, background, border, font, padding, and height.
- `DarkComboBox` does not define a full `ControlTemplate`, `Popup`, `ScrollViewer`, or `ComboBoxItem` style. The opened list therefore falls back to the default WPF control template/theme behavior.

Framing:

- For this codebase, the accurate wording is WPF theme/template leak, not WinUI `RequestedTheme` or `ElementTheme.Dark`.
- WebView2 `PreferredColorScheme` may matter for web content, but it does not explain this specific `ProfilesCombo` popup because the control is WPF chrome.

## 3. Profiles ComboBox Opens an Empty, Oversized Flyout

Issue title: Profiles ComboBox flyout opens empty when no profiles exist, with no empty-state affordance

Observed behavior:

- The opened profile dropdown shows a tall blank region with no rows.
- The flyout/popup appears empty rather than communicating that there are no saved profiles.

Source evidence:

- `AppSettings.Profiles` defaults to an empty list in `src/PiPlay/Models/AppSettings.cs`.
- `LoadProfilesIntoCombo()` sets `ProfilesCombo.ItemsSource = _settings.Profiles.ToList()` and then clears selection in `src/PiPlay/MainWindow.xaml.cs`.
- Based on source inspection, an empty profile list is a valid runtime state. A binding failure is possible only if runtime evidence shows profiles should exist but do not appear.

Why this is a bug:

- The control can present a blank popup that looks broken.
- The blank popup combines with the light-theme leak, making the issue more visually obvious.

## 4. Address-Bar URL Text Appears Low Contrast in the Captured UI

Issue title: Address-bar URL text appears low contrast in screenshot - runtime styling does not match expected dark-textbox tokens

Observed behavior:

- The URL text in the address bar appears barely legible in the screenshot.
- If reproduced in the running app, this should be treated as a text contrast/readability issue. WCAG 2.x Success Criterion 1.4.3 is the correct accessibility reference for normal text contrast.

Source evidence:

- `UrlBox` uses `Style="{StaticResource DarkTextBox}"` in `src/PiPlay/MainWindow.xaml`.
- `DarkTextBox` sets `Foreground` to `TextPrimary` and `Background` to `SurfaceRaised` in `src/PiPlay/Theme/ControlStyles.xaml`.
- The corresponding colors in `src/PiPlay/Theme/Colors.xaml` are `#FFF3F5F7` on `#FF1C2025`, which calculate to about `14.98:1`. That is well above the WCAG 2.x `4.5:1` minimum for normal text.

Fact-checked conclusion:

- The screenshot observation is valid to report, but the current source does not support the narrower claim that the declared text color token itself fails WCAG contrast.
- The issue should be investigated as a runtime/build mismatch, missing resource application, disabled/inherited opacity state, or a stale screenshot build rather than as a confirmed failure of the current XAML color pair.

## 5. ToolTips Render Light and Can Occlude Caption Buttons

Issue title: ToolTips render light on dark chrome; Minimize tooltip overlaps caption buttons

Observed behavior:

- The `Back` and `Minimize` tooltips render as light system-styled boxes over the dark app chrome.
- In the screenshot, the `Minimize` tooltip overlaps the caption-button area, which makes the chrome feel visually crowded and can hide nearby controls.

Source evidence:

- Tooltips are assigned as plain string values in `src/PiPlay/MainWindow.xaml`, for example `ToolTip="Back"` and `ToolTip="Minimize"`.
- There is no app-level `ToolTip` style or template in `src/PiPlay/Theme/ControlStyles.xaml`.
- With no custom tooltip style or placement, WPF uses its default system tooltip appearance and placement behavior.

Why this is a bug:

- The tooltip appearance is visually inconsistent with the dark chrome.
- The placement can obscure the controls it is meant to describe.

## Spec Alignment

Overall assessment: the observed chrome issues are not just cosmetic nits. Most of them fall under the product spec's MVP visual-identity and accessibility/usability bar. The strongest spec failures are the broken icon glyphs and the unthemed `ComboBox` popup. The address-bar contrast item is conditional: it is a real screenshot concern, but the current source color tokens are already within the documented contrast intent.

| Report item | Spec fit | Spec basis | Assessment |
|---|---|---|---|
| Caption/nav icon buttons render `.notdef` boxes | Out of spec | Spec section 5 says PiPlay should feel "quiet, dark, polished, and utility-first"; section 5.4 requires coherent menu/title-bar icons; section 22.2 expects icons to share stroke weight, corner style, and active color behavior; MVP scope requires "basic visual identity: dark shell, coherent icons." | Strong failure. The icon buttons are present but visually broken. Tooltips make them discoverable, but the UI no longer meets the coherent-icon or polished-native-chrome bar. |
| Profiles `ComboBox` popup renders light | Out of spec | Section 5.5 defines the Source Window as a dark native WPF host; section 5 defines the dark visual identity; section 23 includes basic visual identity in MVP. | Strong failure. A default white popup in dark chrome is a WPF template/theme leak and breaks the Source Window's dark-shell requirement. |
| Profiles `ComboBox` opens empty/oversized | Partial spec gap / UX issue | Section 5.5 requires profile save/load as an MVP utility control; section 17 defines profiles as named saved launch targets; section 23 includes basic profile save/load in MVP. | Not a hard spec violation when there are genuinely no saved profiles, but the blank oversized flyout makes a valid empty state look broken. This should be reported as a usability gap around the profile-load control. |
| Address-bar URL text appears low contrast | Conditional | Section 20 requires dark UI to have sufficient contrast; section 5.2 defines `TextPrimary` and `SurfaceRaised`; WCAG 2.x SC 1.4.3 is the right contrast reference. | The screenshot concern is valid if reproduced. The current XAML tokens calculate to about `14.98:1`, so the current source appears within spec. Treat this as a runtime/build/resource-state mismatch unless a live repro shows the source tokens are not what renders. |
| ToolTips render light and overlap caption buttons | Partially out of spec | Section 20 requires buttons to have text labels or tooltips; ADR-0001 notes custom/borderless chrome needs explicit accessibility attention; section 5 requires polished dark utility chrome. | Mixed. The tooltip text exists, so the minimum discoverability requirement is partly met. The light tooltip theme and caption-button occlusion still conflict with the dark/polished chrome bar and custom-chrome accessibility expectations. |

Relevant spec references:

- `docs/PiPlay_Product_Engineering_Spec.md`: section 5, visual identity: quiet, dark, polished, utility-first.
- `docs/PiPlay_Product_Engineering_Spec.md`: section 5.4, icon style: menu/action/title-bar icons should be coherent and compatible with the app icon family.
- `docs/PiPlay_Product_Engineering_Spec.md`: section 5.5, Source Window: dark native WPF host with window controls, `Pin`, and profile save/load.
- `docs/PiPlay_Product_Engineering_Spec.md`: section 20, accessibility/usability: controls need labels or tooltips, obvious state, readable messages, and sufficient dark-UI contrast.
- `docs/PiPlay_Product_Engineering_Spec.md`: section 22.2, UX gates: icons should share stroke weight, corner style, and active color behavior.
- `docs/PiPlay_Product_Engineering_Spec.md`: section 23, MVP scope: WPF shell, basic profile save/load, and basic visual identity with dark shell and coherent icons.
- `docs/adr/0001-app-shell-wpf.md`: custom/borderless WPF chrome requires explicit accessibility attention.

## References

- Microsoft Learn, Segoe MDL2 Assets icons: https://learn.microsoft.com/windows/apps/design/iconography/segoe-ui-symbol-font
- W3C, WCAG 2.2 Success Criterion 1.4.3 Contrast Minimum: https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html

# UI Owner Review Follow-Up

Date: 2026-06-25

Scope: compare the latest committed source change (`f979793`, `fix(ui): stabilize popout action text rendering`) and the current docs working tree against the 2026-06-23 owner UI review now retained at `docs/reviews/2026-06-23-owner-appearance-popout-compact-review.md`.

## Implementation follow-up

Update on 2026-06-25: the first scoped fix pass is represented by
`docs/superpowers/specs/2026-06-25-ui-owner-followup-fixes-design.md` and
`docs/superpowers/plans/2026-06-25-ui-owner-followup-fixes.md`.

Closed in that pass:

- The `AccentButton` outline no longer uses the hardcoded 2 DIP border; it resolves through the shared `BorderThicknessDefault` token.
- The Source Placeholder now has a direct `Show popout` action wired to the existing popout activation helper.

The profile/accent follow-up is represented by
`docs/superpowers/specs/2026-06-25-profile-color-identity-and-accent-fill-design.md` and
`docs/superpowers/plans/2026-06-25-profile-color-identity-and-accent-fill.md`.

Closed in that pass:

- Profile color no longer overrides the global app accent; it is used as a visible filled profile selector chip.
- Settings Appearance always edits the global app accent.
- Accent buttons use accent fill tokens with generated dark/white foreground, not an outline-only treatment.
- Free app/profile colors accept any valid `#RRGGBB`; invalid hex remains blocked/defaulted.

Still open after both passes:

- Theme presets still need stronger final-window differentiation beyond token-level differences.
- Large rounded-card popout silhouettes still require a DWM/WebView2 hosting decision.
- Main-window Browse/Cinema/Compact UX modes remain net-new work.
- `Restore video here` remains a separate return/detach behavior decision.

## Findings

### P1 - The latest source change does not fix the theme/corner visual-difference complaint

The only committed product-code delta since `origin/main` is the popout action button rendering fix in `src/PiPlay/MainWindow.xaml` and `src/PiPlay/Theme/ControlStyles.xaml`. It changes the button margin and text rendering options; it does not change the theme presets, popout silhouette, video clipping, border/shadow model, or WebView2 hosting architecture.

Evidence:

- `src/PiPlay/MainWindow.xaml:129` still uses `Style="{StaticResource AccentButton}"` for `PopOutButton`.
- `src/PiPlay/Theme/ControlStyles.xaml:64-82` only adds pixel-aligned text rendering to `AccentButton`; it still sets `BorderThickness` to `2` at line 68.
- `src/PiPlay/PlayerWindow.xaml:10` and `src/PiPlay/MainWindow.xaml:11` still use `AllowsTransparency="False"`.
- `src/PiPlay/Theme/ThemeCatalog.cs:309-315` still maps both `soft` and `round` corner overrides to `DwmCornerMode.Round`.
- `src/PiPlay/Theme/ThemeResourceApplier.cs:91-92` still publishes `RadiusMainWindowFrame` and `RadiusPopoutFrame` tokens, but the top-level HWND silhouette remains DWM-owned.

Impact: the owner review's core complaint that the themes "do not meaningfully change the final window feel" remains open. The current docs correctly state this, but the source is not materially closer to the rounded floating-card target.

### Closed - Accent actions no longer read as fat outline buttons

`AccentButton` now uses the selected accent as its fill, resolves text through `OnAccent`/`OnAccentPressed`, and keeps border weight on the shared 1 DIP token. That closes the direct "filled button, not outline" complaint for primary actions like Pop out / Show popout and Settings Done.

Evidence:

- `src/PiPlay/Theme/ControlStyles.xaml` sets `Background` to `AccentPrimary`, hover to `AccentHover`, pressed to `AccentPressed`, and foreground to `OnAccent`/`OnAccentPressed`.
- `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` asserts the filled accent tokens and nested Popout text foreground binding.
- `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` asserts replacing `AccentPrimary` recolors the filled button background.

Remaining risk: screenshot QA is still useful because this is a visual density change, but the source contract is now the requested filled-accent treatment.

### Closed - Profile color is identity, not app accent

The owner review's model reversal is implemented in this working tree. The app accent resolves from `theme.accentColor`; an active profile's `AccentColor` is no longer applied to global app chrome.

Evidence:

- `src/PiPlay/Services/ProfileAccentService.cs` returns the normalized global accent from `ResolvedAccentColor` and writes app accent commits to `settings.Theme.AccentColor`.
- `src/PiPlay/MainWindow.xaml` fills `ProfileIdentityChip` with the profile `AccentColor` and uses `AccentForegroundConverter` for chip text.
- `tests/PiPlay.Tests/ProfileAccentServiceTests.cs` and `tests/PiPlay.Tests/Ui/MainWindowProfileAccentTests.cs` now assert profile selection does not recolor `AccentPrimary`.

Remaining risk: optional active-profile popout border is still a separate future enhancement.

### Closed - Accent/profile colors accept any valid RGB hex

The old readability gate has been relaxed to validity. Mid-tone colors such as `#787878` are accepted and previewed; invalid hex is still blocked/defaulted.

Evidence:

- `src/PiPlay/Theme/AccentReadabilityPolicy.cs` accepts any `ThemeCatalog.IsValidHex` value.
- `src/PiPlay/Theme/ThemeColors.cs` picks the higher-contrast dark/white foreground instead of throwing or forcing repair in the mid-tone dead zone.
- `src/PiPlay/Services/ProfileService.cs` validates profile colors by valid hex and normalizes valid values for storage.
- `tests/PiPlay.Tests/Theme/AccentReadabilityPolicyTests.cs`, `ThemeColorsTests.cs`, `ProfileServiceTests.cs`, `AccentColorPickerTests.cs`, and `SettingsWindowAppearanceTests.cs` cover mid-tone acceptance and invalid-hex blocking.

Remaining risk: user-selected colors can still be aesthetically weak for borders/glows; border strength/opacity remains a separate product control, not a reason to reject the color.

### Partially closed - Placeholder now has Show popout; Restore here remains open

The placeholder now offers a direct `Show popout` action wired to the same `ActivateExistingPlayer` helper as the toolbar. The separate owner-requested `Restore video here` behavior is still open.

Evidence:

- `src/PiPlay/MainWindow.xaml` includes `PlaceholderShowPopoutButton` using `AccentButton`.
- `src/PiPlay/MainWindow.xaml.cs` wires `PlaceholderShowPopoutButton_Click` to `ActivateExistingPlayer()`.
- `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` asserts the button exists, uses the accent style, and calls the intended handler.

### P0 - Owner "Compact Mode" is still net-new main-window UX work

Current compact support remains the popout playback-surface mode. It does not implement the owner-requested main-window compact/cinema layout with hidden address/profile controls and hover-reveal chrome.

Evidence:

- `src/PiPlay/SettingsWindow.xaml:195-203` labels the shipped control `Compact player` and says it applies to new Popout Players only.
- `src/PiPlay/MainWindow.xaml.cs:824-827` uses `PlaybackModePolicy.ResolveEffectiveMode` and `BuildPopoutUrl` when creating a new popout.
- `src/PiPlay/Services/PlaybackModePolicy.cs:59` documents the profile/global compact-player resolution.

Impact: the review's "Compact Mode does not work" should be interpreted as a missing UX/layout mode, not a bug fixed by the current compact-player playback setting.

## What Is Correctly Captured In The Docs

The current docs working tree is accurate and usefully cautious:

- `docs/Theme_Preset_Differences.md` now explains that token differences are real, but the final window impact is constrained by opaque video content and DWM/WebView2 airspace.
- `docs/SPEC_GAPS_AND_OWNERSHIP.md` captures the owner directions as real product intent, marks the accent/profile split and valid-hex color gate as implemented, and keeps the remaining architecture decisions open.
- `docs/PiPlay_Product_Engineering_Spec.md` calls out the DWM-owned corner limit, the compact terminology conflict, the identity-only profile color model, and the remaining `Restore video here` gap.
- `docs/reviews/2026-06-24-owner-review-docs-grounding-review.md` correctly says runtime QA was not performed and does not overclaim fixes.

## Verdict

The first code change was a narrow popout action text-rendering fix. The current working tree adds two real appearance fixes: filled accent actions and the global-accent/profile-identity split with wider valid-hex color acceptance.

The major owner review items remain open:

- Make theme presets visibly different in the final window, not only in tokens.
- Decide the corner-silhouette architecture if large rounded popout cards are required.
- Add `Restore video here` if the placeholder should move playback back without closing the popout.
- Build the owner-requested main-window compact/cinema UX model, if still desired.

Recommended next implementation order:

1. Runtime/screenshot QA: check Pop out / Show popout, Settings Done, and profile chips with several custom colors.
2. Architecture decision: choose DWM-limited corners or a WebView2 airspace lift before promising large rounded-card borders/shadows.
3. Product/UX decision: decide whether `Restore video here` is required in addition to `Show popout`.
4. Main-window UX: define Browse/Cinema/Compact as a separate mode model, not a bugfix to the existing popout compact-player setting.

## Validation

- `dotnet test PiPlay.sln --configuration Debug --filter 'FullyQualifiedName~AccentReadabilityPolicyTests|FullyQualifiedName~ThemeColorsTests|FullyQualifiedName~ProfileServiceTests|FullyQualifiedName~ProfileAccentServiceTests|FullyQualifiedName~MainWindowProfileAccentTests|FullyQualifiedName~XamlInvariantTests|FullyQualifiedName~WpfRuntimeTests|FullyQualifiedName~AccentColorPickerTests|FullyQualifiedName~SettingsWindowAppearanceTests|FullyQualifiedName~SettingsServiceTests' --nologo`: passed, 238 tests.
- `dotnet test PiPlay.sln --configuration Debug --nologo`: passed, 682 tests.
- `git diff --check`: passed. PowerShell/Git reported LF-to-CRLF normalization warnings for touched files.

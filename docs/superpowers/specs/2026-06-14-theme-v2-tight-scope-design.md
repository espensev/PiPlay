# PiPlay Theme System V2 — tight-scope design

Canonical theme-flow contract: this supersedes
`docs/superpowers/specs/2026-06-14-theme-differentiation-design.md`.

Status: implementation spec. Imported from `piplay-theme-v2-tight-scope-docs.zip`, then checked
against the local source tree on 2026-06-14. Phase A (theme identity values, exact gates, and
`docs/Theme_Preset_Differences.md`) is now reflected in the current checkout. Phases B-E remain
pending. The original focused validation at import time passed:
`dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~ThemeCatalogTests|FullyQualifiedName~ThemeColorsTests|FullyQualifiedName~ThemePreferenceResolverTests|FullyQualifiedName~ThemeSettingsWriterTests|FullyQualifiedName~XamlInvariantTests"` = 97 passed.

## Goals

Make the theme system feel like a real PiPlay visual layer, not just a label plus accent chip.

Done means:

- `Sharp Dark`, `Minimal`, and `Soft Glass` are visually distinct at a glance.
- Theme presets own palette, rounding, native corner mode, opacity/fade behavior defaults, density, and inner elevation.
- User color freedom stays constrained to one accent value.
- Accent variants are generated from that one accent value so buttons, focus rings, hover states, subtle fills, and glow do not become unrelated colors.
- WebView2 remains safe: no transparent WPF window hacks, no clipping the video surface, no click-through.
- The current browse/popout behavior, profile behavior, privacy behavior, and the current “double/fullview-like” state investigation are not touched by this work.

## Current baseline from the 2026-06-14 checkout

Already in place:

- `ThemeCatalog` has `sharp-dark`, `minimal`, and `soft-glass`.
- `ThemePalette`, `ThemeRadii`, and `DwmCornerMode` exist.
- Phase A target palette, radius, native corner, default accent, fade/top-bar, and opacity values are in the current catalog.
- `ThemeCatalogTests` contains spec-literal gates for display names, target values, and behavior-default identity deltas.
- Settings has theme chips, accent chips, and corner-style chips.
- `ThemeResourceApplier` replaces palette brushes, companion colors, accent brushes, and radius resources.
- The nullable behavior override model exists: `null` means “follow the selected preset default”.
- Accent-preservation on theme switch exists: a custom accent survives, while a previous preset default adopts the next preset default.
- XAML now uses semantic `Radius*` resources for control `CornerRadius` values.
- `docs/Theme_Preset_Differences.md` reflects the current Phase A catalog values.

Remaining gap:

- Only two accent tokens are live today: `AccentPrimary` and `AccentPrimaryLight`.
- Primary button foreground is still hardcoded to `#FF06141A`; this blocks a safe full color wheel.
- Density is still hardcoded in multiple style values: button padding, text box height/padding, combo box height, combo item padding, chip heights, swatch sizes, scrollbar thickness, and tooltip padding.
- Inner elevation/shadow is not theme-owned yet.
- `Media Glow` should remain deferred until the base three themes are actually distinct.

The current-code reference is `docs/Theme_Preset_Differences.md`. It already reflects Phase A and
must be refreshed again in the same PR as later catalog, density, or effective preset-comparison
changes.

## Requirements served

- REQ-UI-01 / REQ-UI-02: dark, intentional, legible app chrome.
- Theme preset intent: presets should represent distinct product moods, not small color deltas.
- Existing theme/corner work: preserve the dynamic-resource replacement model, nullable behavior overrides, and DWM-owned window corners.

## Settled decisions

1. **Theme preset = system rules. Accent = user identity.** Do not create separate theme IDs for every accent. `Sharp Dark + Steel` is a variant, not a new theme.
2. **Three shipped themes first.** Tighten `Sharp Dark`, `Minimal`, and `Soft Glass` before adding `Media Glow`.
3. **Color wheel comes after accent variants.** A wheel is unsafe until `OnAccent`, `AccentHover`, `AccentPressed`, `AccentMuted`, `AccentSubtle`, `AccentBorder`, and `AccentGlow` exist.
4. **No arbitrary per-control styling in v2.** No per-button radius sliders, per-state color pickers, custom shadow editors, imported themes, or marketplace format.
5. **Rounding has two owners.** DWM owns the real top-level HWND corner. XAML owns internal controls, panels, chips, tooltips, popups, and settings surfaces.
6. **WebView2 remains rectangular inside the window.** Do not use `AllowsTransparency=True`; do not clip WebView2 with WPF masks; do not create per-pixel transparent video windows.
7. **Preset behavior defaults remain live until overridden.** A preset click resets behavior overrides to `null`; touching Advanced controls creates explicit overrides.
8. **The “double/fullview-like” state stays out of theme scope.** Keep it as an evaluated playback/window state until it has distinct invariants. Themes should not encode or special-case it.

## Acceptance criteria

A theme implementation is done when all of the following are true:

- Switching between `Sharp Dark`, `Minimal`, and `Soft Glass` visibly changes palette temperature, corner profile, native corner mode, default accent, and popout behavior defaults.
- The three themes have exact catalog values matching this spec or a recorded follow-up decision.
- `ThemeCatalogTests` gates preset IDs, default accents, palette contrast, accent-chip contrast, radii ordering, DWM mode, behavior defaults, density values, and identity deltas.
- `XamlInvariantTests` gates all theme-controlled resource keys and blocks hardcoded `CornerRadius` at control sites.
- New density/elevation resource keys resolve from all reachable scopes and every migrated style uses `DynamicResource`.
- `WpfRuntimeTests` prove that applying each preset replaces palette, radius, accent, density, border, and elevation resources in already-realized controls.
- The URL/search box still does not clip text at 150% DPI.
- Popout WebView2 resize, borderless hit-test zones, and no-click-through invariants remain unchanged.
- Settings copy honestly reflects the apply model: theme/accent/corners apply on close; opacity previews live while dragging.
- No color wheel lands before dynamic `OnAccent` and contrast-safe accent variants.
- `docs/Theme_Preset_Differences.md` is refreshed in the same PR that changes catalog values and
  refreshed again if later density/elevation work changes the effective preset comparison.

## Theme model v2

Keep the existing `ThemePreset` shape and extend it additively.

```csharp
public sealed record ThemePreset(
    string Id,
    string DisplayName,
    string Description,
    string DefaultAccentColor,
    string DefaultFadeDelayPreset,
    bool DefaultStripAutoHide,
    double DefaultActiveWindowOpacity,
    double DefaultIdleWindowOpacity,
    ThemePalette Palette,
    ThemeRadii Radii,
    DwmCornerMode DwmCorners,
    ThemeDensity Density,
    ThemeElevation? Elevation,
    ThemeAccentProfile AccentProfile);
```

New records:

```csharp
public sealed record ThemeDensity(
    double ControlHeight,
    double IconButtonSize,
    double ScrollbarThickness,
    Thickness ButtonPadding,
    Thickness InputPadding,
    Thickness MenuItemPadding,
    Thickness PresetChipPadding,
    Thickness ToolTipPadding,
    Thickness BorderThicknessDefault);

public sealed record ThemeElevation(
    double PopupBlurRadius,
    double PopupShadowDepth,
    double PopupOpacity,
    double PanelBlurRadius,
    double PanelShadowDepth,
    double PanelOpacity);

public sealed record ThemeAccentProfile(
    double HoverWhiteMix,
    double PressedBlackMix,
    double MutedSurfaceMix,
    double BorderWhiteMix,
    byte SubtleAlpha,
    byte GlowAlpha);
```

Persist only stable user choices:

```json
{
  "theme": {
    "themeId": "sharp-dark",
    "accentColor": "#00D4FF",
    "cornerStyle": "theme",
    "fadeDelayPreset": "normal",
    "stripAutoHide": null,
    "activeWindowOpacity": null,
    "idleWindowOpacity": null
  }
}
```

Do not persist derived accent brushes, density resources, generated hover colors, shadows, or radius tokens.

## Theme presets — target values

### Product intent

| Theme | Product mood | Best use |
|---|---|---|
| `sharp-dark` | Compact utility shell | Default browsing, precise controls, least decorative chrome. |
| `minimal` | Warmer low-distraction shell | Daily use, calmer browsing, less neon identity. |
| `soft-glass` | Floating overlay shell | Popout-heavy use, desktop media surface, translucent controls. |
| `media-glow` | Expressive display mode | Deferred. Use only after the first three are accepted. |

### Palette targets

These values intentionally push temperature and border differences harder than the current bundle.

| Token | Sharp Dark | Minimal | Soft Glass | Media Glow, deferred |
|---|---|---|---|---|
| `AppBackground` | `#050609` | `#14120F` | `#0B1018` | `#070712` |
| `SurfaceBase` | `#0B0E12` | `#1C1A16` | `#121A26` | `#101021` |
| `SurfaceRaised` | `#131820` | `#26231E` | `#1B2738` | `#191A2F` |
| `SurfaceHover` | `#1E2630` | `#312D27` | `#26354B` | `#26294A` |
| `BorderSubtle` | `#2B3645` | `#3C372F` | `#44526E` | `#3D4268` |
| `BorderStrong` | `#3E4B5C` | `#50493E` | `#66799E` | `#7079B8` |
| `TextPrimary` | `#F4F7FA` | `#F4F1EC` | `#F6F8FC` | `#F8F7FF` |
| `TextSecondary` | `#9AA2AD` | `#B0A99E` | `#C4CEDC` | `#C8C5E8` |
| `Danger` | `#E45D75` | `#E8564C` | `#E45D75` | `#F05F7D` |

Palette rule:

- Sharp is near-black and cool.
- Minimal is warm charcoal.
- Soft Glass is blue/cool with brighter borders and secondary text.
- Media Glow is violet-blue and expressive, but not part of the next implementation PR.

### Rounding targets

All values are device-independent pixels. These are semantic radii, not one global value.

| Token | Sharp Dark | Minimal | Soft Glass | Media Glow, deferred |
|---|---:|---:|---:|---:|
| `MainWindowFrame` | 2 | 8 | 14 | 12 |
| `PopoutFrame` | 2 | 12 | 22 | 18 |
| `TitleBar` | 2 | 8 | 14 | 12 |
| `Button` | 3 | 8 | 12 | 10 |
| `IconButton` | 3 | 8 | 12 | 10 |
| `Input` | 3 | 8 | 12 | 10 |
| `Panel` | 2 | 10 | 16 | 14 |
| `Popup` | 4 | 10 | 16 | 14 |
| `Thumbnail` | 2 | 6 | 10 | 10 |
| `Swatch` | 4 | 8 | 12 | 12 |
| `ScrollbarThumb` | 3 | 5 | 6 | 6 |
| `ToolTip` | 4 | 8 | 10 | 10 |

Rounding rule:

- Sharp must look intentionally tight, not accidentally unrounded.
- Minimal must be visibly softer than Sharp.
- Soft Glass must be obviously rounder in popout and overlay surfaces.
- Do not exceed 24 DIP for built-in themes.

### Native corners and behavior defaults

| Field | Sharp Dark | Minimal | Soft Glass | Media Glow, deferred |
|---|---|---|---|---|
| DWM corner mode | `Default` | `SmallRound` | `Round` | `Round` |
| Default accent | Cyan `#00D4FF` | Steel blue `#5AA9E6` | Violet `#A78BFA` | Cyan or violet; decide later |
| Recommended sharp muted variant | Steel `#4A8FAB` | N/A | N/A | N/A |
| Fade delay preset | `normal` | `long` | `short` | `normal` |
| Strip auto-hide | `false` | `false` | `true` | `true` |
| Active popout opacity | `1.00` | `1.00` | `0.92` | `0.94` |
| Idle popout opacity | `1.00` | `1.00` | `0.78` | `0.82` |

Default accent rule:

- Keep `Sharp Dark` cyan as the install/default brand identity.
- Use `Sharp Dark + Steel (#4A8FAB)` as the muted sharp variant shown in the recent screenshots.
- Do not create a fourth “Sharp Steel” theme. This is an accent variant.

If the muted sharp look should become the product default later, change only the sharp preset default accent and the tests. Do not add another theme ID.

### Density targets

Density should be theme-owned but not user-overridable in this pass.

| Field | Sharp Dark | Minimal | Soft Glass | Safe fallback |
|---|---:|---:|---:|---:|
| `ControlHeight` | 30 | 34 | 38 | 32 |
| `IconButtonSize` | 30 | 32 | 36 | 32 |
| `ScrollbarThickness` | 8 | 10 | 10 | 10 |
| `ButtonPadding` | `10,5` | `12,6` | `16,9` | `12,6` |
| `InputPadding` | `8,0` | `10,0` | `14,2` | `10,0` |
| `MenuItemPadding` | `8,5` | `10,6` | `14,9` | `10,6` |
| `PresetChipPadding` | `8,0` | `10,0` | `14,0` | `10,0` |
| `ToolTipPadding` | `7,4` | `8,5` | `10,7` | `8,5` |
| `BorderThicknessDefault` | `1` | `1` | `1` | `1` |

Density rule:

- Do not change title bar row heights in the first density pass. That is a higher-risk layout change.
- First migrate controls and settings chips. Leave main/player row definitions alone until density resources are stable.
- Gate text clipping before changing URL/search heights.
- `BorderThicknessDefault` is a uniform WPF `Thickness`, not a `double` or string, because
  `BorderThickness` consumers expect `Thickness`. It is tokenized now to remove hardcoded default
  strokes, but it is intentionally constant at `1` across all three v2 themes. Do not use border
  weight as a visual differentiation axis until pixel-snapping and layout-rounding risk has its own
  test gate.

### Elevation targets

Elevation is inner-only. It applies to popups, menus, settings panels, and raised internal surfaces. It must not be an outer-window shadow or glow.

| Field | Sharp Dark | Minimal | Soft Glass |
|---|---:|---:|---:|
| Popup blur | 0 | 8 | 16 |
| Popup shadow depth | 0 | 1 | 2 |
| Popup opacity | 0 | 0.22 | 0.34 |
| Panel blur | 0 | 6 | 12 |
| Panel shadow depth | 0 | 1 | 2 |
| Panel opacity | 0 | 0.16 | 0.26 |

Implementation rule:

- `null` effect for Sharp Dark.
- Frozen `DropShadowEffect` resources for Minimal and Soft Glass.
- Do not use any outer-window effect while `AllowsTransparency=False` and WebView2 is hosted by HWND.

## Accent variants

### Accent chip set

Keep the current six-chip set for the next pass. `Rose` is defined here for future use but should not land as a chip unless there is a clear UI purpose beyond danger/destructive actions.

| Key | Display | Hex | Use |
|---|---|---|---|
| `cyan` | Cyan | `#00D4FF` | PiPlay brand/default energy. |
| `steel-blue` | Steel blue | `#5AA9E6` | Minimal default, calmer blue. |
| `steel` | Steel | `#4A8FAB` | Muted sharp variant. |
| `violet` | Violet | `#A78BFA` | Soft Glass default. |
| `green` | Emerald | `#38D996` | Play/active/positive identity. Keep JSON key `green` for compatibility. |
| `amber` | Amber | `#FFC857` | Warm identity. |
| `rose` | Rose | `#E45D75` | Deferred; good future accent, but overlaps with danger. |

### Derived accent tokens

The app stores one base accent only. Runtime derives the following tokens:

| Resource key | Meaning | First consumers |
|---|---|---|
| `AccentPrimary` / `AccentPrimaryColor` | Main chosen accent | primary actions, active glyphs. |
| `AccentHover` / `AccentHoverColor` | Lighter hover fill | primary button hover, selected chip hover. |
| `AccentPressed` / `AccentPressedColor` | Darker pressed fill | primary button pressed. |
| `AccentMuted` / `AccentMutedColor` | Theme-muted accent | restrained sharp glyphs/borders and dark selected surfaces; not a dark-text primary-button fill. |
| `AccentSubtle` / `AccentSubtleColor` | Transparent/subtle accent wash | selected rows, focus backgrounds. |
| `AccentBorder` / `AccentBorderColor` | Accent outline | focus border, checked chip border. |
| `AccentGlow` / `AccentGlowColor` | Transparent glow/accent shadow brush | Soft Glass/Media Glow inner effects only. |
| `OnAccent` / `OnAccentColor` | Foreground on `AccentPrimary` / `AccentHover` fill | primary button text/icons. |
| `OnAccentPressed` / `OnAccentPressedColor` | Foreground on `AccentPressed` fill | primary button pressed text/icons. |
| `AccentPrimaryLight` | Compatibility alias | Alias to `AccentHover` for one migration pass. |

### Derivation algorithm

Use the theme’s `ThemeAccentProfile` so each theme can use the same base accent differently.

Suggested profiles:

| Theme | HoverWhiteMix | PressedBlackMix | MutedSurfaceMix | BorderWhiteMix | SubtleAlpha | GlowAlpha |
|---|---:|---:|---:|---:|---:|---:|
| Sharp Dark | 0.18 | 0.16 | 0.58 | 0.10 | `0x22` | `0x33` |
| Minimal | 0.22 | 0.14 | 0.50 | 0.12 | `0x26` | `0x40` |
| Soft Glass | 0.30 | 0.12 | 0.40 | 0.16 | `0x33` | `0x66` |

Pseudo-code:

```csharp
var primary = Parse(baseAccent);
var hover = Mix(primary, White, profile.HoverWhiteMix);
var pressed = Mix(primary, Black, profile.PressedBlackMix);
var muted = Mix(primary, preset.Palette.SurfaceRaised, profile.MutedSurfaceMix);
var border = Mix(primary, White, profile.BorderWhiteMix);
var subtle = WithAlpha(primary, profile.SubtleAlpha);
var glow = WithAlpha(primary, profile.GlowAlpha);
var onAccent = PickReadableForeground(primary);
var onAccentPressed = PickReadableForeground(pressed);
```

`PickReadableForeground`:

```csharp
var dark = Color.FromRgb(0x06, 0x14, 0x1A);
var white = Colors.White;
if (ContrastRatio(dark, primary) >= 4.5) return dark;
if (ContrastRatio(white, primary) >= 4.5) return white;
throw new InvalidOperationException("Accent is outside the readable foreground lane.");
```

Derived-token contrast rules:

- `OnAccent` must contrast at `>= 4.5:1` against both `AccentPrimary` and `AccentHover`.
- `OnAccentPressed` must contrast at `>= 4.5:1` against `AccentPressed`. If a dim accent fails,
  reduce that theme's `PressedBlackMix` or pick the readable foreground for the pressed fill; do
  not reuse `OnAccent` blindly.
- `AccentBorder` must contrast at `>= 3.0:1` against `SurfaceBase` and `SurfaceRaised`.
- `AccentMuted` is a dark, restrained accent token. It may be used as a glyph/border on dark
  surfaces, or as a dark selected backing paired with `TextPrimary`. It must not be used as a
  fill under `OnAccent`/dark text unless that exact pair is separately gated at `>= 4.5:1`.
- `AccentSubtle` and `AccentGlow` are alpha overlays; test them in their composited consumer
  contexts before placing text on top of them.

> **Phase B implementation status (Tasks 3–4 landed).** Task 3 (`feat(theme): derive accent state
> tokens`): `ThemeColors.DeriveAccentSet` + `AccentProfileFor` + fail-closed `PickReadableForeground`
> are in `src`, gated by `ThemeCatalogTests.Derived_accent_tokens_meet_contrast_minimums` (every
> offered accent × every theme profile) for the three rules that have a consumer: `OnAccent`,
> `OnAccentPressed`, `AccentBorder`. All pass on the v2 chips. Task 4 (`feat(theme): apply derived
> accent tokens and migrate accent consumers`): `ThemeResourceApplier` now derives + applies every
> token and its companion `*Color` (review BL-09 fixed); `AccentButton` uses `OnAccent` / `AccentHover`
> / a new `AccentPressed`+`OnAccentPressed` pressed trigger, and `DarkTextBox` focus uses
> `AccentBorder`; `AccentPrimaryLight` aliases `AccentHover`. **CON-1 `AccentPressed` is resolved**:
> re-picking the foreground against the pressed fill flips the dim steel chip from 3.82/3.98/4.14:1
> (naive `OnAccent` reuse) to 4.89/4.70/4.52:1 (white) — soft-glass steel at **4.52:1** is the razor
> margin the gate guards.
>
> **OPEN follow-up from PR #25 audit:** the first real `AccentButton` consumer (`PopOutButton`) has
> nested `TextBlock` content. The `AccentButton` template must forward
> `TextElement.Foreground="{TemplateBinding Foreground}"` through its `ContentPresenter`, and runtime
> coverage must prove nested button content renders `OnAccent` / `OnAccentPressed`, not merely that
> `Button.Foreground` resolves the token. See
> `docs/reviews/2026-06-17-pr25-theme-accent-audit.md`.
>
> **OPEN — `AccentMuted` is NOT yet WCAG-safe as one universal token (design decision owed).** With
> the spec mixes, the two pinned pairings trade off inversely and neither holds across all six chips ×
> three profiles: muted-as-glyph (`>= 3.0` vs `SurfaceBase`) fails the dim chips (steel 2.05–2.76,
> most sharp/minimal chips `< 3.0`), and muted-as-backing (`TextPrimary >= 4.5`) fails the bright
> chips on minimal/soft-glass (amber 3.18, cyan 3.55, green 3.62). Every chip×profile has *at least
> one* safe pairing, but no single use is universally safe — so the review's "muted rides light text"
> fix is insufficient. `AccentMuted` (and the alpha `AccentSubtle`/`AccentGlow`) are therefore derived
> but **un-gated and unwired** this pass; before `AccentMuted` ships, raise `MutedSurfaceMix` so muted
> is reliably dark (and re-confirm `TextPrimary >= 4.5`), or pin it to one constrained use and gate
> that exact pairing. Owner call.

For the first color wheel, avoid arbitrary saturation/value controls. Ship a hue wheel or hue chips
that generate accents on a pre-validated accessible lane, and add a dense hue-sweep invariant before
free-form hex or arbitrary value/saturation controls are allowed.

### Theme-agnostic variant examples

These examples use the existing 30% hover mix for easy visual review. Actual runtime values should use the selected theme’s `ThemeAccentProfile`.

| Accent | Primary | Hover | Pressed | Border | Subtle | Glow |
|---|---|---|---|---|---|---|
| Cyan | `#00D4FF` | `#4CE1FF` | `#00BBE0` | `#1FD9FF` | `#3300D4FF` | `#6600D4FF` |
| Steel blue | `#5AA9E6` | `#8CC3EE` | `#4F95CA` | `#6EB3E9` | `#335AA9E6` | `#665AA9E6` |
| Steel | `#4A8FAB` | `#80B1C4` | `#417E96` | `#609CB5` | `#334A8FAB` | `#664A8FAB` |
| Violet | `#A78BFA` | `#C1AEFC` | `#937ADC` | `#B299FB` | `#33A78BFA` | `#66A78BFA` |
| Emerald | `#38D996` | `#74E4B6` | `#31BF84` | `#50DEA3` | `#3338D996` | `#6638D996` |
| Amber | `#FFC857` | `#FFD889` | `#E0B04D` | `#FFCF6B` | `#33FFC857` | `#66FFC857` |
| Rose, deferred | `#E45D75` | `#EC8E9E` | `#C95267` | `#E77086` | `#33E45D75` | `#66E45D75` |

## Resource keys to add

Add these resource keys to `Colors.xaml` seeds and `ThemeResourceApplier.Apply`.

Accent:

```text
AccentHover / AccentHoverColor
AccentPressed / AccentPressedColor
AccentMuted / AccentMutedColor
AccentSubtle / AccentSubtleColor
AccentBorder / AccentBorderColor
AccentGlow / AccentGlowColor
OnAccent / OnAccentColor
OnAccentPressed / OnAccentPressedColor
```

Density:

```text
DensityControlHeight = double
DensityIconButtonSize = double
DensityScrollbarThickness = double
DensityButtonPadding = Thickness
DensityInputPadding = Thickness
DensityMenuItemPadding = Thickness
DensityPresetChipPadding = Thickness
DensityToolTipPadding = Thickness
BorderThicknessDefault = Thickness, uniform 1 in v2
```

Elevation:

```text
ElevationPopup
ElevationPanel
```

Compatibility:

```text
AccentPrimaryLight = AccentHover
ControlCornerRadius = RadiusInput, one pass only
ButtonCornerRadius = RadiusButton, one pass only
```

## XAML migration list

Migrate only the listed sites in the density pass. Leave row heights and WebView2 margins alone for now.

| File | Current site | New resource |
|---|---|---|
| `Theme/ControlStyles.xaml` | `DarkButton` `Padding="12,6"` | `{DynamicResource DensityButtonPadding}` |
| `Theme/ControlStyles.xaml` | `DarkButton` `BorderThickness="1"` | `{DynamicResource BorderThicknessDefault}` |
| `Theme/ControlStyles.xaml` | `AccentButton` foreground `#FF06141A` | `{DynamicResource OnAccent}` |
| `Theme/ControlStyles.xaml` | `AccentButton` hover uses `AccentPrimaryLight` | `{DynamicResource AccentHover}` with `{DynamicResource OnAccent}` |
| `Theme/ControlStyles.xaml` | `AccentButton` pressed state absent | `{DynamicResource AccentPressed}` with `{DynamicResource OnAccentPressed}` |
| `Theme/ControlStyles.xaml` | `DarkTextBox` `MinHeight="32"` | `{DynamicResource DensityControlHeight}` |
| `Theme/ControlStyles.xaml` | `DarkTextBox` `Padding="10,0"` | `{DynamicResource DensityInputPadding}` |
| `Theme/ControlStyles.xaml` | `DarkTextBox` `BorderThickness="1"` | `{DynamicResource BorderThicknessDefault}` |
| `Theme/ControlStyles.xaml` | `DarkTextBox` focus border `AccentPrimary` | `{DynamicResource AccentBorder}` |
| `Theme/ControlStyles.xaml` | `IconButton` width/height `32` | `{DynamicResource DensityIconButtonSize}` |
| `Theme/ControlStyles.xaml` | `PinToggle` width/height `32` | `{DynamicResource DensityIconButtonSize}` |
| `Theme/ControlStyles.xaml` | `ScrollBar` width/height `10` | `{DynamicResource DensityScrollbarThickness}` |
| `Theme/ControlStyles.xaml` | `ComboBoxItem` `Padding="10,6"` | `{DynamicResource DensityMenuItemPadding}` |
| `Theme/ControlStyles.xaml` | `DarkComboBox` `Height="32"` | `{DynamicResource DensityControlHeight}` |
| `Theme/ControlStyles.xaml` | `DarkComboBox` closed/toggle/dropdown `BorderThickness="1"` | `{DynamicResource BorderThicknessDefault}` |
| `Theme/ControlStyles.xaml` | `ToolTip` `Padding="8,5"` | `{DynamicResource DensityToolTipPadding}` |
| `Theme/ControlStyles.xaml` | `ToolTip` `BorderThickness="1"` | `{DynamicResource BorderThicknessDefault}` |
| `SettingsWindow.xaml` | `PresetToggle` height `30` | `{DynamicResource DensityControlHeight}` or dedicated chip height if needed |
| `SettingsWindow.xaml` | `PresetToggle` padding `10,0` | `{DynamicResource DensityPresetChipPadding}` |
| `SettingsWindow.xaml` | `SwatchToggle` width/height `34` | defer or add `DensitySwatchSize` if needed |

Do not migrate:

- Main/player title bar row heights.
- WebView2 resize-zone margins.
- WindowChrome `CornerRadius="0"`.
- Top-level window/frame border thicknesses and intentionally borderless controls such as
  `AccentButton`.
- WebView2 or YouTube content.

## Implementation phases

### Phase A — Theme identity value pass

Status: complete in the current checkout.

Scope:

- Update `ThemeCatalog` palette/radius/default behavior values to this spec.
- Update `Colors.xaml` Sharp Dark seeds.
- Update tests for exact values and identity deltas.
- Refresh `docs/Theme_Preset_Differences.md`.

No new resource keys except updated existing values.

Done when:

- The three presets are visually different even before density/elevation lands.
- Tests prove the values are not near-identical.
- `docs/Theme_Preset_Differences.md` reflects the Phase A catalog values in the same PR.

### Phase B — Accent variant pass

Status: pending.

Scope:

- Add `ThemeAccentProfile` and `ThemeAccentSet` generation.
- Add new accent resource keys and their companion `*Color` entries.
- Replace hardcoded `AccentButton` foreground with `OnAccent`.
- Migrate focus/hover/pressed states to semantic accent tokens, including `OnAccentPressed` for the pressed fill.
- Keep `AccentPrimaryLight` as alias to `AccentHover` for one migration pass.

Done when:

- Every offered accent chip can drive primary buttons, glyph states, focus rings, and subtle fills without manual per-color tweaks.
- All six offered accents pass the derived-token contrast gates above across all three theme profiles.
- A future hue wheel can store only one base accent.

### Phase C — Density and elevation pass

Status: pending.

Scope:

- Add `ThemeDensity` and `ThemeElevation` to `ThemePreset`.
- Add density/elevation resource keys.
- Migrate the listed hardcoded style values only.
- Add inner popup/panel effects for Minimal and Soft Glass.

Done when:

- Sharp feels compact.
- Minimal feels normal/calm.
- Soft Glass feels airy and overlay-like.
- Exact per-preset density values are gated, with distinctness checks on the axes that should diverge.
- The URL/search clipping test arranges a real dense-end field (or size-to-content host) so `DensityControlHeight`
  actually affects measured height.
- No clipping regression in the URL/search field.

### Phase D — Settings polish pass

Scope:

- Keep existing chips unless drift becomes painful.
- Add a simple “Reset accent to theme default” action.
- Add a preview swatch/card that shows primary, hover, pressed, subtle, and border states.
- Do not add a full wheel yet unless Phase B is complete.

Done when:

- Settings explains the model clearly: theme = rules, accent = identity, corners = override.

### Phase E — Color wheel pass

Scope:

- Add a hue wheel only after `OnAccent` and accent variants are live.
- Store only `theme.accentColor`.
- Preserve custom accent across theme switches using the rule that already exists.
- Optional: add hex entry later, but only with contrast validation.

Done when:

- Any wheel-selected hue produces readable primary buttons and focus states.
- No derived colors are persisted.

### Phase F — Media Glow pass, deferred

Scope:

- Add the fourth preset only after the base three have screenshots and QA signoff.
- It may use the deferred palette/radii/density values in this spec.

Done when:

- It has a clear role: expressive display/player mode, not “Soft Glass but more”.

## Testing approach

Logic tests:

- `ThemeCatalogTests`
  - preset IDs and display names are stable.
  - exact palette/radius/default behavior values match this spec.
  - radii are ordered: Sharp ≤ Minimal ≤ Soft Glass for every token.
  - `Minimal` uses `SmallRound`; `Soft Glass` uses `Round`; `Sharp Dark` keeps `Default`.
  - every preset palette meets text contrast gates.
  - every offered accent meets glyph contrast on every preset `SurfaceHover`.
  - every offered accent supports `OnAccent` contrast.
  - every offered accent supports `OnAccentPressed` contrast against `AccentPressed`.
  - `AccentMuted` is gated only in its pinned light-on-muted or glyph/border pairing.
  - density values are exact literals from this spec, not only sane ranges.
  - diverging density axes remain distinct while intentionally tied axes remain exact.
  - accent switch preserves custom accents.

Markup tests:

- `XamlInvariantTests`
  - all new resource keys exist in `Colors.xaml` seeds.
  - every `{DynamicResource}` key resolves from its scope.
  - no hardcoded `CornerRadius` except `WindowChrome.CornerRadius="0"`.
  - listed density sites use `DynamicResource` after Phase C, asserted by named style and Setter property so literals cannot survive silently.
  - `Colors.xaml` seeds match `Sharp Dark` catalog values.

Runtime tests:

- `WpfRuntimeTests`
  - applying each preset updates existing controls.
  - `AccentButton` foreground updates through `OnAccent`; pressed foreground updates through `OnAccentPressed`.
  - nested `AccentButton` content receives the templated foreground; `PopOutButtonIcon` /
    `PopOutButtonText` or an equivalent nested `TextBlock` fixture must render `OnAccent` and
    `OnAccentPressed`.
  - realized consumers re-resolve density, border, and elevation tokens.
  - `SettingsWindow` preset click shows selected theme/accent/corner state correctly.
  - opacity preview/rollback behavior is unchanged.
  - URL/search box remains unclipped at 150% DPI using a host where `DensityControlHeight` drives the arranged height.

Manual smoke:

- Capture evidence screenshots after a Stable deploy:
  - `theme-sharp-dark-browse.png`
  - `theme-minimal-browse.png`
  - `theme-soft-glass-browse.png`
  - `theme-soft-glass-popout-idle.png`
  - `theme-sharp-dark-steel-variant.png`

> **Note — two-layer visual verification (added 2026-06-16, from the Phase C density/elevation
> landing).** The "manual smoke" above is really two separable layers; keep them distinct so neither
> masquerades as the other:
>
> 1. **Automatable render-smoke (dev build).** Driving the repo Debug build via the `run-piplay` skill
>    confirms a theme change *renders as coded* — chrome legible, the soft-glass combo-dropdown
>    `ElevationPopup` shadow appears and is **not clipped flush**, and the URL/search field is unclipped
>    at 150% DPI. This is repeatable, but it is **change-verification, not release evidence, and never an
>    aesthetic sign-off** (record it as "renders as coded", per the QA-recording convention). Verified
>    on 2026-06-16 for Phase C: the soft-glass dropdown shadow renders unclipped with the inset margin,
>    and the density chrome is legible — on the Debug build, not the deployed copy.
> 2. **Owner-gated visual sign-off (deployed Stable).** The aesthetic acceptance — shadow intensity /
>    inset amount, whether Sharp feels appropriately tight — and the check on the actual release binary
>    require a `Publish-Stable.ps1` deploy first (the dev build is not release evidence). Only the owner
>    signs this off; the screenshots above belong under `docs/evidence/` after that deploy.
>
> **Suggested tooling:** add a `capture-dropdown` / popup-capture helper to the `run-piplay` skill
> (expand a control by `AutomationId` → screenshot the transient popup HWND, DPI-aware) so the
> elevation/dropdown render-smoke — and any future popup/menu surface — is one command across all three
> presets rather than an ad-hoc script. A working one-off proved this is feasible on 2026-06-16.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Theme/ThemeCatalog.cs` | Update target palettes/radii/defaults. Add `ThemeDensity`, `ThemeElevation`, `ThemeAccentProfile`. |
| `src/PiPlay/Theme/ThemeColors.cs` | Add mix, alpha, contrast, `PickReadableForeground`, and accent set generation. |
| `src/PiPlay/Theme/ThemeResourceApplier.cs` | Apply new accent, density, elevation, and border resources, including companion `*Color` entries. Keep compatibility aliases for one pass. |
| `src/PiPlay/Theme/Colors.xaml` | Seed Sharp Dark palette/radii plus new accent/density/elevation resource keys. |
| `src/PiPlay/Theme/ControlStyles.xaml` | Migrate accent foreground/hover/pressed/focus and listed density sites to dynamic tokens. |
| `src/PiPlay/SettingsWindow.xaml` | Keep existing chips first; later add accent reset and preview card. |
| `src/PiPlay/SettingsWindow.xaml.cs` | Keep current custom-accent preservation; add accent reset handler if UI lands. |
| `tests/PiPlay.Tests/ThemeCatalogTests.cs` | Add exact target gates, density/elevation gates, and accent-set contrast gates. |
| `tests/PiPlay.Tests/ThemeColorsTests.cs` | Add derivation, alpha, mix, `OnAccent`, `OnAccentPressed`, and hue-lane tests. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | Add density/elevation resource definedness and positive migrated-site DynamicResource assertions after migration. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Add live re-resolution checks for new resources, settings preview/apply behavior, and a density-driven URL clipping test. |
| `docs/Theme_Preset_Differences.md` | Regenerate after Phase A and again after Phase C. |
| `docs/CHANGELOG.md` | Add a user-visible theme-system entry after each shipped PR. |

## Docs & changelog impact

- `docs/Theme_Preset_Differences.md` is the current-code preset reference. Refresh it in the same
  PR as any catalog value change; do not defer that refresh to a later screenshot/evidence pass.
- `docs/README.md` points contributors to this spec and plan as the next theme-system flow.
- `docs/CHANGELOG.md` gets user-visible entries when implementation phases ship.
- Stable-deploy screenshots belong under `docs/evidence/` after the implementation is deployed and
  manually smoked.

## Non-goals

- No click-through windows.
- No transparent WPF WebView2 windows.
- No per-pixel rounded video clipping.
- No YouTube DOM/CSS theming.
- No media playback state changes.
- No per-control manual radius sliders.
- No per-state color pickers.
- No imported/exported themes.
- No theme marketplace.
- No wallpaper/accent auto-detection.
- No `Media Glow` until the first three themes are accepted.

## Unresolved decisions

None blocking.

The only product-choice fork is whether `Sharp Dark` should remain cyan by default or become steel by default. This spec recommends keeping cyan as the install/default brand identity and treating `Sharp Dark + Steel` as the muted sharp variant.

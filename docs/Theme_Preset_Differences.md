# PiPlay Theme Preset Differences

This document compares the current implementation of `Sharp Dark`, `Minimal`, and `Soft Glass` from the code, not from an older design note or a screenshot.

For the next theme-system implementation flow, use
`docs/superpowers/specs/2026-06-14-theme-v2-tight-scope-design.md` and
`docs/superpowers/plans/2026-06-14-theme-v2-tight-scope.md`. This file remains the current-code
reference and must be regenerated in the same PR as any theme catalog value changes.

Source of truth:

- `src/PiPlay/Theme/ThemeCatalog.cs`
- `src/PiPlay/Theme/ThemeResourceApplier.cs`
- `src/PiPlay/Theme/Colors.xaml`
- `src/PiPlay/Theme/ThemePreferenceResolver.cs`
- `src/PiPlay/SettingsWindow.xaml`
- `src/PiPlay/SettingsWindow.xaml.cs`

Terminology:

- `Sharp Dark` means theme id `sharp-dark`.
- `Minimal` means theme id `minimal`.
- `Soft Glass` means theme id `soft-glass`.
- "Glass" in casual discussion maps to `Soft Glass`, not to transparent WebView2 content or click-through behavior.

## Short Version

| Preset | Intent | Main difference |
|---|---|---|
| Sharp Dark | Utility-first PiPlay dark shell | Darkest cool-black palette, cyan default accent, tightest radii, pristine native corners, normal fade, fully opaque popout defaults, compact density, no inner shadow. |
| Minimal | Low-distraction daily browsing | Warm charcoal palette, steel-blue default accent, moderate radii, small native corners, long fade, fully opaque popout defaults, normal density, subtle inner shadow. |
| Soft Glass | Desktop overlay / floating popout style | Cool blue palette, violet default accent, largest radii, rounded native corners, short fade, auto-hiding strip, translucent popout defaults, airy density, soft inner shadow. |

## Token Differences vs Perceived Window Impact (2026-06-23 owner review)

The tables in this document are accurate at the token level — palette, radii, density, elevation, DWM
corner mode, fade, and opacity all differ across the three presets. The 2026-06-23 owner review
(`docs/reviews/2026-06-23-owner-appearance-popout-compact-review.md`) nonetheless reports that the
*final window* feel does not differ enough. Both can be true: the perceived silhouette/feel is
constrained by factors this token catalog does not capture —

- The **video surface is opaque** and dominates the window, so palette/border tokens only show on thin chrome.
- **Outer-window corners are DWM-owned**: three fixed OS radii only, and `soft`/`round` both resolve to `DWMWCP_ROUND` (identical) — there is no large "card" radius and no outer border or shadow following the curve, because the windows host WebView2 by HWND with `AllowsTransparency=False` (airspace; see the Inner Elevation note below).
- **Default transparency is low** (only Soft Glass ships translucent), by design.

The owner also requests a 4th **Blackout** preset and explicit border/shadow controls. These are
direction, not current code; the catalog tables below stay code-backed. Tracked in
`SPEC_GAPS_AND_OWNERSHIP.md` (2026-06-23 owner appearance / popout / compact review).

## Preset Identity

| Field | Sharp Dark | Minimal | Soft Glass | Difference |
|---|---|---|---|---|
| Theme id | `sharp-dark` | `minimal` | `soft-glass` | Persisted `theme.themeId` values are all different. |
| Display name | `Sharp Dark` | `Minimal` | `Soft Glass` | Settings button labels are all different. |
| Description | `The current utility-first PiPlay dark shell.` | `A quieter preset for daily browsing and low-distraction popouts.` | `A softer overlay-friendly preset for desktop popouts.` | Each preset is positioned for a different use case. |
| Settings toggle | `ThemeSharpDarkPreset` | `ThemeMinimalPreset` | `ThemeSoftGlassPreset` | Three explicit Settings controls, each tagged with its theme id. |
| Settings automation name | `Sharp Dark theme` | `Minimal theme` | `Soft Glass theme` | Screen-reader labels mirror the display names. |

## Default Accent

The preset default accent is separate from the surface palette. `ThemeResourceApplier` writes:

- `AccentPrimary` from the selected `theme.accentColor`.
- `AccentPrimaryLight` by blending the selected accent 30 percent toward white.

| Field | Sharp Dark | Minimal | Soft Glass | Difference |
|---|---|---|---|---|
| Default accent color | `#00D4FF` | `#5AA9E6` | `#A78BFA` | Sharp uses cyan, Minimal uses steel blue, Soft Glass uses violet. |
| Default accent family | Cyan | Steel blue | Violet | The presets start from different color identities. |
| Runtime `AccentPrimaryLight` from default accent | `#4CE1FF` | `#8CC3EE` | `#C1AEFC` | Hover/light accent differs because it is derived from each preset accent. |
| XAML seed before startup apply | `#00D4FF` / `#5BE6FF` | Not seeded in XAML | Not seeded in XAML | `Colors.xaml` seeds the Sharp Dark fallback only; runtime apply replaces it. |

Preset switching rule:

- If the current accent equals the previous preset's default, selecting another preset adopts the new preset's default accent.
- If the current accent is custom or deliberately chosen, it survives the preset switch.
- The accent picker's preset quick-picks are the same for every preset: cyan `#00D4FF`, steel blue `#5AA9E6`, steel `#4A8FAB`, violet `#A78BFA`, green `#38D996`, amber `#FFC857`.

## Behavior Defaults

| Field | Sharp Dark | Minimal | Soft Glass | Difference |
|---|---|---|---|---|
| Default fade delay preset | `normal` | `long` | `short` | All three differ. Minimal lets the strip linger; Soft Glass hides it quickly. |
| Default fade delay milliseconds | `2500` | `4000` | `1500` | Derived from the preset (`short`/`normal`/`long` = `1500`/`2500`/`4000`). |
| Default popout top-bar auto-hide | `false` | `false` | `true` | Soft Glass auto-hides the popout strip by default; Sharp and Minimal keep it pinned. |
| Default active window opacity | `1.0` | `1.0` | `0.92` | Soft Glass is translucent while active; Sharp and Minimal are opaque. |
| Default idle window opacity | `1.0` | `1.0` | `0.78` | Soft Glass fades down while idle; Sharp and Minimal stay opaque. |
| Active alpha byte | `255` / `0xFF` | `255` / `0xFF` | `235` / `0xEB` | Soft Glass engages layered-window opacity by default. |
| Idle alpha byte | `255` / `0xFF` | `255` / `0xFF` | `199` / `0xC7` | Soft Glass idle state is visibly more transparent. |
| Click-through | Off | Off | Off | No preset enables click-through; `WS_EX_TRANSPARENT` remains a non-goal. |

Behavior values are resolved as:

1. Preset default.
2. Explicit nullable override from `theme.stripAutoHide`, `theme.activeWindowOpacity`, or `theme.idleWindowOpacity`.
3. Normalized safe value.

A `null` behavior value in schema 3 means "follow the selected preset", not "fall back to legacy player values".

## Palette Values

These are the exact `ThemePalette` tokens applied to the shared resource dictionary. The accent is not part of this table.

| Token | Sharp Dark | Minimal | Soft Glass |
|---|---|---|
| `AppBackground` | `#050609` | `#14120F` | `#0B1018` |
| `SurfaceBase` | `#0B0E12` | `#1C1A16` | `#121A26` |
| `SurfaceRaised` | `#131820` | `#26231E` | `#1B2738` |
| `SurfaceHover` | `#1E2630` | `#312D27` | `#26354B` |
| `BorderSubtle` | `#2B3645` | `#3C372F` | `#44526E` |
| `BorderStrong` | `#3E4B5C` | `#50493E` | `#66799E` |
| `TextPrimary` | `#F4F7FA` | `#F4F1EC` | `#F6F8FC` |
| `TextSecondary` | `#9AA2AD` | `#B0A99E` | `#C4CEDC` |
| `Danger` / `DangerPin` | `#E45D75` | `#E8564C` | `#E45D75` |

Palette interpretation:

- Sharp Dark is the darkest, coolest, most compact-looking palette (near-black with cool slate borders).
- Minimal is a warm charcoal palette: its surfaces are warmer (more red, less blue) than Sharp Dark.
- Soft Glass is cooler and bluer, with the brightest borders and secondary text.
- Sharp Dark and Soft Glass share the same danger color `#E45D75`.
- Minimal uses a warmer red-orange danger color `#E8564C`.

## Palette RGB Deltas

Positive values mean the target preset channel is brighter than the source preset in sRGB channel units. Negative values mean it is darker.

| Token | Minimal minus Sharp | Soft Glass minus Sharp | Soft Glass minus Minimal |
|---|---:|---:|---:|
| `AppBackground` | `R+15 G+12 B+6` | `R+6 G+10 B+15` | `R-9 G-2 B+9` |
| `SurfaceBase` | `R+17 G+12 B+4` | `R+7 G+12 B+20` | `R-10 G0 B+16` |
| `SurfaceRaised` | `R+19 G+11 B-2` | `R+8 G+15 B+24` | `R-11 G+4 B+26` |
| `SurfaceHover` | `R+19 G+7 B-9` | `R+8 G+15 B+27` | `R-11 G+8 B+36` |
| `BorderSubtle` | `R+17 G+1 B-22` | `R+25 G+28 B+41` | `R+8 G+27 B+63` |
| `BorderStrong` | `R+18 G-2 B-30` | `R+40 G+46 B+66` | `R+22 G+48 B+96` |
| `TextPrimary` | `R0 G-6 B-14` | `R+2 G+1 B+2` | `R+2 G+7 B+16` |
| `TextSecondary` | `R+22 G+7 B-15` | `R+42 G+44 B+47` | `R+20 G+37 B+62` |
| `Danger` / `DangerPin` | `R+4 G-7 B-41` | `R0 G0 B0` | `R-4 G+7 B+41` |

## Corner Radii

All values are device-independent pixels. These are semantic radii, not one universal radius.

| Radius token | Sharp Dark | Minimal | Soft Glass |
|---|---:|---:|---:|
| `MainWindowFrame` | `2` | `8` | `14` |
| `PopoutFrame` | `2` | `12` | `22` |
| `TitleBar` | `2` | `8` | `14` |
| `Button` | `3` | `8` | `12` |
| `IconButton` | `3` | `8` | `12` |
| `Input` | `3` | `8` | `12` |
| `Panel` | `2` | `10` | `16` |
| `Popup` | `4` | `10` | `16` |
| `Thumbnail` | `2` | `6` | `10` |
| `Swatch` | `4` | `8` | `12` |
| `ScrollbarThumb` | `3` | `5` | `6` |
| `ToolTip` | `4` | `8` | `10` |

Radii interpretation:

- Sharp Dark has the tightest corners (intentionally tight, not accidentally unrounded).
- Minimal is visibly softer than Sharp Dark.
- Soft Glass has the largest overlay-style corners.
- The biggest Soft Glass difference is `PopoutFrame`: `22`, which is `+20` over Sharp Dark and `+10` over Minimal.
- Every radius token now differs across all three presets — there are no cross-preset ties.

## Radius Deltas

| Radius token | Minimal minus Sharp | Soft Glass minus Sharp | Soft Glass minus Minimal |
|---|---:|---:|---:|
| `MainWindowFrame` | `+6` | `+12` | `+6` |
| `PopoutFrame` | `+10` | `+20` | `+10` |
| `TitleBar` | `+6` | `+12` | `+6` |
| `Button` | `+5` | `+9` | `+4` |
| `IconButton` | `+5` | `+9` | `+4` |
| `Input` | `+5` | `+9` | `+4` |
| `Panel` | `+8` | `+14` | `+6` |
| `Popup` | `+6` | `+12` | `+6` |
| `Thumbnail` | `+4` | `+8` | `+4` |
| `Swatch` | `+4` | `+8` | `+4` |
| `ScrollbarThumb` | `+2` | `+3` | `+1` |
| `ToolTip` | `+4` | `+6` | `+2` |

## Control Density

Theme-owned control sizing in device-independent pixels (`ThemeDensity`). Heights/sizes are plain doubles; paddings and the default border are WPF `Thickness`. Density makes Sharp feel compact and Soft Glass airy; it is theme-owned, not user-overridable in this pass.

| Density token | Sharp Dark | Minimal | Soft Glass |
|---|---:|---:|---:|
| `ControlHeight` | `30` | `34` | `38` |
| `IconButtonSize` | `30` | `32` | `36` |
| `ScrollbarThickness` | `8` | `10` | `10` |
| `ButtonPadding` (h,v) | `10,5` | `12,6` | `16,9` |
| `InputPadding` (h,v) | `8,0` | `10,0` | `14,2` |
| `MenuItemPadding` (h,v) | `8,5` | `10,6` | `14,9` |
| `PresetChipPadding` (h,v) | `8,0` | `10,0` | `14,0` |
| `ToolTipPadding` (h,v) | `7,4` | `8,5` | `10,7` |
| `BorderThicknessDefault` | `1` | `1` | `1` |

Density interpretation:

- `ControlHeight` and `IconButtonSize` increase strictly Sharp < Minimal < Soft Glass (the compact-to-airy axis).
- `ScrollbarThickness` thickens off Sharp (`8`) then ties between Minimal and Soft Glass (`10`).
- `BorderThicknessDefault` is a uniform `1` across all three this pass: border weight is not a v2 differentiation axis until pixel-snapping/layout-rounding risk has its own gate.
- Migrated consumer sites resolve these via `DynamicResource` (`DarkButton`, `DarkTextBox`, `IconButton`, `PinToggle`, `ScrollBar`, `DarkComboBoxItem`, `DarkComboBox`, `ToolTip`, and the Settings `PresetToggle`). The intentionally borderless `AccentButton`/`DangerButton`, the `SwatchToggle` chips (deferred), the `PinToggle` template border, and the dialog's outer window frame keep their own values.

## Inner Elevation

Theme-owned inner drop-shadow for popups, menus, and raised internal panels (`ThemeElevation`). Inner-only — never an outer-window glow (the windows host WebView2 by HWND and stay `AllowsTransparency=False`). Sharp Dark has no inner elevation: the applier writes a `null` Effect, not a no-op `DropShadowEffect`.

| Elevation field | Sharp Dark | Minimal | Soft Glass |
|---|---:|---:|---:|
| Popup blur radius | none | `8` | `16` |
| Popup shadow depth | none | `1` | `2` |
| Popup opacity | none | `0.22` | `0.34` |
| Panel blur radius | none | `6` | `12` |
| Panel shadow depth | none | `1` | `2` |
| Panel opacity | none | `0.16` | `0.26` |

Elevation interpretation:

- Sharp Dark is flat (`Elevation = null`); Minimal is subtle; Soft Glass is the softest.
- The applier replaces `ElevationPopup` / `ElevationPanel` with frozen `DropShadowEffect`s for Minimal and Soft Glass, and a `null` Effect for Sharp Dark.
- `ElevationPopup` is consumed by the ComboBox dropdown (`DropDownBorder`) — a real Popup HWND, so the shadow is airspace-safe (FEAS-04): null/flat under Sharp Dark, the soft frozen shadow under Minimal/Soft Glass. `ElevationPanel` is applied to the dictionary but not yet consumed: there is no airspace-safe raised panel for it yet (the Settings sections are one flat StackPanel; the source placeholder and popout error bar sit over the WebView2 HWND, where a WPF shadow would not composite).

## Native DWM Window Corners

| Field | Sharp Dark | Minimal | Soft Glass | Difference |
|---|---|---|---|---|
| Preset DWM corner mode | `Default` | `SmallRound` | `Round` | All three differ. Sharp leaves the HWND corner preference untouched; Minimal requests small native rounding; Soft Glass requests full rounding. |
| Default-theme pristine guarantee | Yes | No | No | Only Sharp Dark leaves the window DWM-pristine. Minimal and Soft Glass intentionally write a native corner preference. |
| Relationship to opacity | Independent | Independent | Independent | Corner mode is explicit preset data, not inferred from opacity. |

## Corner Style Overrides

The Settings "Corners" row can override the preset's whole corner profile. This is not a per-control tweak.

| Corner style | Effective radii | Effective DWM mode | Effect on the three presets |
|---|---|---|---|
| `theme` | Selected preset radii | Selected preset DWM mode | Keeps Sharp, Minimal, or Soft Glass defaults. |
| `square` | `SquareRadii` | `Square` | Forces all radii to `0` and disables native rounding. |
| `small` | `SharpRadii` | `SmallRound` | Makes any preset use Sharp-style control radii with small native rounding. |
| `soft` | `MinimalRadii` | `Round` | Makes any preset use Minimal-style control radii with round native corners. |
| `round` | `SoftGlassRadii` | `Round` | Makes any preset use Soft Glass-style control radii with round native corners. |

Selecting a theme preset resets `CornerStyle` back to `theme`.

## Resource Keys Changed At Runtime

`ThemeResourceApplier.Apply` replaces these resource entries:

Palette brushes and companion colors:

- `AppBackground`
- `AppBackgroundColor`
- `SurfaceBase`
- `SurfaceBaseColor`
- `SurfaceRaised`
- `SurfaceRaisedColor`
- `SurfaceHover`
- `SurfaceHoverColor`
- `BorderSubtle`
- `BorderSubtleColor`
- `BorderStrong`
- `BorderStrongColor`
- `TextPrimary`
- `TextPrimaryColor`
- `TextSecondary`
- `TextSecondaryColor`
- `DangerPin`
- `DangerPinColor`

Accent brushes:

- `AccentPrimary`
- `AccentPrimaryLight`

Radius resources:

- `RadiusMainWindowFrame`
- `RadiusPopoutFrame`
- `RadiusTitleBar`
- `RadiusButton`
- `RadiusIconButton`
- `RadiusInput`
- `RadiusPanel`
- `RadiusPopup`
- `RadiusThumbnail`
- `RadiusSwatch`
- `RadiusScrollbarThumb`
- `RadiusToolTip`
- `ControlCornerRadius`
- `ButtonCornerRadius`

Density resources (doubles and `Thickness`):

- `DensityControlHeight`
- `DensityIconButtonSize`
- `DensityScrollbarThickness`
- `DensityButtonPadding`
- `DensityInputPadding`
- `DensityMenuItemPadding`
- `DensityPresetChipPadding`
- `DensityToolTipPadding`
- `BorderThicknessDefault`

Inner elevation resources (`DropShadowEffect` or `null`):

- `ElevationPopup`
- `ElevationPanel`

Runtime mechanics:

- Brushes are replaced, not mutated.
- Replaced brushes are frozen.
- DynamicResource consumers re-resolve when the dictionary entries are replaced.
- `Colors.xaml` seeds the Sharp Dark palette so design-time and the pre-apply startup instant have a valid dark look.

## Settings Click Behavior

When a preset button is clicked in Settings:

1. The previous preset is captured.
2. `ThemeId` is normalized from the clicked toggle tag.
3. The new preset is loaded from `ThemeCatalog`.
4. The accent becomes the new preset default only if the current accent still equals the previous preset default.
5. `CornerStyle` resets to `theme`.
6. `FadeIdleDelayMs` becomes the selected preset's default fade delay.
7. `StripAutoHideOverride`, `ActiveOpacityOverride`, and `IdleOpacityOverride` reset to `null`.
8. The Settings controls display the selected preset's default strip auto-hide and opacity behavior.
9. Opacity preview fires for the displayed active/idle opacity values.
10. Theme, accent, and corners apply to every open window when Settings closes.

The visible behavioral jump on preset click now spans every preset-owned axis:

- default accent,
- palette,
- radii,
- native DWM corner mode (`Default` / `SmallRound` / `Round`),
- fade delay preset (`normal` / `long` / `short`),
- popout strip auto-hide (Soft Glass on, others off),
- Soft Glass active/idle opacity,
- control density (Sharp compact, Soft Glass airy),
- inner elevation (Sharp none, Minimal subtle, Soft Glass soft; the ComboBox dropdown consumes `ElevationPopup`, `ElevationPanel` awaits a safe raised surface).

## Persistence Fields

Current schema stores:

| Setting | Meaning |
|---|---|
| `theme.themeId` | One of `sharp-dark`, `minimal`, `soft-glass`; invalid values normalize to `sharp-dark`. |
| `theme.accentColor` | Normalized `#RRGGBB`; invalid values normalize to Sharp Dark cyan or a legacy fallback. |
| `theme.fadeDelayPreset` | `short`, `normal`, or `long`; preset defaults are Sharp Dark `normal`, Minimal `long`, Soft Glass `short`. |
| `theme.cornerStyle` | `theme`, `square`, `small`, `soft`, or `round`; invalid values normalize to `theme`. |
| `theme.stripAutoHide` | Nullable behavior override; `null` means follow preset. |
| `theme.activeWindowOpacity` | Nullable behavior override; `null` means follow preset. |
| `theme.idleWindowOpacity` | Nullable behavior override; `null` means follow preset. |

Legacy mirrors are still written under `player` for older builds:

- `player.constantWindowOpacity`
- `player.idleWindowOpacity`
- `player.stripAutoHide`
- `player.fadeIdleDelayMs`

These mirrors carry effective values, while schema 3 reads preset behavior through `theme`.

## What Does Not Differ By Preset

The theme preset does not change:

- WebView2 page content.
- YouTube rendering.
- Browser data, cookies, or account state.
- Current URL.
- Saved profiles.
- Profile playback mode.
- Global compact mode.
- Existing popout mode after the popout is already open.
- Main window or popout saved placement.
- Topmost state.
- Available accent chip list.
- Available corner style override list.
- Available fade delay options.
- Slider min/max policy for opacity.
- No-click-through policy.

## Complete Difference Inventory

Every current preset-level difference falls into one of these buckets:

1. Identity: id, display name, Settings control name, automation name, description.
2. Default accent: preset default hex and derived runtime hover/light accent.
3. Palette: nine surface/text/danger tokens.
4. Radii: twelve semantic radius tokens (all distinct across the three presets).
5. Native DWM corner mode: all three differ (`Default` / `SmallRound` / `Round`).
6. Fade delay preset: all three differ (`normal` / `long` / `short`).
7. Popout strip auto-hide: Soft Glass differs from Sharp Dark and Minimal.
8. Popout opacity defaults: Soft Glass differs from Sharp Dark and Minimal.
9. Control density: heights, icon-button size, scrollbar thickness, and paddings (Sharp compact, Soft Glass airy; scrollbar ties between Minimal and Soft Glass; the default border is a uniform `1`).
10. Inner elevation: popup/panel drop-shadow (Sharp none, Minimal subtle, Soft Glass soft). The ComboBox dropdown consumes `ElevationPopup`; `ElevationPanel` awaits a safe raised surface.

Everything else is currently shared by all three presets or controlled by user overrides rather than by the preset itself.

# PiPlay Theme Preset Differences

This document compares the current implementation of `Sharp Dark`, `Minimal`, and `Soft Glass` from the code, not from an older design note or a screenshot.

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
| Sharp Dark | Utility-first PiPlay dark shell | Darkest base palette, cyan default accent, tightest radii, fully opaque popout defaults. |
| Minimal | Low-distraction daily browsing | Slightly lighter pre-theme shared palette, steel-blue default accent, moderate radii, fully opaque popout defaults. |
| Soft Glass | Desktop overlay / floating popout style | Cooler blue palette, violet default accent, largest radii, rounded native window corners, translucent popout defaults. |

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
- Available accent chips are the same for every preset: cyan `#00D4FF`, steel blue `#5AA9E6`, steel `#4A8FAB`, violet `#A78BFA`, green `#38D996`, amber `#FFC857`.

## Behavior Defaults

| Field | Sharp Dark | Minimal | Soft Glass | Difference |
|---|---|---|---|---|
| Default fade delay preset | `normal` | `normal` | `normal` | No difference. |
| Default fade delay milliseconds | `2500` | `2500` | `2500` | No difference. |
| Default popout top-bar auto-hide | `false` | `false` | `false` | No difference. |
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
| `AppBackground` | `#07090B` | `#0B0D0E` | `#090B0F` |
| `SurfaceBase` | `#0D1014` | `#111316` | `#10141B` |
| `SurfaceRaised` | `#141920` | `#1A1E22` | `#171C26` |
| `SurfaceHover` | `#202833` | `#252B31` | `#242B38` |
| `BorderSubtle` | `#2A3441` | `#30363D` | `#384255` |
| `BorderStrong` | `#3A4655` | `#414A55` | `#526179` |
| `TextPrimary` | `#F2F5F7` | `#F3F5F7` | `#F7F8FA` |
| `TextSecondary` | `#A8B0BA` | `#A7ADB4` | `#C0C6CF` |
| `Danger` / `DangerPin` | `#E45D75` | `#FF4B55` | `#E45D75` |

Palette interpretation:

- Sharp Dark is the darkest and most compact-looking palette.
- Minimal preserves the earlier pre-theme shared palette tones.
- Soft Glass is cooler and bluer, with brighter borders and secondary text.
- Sharp Dark and Soft Glass share the same danger color.
- Minimal uses a hotter red danger color.

## Palette RGB Deltas

Positive values mean the target preset channel is brighter than the source preset in sRGB channel units. Negative values mean it is darker.

| Token | Minimal minus Sharp | Soft Glass minus Sharp | Soft Glass minus Minimal |
|---|---:|---:|---:|
| `AppBackground` | `R+4 G+4 B+3` | `R+2 G+2 B+4` | `R-2 G-2 B+1` |
| `SurfaceBase` | `R+4 G+3 B+2` | `R+3 G+4 B+7` | `R-1 G+1 B+5` |
| `SurfaceRaised` | `R+6 G+5 B+2` | `R+3 G+3 B+6` | `R-3 G-2 B+4` |
| `SurfaceHover` | `R+5 G+3 B-2` | `R+4 G+3 B+5` | `R-1 G0 B+7` |
| `BorderSubtle` | `R+6 G+2 B-4` | `R+14 G+14 B+20` | `R+8 G+12 B+24` |
| `BorderStrong` | `R+7 G+4 B0` | `R+24 G+27 B+36` | `R+17 G+23 B+36` |
| `TextPrimary` | `R+1 G0 B0` | `R+5 G+3 B+3` | `R+4 G+3 B+3` |
| `TextSecondary` | `R-1 G-3 B-6` | `R+24 G+22 B+21` | `R+25 G+25 B+27` |
| `Danger` / `DangerPin` | `R+27 G-18 B-32` | `R0 G0 B0` | `R-27 G+18 B+32` |

## Corner Radii

All values are device-independent pixels. These are semantic radii, not one universal radius.

| Radius token | Sharp Dark | Minimal | Soft Glass |
|---|---:|---:|---:|
| `MainWindowFrame` | `4` | `6` | `10` |
| `PopoutFrame` | `4` | `8` | `16` |
| `TitleBar` | `4` | `6` | `10` |
| `Button` | `5` | `6` | `10` |
| `IconButton` | `5` | `6` | `10` |
| `Input` | `5` | `6` | `10` |
| `Panel` | `4` | `8` | `14` |
| `Popup` | `6` | `8` | `14` |
| `Thumbnail` | `3` | `4` | `8` |
| `Swatch` | `6` | `8` | `10` |
| `ScrollbarThumb` | `4` | `5` | `5` |
| `ToolTip` | `6` | `6` | `8` |

Radii interpretation:

- Sharp Dark has the tightest corners.
- Minimal is slightly softer than Sharp Dark.
- Soft Glass has the largest overlay-style corners.
- The biggest Soft Glass difference is `PopoutFrame`: `16`, which is `+12` over Sharp Dark and `+8` over Minimal.
- `ScrollbarThumb` is identical for Minimal and Soft Glass at `5`.
- `ToolTip` is identical for Sharp Dark and Minimal at `6`.

## Radius Deltas

| Radius token | Minimal minus Sharp | Soft Glass minus Sharp | Soft Glass minus Minimal |
|---|---:|---:|---:|
| `MainWindowFrame` | `+2` | `+6` | `+4` |
| `PopoutFrame` | `+4` | `+12` | `+8` |
| `TitleBar` | `+2` | `+6` | `+4` |
| `Button` | `+1` | `+5` | `+4` |
| `IconButton` | `+1` | `+5` | `+4` |
| `Input` | `+1` | `+5` | `+4` |
| `Panel` | `+4` | `+10` | `+6` |
| `Popup` | `+2` | `+8` | `+6` |
| `Thumbnail` | `+1` | `+5` | `+4` |
| `Swatch` | `+2` | `+4` | `+2` |
| `ScrollbarThumb` | `+1` | `+1` | `0` |
| `ToolTip` | `0` | `+2` | `+2` |

## Native DWM Window Corners

| Field | Sharp Dark | Minimal | Soft Glass | Difference |
|---|---|---|---|---|
| Preset DWM corner mode | `Default` | `Default` | `Round` | Soft Glass requests rounded native outer corners; Sharp and Minimal leave the HWND corner preference untouched. |
| Default-theme pristine guarantee | Yes | Yes | No | Sharp Dark and Minimal do not force a DWM corner write from the preset. Soft Glass intentionally does. |
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

Because all three presets currently use `DefaultFadeDelayPreset = normal` and `DefaultStripAutoHide = false`, the visible behavioral jump on preset click is mainly:

- default accent,
- palette,
- radii,
- native DWM corner mode,
- Soft Glass active/idle opacity.

## Persistence Fields

Current schema stores:

| Setting | Meaning |
|---|---|
| `theme.themeId` | One of `sharp-dark`, `minimal`, `soft-glass`; invalid values normalize to `sharp-dark`. |
| `theme.accentColor` | Normalized `#RRGGBB`; invalid values normalize to Sharp Dark cyan or a legacy fallback. |
| `theme.fadeDelayPreset` | `short`, `normal`, or `long`; all three presets default to `normal`. |
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
4. Radii: twelve semantic radius tokens.
5. Native DWM corner mode: Soft Glass differs from Sharp Dark and Minimal.
6. Popout opacity defaults: Soft Glass differs from Sharp Dark and Minimal.

Everything else is currently shared by all three presets or controlled by user overrides rather than by the preset itself.

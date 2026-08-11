# Theme preset reference

Current code values. Sources: `src/PiPlay/Theme/ThemeCatalog.cs`, `ThemeColors.cs`, `ThemeResourceApplier.cs`, `Colors.xaml`, `ThemePreferenceResolver.cs`, and `src/PiPlay/Services/WindowOpacityPolicy.cs`.

## Presets

| Value | Sharp Dark | Minimal | Soft Glass |
|---|---|---|---|
| ID / card cue | `sharp-dark` / `Crisp · 100%` | `minimal` / `Quiet · 94%` | `soft-glass` / `Glass · 82%` |
| Default accent | `#2BAED0` | `#3F84C0` | `#9E84F0` |
| Fade preset / ms | `normal` / `2500` | `long` / `4000` | `short` / `1500` |
| Strip auto-hide | `true` | `true` | `true` |
| Active / idle opacity | `1.00 / 1.00` | `0.94 / 0.86` | `0.82 / 0.72` |
| DWM corners | `Default` | `SmallRound` | `Round` |
| Density / elevation | compact / none | normal / subtle | airy / soft |

Theme changes never select Standard/Focused presentation, Compact mode, placement, Pin, profile, browser data, or click-through.

## Palette

| Token | Sharp Dark | Minimal | Soft Glass |
|---|---|---|---|
| `AppBackground` | `#050609` | `#14120F` | `#0B1018` |
| `SurfaceBase` | `#0B0E12` | `#1C1A16` | `#121A26` |
| `SurfaceRaised` | `#131820` | `#26231E` | `#1B2738` |
| `SurfaceHover` | `#1E2630` | `#312D27` | `#26354B` |
| `BorderSubtle` | `#181F29` | `#2E2A23` | `#2A3A52` |
| `BorderStrong` | `#262F3D` | `#3C362D` | `#3A4D6A` |
| `TextPrimary` | `#F4F7FA` | `#F4F1EC` | `#F6F8FC` |
| `TextSecondary` | `#9AA2AD` | `#B0A99E` | `#C4CEDC` |
| `Danger` / `DangerPin` | `#E45D75` | `#E8564C` | `#E45D75` |

## Radii and density (DIP)

| Radius | Sharp | Minimal | Soft Glass |
|---|---:|---:|---:|
| Main / Popout / Title | `2 / 2 / 2` | `8 / 12 / 8` | `14 / 22 / 14` |
| Button / Icon / Input | `3 / 3 / 3` | `8 / 8 / 8` | `12 / 12 / 12` |
| Panel / Popup / Thumbnail | `2 / 4 / 2` | `10 / 10 / 6` | `16 / 16 / 10` |
| Swatch / Scrollbar / Tooltip | `4 / 3 / 4` | `8 / 5 / 8` | `12 / 6 / 10` |

| Density | Sharp | Minimal | Soft Glass |
|---|---|---|---|
| Control / icon / scrollbar | `30 / 30 / 8` | `34 / 32 / 10` | `38 / 36 / 10` |
| Button padding H,V | `10,5` | `12,6` | `16,9` |
| Input padding H,V | `8,0` | `10,0` | `14,2` |
| Menu / preset padding H,V | `8,5 / 8,0` | `10,6 / 10,0` | `14,9 / 14,0` |
| Tooltip padding H,V | `7,4` | `8,5` | `10,7` |
| Default border | `1` | `1` | `1` |

Corner override `theme` uses the preset; `square` uses all-zero radii + `Square`; `small` uses Sharp radii + `SmallRound`; `round` uses Soft Glass radii + `Round`. Legacy `soft` normalizes to `round`. Only a floating effective-`Round` Popout applies the 22 DIP native region; maximize/snap clears it.

## Elevation

| Value | Sharp | Minimal | Soft Glass |
|---|---:|---:|---:|
| Popup blur/depth/opacity | none | `8 / 1 / 0.22` | `16 / 2 / 0.34` |
| Panel blur/depth/opacity | none | `6 / 1 / 0.16` | `12 / 2 / 0.26` |

`ElevationPopup` is consumed by the ComboBox popup. `ElevationPanel` is published but has no safe HWND-airspace consumer.

## Accent derivation

Accent chips: cyan `#2BAED0`, steel blue `#3F84C0`, steel `#4A8FAB`, violet `#9E84F0`, green `#2DB57F`, amber `#D69A2E`. Any valid `#RRGGBB` may be stored exactly.

| Profile tuple `(hoverWhite, pressedBlack, mutedSurface, borderWhite, subtleAlpha, glowAlpha)` |
|---|
| Sharp `(0.18, 0.16, 0.58, 0.10, 0x22, 0x33)` |
| Minimal `(0.22, 0.14, 0.50, 0.12, 0x26, 0x40)` |
| Soft Glass `(0.30, 0.12, 0.40, 0.16, 0x33, 0x66)` |

- `AccentPrimary` is lifted only for presentation to 3:1 against `SurfaceHover`; stored global/profile RGB remains exact. Pressed-state delta is at least 1.10:1; `OnAccent` and `OnAccentPressed` independently choose dark/white text.
- `theme.accentIntensity` is 0–100, default `50`. At 0, shared toolbar/title/background reach and the Popout edge are off; primary controls remain accented, and profile-row identity remains at preset `SubtleAlpha`. Toolbar glyphs reach full accent at 50; title wash scales from contrast `1.00` to `1.90` through 100.
- `AccentLetterbox = Mix(#000000, Primary, 0.06 × reach)`; `AppBackgroundWash = Mix(AppBackground, Primary, 0.04 × reach)`; `PopoutAccentEdge` is `AccentBorder` with alpha `255 × reach`; `ProfileRowWashAlpha` is the preset `SubtleAlpha`.
- Active profile accent wins; a null profile color inherits global. A custom global survives preset switches; an untouched prior-preset default advances to the next preset default. Profile-owned RGB never changes on preset switch.

Open constraint: `AccentMuted`, `AccentSubtle`, and `AccentGlow` remain unwired. Do not attach text without a consumer-specific composited contrast test. Known unsafe candidates include steel glyphs on `SurfaceBase` (`2.05–2.76:1`) and `TextPrimary` over bright muted Minimal/Soft Glass fills (amber `3.18:1`, cyan `3.55:1`, green `3.62:1`). Either raise `MutedSurfaceMix` until the exact pairing reaches 4.5:1 or constrain/gate one consumer.

## Persistence and apply rules

Schema `4` stores `theme.themeId`, `theme.accentColor`, `theme.accentIntensity`, `theme.fadeDelayPreset`, `theme.cornerStyle`, nullable `theme.stripAutoHide`, `theme.activeWindowOpacity`, `theme.idleWindowOpacity`, root `activeProfileName`, `profiles[].accentColor`, `player.focusedPresentation`, and `profiles[].presentation`. Null behavior overrides mean “use preset.” Legacy `player.constantWindowOpacity`, `player.idleWindowOpacity`, `player.stripAutoHide`, and `player.fadeIdleDelayMs` remain migration mirrors.

Runtime replaces only changed frozen brush/color pairs. Core keys include palette `*Color`/brush pairs; `AccentPrimary`, `AccentHover`, `AccentPressed`, `AccentBorder`, `AccentShellTint`, `AccentChromeGlyph`, `OnAccent`, `OnAccentPressed`, `AccentLetterbox`, `AppBackgroundWash`, `PopoutAccentEdge`; `Radius*`; `Density*`; `ElevationPopup`; `ElevationPanel`; and `ProfileRowWashAlpha`.

Preset clicks reset corners to `theme`, fade delay and behavior overrides to preset defaults, and preview all open surfaces. Done commits; any non-Done dismissal restores the exact prior state.

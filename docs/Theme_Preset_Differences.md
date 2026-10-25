# Current theme values

Source: `ThemeCatalog.cs`, `ThemeColors.cs`, `ThemeResourceApplier.cs`, `ContrastBrushConverter.cs`. Tests: `ThemeCatalogTests`, `ThemeColorsTests`, `ContrastBrushConverterTests`, `Ui/XamlInvariantTests`.

## Presets

| | Sharp Dark | Minimal | Soft Glass |
|---|---:|---:|---:|
| ID / card cue | `sharp-dark` / `Crisp · 100%` | `minimal` / `Quiet · 94%` | `soft-glass` / `Glass · 82%` |
| Default accent | `#2BAED0` | `#3F84C0` | `#9E84F0` |
| Fade / ms | normal / `2500` | long / `4000` | short / `1500` |
| Strip auto-hide | true | true | true |
| Active / idle opacity | `1.00 / 1.00` | `0.94 / 0.86` | `0.82 / 0.72` |
| DWM corners | Default | SmallRound | Round |
| Density / elevation | compact / none | normal / subtle | airy / soft |

Theme changes do not select playback presentation/mode, placement, Pin, profile, browser data, or input transparency.

## Palette

| Token | Sharp Dark | Minimal | Soft Glass |
|---|---|---|---|
| `AppBackground` | `#050609` | `#120E0B` | `#070C14` |
| `SurfaceBase` | `#0B0E12` | `#1B1510` | `#0E1726` |
| `SurfaceRaised` | `#12161D` | `#231C15` | `#162338` |
| `SurfaceHover` | `#1B212C` | `#2E241C` | `#243652` |
| `BorderSubtle` / `BorderStrong` | `#1F2733` / `#2E394A` | `#231C15` / `#3E3226` | `#18263C` / `#263B59` |
| `TextPrimary` / `TextSecondary` | `#F4F7FA` / `#9AA2AD` | `#F5EFE6` / `#B5A895` | `#F2F6FC` / `#94A6C0` |
| `Danger` | `#E45D75` | `#E8564C` | `#E45D75` |

## Geometry and density

Radii are `(main, popout, title; button, icon, input; panel, popup, thumbnail; swatch, scrollbar, tooltip)` in DIP:

| | Sharp | Minimal | Soft Glass |
|---|---|---|---|
| Radii | `2,2,2; 3,3,3; 2,4,2; 4,3,4` | `8,12,8; 8,8,8; 10,10,6; 8,5,8` | `14,22,14; 12,12,12; 16,16,10; 12,6,10` |
| Control / icon / scrollbar | `30 / 30 / 8` | `34 / 32 / 10` | `38 / 36 / 10` |
| Button padding H,V | `10,5` | `12,6` | `16,9` |
| Input padding H,V | `8,0` | `10,0` | `14,2` |
| Menu / preset padding H,V | `8,5 / 8,0` | `10,6 / 10,0` | `14,9 / 14,0` |
| Tooltip padding H,V | `7,4` | `8,5` | `10,7` |
| Default border | `1` | `1` | `1` |

Inner elevation is Sharp `none`; Minimal popup/panel `8 / 1 / 0.22` and `6 / 1 / 0.16`; Soft Glass `16 / 2 / 0.34` and `12 / 2 / 0.26` (blur / depth / opacity). `ElevationPopup` is consumed by ComboBox, ContextMenu, and ToolTip popups (`Theme/ControlStyles.xaml`); `ElevationPanel` is consumed by non-airspace panels (`SettingsWindow.xaml`, `MainWindow.xaml`).

Corner override `theme` follows the preset; `square` is zero radii plus `Square`; `small` uses Sharp radii plus `SmallRound`; `round` uses Soft Glass radii plus `Round`; legacy `soft` normalizes to `round`. Only a floating effective-`Round` Popout receives the `22 DIP` native region; maximize/snap clears it.

## Accent and persistence

Offered accents are cyan `#2BAED0`, steel-blue `#3F84C0`, steel `#4A8FAB`, violet `#9E84F0`, green `#2DB57F`, and amber `#D69A2E`. Any valid `#RRGGBB` may be stored exactly; presentation tokens are contrast-corrected.

`theme.accentIntensity` is integer `0–100`, default `50`. At `0`, shared background/title reach and the Popout edge are off; primary controls remain accented, checked preset toggles use `AccentSubtle` (retaining preset `SubtleAlpha`), and profile-row identity rails use `ContrastBrushConverter` (`>= 3:1` against `SurfaceRaised`). Toolbar glyphs reach full accent at `50`; the wash continues to `100`. Derivation ceilings are `0.06` for `AccentLetterbox` toward black and `0.04` for `AppBackgroundWash` toward the preset background. `PopoutAccentEdge` is a 1 px inset edge with alpha proportional to intensity and is displayed only when `RoundedWindowRegionPolicy.ShouldShowPopoutEdgeBorder` is true (floating non-round `Standard` Popouts with visible chrome and `AccentIntensity > 0`); Settings uses `{DynamicResource AppBackground}` (`SettingsWindow.xaml`). `ThemeResourceApplier` publishes `AccentSubtle` (`AccentSubtleColor`) and does not publish `Muted` or `Glow` as resource keys.

Schema `4` persists theme ID, accent, intensity, fade/corner/opacity overrides, active profile, profile accents, and presentation. Null overrides mean “use the preset.” Preset preview applies to open surfaces; **Done** commits, and any other dismissal restores the prior state. (`AppSettings`, `ThemeSettingsWriter`, `ThemePreferenceResolver`, `SettingsWindow`.)

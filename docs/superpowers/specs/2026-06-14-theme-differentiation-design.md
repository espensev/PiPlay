# Theme differentiation (distinct identities) — design

> Status: Superseded draft.
>
> Implement the next theme-system pass from
> [`2026-06-14-theme-v2-tight-scope-design.md`](2026-06-14-theme-v2-tight-scope-design.md)
> and track work in
> [`../plans/2026-06-14-theme-v2-tight-scope.md`](../plans/2026-06-14-theme-v2-tight-scope.md).
> This file is retained only as historical context for the initial differentiation direction.

## Goals

The three theme presets (`Sharp Dark`, `Minimal`, `Soft Glass`) are technically distinct but read as
near-identical: their surface palettes differ by only ~2–4 sRGB units, main-window radii by 2–6 px,
and every behavior default except Soft Glass opacity is shared (see `docs/Theme_Preset_Differences.md`).
This pass makes each preset a **distinct visual identity** so a user can tell them apart at a glance.

Direction (owner-approved 2026-06-14): push **every** axis — palette/temperature, corner shape,
translucency, and (new) density/border-weight/elevation — so each preset feels like a different app.

Delivered in two phases:

- **Phase 1 — diverge what the theme engine already drives**: palette, radii, native (DWM) corners,
  per-preset default accent, window opacity. No new engine code paths; pure value + small wiring
  change. Mergeable and QA-able on its own.
- **Phase 2 — extend the engine with new per-preset axes**: density (spacing/control height),
  border weight, and inner elevation/shadow. Strictly additive (new tokens with safe defaults equal
  to today's hardcoded values), so a partially-migrated `ControlStyles.xaml` cannot regress.

What is explicitly **not** changing: the accent-as-separate-user-value model, the
replace-not-mutate `DynamicResource` mechanism, the corner-style override system, the "Sharp Dark is
the DWM-pristine default" guarantee, the no-click-through invariant (ADR-0006), WebView2/YouTube
content, profiles, placement, and the settings schema shape (Phase 2 adds no persisted fields —
density/border/elevation are preset-derived, not user-overridable in this pass).

## Requirements served

- REQ-UI-01 / REQ-UI-02 (dark, intentional, legible chrome) — the presets stay WCAG-gated.
- Spec §“theme presets” intent (distinct, purposeful looks) — currently unmet in practice.
- Motivating note: owner feedback 2026-06-14 ("themes look near-identical; want different rounding
  and such"). Builds on the shipped `2026-06-11-theme-corners-and-palettes-design.md` foundation
  (PR #19) — this pass changes values and adds axes; it does not re-architect the engine.

## Acceptance criteria

- Side by side, the three presets are obviously different in base color temperature, corner shape,
  and (Phase 2) density — not just accent.
- `ThemeCatalogTests.Preset_palettes_meet_contrast_minimums` stays green for all three presets
  (numbers verified below, see Settled decision 2).
- `Colors.xaml` Sharp Dark seeds match the new Sharp Dark palette; its XamlInvariant contrast
  theories stay green.
- Switching presets in Settings still applies live on close to every open window (no restart), and
  the custom-accent-preservation rule is unchanged.
- Phase 2: every new density/border/elevation token resolves at runtime in all open windows; with a
  token unmigrated at a consumption site, the rendered result is identical to today (safe defaults).
- Full deterministic gate stays green (currently 561) plus new tests; manual smoke shows the three
  identities live on the deployed Stable copy.

## Settled decisions

1. **Identity rides on hue/temperature + shape + translucency + density, NOT on lightening dark
   surfaces.** Lightening `SurfaceBase`/`SurfaceHover` is both contrast-risky (it backs accent
   glyphs and the dimmest text) and reads weakly on a dark UI. Surfaces stay dark; the eye is told
   "different theme" by warm-vs-cool palette, corner radius, opacity, and Phase-2 spacing.

2. **Phase 1 palettes are contrast-pinned now (real WCAG output), not deferred.** Computed with the
   exact `tests/.../Wcag.cs` formula against the exact gates (`ThemeCatalogTests.cs:171-189`). The
   binding pairs — dimmest accent `steel #4A8FAB` on `SurfaceHover` (≥3.0) and `TextSecondary` on
   `SurfaceBase` (≥4.5) — clear on all three:

   | Preset | TextPri/AppBg | TextPri/Base | TextSec/Base | white/Danger | steel/Hover (tightest accent) |
   |---|---:|---:|---:|---:|---:|
   | Sharp Dark | 18.84 | 17.99 | 7.50 | 3.43 | 4.22 |
   | Minimal | 16.60 | 15.42 | 7.46 | 3.58 | 3.78 |
   | Soft Glass | 17.93 | 16.44 | 10.99 | 3.43 | 3.43 |

   (All six accents clear ≥3.0 on every preset's hover; steel is the floor shown. Dark button text
   ≥4.5 on every accent is unaffected — accents are unchanged.)

3. **Sharp Dark keeps DWM `Default` (pristine).** The settled "default-theme window stays
   byte-identical" guarantee holds. Truly-square outer corners remain reachable via the existing
   `Square` corner-style override — not the preset default.

4. **Minimal's DWM corner changes `Default` → `SmallRound` (intentional deviation).** Minimal today
   uses `Default`; giving it a soft-but-not-round outer corner separates it from Sharp (pristine)
   and Soft Glass (full `Round`). Recorded here so it is not a silent change.

5. **Phase 2 is strictly additive.** New tokens carry safe defaults equal to today's hardcoded
   values; consumption sites are migrated to `DynamicResource` one at a time; nothing breaks if a
   site is left unmigrated mid-PR. Phase 1 ships and is QA'd before Phase 2 lands.

6. **Phase 2 elevation/shadow applies to INNER surfaces only** (popups, menus, raised panels), not
   an outer-window glow — the popout keeps `AllowsTransparency=False` for WebView2 airspace
   (ADR-0006), so a window cannot cast a shadow outside its own bounds. Soft Glass gets inner
   depth; Sharp gets none; Minimal gets a subtle lift.

## Design — Phase 1 (values the engine already drives)

All changes live in `src/PiPlay/Theme/ThemeCatalog.cs` (the three `ThemePreset` records + the
`SharpRadii`/`MinimalRadii`/`SoftGlassRadii` constants) plus the `Colors.xaml` Sharp Dark seeds.
Note: the radii constants are reused by the corner-style overrides (`small`→Sharp, `soft`→Minimal,
`round`→Soft Glass), so changing them updates both preset defaults and overrides consistently —
intended.

### Palettes (contrast-verified, Settled decision 2)

| Token | Sharp Dark (neutral/cool) | Minimal (warm charcoal) | Soft Glass (cool blue) |
|---|---|---|---|
| AppBackground | `#050609` | `#14120F` | `#0B1018` |
| SurfaceBase | `#0B0E12` | `#1C1A16` | `#121A26` |
| SurfaceRaised | `#131820` | `#26231E` | `#1B2738` |
| SurfaceHover | `#1E2630` | `#312D27` | `#26354B` |
| BorderSubtle | `#2B3645` | `#3C372F` | `#44526E` |
| BorderStrong | `#3E4B5C` | `#50493E` | `#66799E` |
| TextPrimary | `#F4F7FA` | `#F4F1EC` | `#F6F8FC` |
| TextSecondary | `#9AA2AD` | `#B0A99E` | `#C4CEDC` |
| Danger | `#E45D75` | `#E8564C` | `#E45D75` |

Identity: Sharp = coolest, darkest, near-black; Minimal = warm (R↑ B↓) charcoal with a warm
orange-red danger; Soft Glass = cool blue with the brightest borders/secondary text (it carries the
"glass" read).

### Radii (DIPs)

| Token | Sharp (near-square) | Minimal (soft) | Soft Glass (pill) |
|---|---:|---:|---:|
| MainWindowFrame | 2 | 8 | 14 |
| PopoutFrame | 2 | 12 | 22 |
| TitleBar | 2 | 8 | 14 |
| Button | 3 | 8 | 12 |
| IconButton | 3 | 8 | 12 |
| Input | 3 | 8 | 12 |
| Panel | 2 | 10 | 16 |
| Popup | 4 | 10 | 16 |
| Thumbnail | 2 | 6 | 10 |
| Swatch | 4 | 8 | 12 |
| ScrollbarThumb | 3 | 5 | 6 |
| ToolTip | 4 | 8 | 10 |

### DWM corners, opacity, default accent

| Field | Sharp Dark | Minimal | Soft Glass |
|---|---|---|---|
| DWM corner mode | `Default` (unchanged) | `SmallRound` (was `Default`, dec. 4) | `Round` (unchanged) |
| Active / idle opacity | 1.0 / 1.0 | 1.0 / 1.0 | 0.92 / 0.78 (unchanged) |
| Default accent | cyan `#00D4FF` | steel-blue `#5AA9E6` | violet `#A78BFA` |

## Design — Phase 2 (new per-preset axes, additive)

### New catalog data

- `ThemeDensity` record (new): `ControlHeight` (double), `ButtonPadding`, `InputPadding`,
  `MenuItemPadding` (Thickness), `BorderThickness` (double). Carried on `ThemePreset` as
  `Density`. Inner-elevation carried as a nullable `ThemeElevation?` (`Blur`, `Depth`, `Opacity`)
  on the preset; `null` = no shadow.
- Proposed per-preset values (tuned in impl against the layout invariants below):

  | Field | Sharp (dense) | Minimal (standard) | Soft Glass (airy) | Safe default (fallback) |
  |---|---|---|---|---|
  | ControlHeight | 30 | 34 | 36 | 32 |
  | ButtonPadding | 10,5 | 14,8 | 16,9 | 12,6 |
  | InputPadding | 8,0 | 12,2 | 14,3 | 10,0 |
  | MenuItemPadding | 8,5 | 12,8 | 14,9 | 10,6 |
  | BorderThickness | 1 | 1 | 1 | 1 |
  | Elevation | none | subtle (blur 8, depth 1, 0.25) | soft (blur 16, depth 2, 0.35) | none |

  Border weight stays 1 across presets in v1 (changing stroke weight per theme interacts with the
  pixel-snapping/UseLayoutRounding invariants — deferred; the token exists so it is themeable later).
  `ControlHeight` deltas are deliberately small because the URL-not-clipped-at-150%-DPI invariant
  (`WpfRuntimeTests`) gates the minimum; impl tunes against that test.

### New resource keys (written by `ThemeResourceApplier.Apply`, additive)

`DensityControlHeight` (double), `DensityButtonPadding` / `DensityInputPadding` /
`DensityMenuItemPadding` (Thickness), `BorderThicknessDefault` (Thickness), `ElevationPopup` /
`ElevationPanel` (Effect or `null`). Frozen where applicable; replaced-not-mutated like the existing
keys. `ThemeResourceApplier.PaletteBrushKeys` is unchanged; a parallel test-shared key list guards
the new set against drift (mirrors the existing pattern).

### `ControlStyles.xaml` consumption sites (migrate hardcoded → `DynamicResource`)

Enumerated so the plan is concrete (line numbers as of this writing):

| Site | Today | Becomes |
|---|---|---|
| `Button` Padding (33) | `12,6` | `{DynamicResource DensityButtonPadding}` |
| `Button` BorderThickness (30) | `1` | `{DynamicResource BorderThicknessDefault}` |
| `DarkTextBox` Padding (221) | `10,0` | `{DynamicResource DensityInputPadding}` |
| `DarkTextBox` BorderThickness (217) | `1` | `{DynamicResource BorderThicknessDefault}` |
| `DarkTextBox` MinHeight (220) | `32` | `{DynamicResource DensityControlHeight}` |
| `DarkComboBox` / `ComboBox` Height (175, 354) | `32` | `{DynamicResource DensityControlHeight}` |
| ComboBox / icon / dropdown BorderThickness (190, 351, 366, 399) | `1` | `{DynamicResource BorderThicknessDefault}` |
| IconButton Height (116) | `32` | `{DynamicResource DensityControlHeight}` |
| MenuItem/Popup item Padding (322) | `10,6` | `{DynamicResource DensityMenuItemPadding}` |
| ToolTip Padding/BorderThickness (449, 437) | `8,5` / `1` | menu-item padding token / `BorderThicknessDefault` |
| Popup/menu container border (366, 399) | (border) | add `Effect="{DynamicResource ElevationPopup}"` |
| Raised panels (Settings sections, source placeholder) | (none) | add `Effect="{DynamicResource ElevationPanel}"` |

`AccentButton`/`IconButton` fill BorderThickness `0` (64, 90) stay `0` — they are intentionally
borderless. Scrollbar thumb height (284–285) stays fixed (not density-driven).

## Testing approach

- **Logic/unit (`ThemeCatalogTests`)**: the existing `Preset_palettes_meet_contrast_minimums`
  theory already covers all three new palettes (numbers in dec. 2). Add: per-preset radii are
  distinct/ordered; Phase 2 — every preset exposes a `Density` (and the catalog `ThemeDensity`
  values normalize/clamp sanely).
- **Markup (`XamlInvariantTests`)**: update the `Colors.xaml`-seed contrast theories to the new
  Sharp Dark seeds (must stay ≥ gates); add: the new density/border/elevation resource keys exist
  and every consuming style references them via `DynamicResource` (no orphan hardcoded values at the
  migrated sites); `{DynamicResource}` keys all resolve.
- **WPF runtime (`WpfRuntimeTests`)**: applying each preset replaces the new keys and open windows
  re-resolve; the URL-not-clipped-at-150%-DPI render test stays green at each preset's
  `ControlHeight` (gates the dense end).
- **Manual smoke (deployed Stable, per `CLAUDE.md`)**: after a future release publish, cycle the
  three presets and capture `docs/evidence/…-theme-<preset>.png` showing the three identities; not a
  repo-build screenshot.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Theme/ThemeCatalog.cs` | P1: new palette + radii values, Minimal `DwmCorners`→`SmallRound`. P2: `ThemeDensity`/`ThemeElevation` records, `Density`/`Elevation` on `ThemePreset`, per-preset values. |
| `src/PiPlay/Theme/Colors.xaml` | P1: Sharp Dark seed palette → new values. P2: seed the new density/border/elevation keys with safe defaults. |
| `src/PiPlay/Theme/ThemeResourceApplier.cs` | P2: write the new density/border/elevation resource keys (additive); extend the shared key list. |
| `src/PiPlay/Theme/ControlStyles.xaml` | P2: migrate the enumerated hardcoded padding/height/border sites to `DynamicResource`; add `Effect` on popups/menus/raised panels. |
| `tests/PiPlay.Tests/ThemeCatalogTests.cs` | P1 contrast already covers it; add radii-distinct + P2 density assertions. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | P1 update Sharp seed contrast theories; P2 new-key existence + DynamicResource-usage invariants. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | P2 live-apply of new keys + clipping invariant at each density. |
| `docs/Theme_Preset_Differences.md` | Refresh tables to the new values once shipped. |

## Docs & changelog impact

- `docs/CHANGELOG.md`: user-visible entry under the theme work — "Theme presets are now visually
  distinct (palette/temperature, corner shape, opacity; density & elevation in a follow-up)."
- `docs/Theme_Preset_Differences.md`: regenerate the difference tables from the new code values.
- No ADR change (the engine architecture is unchanged; ADR-0006 no-click-through still holds).

## Non-goals / out of scope

- No new persisted settings fields (density/border/elevation are preset-derived, not user-tunable
  this pass).
- No per-theme typography/font scaling (layout/clipping risk — future).
- No per-theme border *weight* change in v1 (token added, value stays 1 — pixel-snapping risk).
- No outer-window drop shadow/glow (AllowsTransparency=False / ADR-0006).
- No change to the accent chip set, corner-style override set, or fade-delay options.

## Unresolved decisions

- None blocking. Phase-2 exact density numbers are tuned during implementation against the
  URL-clipping / layout-rounding invariants (the plan pins them via TDD); the values above are the
  starting proposal.

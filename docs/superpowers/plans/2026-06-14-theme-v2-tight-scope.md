# PiPlay Theme System V2 — implementation plan

**Spec:** `docs/superpowers/specs/2026-06-14-theme-v2-tight-scope-design.md`
**Goal:** make theme presets visibly distinct while keeping the engine small, testable, WebView2-safe, and color-wheel-ready.
**Result:** Phase A is complete in the current checkout: target identity values, exact catalog
gates, behavior-default differentiation gates, and `docs/Theme_Preset_Differences.md` are in place.
Phases B-E remain pending. The original planning import was checked against the local tree on
2026-06-14; focused theme/markup validation passed with 97 tests at that time.

## Task order

Each task should leave the tree green and committable. Do not merge the color wheel before the accent variant pass.

## Tasks

- [x] **Task 1 — Lock the tighter theme identity values.**
  - Update `ThemeCatalog.cs` with the target palette, radius, DWM, default fade/top-bar/opacity values from the spec.
  - Update `Colors.xaml` Sharp Dark seed values to match the new Sharp Dark catalog values.
  - Keep `Sharp Dark` default accent cyan; keep `Sharp Dark + Steel` as an accent variant, not a new theme.
  - Regenerate `docs/Theme_Preset_Differences.md` in this same PR so the current-code reference does not lag the catalog.
  - Verification:
    - `ThemeCatalogTests` exact spec-literal value gates, independent of catalog-derived expected values.
    - `XamlInvariantTests.Colors_xaml_seeds_match_the_sharp_dark_preset`.
    - existing contrast tests stay green.
    - `git diff --check -- docs/Theme_Preset_Differences.md`.
  - Commit: `feat(theme): tighten preset identities`

- [x] **Task 2 — Add theme identity gates.**
  - Add tests that fail if presets collapse back into near-identical values.
  - Minimum gates:
    - `Sharp.Dark.Radii.PopoutFrame < Minimal.Radii.PopoutFrame < SoftGlass.Radii.PopoutFrame`.
    - Soft Glass popout radius is at least 16 DIP above Sharp.
    - Minimal DWM mode is `SmallRound`; Soft Glass is `Round`; Sharp Dark is `Default`.
    - Soft Glass has translucent active/idle defaults; Sharp and Minimal remain opaque.
    - Each preset has a distinct default accent.
    - Fade-delay defaults are exact and distinct: Sharp `normal`, Minimal `long`, Soft Glass `short`.
    - Strip auto-hide defaults are exact: Soft Glass `true`, Sharp and Minimal `false`.
  - Verification: `dotnet test PiPlay.sln --configuration Debug --filter ThemeCatalogTests`.
  - Commit: `test(theme): gate preset differentiation`

- [ ] **Task 3 — Add accent variant generation.**
  - Add `ThemeAccentProfile` and a generated accent set in `ThemeColors.cs`.
  - Add `AccentHover`, `AccentPressed`, `AccentMuted`, `AccentSubtle`, `AccentBorder`,
    `AccentGlow`, `OnAccent`, and `OnAccentPressed` resource keys.
  - Add companion `*Color` entries for every generated accent/foreground token; keep them in step
    with the brush entries in `ThemeResourceApplier`.
  - Keep `AccentPrimaryLight` as an alias to `AccentHover` for one migration pass.
  - Add fail-closed `PickReadableForeground` with WCAG contrast logic. Do not return a subthreshold
    fallback if neither candidate foreground reaches 4.5:1.
  - Pin derived-token pairings:
    - `OnAccent` against `AccentPrimary` and `AccentHover`.
    - `OnAccentPressed` against `AccentPressed`.
    - `AccentBorder` against dark surfaces at the UI-component threshold.
    - `AccentMuted` only in its declared light-on-muted or glyph/border pairing; do not use it as
      a dark-text fill unless that exact pair is gated.
  - Verification:
    - `ThemeColorsTests` for mix/alpha/foreground selection and fail-closed behavior.
    - `ThemeCatalogTests` for all offered accents against all preset palettes and every pinned
      derived-token pairing across all three theme profiles.
  - Commit: `feat(theme): derive accent state tokens`

- [ ] **Task 4 — Migrate first accent consumers.**
  - In `ControlStyles.xaml`:
    - `AccentButton.Foreground` → `{DynamicResource OnAccent}`.
    - `AccentButton` hover → `{DynamicResource AccentHover}`.
    - Add pressed state → `{DynamicResource AccentPressed}` with `{DynamicResource OnAccentPressed}`.
    - `DarkTextBox` focus border → `{DynamicResource AccentBorder}`.
    - checked/active chip borders may use `AccentBorder` or `AccentPrimary`, based on visual review.
  - Leave existing accent chips hand-written for now.
  - Verification:
    - `XamlInvariantTests` resource definedness.
    - `WpfRuntimeTests` realized `AccentButton` re-resolves foreground/background/pressed resources
      after theme/accent apply.
  - Commit: `refactor(theme): use semantic accent resources`

- [ ] **Task 5 — Add density/elevation model and resources.**
  - Add `ThemeDensity` and `ThemeElevation` records to `ThemeCatalog.cs`.
  - Add per-preset values from the spec.
  - Include `BorderThicknessDefault` on `ThemeDensity` as a uniform WPF `Thickness`; keep it `1`
    for Sharp Dark, Minimal, and Soft Glass in this pass.
  - Add seed resources to `Colors.xaml`:
    - `DensityControlHeight`
    - `DensityIconButtonSize`
    - `DensityScrollbarThickness`
    - `DensityButtonPadding`
    - `DensityInputPadding`
    - `DensityMenuItemPadding`
    - `DensityPresetChipPadding`
    - `DensityToolTipPadding`
    - `BorderThicknessDefault` (`Thickness`, not `double`)
    - `ElevationPopup`
    - `ElevationPanel`
  - Apply these from `ThemeResourceApplier` by replacing dictionary entries. `BorderThicknessDefault`
    should resolve as `Thickness` at every consumer.
  - Verification:
    - `ThemeCatalogTests` validates exact per-preset density/elevation literals from the spec, plus
      distinctness checks on diverging axes. Do not rely on "sane range" checks alone.
    - `XamlInvariantTests` validates all resource keys exist and `BorderThicknessDefault` is a
      `Thickness` resource.
    - runtime apply test verifies keys are replaced, including null/no-effect for Sharp and non-null
      popup/panel effects for Minimal and Soft Glass.
  - Commit: `feat(theme): add density and elevation tokens`

- [ ] **Task 6 — Migrate density consumers.**
  - Migrate only the scoped sites:
    - `DarkButton` padding/border.
    - `DarkTextBox` min height/padding/border.
    - `IconButton` and `PinToggle` size.
    - `ScrollBar` thickness.
    - `ComboBoxItem` padding.
    - `DarkComboBox` height plus existing one-pixel closed/toggle/dropdown borders.
    - `ToolTip` padding/border.
    - `SettingsWindow` preset chip height/padding.
  - Do not change title bar row heights, WebView2 margins, top-level window/frame border
    thicknesses, or intentionally borderless controls.
  - Verification:
    - `XamlInvariantTests` positively asserts each named migrated style/Setter property references
      the expected `Density*` or `BorderThicknessDefault` `DynamicResource` key.
    - `WpfRuntimeTests` proves realized controls re-resolve at least one density token and
      `BorderThicknessDefault`.
    - Before relying on the URL/search clipping gate, update it so the arranged field height is
      driven by `DensityControlHeight` (for example size-to-content, or an explicit 30-DIP dense
      field). Then keep the 150% DPI clipping test green.
  - Commit: `refactor(theme): migrate control density to resources`

- [ ] **Task 7 — Add inner elevation consumers.**
  - Apply `ElevationPopup` to combo/popup borders.
  - Apply `ElevationPanel` only to raised internal panels where it does not affect WebView2 airspace.
  - No outer window glow.
  - Verification:
    - WPF runtime test confirms Sharp uses null/no effect and Soft Glass has a non-null popup effect.
    - manual smoke verifies no weird clipping or resize artifact.
  - Commit: `feat(theme): add inner surface elevation`

- [ ] **Task 8 — Settings polish before wheel.**
  - Add “Reset accent to theme default”.
  - Add a small appearance preview card showing:
    - primary button
    - hover/pressed samples
    - subtle selected surface
    - border/focus line
  - Keep chips hand-written unless code generation is low-risk.
  - Verification:
    - Settings runtime tests for reset behavior.
    - catalog drift tests still pass.
  - Commit: `feat(settings): preview theme accent variants`

- [ ] **Task 9 — Hue wheel, not full color editor.**
  - Add a hue wheel only after Tasks 3–8 are green.
  - Store only `theme.accentColor`.
  - Preserve custom accent across theme changes using the existing rule.
  - Do not add free-form hex until contrast validation and error copy are ready.
  - Verification:
    - wheel-selected hues update derived resources.
    - `OnAccent` and `OnAccentPressed` stay readable across a dense hue sweep, or the wheel emitter
      is constrained to a pre-validated accessible lane.
    - no derived values appear in `settings.json`.
  - Commit: `feat(settings): add accent hue wheel`

- [ ] **Task 10 — Capture evidence and final docs wrap.**
  - Verify `docs/Theme_Preset_Differences.md` still matches the final shipped code values after the later token migrations; patch it if Phase C changed the effective preset comparison.
  - Update `docs/CHANGELOG.md`.
  - Add Stable-deploy evidence screenshots:
    - `theme-sharp-dark-browse.png`
    - `theme-minimal-browse.png`
    - `theme-soft-glass-browse.png`
    - `theme-soft-glass-popout-idle.png`
    - `theme-sharp-dark-steel-variant.png`
  - Verification:
    - full deterministic gate green.
    - manual smoke evidence stored.
  - Commit: `docs(theme): refresh preset differences and evidence`

## Suggested PR slices

### PR 1 — Theme identities

Includes Tasks 1–2 only. Complete in the current checkout.

Why: low risk, mostly value/test/doc changes. This answers the immediate “themes are too similar” problem without touching layout or the color system. It also keeps `docs/Theme_Preset_Differences.md` synchronized with the first shipped catalog changes.

### PR 2 — Accent variants

Includes Tasks 3–4.

Why: unlocks color wheel safely and removes the hardcoded primary-button foreground problem.
Do not start until the derived-token contrast rules from the spec are represented as failing tests.

### PR 3 — Density and elevation

Includes Tasks 5–7.

Why: makes themes feel genuinely different beyond color and corners, but touches layout, so it deserves its own QA pass.

### PR 4 — Settings polish and wheel

Includes Tasks 8–9.

Why: the wheel should be UI polish on top of a safe token system, not the foundation.

### PR 5 — Docs/evidence

Includes Task 10, or can be folded into each PR if preferred.

Why: final screenshots and release evidence should not carry current-code documentation debt from earlier PRs.

## Self-review checklist

Before opening each PR:

- [ ] No WebView2 clipping or `AllowsTransparency=True` changes.
- [ ] No click-through window behavior.
- [ ] No playback/window-state logic changes.
- [ ] No separate theme ID created for an accent variant.
- [ ] No color wheel before `OnAccent`, `OnAccentPressed`, and derived-token contrast gates exist.
- [ ] `settings.json` stores only theme id, base accent, corner style, and nullable behavior overrides.
- [ ] All new resources are applied by replacement, not mutation, and companion `*Color` entries stay in step.
- [ ] `BorderThicknessDefault` is a uniform `Thickness` resource and remains constant at `1` until
      border weight has a dedicated risk gate.
- [ ] `Sharp Dark` remains the safest/default shell.
- [ ] `Soft Glass` is visibly overlay-like.
- [ ] `docs/Theme_Preset_Differences.md` matches code after the final PR.

## Risk notes

- **Highest risk:** density migration, because it can clip the URL/search box and profile combo content.
- **Medium risk:** derived accent pairings and arbitrary color selection, because color contrast can silently regress.
- **Low risk:** palette/radii value tightening, because the engine already applies those resources.
- **Do not combine:** density migration and color wheel. They have different failure modes and should be reviewed separately.

# PiPlay Theme System V2 — implementation plan

**Spec:** `docs/superpowers/specs/2026-06-14-theme-v2-tight-scope-design.md`
**Goal:** make theme presets visibly distinct while keeping the engine small, testable, WebView2-safe, and color-wheel-ready.
**Result:** pending implementation. Planning import was checked against the local tree on 2026-06-14;
focused theme/markup validation passed with 97 tests.

## Task order

Each task should leave the tree green and committable. Do not merge the color wheel before the accent variant pass.

## Tasks

- [ ] **Task 1 — Lock the tighter theme identity values.**
  - Update `ThemeCatalog.cs` with the target palette, radius, DWM, default fade/top-bar/opacity values from the spec.
  - Update `Colors.xaml` Sharp Dark seed values to match the new Sharp Dark catalog values.
  - Keep `Sharp Dark` default accent cyan; keep `Sharp Dark + Steel` as an accent variant, not a new theme.
  - Regenerate `docs/Theme_Preset_Differences.md` in this same PR so the current-code reference does not lag the catalog.
  - Verification:
    - `ThemeCatalogTests` exact value gates.
    - `XamlInvariantTests.Colors_xaml_seeds_match_the_sharp_dark_preset`.
    - existing contrast tests stay green.
    - `git diff --check -- docs/Theme_Preset_Differences.md`.
  - Commit: `feat(theme): tighten preset identities`

- [ ] **Task 2 — Add theme identity gates.**
  - Add tests that fail if presets collapse back into near-identical values.
  - Minimum gates:
    - `Sharp.Dark.Radii.PopoutFrame < Minimal.Radii.PopoutFrame < SoftGlass.Radii.PopoutFrame`.
    - Soft Glass popout radius is at least 16 DIP above Sharp.
    - Minimal DWM mode is `SmallRound`; Soft Glass is `Round`; Sharp Dark is `Default`.
    - Soft Glass has translucent active/idle defaults; Sharp and Minimal remain opaque.
    - Each preset has a distinct default accent.
  - Verification: `dotnet test PiPlay.sln --configuration Debug --filter ThemeCatalogTests`.
  - Commit: `test(theme): gate preset differentiation`

- [ ] **Task 3 — Add accent variant generation.**
  - Add `ThemeAccentProfile` and a generated accent set in `ThemeColors.cs`.
  - Add `AccentHover`, `AccentPressed`, `AccentMuted`, `AccentSubtle`, `AccentBorder`, `AccentGlow`, and `OnAccent` resource keys.
  - Keep `AccentPrimaryLight` as an alias to `AccentHover` for one migration pass.
  - Add `PickReadableForeground` with WCAG contrast logic.
  - Verification:
    - `ThemeColorsTests` for mix/alpha/foreground selection.
    - `ThemeCatalogTests` for all offered accents against all preset palettes.
  - Commit: `feat(theme): derive accent state tokens`

- [ ] **Task 4 — Migrate first accent consumers.**
  - In `ControlStyles.xaml`:
    - `AccentButton.Foreground` → `{DynamicResource OnAccent}`.
    - `AccentButton` hover → `{DynamicResource AccentHover}`.
    - Add pressed state → `{DynamicResource AccentPressed}`.
    - `DarkTextBox` focus border → `{DynamicResource AccentBorder}`.
    - checked/active chip borders may use `AccentBorder` or `AccentPrimary`, based on visual review.
  - Leave existing accent chips hand-written for now.
  - Verification:
    - `XamlInvariantTests` resource definedness.
    - `WpfRuntimeTests` realized `AccentButton` re-resolves foreground/background after theme/accent apply.
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
    - `ThemeCatalogTests` validates sane density ranges.
    - `XamlInvariantTests` validates all resource keys exist and `BorderThicknessDefault` is a
      `Thickness` resource.
    - runtime apply test verifies keys are replaced.
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
    - `XamlInvariantTests` bans hardcoded values at migrated sites.
    - URL/search clipping test at 150% DPI remains green.
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
    - `OnAccent` stays readable.
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

Includes Tasks 1–2 only.

Why: low risk, mostly value/test/doc changes. This answers the immediate “themes are too similar” problem without touching layout or the color system. It also keeps `docs/Theme_Preset_Differences.md` synchronized with the first shipped catalog changes.

### PR 2 — Accent variants

Includes Tasks 3–4.

Why: unlocks color wheel safely and removes the hardcoded primary-button foreground problem.

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
- [ ] No color wheel before `OnAccent` exists.
- [ ] `settings.json` stores only theme id, base accent, corner style, and nullable behavior overrides.
- [ ] All new resources are applied by replacement, not mutation.
- [ ] `BorderThicknessDefault` is a uniform `Thickness` resource and remains constant at `1` until
      border weight has a dedicated risk gate.
- [ ] `Sharp Dark` remains the safest/default shell.
- [ ] `Soft Glass` is visibly overlay-like.
- [ ] `docs/Theme_Preset_Differences.md` matches code after the final PR.

## Risk notes

- **Highest risk:** density migration, because it can clip the URL/search box and profile combo content.
- **Medium risk:** `OnAccent` and arbitrary color selection, because color contrast can silently regress.
- **Low risk:** palette/radii value tightening, because the engine already applies those resources.
- **Do not combine:** density migration and color wheel. They have different failure modes and should be reviewed separately.

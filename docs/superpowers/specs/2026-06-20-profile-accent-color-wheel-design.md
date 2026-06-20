# Design — Per-profile accent via a unified color-wheel picker

**Date:** 2026-06-20
**Status:** Draft v2 (post adversarial review; brainstorming output; no implementation yet)
**Review:** hardened against `wkd8ww9m8` (5-dimension adversarial review vs. the real codebase). All
blockers/majors folded in; the two product forks resolved by the owner (D6, D7 below).

## 1. Summary

Add a reusable **full color-wheel picker** (HSV disc + brightness slider + R/G/B + hex fields +
curated preset quick-picks) and use it in two places:

1. **Settings → Appearance** — it *replaces* the six `AccentChip*` swatches. By default it edits the
   **global app accent** (fulfils the deferred Theme-V2 **Task 9 "hue wheel"** and closes **TG-4**).
   When a profile with a color override is active, it edits **that profile's color** instead (D7).
2. **Profile editor** — each profile gains an optional **accent override**. Selecting a profile
   re-themes the whole app's accent to that profile's color; profiles with no color follow the global
   accent. The active profile is **remembered across restart** (D6).

The picker previews the accent **live** while the user interacts (mirroring the opacity-slider live
preview), commits on **Done**, and reverts on dismiss-without-apply. Because the accent pipeline is
**fail-closed on WCAG** (`PickReadableForeground` *throws* when neither dark nor white text reaches
4.5:1 — `src/PiPlay/Theme/ThemeColors.cs:86`), **both the commit and the live-preview paths only ever
handle readable colors**: the picker blocks an unreadable selection (Done disabled) and offers
"Use nearest readable", and the preview short-circuits unreadable in-flight colors (holding the last
readable value) so a mid-drag color can never reach the throwing pipeline.

## 2. Decisions (locked with owner)

| # | Decision | Choice |
|---|----------|--------|
| D1 | What a profile's color does | **Re-theme the app accent** when that profile is active (global default + per-profile override). |
| D2 | Picker richness | **Full editor**: HSV hue/sat disc + brightness slider + R/G/B + hex + preset quick-picks. |
| D3 | Relationship to the global accent picker | **Replace** the six swatches with the wheel (fulfils TG-4 / Task 9). |
| D4 | Unreadable-color handling (commit) | **Block + offer fix**: live "won't be readable" indicator; Done disabled; "Use nearest readable" snaps to the closest safe color. |
| D5 | Live preview | **Yes** — preview the accent live during interaction; commit on Done; revert on dismiss-without-apply (mirrors `OpacityPreviewChanged`). The preview path is itself readability-gated (never previews an unreadable color). |
| D6 | Active-profile persistence | **Persist the active profile** (`AppSettings.ActiveProfileName`); restore it (and its accent) on startup. |
| D7 | Settings picker target when a profile overrides | **Context-sensitive**: while an active profile has a color override, the Settings picker edits *that profile's* color; the **global** accent is editable only when no profile is overriding. |

## 3. Background / current state (verified against source)

- **Accent model:** a single normalized `#RRGGBB` in `ThemeSettings.AccentColor`
  (`src/PiPlay/Models/AppSettings.cs:101`). Curated palette of six in `ThemeCatalog.AccentOptions`
  (`ThemeCatalog.cs:252-261`); default `#00D4FF`. No per-profile accent exists.
- **Hex validation is private:** `ThemeColors.ParseColor(hex)` → `ThemeCatalog.NormalizeAccentColor`,
  which **never throws** — invalid input silently falls back to `#00D4FF` (`ThemeColors.cs:16-23`,
  `ThemeCatalog.cs:316-321`). The real validator `NormalizeHex6` is **private** (`ThemeCatalog.cs:376`).
  → we must expose a public validator (see §5.1) so "invalid hex" can be detected, not silently cyan'd.
- **Profile model:** `Name`, `Url`, `Mode?`, `Topmost?`, `FadeEnabled?`, `Bounds?`
  (`src/PiPlay/Models/Profile.cs:1-26`) — nullable overrides over global. **No color field.**
- **Persistence:** `AppSettings.Profiles` (`AppSettings.cs:33`), System.Text.Json camelCase,
  `CurrentSchemaVersion = 3` (`AppSettings.cs:19`), normalized in `SettingsService.Sanitize()`
  (`SettingsService.cs:161-202`; per-profile `Mode` normalization at 200-201 is the pattern to copy).
  **No persisted "active profile":** selection is only `ProfilesCombo.SelectedItem`, and
  `LoadProfilesIntoCombo` resets `SelectedIndex = -1` on every reload (`MainWindow.xaml.cs:373`).
- **Derivation:** `ThemeColors.DeriveAccentSet(string? baseAccent, ThemePreset preset)` — note it takes
  the **hex string** and parses internally (`ThemeColors.cs:108`); returns a `DerivedAccentSet` with
  `Primary/Hover/Pressed/Muted/Border/Subtle/Glow/OnAccent/OnAccentPressed`. `OnAccent`/`OnAccentPressed`
  are re-picked for contrast on their respective fills (CON-1).
- **Fail-closed gate:** `PickReadableForeground` throws if neither `#06141A` nor white reaches 4.5:1
  on the fill (`ThemeColors.cs:86-92`). Unhandled throws pop the error MessageBox via
  `App.OnDispatcherUnhandledException` (`App.xaml.cs:89-97`).
- **Application pipeline:** public `ThemeResourceApplier.Apply(resources, ThemeSettings?, PlayerSettings)`
  (`ThemeResourceApplier.cs:35`) re-applies the *whole* theme; the accent-only step `ApplyAccent` is
  **private** (`:53`). → we add a public **`ApplyAccentOnly`** (see §5.3) so live preview replaces only
  the ~14 accent brushes per tick, not palette/radii/density/elevation.
- **Existing accent value:** `MainWindow.EffectiveAccentColor` (`MainWindow.xaml.cs:582`) means the
  **global** accent (`ThemePreferenceResolver.AccentColor(theme, player)`, no profile param) and is
  consumed at 5 sites: pin brush (`:286`), player appearance (`:614`), test seam (`:623`), startup
  popout (`:747`), and the Settings picker seed (`:484`). → must NOT be silently redefined (see §5.4).
- **Imperative accent surfaces:** the pin checked-glyph + `PinnedHint` use
  `ThemeColors.Brush(EffectiveAccentColor)` imperatively (`MainWindow.xaml.cs:286-288`), and the open
  popout via `_player.ApplyAppearance(EffectiveAccentColor, …)` (`:614`) — these are **not**
  DynamicResource consumers, so live preview must drive them explicitly (see §5.3).
- **Color math available:** `ParseColor`, `Mix`, `Lighten`, `WithAlpha`, `Brush`, `ContrastRatio`
  (`ThemeColors.cs`). **Missing (greenfield):** RGB↔HSV, hue-wheel geometry, saturation handling.
- **No existing color-picker UI** — fully greenfield.
- **DPI:** `app.manifest` declares PerMonitorV2 (`app.manifest:13-21`); this machine renders 150%.
- **Prior plan:** Theme-V2 Task 9 "hue wheel" (app-wide only) was deferred; **TG-4**
  (`docs/reviews/2026-06-14-theme-v2-spec-eval.md:106-107,165`) requires a wheel to enforce a hue-sweep
  readability invariant **or** a pre-validated lane. This spec satisfies TG-4 via
  `AccentReadabilityPolicy` (§5.1) + a sweep test (§8).

## 4. The contrast contract (what "readable accent" means)

A candidate accent **hex string** `C` is **readable** iff, for **every** preset
`P ∈ {sharp-dark, minimal, soft-glass}`, the derived set `D = DeriveAccentSet(C, P)` satisfies **all**
(this unions the gates in `ThemeCatalogTests.Derived_accent_tokens_meet_contrast_minimums:353-378` and
the glyph-on-hover gate in `XamlInvariantTests:691-706`):

1. `ContrastRatio(D.OnAccent, D.Primary) ≥ 4.5`.
2. `ContrastRatio(D.OnAccent, D.Hover) ≥ 4.5`.
3. `ContrastRatio(D.OnAccentPressed, D.Pressed) ≥ 4.5` (CON-1; the tightest pair today is steel/soft-glass at 4.52).
4. `ContrastRatio(D.Border, P.Palette.SurfaceBase) ≥ 3.0` **and** `≥ 3.0` on `P.Palette.SurfaceRaised`.
5. `ContrastRatio(D.Primary, P.Palette.SurfaceHover) ≥ 3.0` (accent-as-glyph on the hover surface).

`DeriveAccentSet` may itself throw (via `PickReadableForeground`) when no foreground reaches 4.5 — that
throw is caught and treated as **unreadable** (gate 1/3 failure), never propagated.

All three presets are required because a custom accent **persists across theme switches**
(`ThemeCatalog.AccentForThemeSwitch`).

**`NearestReadable(C)`** returns the perceptually-closest readable `#RRGGBB`:
- Parse via the new public validator; **malformed → return `DefaultAccentColor`**.
- Work in HSV. The dominant failure for free colors is **insufficient luminance** (gates 3 & 5), so
  the search **raises Value** in small steps; if still failing at `V = 1`, it **reduces Saturation**;
  the first (V, S) that passes §4 for all presets wins.
- If no (V, S) on the hue passes, snap to the **nearest curated preset by hue angle** (guaranteed-
  readable anchors) — so the function is **total** and **deterministic**, with bounded iteration.

## 5. Components & architecture

### 5.1 Phase 0 — public hex validator + `ColorMath` + `AccentReadabilityPolicy` (pure)
- **Public hex validator:** expose `ThemeCatalog.IsValidHex(string?) → bool` (or
  `TryParseAccentColor(string?, out string normalized)`) extracted from the private `NormalizeHex6`.
  Single detector for malformed input, used by the policy, `Sanitize`, and `ProfileService`.
- `ColorMath` (new, `src/PiPlay/Theme/ColorMath.cs`): `RgbToHsv(Color) → (H,S,V)` (H 0–360, S/V 0–1;
  H = 0 when S = 0), `HsvToRgb(h,s,v) → Color` (h wrapped, s/v clamped, alpha 255). Documented round-trip.
- `AccentReadabilityPolicy` (new, `src/PiPlay/Theme/AccentReadabilityPolicy.cs`):
  - `Evaluate(hex) → AccentReadability(bool IsReadable, AccentGate FailingGate)` — **first** checks
    `IsValidHex` (invalid ⇒ unreadable), then §4 across all presets. Passes the **hex string** to
    `DeriveAccentSet` (never a `Color`).
  - `NearestReadable(hex) → string` (§4 algorithm; total).
  - Reuses `DeriveAccentSet`, `ContrastRatio`, `ThemeCatalog.Presets/AccentOptions`, `ColorMath`.
  - **Single source of truth** for the picker's live state, `Sanitize`, and `ProfileService.ValidateAccent`.

### 5.2 Phase 1 — `AccentColorPicker` UserControl (reusable)
- New `src/PiPlay/Controls/AccentColorPicker.xaml(.cs)`.
- Visuals: a hue/saturation **disc** (angle = hue, radius = saturation), a **Value** slider, **R/G/B**
  fields, a **hex** field, a row of the six curated **presets** (from `ThemeCatalog.AccentOptions`), a
  live preview swatch, the readability indicator, and a "Use nearest readable" button.
- **Authoritative state is `(H, S, V)` doubles in the control** (not RGB). Disc/slider drags update
  H/S/V → derive RGB/hex/`SelectedColor` *from* that state. RGB/hex **text edits** recompute H/S/V from
  the typed RGB. A `_changeOrigin` guard skips the thumb/slider recompute for disc/slider-originated
  changes — preventing HSV-round-trip jitter and the hue-collapse-at-S=0 defect (recomputing HSV from
  8-bit RGB each tick is lossy).
- **Readability gating:** on every change, set `IsSelectedReadable = Evaluate(value).IsReadable`,
  toggle the warning + "Use nearest readable", and **only raise `PreviewColorChanged(value)` when the
  value is readable** (otherwise hold the last readable preview). "Use nearest readable" sets
  `SelectedColor = NearestReadable(SelectedColor)`.
- **Rendering / DPI:** build the HSV disc once into a frozen `WriteableBitmap` at **physical pixels**
  (`logicalSize * VisualTreeHelper.GetDpi(this).DpiScaleX`, bitmap DPI `96*scale`); cache keyed by
  `(size, dpiScale)`; regenerate on `OnDpiChanged` (PerMonitorV2). Map pointer positions through the
  same scale. No per-frame allocation; a thumb overlays at (hue, saturation).
- Theming/a11y: app theme tokens (DynamicResource); every interactive element gets a tooltip +
  `AutomationProperties.Name`.
- API:
  - `SelectedColor` (`string`, two-way DependencyProperty, normalized `#RRGGBB`).
  - `event Action<string>? PreviewColorChanged` (fires during interaction; readable values only).
  - `IsSelectedReadable` (read-only) — hosts bind it to gate Done.
  - `void UseNearestReadable()`.
  - Named parts: `HueSatDisc`, `ValueSlider`, `RInput`, `GInput`, `BInput`, `HexInput`, `PresetRow`,
    `PreviewSwatch`, `ReadabilityWarning`, `UseNearestReadableButton`.

### 5.3 Accent value model + live-apply primitives (MainWindow / applier)
- **Distinct members (do NOT overload the existing global one):**
  - `MainWindow.EffectiveAccentColor` keeps its current meaning: the **global** accent
    (`ThemePreferenceResolver.AccentColor(theme, player)`).
  - New `MainWindow.ResolvedAccentColor` = `activeProfile?.AccentColor ?? EffectiveAccentColor` — the
    **visible** accent. Re-point the *visible* consumers to it: pin brush (`:286`), player appearance
    (`:614`), startup popout (`:747`). The test seam (`:623`) updates in step.
  - The **Settings seed** (`:484`) uses `ResolvedAccentColor` (per D7 the picker's seed IS the edit
    target: profile color when overriding, else global) — documented so it isn't mistaken for the
    global-overwrite defect the review flagged.
- **Public accent-only applier:** add
  `ThemeResourceApplier.ApplyAccentOnly(ResourceDictionary resources, string accentHex, ThemePreset preset)`
  (extracted from the private `ApplyAccent`) so live preview replaces only the accent brushes/`*Color`
  tokens, not the whole theme.
- **`MainWindow.LivePreviewAccent(string hex)`** (used by both hosts) must drive **all** accent
  surfaces from `hex`:
  1. `ApplyAccentOnly(Application.Current.Resources, hex, currentPreset)` → DynamicResource consumers
     re-resolve in open windows.
  2. the imperative pin brush (`ToggleAccent.SetCheckedBrush(PinToggle, Brush(hex))` + `PinnedHint`).
  3. the open popout: `_player?.ApplyAppearance(hex, …)`.
  Refactor `ApplySourceAppearance`/`ApplyOpenPlayerAppearance` to take an explicit accent argument so
  **preview and commit share one path** against a single accent value. `hex` is always readable
  (preview is gated upstream), so the pipeline cannot throw.
- **Commit target (D7):** `MainWindow.CommitAccent(string hex)` → if the active profile has a non-null
  color override, persist to `profile.AccentColor`; else persist to global `theme.AccentColor`. The
  Settings dialog also surfaces *which* it is editing (a note: "Editing the app accent" vs "Editing
  accent for profile 'X'").

### 5.4 Phase 2 — Global accent uses the picker (closes TG-4 / Task 9)
- `SettingsWindow.xaml`: replace the `AccentChip*` `StackPanel` with `<controls:AccentColorPicker
  x:Name="AccentPicker" .../>` + a context note `TextBlock`.
- `SettingsWindow.xaml.cs`: seed `AccentPicker.SelectedColor` from the ctor accent (the resolved edit
  target); add `event Action<string>? AccentPreviewChanged` raised from the picker's
  `PreviewColorChanged`; on change set `AccentColor`/`AppearanceChanged`; **gate Done**:
  `DoneButton.IsEnabled = AccentPicker.IsSelectedReadable`. Update `ThemeHintText` copy (accent
  previews live; it no longer mentions "chips").
- `MainWindow`: `dialog.AccentPreviewChanged += LivePreviewAccent;`. On `ShowDialog() != true`
  (`:500-505`) **revert** every accent surface to `ResolvedAccentColor` (mirrors opacity revert
  `:503`). On `true` + `AppearanceChanged`, `CommitAccent(dialog.AccentColor)`.

### 5.5 Phase 3 — Per-profile accent + active-profile persistence
- `Profile.AccentColor?` (`string?`, null = follow global) — `Profile.cs`.
- `AppSettings.ActiveProfileName?` (`string?`) + bump `CurrentSchemaVersion` to **4** — `AppSettings.cs`.
- `SettingsService.Sanitize()`: per profile, normalize the color (invalid hex → null; valid-but-
  unreadable → `NearestReadable`); drop `ActiveProfileName` if it names no existing profile.
- `ProfileService.ValidateAccent(string? hex)` (null OK = follow global; else `IsValidHex` + readable).
- `Prompt.EditProfile` (`Prompt.cs:166`): add the picker; return tuple gains `AccentColor`. While the
  dialog is open it live-previews via a `MainWindow`-supplied callback; on cancel `MainWindow` reverts
  to `ResolvedAccentColor`.
  - **`MainWindow.EditProfileButton_Click` (`:427-435`) must thread `edited.Value.AccentColor` into the
    reconstructed `Profile`** (it currently rebuilds copying only Name/Url/Mode — the compiler flags the
    tuple arity but NOT the dropped assignment).
- `ProfilesCombo` (`MainWindow.xaml:91`): replace `DisplayMemberPath="Name"` with an `ItemTemplate`
  (color **swatch** bound to `AccentColor`, neutral "follows global" dot when null, + Name).
- **Activation + persistence:** `ProfilesCombo_SelectionChanged` (`:382-388`) sets the active profile,
  records `ActiveProfileName`, applies the **resolved** accent (commit-apply, not just preview), and
  reverts to global when a colorless/none profile is selected. On startup, restore `ActiveProfileName`
  → select it (without forcing navigation if undesired) → apply its accent. The accent-apply must be
  isolable from `NavigateInternal` for testing (a seam that does not trigger the live browser).

## 6. Data flow

```
Drag wheel ─► picker updates (H,S,V) ─► derive hex ─► if readable: PreviewColorChanged(hex)
   (Settings host)  ─► SettingsWindow.AccentPreviewChanged(hex)
                     ─► MainWindow.LivePreviewAccent(hex): ApplyAccentOnly + pin brush + popout
Done (readable) ─► CommitAccent(hex): active override? profile.AccentColor : global theme.AccentColor
Dismiss-without-apply ─► revert all accent surfaces to ResolvedAccentColor

Profile edit ─► picker live-previews via MainWindow callback ─► Save stores Profile.AccentColor;
               cancel ─► revert to ResolvedAccentColor
Profile select ─► ActiveProfileName recorded ─► ResolvedAccentColor applied; colorless/none ─► global
Startup ─► restore ActiveProfileName ─► apply its ResolvedAccentColor
```

## 7. Error handling & edge cases

- **Unreadable selection (D4):** indicator + Done disabled + "Use nearest readable". The pipeline is
  never handed an unreadable accent at commit.
- **Unreadable in-flight color during a drag (D5/B4):** the picker does **not** raise preview for
  unreadable values (holds last readable), and `LivePreviewAccent` only ever receives readable hex, so
  `PickReadableForeground` cannot throw mid-drag.
- **Apply vs dismiss with live preview:** **Done is the commit affordance** for the color picker.
  Dismissal paths (title-bar close, Esc, or an explicit Cancel if the implementation keeps close-to-
  apply for other settings) must close without `DialogResult == true`, and `MainWindow` must revert all
  live-previewed accent surfaces to `ResolvedAccentColor`. This is a deliberate split from the current
  close-applies Settings path because accent preview changes App-level resources before persistence.
- **Malformed hex (typed/hand-edited json):** `IsValidHex` rejects → policy unreadable / `Sanitize`
  drops to null / `ValidateAccent` false.
- **Global edit while a profile overrides (B3, resolved by D7):** the Settings picker edits *that
  profile's* color; the note states so. To edit the **global** accent, no profile must be overriding
  (deselect it or pick a colorless profile) — documented, owner-accepted.
- **Theme switch with a custom/profile accent:** §4 requires readability under all presets, so a
  switch can never make a stored accent unreadable.
- **Active profile edited/deleted/overwritten:** re-resolve immediately in the live `MainWindow` path,
  not only on next settings load. If the active profile is renamed, move `ActiveProfileName` to the new
  name; if it loses its color, is deleted, or is overwritten as colorless, clear/re-resolve and apply the
  global accent. `Sanitize` is the persisted-file backstop for dangling names, not the only cleanup path.

## 8. Testing strategy (TDD; 3-layer model)

**New (pure):** `IsValidHex` (valid/invalid); `ColorMath` HSV↔RGB round-trip + S=0 hue + black/white;
`AccentReadabilityPolicy`: every curated preset readable; `Evaluate("not-a-color").IsReadable == false`;
a dim color unreadable; `NearestReadable` total + converges + preserves hue when possible; **TG-4 dense
hue sweep** (`NearestReadable(each)` passes §4 across all presets); `Profile.AccentColor` roundtrip;
`Sanitize` drops invalid→null, clamps unreadable, clears dangling `ActiveProfileName`;
`ProfileService.ValidateAccent`; `ResolvedAccentColor` resolution (profile ?? global).

**New (runtime / markup):** `AccentColorPicker` constructs; `SelectedColor` DP seeds/reads;
`PreviewColorChanged` fires for readable, **does not** fire for an unreadable value (and the App accent
tokens stay on the last readable value — drag-through-`#787878`/`#909090` asserts no throw);
`IsSelectedReadable` flips; `UseNearestReadable` fixes; **`PresetRow.ItemsSource` equals
`ThemeCatalog.AccentOptions`** (the catalog-drift guard, as a **runtime** test — a markup test can't see
the code-set ItemsSource); `SettingsWindow` exposes `AccentPicker`; live `AccentPreviewChanged`; Done
disabled while unreadable; **revert-on-dismiss** restores the App accent to `ResolvedAccentColor`;
profile activation applies/reverts the accent **without invoking the browser**; **add** a `ThemeHintText`
copy test (none exists today) asserting the live-preview wording.

**Updated (existing):**
- `XamlInvariantTests.Required_named_controls_exist` (SettingsWindow): drop the six `AccentChip*`, add
  `AccentPicker`. `Settings_appearance_controls_have_tooltips_and_accessible_names`: swap chips → picker.
  `Settings_theme_and_accent_controls_match_the_catalog`: the accent-chip-Tags assertion **moves to the
  runtime catalog-drift test** above (the preset chip + corner assertions stay).
- `WpfRuntimeTests`: re-point **four** tests that `FindName("AccentChip…")` — replace chip clicks with
  `picker.SelectedColor = "<hex>"`, preserving every assertion (especially
  `SettingsWindow_preset_click_preserves_a_custom_accent`'s theme-switch-survives gate):
  `SettingsWindow_reflects_and_updates_theme_and_accent_input`,
  `SettingsWindow_accent_only_change_keeps_behavior_on_preset_defaults`,
  `SettingsWindow_preset_click_preserves_a_custom_accent`, `SettingsWindow_done_button_dismisses_the_dialog`.
- `ThemeCatalogTests`: keep the curated-palette gates; the free-color gates live in the policy tests.

**No magic literals:** derive expected contrast/thresholds from the catalog/policy, per existing convention.

## 9. Phasing & rollout

1. **Phase 0** — `IsValidHex` + `ColorMath` + `AccentReadabilityPolicy` (+ TG-4 sweep). No UI.
2. **Phase 1** — `AccentColorPicker` control (+ tests), not yet wired.
3. **Phase 2** — global accent → wheel; `ApplyAccentOnly`; `LivePreviewAccent` (App + imperative
   surfaces); live preview + revert; Done gating; re-point the four chip tests; TG-4 closed.
4. **Phase 3** — `Profile.AccentColor` + `ActiveProfileName` + persistence/sanitize + edit-dialog
   picker + combo swatch + activation/restore + the context-sensitive Settings target (D7).

Version: user-facing feature → **minor** bump at publish (per `CLAUDE.md`); each phase behind the green
CI gate; manual/visual sign-off on the deployed Stable copy is owner-gated; dev loop uses `run-piplay`.

## 10. Out of scope / non-goals

- Per-window accents; multiple simultaneous accents.
- Alpha/transparency in the accent (`#RRGGBB` only).
- Importing OS/peripheral lighting palettes (the screenshot is a *visual* reference only).
- Named-color input beyond hex/RGB.

## 11. Resolved (no longer open)

- Active-profile persistence across restart → **D6 (persist)**.
- Global-edit-while-override interaction → **D7 (Settings edits the active profile's color)**.
- Live preview on the active popout → **yes** (§5.3 drives `_player.ApplyAppearance`).
- Accent-only applier entry → **public `ApplyAccentOnly`** (§5.3).
- Remaining Phase-1 detail (R/G/B steppers vs free text, exact hex-field affordance) is an
  implementation choice within Task 3, with tooltips + a11y names required.

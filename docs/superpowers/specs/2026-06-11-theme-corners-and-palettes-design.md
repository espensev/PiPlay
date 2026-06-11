# Theme pass 2: theme-owned corners, per-preset palettes, corner-style override

Date: 2026-06-11
Status: implemented (PR #19)
Source: `piplay-theme-review-and-variants.md` (external review of the v2 bundle) — this pass drives
its §§2.5–2.6, 3, 7, and 8 home. Builds on the Task 8–10 theme pass (PR #18, design addendum in
`2026-06-10-ui-overhaul-stabilization-design.md`).

## Problem

After PR #18 the presets were theme labels plus accent defaults: rounding, surface colors, border
strength, and control radii stayed hardcoded in XAML, and the only DWM corner control was an
opacity side effect (popout rounds iff a configured opacity level is below 1.0). The review doc's
verdict: "the current presets are not yet real visual themes."

## Shape of the change

### 1. Catalog becomes the visual source of truth

`ThemePreset` gains three records (review doc §3 staged shape):

- **`ThemePalette`** — AppBackground/SurfaceBase/SurfaceRaised/SurfaceHover/BorderSubtle/
  BorderStrong/TextPrimary/TextSecondary/Danger as `#RRGGBB`. Sharp Dark adopts the darker §7.1
  palette (`#07090B` base, slate borders, rose danger `#E45D75`); **Minimal carries the previous
  shared palette unchanged** (it IS today's look); Soft Glass goes cooler/bluer. The accent is NOT
  palette data — it stays the separate user-selected value.
- **`ThemeRadii`** — 12 semantic radii (frame/title-bar/button/icon/input/panel/popup/thumbnail/
  swatch/scrollbar-thumb/tooltip). Sharp 3–6, Minimal 4–8, Soft Glass 5–16. Softness ordering
  (sharp ≤ minimal ≤ soft-glass per token) is test-pinned.
- **`DwmCornerMode`** (Default/Square/SmallRound/Round) — the native outer corner. Sharp Dark and
  Minimal use **Default**, NOT SmallRound: the default theme must keep the pristine-window
  guarantee (no DWM write on untouched windows, byte-identical default look). Soft Glass = Round.

New accent option: **steel `#4A8FAB`** (the review doc's muted primary) — 5.2:1 with the dark
button text and ≥3:1 as a glyph on every preset's hover surface, so it passes the existing
readability gates. The "sharp screenshot" look is Sharp Dark + steel, not a fourth theme.

### 2. Resources: replace-not-mutate, now for everything

`ThemeResourceApplier.Apply` replaces (same mechanism as the PR #18 accent — frozen brushes,
DynamicResource consumers re-resolve live):

- the 9 palette brushes + their `*Color` companions (`PaletteBrushKeys` is shared with tests),
- the 12 `Radius*` CornerRadius entries + the two compatibility aliases,
- `AccentPrimary`/`AccentPrimaryLight` (unchanged),
- and stashes `CurrentDwmCorners` for windows built outside the settings flow (Prompt dialogs).

Every themed brush/radius reference in XAML moved `StaticResource` → `DynamicResource`. Fixed
aliases (`AccentCyan`, `AccentAmber` as the placeholder-note color, …) stay static. The
`Colors.xaml` seeds equal the sharp-dark palette/radii exactly (drift-gated by
`Colors_xaml_seeds_match_the_sharp_dark_preset`) so a fresh launch shows no pre-Apply flash.

### 3. Native corners decoupled from opacity (review doc §2.6)

`WindowOpacityApplier.SetRoundedCorners(hwnd, bool)` → `SetCornerMode(hwnd, DwmCornerMode)`.
PlayerWindow's opacity path no longer touches DWM corners. Each window applies the resolved mode
itself at `SourceInitialized` and on settings change: MainWindow (`ApplyOwnCornerMode`),
PlayerWindow (ctor param + `ApplyCornerMode` live seam), SettingsWindow (wears the PENDING
selection for instant feedback), and Prompt shells (read `CurrentDwmCorners`).

Guard semantics: `Default` on a never-touched window writes nothing (pristine guarantee);
`Default` on a previously-modified window DOES land `DWMWCP_DEFAULT` (the "corner style back to
Theme" / "theme switched off soft-glass" path — mutation-tested). `WindowChrome.CornerRadius`
stays 0 everywhere; outer corners belong to DWM (ADR-0004's AllowsTransparency=False is
load-bearing and untouched).

**Behavior change, accepted:** a translucent popout no longer auto-rounds. Corner shape is theme/
override data; a sharp-dark user with custom opacity keeps square corners unless they pick a
rounder corner style. This is the review doc's explicit recommendation.

### 4. User-facing corner control (review doc §8.1)

`ThemeSettings.CornerStyle` ∈ theme/square/small/soft/round (default "theme", sanitized on load).
The Settings Appearance section gains a "Corners" chip row. The override swaps the WHOLE profile —
radius set + DWM mode (`RadiiFor`/`DwmCornersFor`): square = all-zero + DONOTROUND, small = sharp
profile + ROUNDSMALL, soft = minimal profile + ROUND, round = soft-glass profile + ROUND. Never
individual per-control values.

### 5. Preset click adopts preset defaults (review doc §2.1)

An explicit preset selection now adopts the preset's accent, fade delay, strip auto-hide, opacity
levels (assigned directly — `Slider.Value` raises no event when the display percent doesn't move),
and resets corners to "theme". Controls touched afterwards are overrides. The opacity adoption
fires the live preview, so Soft Glass's translucency is visible before the dialog closes.

## Test strategy (509 → 538)

- Catalog: radii sanity (0..24) + softness ordering; corner-style normalization + whole-profile
  swaps; per-preset WCAG gates (text on surfaces ≥4.5, every accent ≥3.0 on every hover surface,
  dark button text ≥4.5 on every accent, white ≥3.0 on Danger).
- Markup: hardcoded-radius ban (direct attribute AND Setter form; only WindowChrome "0" exempt;
  references must be `{DynamicResource Radius*}`), per-scope DynamicResource definedness (a
  window-local key cannot satisfy another window's reference), seed↔catalog drift gate
  (culture-invariant comparison).
- Runtime: applier writes palette+radii per preset (frozen, unknown-id falls back to sharp-dark,
  override swaps radii but never palette); replace-entry restyle reaches a realized DarkButton's
  fill AND template corner; END-TO-END corner wiring for all four window types (added after a
  review-agent mutation experiment proved the wiring could be deleted with the prior suite green);
  Default-reset transition on a modified HWND; Settings corner-style flow + preset-defaults
  adoption.

## Addendum: end-pass review disposition (2026-06-11)

`piplay-theme-end-pass-review.md` audited the live checkout at `6e843a2` — the superseded
parallel draft (AccentPalette.cs, `Theme.Accent*` keys, separate Pin/Fade color rows), NOT the
merged lineage. Disposition against current code:

- **F1** (split-brain settings) / **F3** (accent not wired) / **F5** (old Settings model) /
  **F6** (one-shot resource apply) — already fixed by PR #18 + follow-up `3d530cd`
  (theme/accent UI, DynamicResource + re-apply on save, single accent everywhere).
- **F4** (radius/DWM staged) / **F7** (no preset palettes) / **F8** (alias drift; current
  lineage has ONE key set, applier updates Brush + Color together) — fixed by this PR.
- **F2** (preset defaults dead data) — REAL residual, fixed in this addendum pass:
  `ThemePreferenceResolver` now resolves preset default → explicit override → normalized for
  strip/opacities. Migration safety is two-layered: `ThemeSettings.FromLegacy` copies legacy
  behavior values as explicit overrides at seed time, and **schema 3** backfills the nulls of
  schema ≤2 theme blocks from the Player fields once on load (those files' nulls meant "use
  Player"; schema 3 nulls mean "use the preset default"). Raw-Player fallback remains only for
  a null theme block.
- **§3.3** (accent preservation rule, wanted before any color wheel) — implemented: a preset
  click adopts the new preset's default accent ONLY when the current accent is the previous
  preset's default; a custom accent survives theme switches. (Refines §2.1 adoption, which
  still applies to fade/strip/opacity/corners unconditionally.)
- **§3.2** (override naming) — kept JSON-compatible names; fields are now documented as
  overrides and the resolver enforces the semantics.
- **§3.1** (generated chips), **§3.5** (MediaBackdrop token), **§3.7** (media-glow), color
  wheel — still deferred by design, matching both review docs' ordering.

## Accepted residuals / next pass

- `ControlCornerRadius`/`ButtonCornerRadius` aliases kept one migration pass (review doc §8.4)
  with zero consumers left — drop next pass.
- `RadiusMainWindowFrame`/`RadiusPopoutFrame`/`RadiusTitleBar`/`RadiusThumbnail` are
  forward-looking seams (no consumer; outer corners are DWM-owned today) — commented as such.
- Accent token expansion (AccentPressed/Muted/Subtle/Border/OnAccent) and the color wheel stay
  deferred per the review doc's ordering (§9 items 7–13); all offered accents pass the dark-text
  gate so OnAccent logic is not yet needed.
- Settings chips remain hand-written; catalog drift is test-gated (review doc §2.7 first pass).
- DangerPin recolor `#FF4B55` → `#E45D75` on sharp-dark/soft-glass follows §7.1/§7.3; Minimal
  keeps the original red. White-on-danger has never met 4.5:1 (3.29:1 before, 3.43:1 after) —
  gated at the 3.0 UI-component level.

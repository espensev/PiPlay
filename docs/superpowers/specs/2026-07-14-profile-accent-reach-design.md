# Design — Profile accent reach (P2 + tinted top chrome)

**Date:** 2026-07-14
**Requirement IDs:** REQ-UI-01, REQ-PROFILE-01, roadmap **P2**
**Owner decision:** resolved 2026-07-14 (see Goals)

## Goals

The app accent currently paints **one always-visible thing: the Pop-out button.** A title-bar wash exists
but is tuned to a **1.20:1** contrast target against the base surface — barely perceptible. Everything else
that consumes the accent is conditional (Pin/Auto when *on*, the "• Pinned" hint when *pinned*), transient
(caret, selection, focus ring), or hidden (Settings, error states).

That is the whole reason roadmap **P2** looked pointless. The owner's words: *"that would be ok, BUT it
just changed one button."* Making a **profile's** color drive a one-button accent would still be a
one-button accent.

So this pass does two things, in this order:

1. **Give the accent reach** — the top chrome visibly carries it.
2. **Let the active profile drive it (P2)** — so switching profiles visibly re-tints PiPlay.

**The P2 contradiction is hereby resolved in favour of P2.** Until now the product spec said a profile's
`accentColor` *"must not replace the global app accent"* (the v0.6.0 split) while roadmap P2 said it
*should*, carrying its own `CONFLICT: confirm before implementing` flag. The owner has now confirmed:
**the profile color drives the app accent.** The spec, the roadmap flag, and the open decision in
`SPEC_GAPS_AND_OWNERSHIP.md` are all updated by this pass.

## Settled decisions

1. **Profile accent wins; global is the fallback.** Effective accent = the active profile's `accentColor`
   if it has a valid one, else `theme.accentColor`. A profile with no color inherits the global accent —
   it does not blank the app out.
2. **The toolbar row gets accented glyphs; the caption row does not.** `Back / Reload / Home / Save /
   Edit / Delete` take the accent. `Settings / Minimize / Maximize / Close` stay neutral. Two reasons,
   both load-bearing:
   - The **title-bar wash already sits behind the caption row**. Accenting those glyphs too would
     double-dip and put accent-on-accent-tint, which is exactly where contrast dies.
   - Window-management controls follow OS convention (`Close` keeps its red hover; it has its own
     `CloseIconButton` style and is not touched).
3. **No new borders, lines, or fills.** P1 is busy *removing* the framed look and v0.6.0 deliberately
   quieted the UI. The accent gets reach by **re-coloring chrome that is already there**, not by adding
   chrome. This rules out accent hairlines, accent window edges, and a saturated title bar.
4. **The wash gets more presence, but stays a tint.** The shell-tint contrast target moves from
   **1.20 → 1.45** against `SurfaceBase`. It remains a decorative wash, not a banner — `QA_Checklist`
   row **UI-CHK-9** still governs it.
5. **Contrast is free — do not re-derive it.** `AccentPrimary` is already
   `EnsureContrast(requested, SurfaceHover)`, i.e. lifted for *presentation* to clear the 3:1 non-text
   floor on the lightest dark interaction surface, while the **stored hex stays exact**. So a very dark
   profile color yields visible glyphs automatically. No new contrast code.
6. **Scope stops at the Source Window's top chrome.** The popout's chrome strip and an active-profile
   popout border were offered and **declined** for this pass. (The popout's existing accent consumers —
   its fallback button — follow the profile for free, because `ResolvedAccentColor` is already what gets
   passed to the player.)
7. **Settings' accent picker edits whatever is currently painting the app — and names it.** P2 creates a
   trap here, and the obvious fix is the wrong one.
   - The trap: with a colored profile active, if the picker kept editing `theme.accentColor`, the user
     would pick a color, watch it preview live, press Done — and the app would **snap back** to the
     profile's color. Nothing they did would stick. That reads as a bug, not as a policy.
   - So: when the active profile has its **own** color, the picker edits **that profile's** color. When
     no profile is active, or the active profile has no color of its own, it edits the **global**
     `theme.accentColor`. In both cases the picker edits exactly the value that is driving the app, so
     the live preview is always truthful and Done always sticks.
   - This is **not** a silent mutation: the existing `accentEditContext` line above the picker names the
     target ("Editing the color for profile 'Violet' — it is driving the app accent."). The parameter
     already exists for precisely this.
   - To change the global default while a colored profile is active, deselect the profile. The hint says so.
   - The profile editor's color wheel remains the other way to set a profile's color. Both paths write
     the same field; that is consistency, not duplication.

## Non-goals

- No accent hairline, accent window edge, or saturated title bar (rejected: re-adds the framed look P1 is
  removing).
- No accent on window-management controls.
- No change to how a profile's color is *stored* or *edited*.
- No popout chrome changes.

## Why this is a small change

The architecture for P2 **already exists and was deliberately left inert**:

- `ProfileAccentService.ResolvedAccentColor(settings, globalAccent)` takes `settings`, **never reads it**,
  and returns the global accent. That stub *is* the v0.6.0 split.
- `MainWindow.ResolvedAccentColor` already feeds the source appearance, the popout appearance, and the
  popout launch.
- `ProfilesCombo_SelectionChanged` **already calls `ApplyResolvedAccent()`** on every profile change —
  it just re-applies the same global accent every time.
- `DerivedAccentSet` already derives `Primary / Hover / Pressed / Border / ShellTint / OnAccent`.

So P2 is one function body. The reach is one style plus one constant.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Services/ProfileAccentService.cs` | `ResolvedAccentColor` returns the active profile's accent when it has a valid one, else the global accent. Invalid/blank profile hex falls back to global, never to a broken color. |
| `src/PiPlay/Theme/ThemeColors.cs` | Shell-tint contrast target `1.20` → `1.45`, extracted to a named constant (`ShellTintContrastTarget`) so "how present is the wash" is one tunable, not a magic number. |
| `src/PiPlay/Theme/ControlStyles.xaml` | New `AccentIconButton` style, `BasedOn` `IconButton`, overriding only `Foreground` → `{DynamicResource AccentPrimary}`. `IconButton` itself stays neutral. |
| `src/PiPlay/MainWindow.xaml` | `BackButton`, `ReloadButton`, `HomeButton`, `SaveProfileButton`, `EditProfileButton`, `DeleteProfileButton` → `AccentIconButton`. Caption row untouched. |

## Tests

The current suite **pins the v0.6.0 split** — those assertions are the spec being changed, so they get
rewritten first (TDD), and must be seen failing before the implementation lands.

- `ProfileAccentServiceTests` — rewrite: profile accent wins; no active profile → global; active profile
  with **no** color → global; invalid stored hex → global. Add: profile accent that is *very dark* still
  resolves to the exact stored hex (presentation correction happens downstream, not in resolution).
- `Ui/MainWindowProfileAccentTests` — rewrite: selecting a profile with a color changes
  `ResolvedAccentColorForTests`; selecting the empty/no-profile entry restores the global.
- `ThemeColorsTests` — pin `ShellTintContrastTarget` and that the derived `ShellTint` clears it against
  `SurfaceBase` for every preset, and that it stays **below** a saturation ceiling (it is a tint, not a
  banner) so a future tweak cannot quietly turn it into one.
- `Ui/XamlInvariantTests` — pin that the six toolbar buttons use `AccentIconButton` and that the caption
  buttons (`Settings/Minimize/Maximize`) and `CloseButton` do **not**. This is the guard for decision 2.
- `Ui/WpfRuntimeTests` — realize the toolbar and assert its glyph brush tracks the active profile's accent
  through a live profile switch, and that a very dark profile color still yields a glyph that clears 3:1
  against the chrome surface.

## Acceptance

- Selecting a profile with a color visibly re-tints the toolbar glyphs, the Pop-out button, and the
  title-bar wash. Selecting a profile with no color falls back to the global accent.
- The stored hex is never mutated by presentation correction.
- Caption/window controls are unchanged; `Close` keeps its red hover.
- No new border, line, or fill anywhere.
- **Owner visual QA on deployed Stable is the real gate** (UI-CHK-9). The 1.45 target is a first
  proposal, not a settled value — it is one constant, and it is meant to be tuned by eye.

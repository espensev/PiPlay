# Popout look cleanup + drop embed Compact — design

Date: 2026-06-25
Status: approved direction (owner UI review follow-up — "cheap cleanup" tier)
Owner asks addressed: 2.1 (transparency/feel), 2.4 (corners read), 4.x (compact), P1/P2 border items.

## Context

The 2026-06-23 owner UI review and direct owner feedback (2026-06-25) said PiPlay still
reads as a boxed-in browser window, not a clean floating media card: borders are "grey and
too visible," compact mode "doesn't work / what's the point," and the popout wants a
rounded, soft-bordered card feel.

This spec is the **cheap-cleanup tier**, chosen by the owner over the heavier architecture
lift after an empirical look at the running app. It deliberately takes **no** window-hosting
change.

## Findings that shaped this (verified against the running app + source)

- **Popout corners are already rounded by the OS.** `cornerStyle: round`/`soft` →
  `DwmCornerMode.Round`; the WebView clips to the DWM rounding with **no square
  poke-through** (confirmed by zoomed corner capture). So "rounded" is already satisfied at
  the ~8px Win11 radius.
- **Windows already ship opaque.** `AppSettings.ConstantWindowOpacity = 1.0`,
  `WindowOpacityPolicy.Default = 1.0`, and every theme (incl. soft-glass) sets
  `DefaultActiveWindowOpacity = WindowOpacityPolicy.Default`. The see-through observed on
  the dev machine is a local `settings.json` override (`constantWindowOpacity 0.92`,
  `idleWindowOpacity 0.78`), **not** the shipped default.
- **The grey "border" is control chrome, not a window frame.** `BorderSubtleColor #FF2B3645`
  / `BorderStrongColor #FF3E4B5C` (`src/PiPlay/Theme/Colors.xaml`) render on controls via
  `ControlStyles.xaml` (TextBox / ComboBox / Button / panels) and on `SettingsWindow.xaml`.
  The main/popout window roots have no frame border.

## Goal

Make PiPlay read as a cleaner surface with no architecture change:

1. Quiet the grey control borders so the UI stops looking boxed-in.
2. Remove the embed "Compact player" feature (high failure on embed-disabled videos,
   near-zero visible benefit).
3. Keep windows opaque by default (already true — confirm, no product change).
4. (Optional polish) give the popout a subtle card edge.

## Non-goals (explicitly deferred — the "big card" escalation tier)

- No transparency / `AllowsTransparency` change, no WebView2 composition or region-clip lift.
- No card radius larger than the OS DWM rounding (~8px).
- No true gradient on the window silhouette edge.
- No deletion of the player shell / IFrame plumbing — kept **dormant** (it is wired into the
  compact-mode timestamp path; ripping it out is a separate, larger job).
- No main-window Browse / Cinema / Compact UX mode model.

## Design

### Part A — Quiet the grey borders (the main fix)

- Soften `BorderSubtleColor` (`#FF2B3645`) and `BorderStrongColor` (`#FF3E4B5C`) in
  `Colors.xaml` so control outlines read as a **faint hairline** against
  `AppBackground` / `SurfaceBase`, not a hard grey line. Target: present-but-quiet — a
  deliberate low-contrast step, neither the current hard grey nor invisible.
- Validate the new contrast with `ContrastReportTests` (reuses the canonical `Wcag.cs`
  formula) so the chosen value is intentional and recorded, not eyeballed.
- Where a resting outline adds nothing (e.g. toolbar icon buttons that already read via
  fill/hover), drop the resting border and reveal it only on hover/focus. **Keep focus
  rings** (accessibility — REQ-UI-02).
- `BorderThicknessDefault` stays `1`.

### Part B — Drop embed Compact

- Remove the "Compact player" toggle + hint text and its handler from `SettingsWindow.xaml`
  / `SettingsWindow.xaml.cs` (`CompactModeToggle`, ~lines 195–203).
- New popouts always launch **Normal** (the full watch page — never breaks on
  embed-disabled videos). Default `Player.CompactMode = false`; the popout-creation path
  resolves to Normal regardless of the global compact flag.
- Keep `PlaybackMode.Compact`, `PlaybackModePolicy`, the player shell/IFrame assets, and
  `Profile.Mode` in place but **dormant** (compact not honored for new popouts). This avoids
  a large deletion touching the IFrame timestamp source.
- Settings copy: remove the compact explanation; no replacement control this pass.

### Part C — (Optional) subtle popout card edge

- Optionally add a quiet 1px inner border — or a faint top→bottom gradient ring — just
  inside the popout's DWM-rounded edge, to give the floating card a touch of definition
  without transparency. **Low priority; ship A + B first**, add C only if the card still
  reads flat.

### Part D — Opacity (confirm only, no product change)

- No default change (already opaque). Reset this dev machine's `settings.json` opacity to
  `1.0` so local QA matches the shipped default. No product code touched.

## Testing

- **Layer 1 (Markup)** — `XamlInvariantTests`: assert the softened border tokens and the
  absence of the Compact toggle.
- **Contrast** — `ContrastReportTests`: a row pinning the new border contrast value.
- **Logic** — `PlaybackModePolicyTests` / settings tests: new popouts resolve to Normal; no
  compact toggle path.
- **Layer 3 (Wpf)** — `WpfRuntimeTests`: softened border brushes realize; (if Part C)
  popout card edge realizes.
- Full Lane A green, then **deploy to Stable** (`Publish-Stable.ps1`) for visual judging.
  Escalate to the big-card tier **only** if the ~8px rounding / lack of a gradient edge
  still reads wrong on the real build.

## Resolved choices

- Main-window / control borders: **barely-there hairline** (not fully off) so edges still
  have definition and focus/hover affordances survive.
- Build base: this stacks on commit `9e58734` (filled accent buttons + profile/accent
  split), which is committed but **not yet deployed**.

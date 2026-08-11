# Playlist mixes in the popout + profile-driven backgrounds — tight-scope design

Date: 2026-08-09 · Status: implemented; deployed-Stable/manual gates pending. Feature A landed at
`e43c98b`, B/C at `935b234`, and D at `e2642fc` on `feature/popout-playlist-support` and
`feature/profile-backgrounds`.

## Scope

- Normal Popouts retain `list=RD...` mix/radio queues and return current video/list context.
- Profile accents extend into Source/Popout letterboxes, Source background, profile-row washes,
  and the Popout identity edge without painting over video.

## Feature A — Mix/radio queues pop out (Normal mode)

Behavior: popping out `watch?v=X&list=RD...` carries the mix into the Normal popout exactly like
a regular playlist: the queue advances in the popout, and closing the popout returns the source
to `watch?v=<current>&list=RD...`. No unsupported-mix note is shown.

Mechanism: `YouTubeUrlHelper.ApplyList` keeps any charset-valid list ID. The Normal watch page
renders mixes natively, so no other Normal-path change is needed.
The compact tiers are the one place mixes genuinely fail (the IFrame API cannot load
auto-generated lists), so `BuildShellUrl` and `BuildEmbedUrl` omit RD lists themselves — the
degrade lives exactly where the limitation lives. No note plumbing for that path: compact is
kill-switched (`ResolveEffectivePopoutMode` forces Normal), and its error-bar/fallback machinery
already covers a shell that cannot play.

Malformed list IDs (charset-rejected) produce a non-blocking `FallbackReason`, which keeps the
existing Q-6 placeholder-note plumbing alive with an honest producer.

## Features B–D — accent reaches the backgrounds

All use the resolved-accent pipeline (`ApplyResolvedAccent` →
`ThemeResourceApplier.ApplyAccentOnly`, replace-not-mutate). Letterbox, room tone, Popout edge,
and shell/glyph reach scale with AccentIntensity; profile-row washes remain at the preset
`SubtleAlpha`, independent of intensity. At intensity 0, letterboxes are black, room-tone wash is
the flat palette, and the Popout edge is transparent; primary controls and profile-row identity
remain accented. No new settings UI or airspace lift; nothing paints over WebView2 video.

### B — Letterbox tint

`DeriveAccentSet` provides `Letterbox` = `Mix(#000000, Primary, LetterboxMixCeiling × reach)` with
`LetterboxMixCeiling = 0.06` and `reach = intensity/100` (so default 50 ≈ 3% toward the accent —
a near-black room tint, not a color). Applied as `AccentLetterboxColor`/`AccentLetterbox` brush
(Colors.xaml seed + `ApplyAccentOnly`). Consumers replace hard-coded `Black`: the Source content
grid and `SourcePlaceholder` (MainWindow.xaml) and the popout `Window.Background`
(PlayerWindow.xaml), all via `DynamicResource` so profile switches re-tint live.

### C — Room tone

`DeriveAccentSet` provides `BackgroundWash` = `Mix(AppBackground, Primary, WashMixCeiling × reach)`
with `WashMixCeiling = 0.04`. Applied as `AppBackgroundWashColor`/`AppBackgroundWash`; consumed
by the main window `Window.Background` and the toolbar row. The
Settings window deliberately keeps plain `AppBackground` (settings are app chrome, not profile
identity). The title-wash gradient's end stop extends `0.62 → 0.80` so the wash reads as one
sweep into the washed background instead of dying mid-bar.

Contrast gate: `TextPrimary` on `BackgroundWash` at intensity 100 must clear 4.5:1 (WCAG AA) for
all three shipped presets, enforced by a derivation test using the same `Wcag.ContrastRatio` the
existing gates use.

### D — Identity accents

1. Profile dropdown rows: behind the existing 4px identity rail, each row gets a wash of its OWN
   accent — the row's `AccentColor` at the theme's `SubtleAlpha`, over the normal row surface.
   Hover/selection visuals keep their existing
   states on top. `AccentMuted` stays unwired; CON-1 remains an open owner decision.
2. Popout accent edge: a 1px inset border on the popout root uses `PopoutAccentEdge`
   (`AccentBorder` alpha-scaled by accent reach and transparent at intensity 0), updated by the
   existing `ApplyAccent` path. It must not disturb DWM corner styles or Win11 hairline
   suppression.

## Non-goals

- Wallpapers/imagery or any surface under/over the video (deferred architecture-lift tier).
- True transparency (`AllowsTransparency` stays false), curve-following glow.
- Compact-shell mix support, compact revival, or note plumbing for compact degrades.
- Wiring `AccentMuted` (CON-1) or `AccentGlow`; Settings-window room tone.
- New dials. Strength constants are code literals; the owner tunes by eye at Stable QA and we
  adjust literals then.

## Automated gates

- G-A1 parse: `watch?v=X&list=RDx...` round-trips with `PlaylistId` kept; `BuildWatchUrl`
  carries it; `BuildShellUrl`/`BuildEmbedUrl` omit RD lists (regular `PL` lists still carried).
- G-A2 malformed list IDs set a non-blocking `FallbackReason`; the video still pops out.
- G-A3 RD lists have no `FallbackReason`; return tracking keeps an
  RD list like any other (`TrackReturnIdentity` row).
- G-A4 product-spec §22.1 acceptance bullet matches the behavior.
- G-B1 exact `Letterbox` values at intensity 0/50/100 for sharp-dark (0 → `#000000`).
- G-B2 WPF-lane: content grid, `SourcePlaceholder`, and popout background resolve to the
  `AccentLetterbox` resource (not `Black`).
- G-C1 exact `BackgroundWash` values at 0/50/100; intensity 0 equals the palette
  `AppBackground` exactly; AA contrast gate at 100 for all presets.
- G-C2 main window + toolbar consume `AppBackgroundWash`; title-wash end stop literal `0.80`.
- G-D1 row-wash converter: row accent at theme `SubtleAlpha` over the row surface; null/invalid
  accent falls back to the plain surface.
- G-D2 popout root border: 1px, `PopoutAccentEdge` brush, present in the WPF lane and transparent at intensity 0.

## Open gates

- Owner by-eye sign-off of all new tints on deployed Stable; strength constants remain tunable.
- CON-1 (`AccentMuted`) unchanged and still open.
- RD lists in the compact tier if compact is ever revived (explicitly out of scope).

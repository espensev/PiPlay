# Profile color identity and accent fill - design

## Goals

Address the owner review correction that profile colors currently do too little and the app accent reads like an outline decoration instead of an actual accent.

This pass changes the model:

- The **global app accent** remains the app accent for primary actions, pin/fade glyphs, focus/highlight tokens, and the open popout.
- A **profile color** becomes a visible profile identity color, not an app-wide accent override.
- Primary accent buttons use the accent as a **fill**, not just an outline.
- Free accent/profile colors accept any valid `#RRGGBB` value; the app picks the best dark/white foreground where text sits on a color.

## Requirements served

- `REQ-UI-01`: visible chrome must be intentionally themed, not token-only.
- `REQ-UI-02`: filled action buttons remain legible and accessible by name.
- `REQ-PROFILE-01`: profile fields still override their own scoped behavior, but profile color no longer overrides unrelated app appearance.
- 2026-06-23 owner review: profiles define content plus optional identity color; appearance defines the app accent.

## Acceptance criteria

- Active profile color no longer changes `ResolvedAccentColor` or app-level accent resources.
- Settings Appearance always edits the global app accent.
- Profile editor color changes store a profile identity color, not an app accent override.
- Profile colors are visibly used in the profile selector as a color frame/marker, not as a second filled button inside the dropdown control.
- `AccentButton` background uses `AccentPrimary`, hover uses `AccentHover`, pressed uses `AccentPressed`, and foreground uses the matching `OnAccent` / `OnAccentPressed`.
- Nested `PopOutButton` text/icon foreground follows the button foreground.
- Any valid `#RRGGBB` accent/profile color is accepted and normalized.
- Invalid hex still fails and can fall back to the default accent.
- Tests cover global-vs-profile accent resolution, button fill tokens, profile color marker/frame use, and wider color acceptance.

## Settled decisions

1. Profile color is identity, not app accent.
   Selecting a profile can still persist `ActiveProfileName`, navigate to the profile URL, and apply profile playback/topmost fields. It must not recolor the whole app.
2. Settings edits the global app accent only.
   Profile color is edited through the profile editor.
3. Foreground choice is best-effort black/white.
   Some mid-tone colors cannot satisfy 4.5:1 against both dark and white, but the app should not reject them. It picks the higher-contrast candidate.
4. The profile selector is the first identity surface.
   The dropdown remains one dark control; profile color decorates its frame and row marker without turning the selected name into a second filled button.

## Non-goals / out of scope

- No new theme preset.
- No border/shadow strength controls.
- No large rounded-card WebView2 architecture change.
- No main-window compact/cinema mode model.
- No per-profile popout border in this pass.

## Testing approach

- Unit tests update `AccentReadabilityPolicy` and `ProfileService` from readability-gated to valid-hex-gated.
- Unit tests update `ProfileAccentService` so resolved/committed app accent stays global.
- WPF tests assert profile selection no longer recolors `AccentPrimary`.
- Markup tests assert profile color decorates the selector marker/frame and `AccentButton` uses fill/foreground accent tokens.
- Runtime tests assert filled accent buttons resolve background/foreground tokens and nested popout text follows the button foreground.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Theme/ThemeColors.cs` | Make foreground choice best-effort instead of throwing in the mid-tone dead zone. |
| `src/PiPlay/Theme/AccentReadabilityPolicy.cs` | Treat any valid hex as accepted; keep invalid fallback behavior. |
| `src/PiPlay/Theme/ControlStyles.xaml` | Make `AccentButton` filled by accent tokens. |
| `src/PiPlay/MainWindow.xaml` | Replace filled profile selector chips with a profile color marker; bind popout nested text foreground to the button foreground. |
| `src/PiPlay/MainWindow.xaml.cs` | Stop resolving active profile color as app accent; Settings edits global app accent. |
| `src/PiPlay/Prompt.cs` | Treat profile accent as identity color and remove readability-copy assumptions. |
| Tests | Update old readability/profile-override contracts to the new valid-hex/profile-identity model. |

## Unresolved decisions

- Whether profile color should also decorate the active popout border.
- Whether the profile selector should grow into a richer profile chip control.

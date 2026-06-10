# PiPlay theme system overhaul - evaluation

Status: evaluation only; no implementation in this pass.

Source input: proposed "PiPlay UI Theme System and Visual Guide" plus live UI-state notes captured on
2026-06-10.

## Summary verdict

The proposed direction is right: PiPlay has outgrown separate Pin color, Fade color, fade delay, and
opacity controls. A coherent theme preset plus one user accent would make the app easier to tune,
test, and explain.

This should be treated as a refactor, not a small settings tweak. The current code has styling split
between XAML static resources, per-window accent application, settings persistence, fade timing
constants, and native-window opacity/corner code. Moving to a real theme system means introducing a
central theme model and applying it through resource tokens before replacing the Settings UI.

Recommended first implementation later: ship preset theme selection and accent preset chips, plus a
scrollable/restructured Settings window. Defer a full color wheel until the derived-token and contrast
rules are stable.

## Current implementation map

| Area | Current code | Current behavior |
|---|---|---|
| Base colors | `src/PiPlay/Theme/Colors.xaml` | Hardcoded dark palette and fixed accent brushes (`AccentCyan`, `AccentViolet`, `AccentGreen`, `AccentAmber`). |
| Control styles | `src/PiPlay/Theme/ControlStyles.xaml` | Mostly `StaticResource` styles with hardcoded corner radii and accent tokens. |
| Toggle accent plumbing | `src/PiPlay/Theme/ToggleAccent.cs` | Per-toggle checked brush can be set in code for Pin/Fade. |
| Appearance policy | `src/PiPlay/Services/PlayerAppearancePolicy.cs` | Fixed accent keys and fade-delay presets for current player customization. |
| Settings storage | `PlayerSettings` in `src/PiPlay/Models/AppSettings.cs` | Separate `PinAccent`, `FadeAccent`, `FadeIdleDelayMs`, `ConstantWindowOpacity`, `IdleWindowOpacity`, `StripAutoHide`, and `CompactMode`. |
| Settings UI | `src/PiPlay/SettingsWindow.xaml` | Tall non-resizable dialog with Privacy, Appearance, and Playback sections. No `ScrollViewer`. |
| Settings apply path | `MainWindow.SettingsButton_Click(...)` / `ApplyPlayerPreferences(...)` | Opacity sliders live-preview; other appearance values apply on dialog close. Compact mode applies only to the next popout. |
| Popout fade | `FadePolicy`, `PlayerWindow.ApplyFadeState()` | Fade delay is configurable; fade duration is still a constant. |
| Whole-window opacity | `WindowOpacityPolicy`, `WindowOpacityApplier` | Active/idle opacity are independent stored values; opacity animation duration is tied to `FadePolicy.FadeDurationMs`. |
| Window rounding | XAML border/template radii plus DWM helper | There is no single radius token; popout native/DWM rounding is currently coupled to opacity handling. |

## What fits well

- Theme preset plus user accent matches the product need better than separate Pin/Fade color settings.
- The suggested themes map cleanly to real PiPlay modes:
  - `Sharp Dark`: current browse-first utility shell.
  - `Minimal`: low-distraction daily use.
  - `Soft Glass`: popout/desktop-overlay use.
  - `Media Glow`: expressive media surface, probably later than the first implementation.
- Persisting stable choices only is the right direction. Derived brushes should be regenerated from
  the selected theme and accent.
- The current `PlayerAppearancePolicy` is a good seed for a future `ThemeCatalog` / `ThemeService`,
  but it should not remain the long-term owner of app-wide theme state.
- The Settings UI should show theme and accent as first-class choices, with opacity/fade/top-bar as
  advanced overrides.

## Required adjustments to the proposal

1. **Do not remove legacy fields immediately.** Existing users may have `PinAccent`, `FadeAccent`,
   `FadeIdleDelayMs`, and opacity values in `settings.json`. The migration should either read them
   into the new shape or keep them as compatibility fields for at least one schema version.

2. **Start with accent preset chips, not a full color wheel.** A color wheel is desirable, but the
   first pass needs contrast-safe derived tokens and test coverage. Preset chips can land first while
   the model stores a hex accent from day one.

3. **Make Settings scrollable before adding more controls.** State 07 captured a 720 x 1249 physical
   pixel settings window. The current `SizeToContent="Height"` / `ResizeMode="NoResize"` / no-scroll
   layout is already close to the edge.

4. **Treat `Compact player` as Playback, not Appearance.** Theme should affect how compact mode is
   framed, but compact-vs-normal playback remains a behavior setting that applies to new popouts.

5. **Theme opacity defaults should not erase user overrides silently.** If the user chooses a theme,
   the app can offer to use theme opacity defaults. If the user manually changes active/idle opacity,
   that should become an override with clear reset behavior.

6. **Fade duration is not currently configurable.** The proposal has `FadeDurationMs`, but current
   code treats fade duration as a constant shared by controls and window opacity. Making it theme-owned
   requires a careful policy/API change.

7. **Live theme switching requires DynamicResource or explicit reapply.** Many current XAML styles use
   `StaticResource`. Runtime theme changes will not be uniformly live until tokens move to
   `DynamicResource` or each window has a reliable reapply path.

8. **Window corner radius is not just XAML.** WPF template radii, borderless window hit testing,
   WebView2 airspace, and DWM rounded corners are separate mechanisms. Theme radius tokens should land
   in stages.

## Recommended target model

The proposal's `PiPlayTheme` shape is a good start. For PiPlay, split preset defaults from user
overrides so reset/migration are understandable:

```csharp
public sealed class ThemeSettings
{
    public string ThemeId { get; set; } = "sharp-dark";
    public string AccentColor { get; set; } = "#2D6F8F";
    public string FadeDelayPreset { get; set; } = "normal";
    public bool? StripAutoHideOverride { get; set; }
    public double? ActiveOpacityOverride { get; set; }
    public double? IdleOpacityOverride { get; set; }
}
```

Open decision: whether this lives under `AppSettings.Theme` or under `PlayerSettings`. Recommendation:
use `AppSettings.Theme` because the theme applies to `MainWindow`, `PlayerWindow`, `SettingsWindow`,
and prompts, not only the player.

## Theme presets to settle first

| Theme | Use | First-pass recommendation |
|---|---|---|
| `sharp-dark` | Utility/browse baseline | Default. Should stay close to current PiPlay, with a muted blue-cyan accent. |
| `minimal` | Daily low-distraction use | Include in first pass if it is visually distinct without adding new effects. |
| `soft-glass` | Popout/overlay use | Include in first pass, but keep opacity floor and no-click-through constraints from ADR-0006. |
| `media-glow` | Expressive media display | Defer until glow/shadow tokens and visual QA are ready. |

## Settings UI recommendation

Use this order:

1. Privacy
2. Appearance
3. Playback
4. Advanced

Appearance:

- Theme selector: `Sharp Dark`, `Minimal`, `Soft Glass`; `Media Glow` can be disabled or absent until
  implemented.
- Accent selector: preset chips first (`Muted cyan`, `Steel blue`, `Violet`, `Green`, `Amber`), storing
  a normalized hex value.
- Small preview row: show a normal button, active toggle, and border/focus sample using the selected
  theme/accent.

Playback:

- `Compact player` remains here, with visible copy that says it applies to new popouts.

Advanced:

- Fade delay preset.
- Active/idle opacity sliders.
- Auto-hide top bar.
- Reset theme defaults.

The old separate Pin color and Fade color sections should be removed once theme accent is live. Their
behavior becomes derived accent tokens.

## Implementation sequence for later

1. Add `PiPlayTheme`, `ThemeSettings`, `ThemeCatalog`, and normalization/migration tests.
2. Add generated theme resource tokens while keeping existing brush names as compatibility aliases.
3. Move key XAML styles from hardcoded color/radius values to theme tokens.
4. Apply the selected theme at app startup before windows are created.
5. Make Settings scrollable/restructured and add theme/accent controls.
6. Replace separate Pin/Fade color controls with the single accent path.
7. Thread theme opacity/fade defaults into `PlayerWindow` without changing compact/normal playback.
8. Add optional advanced overrides only after preset behavior is stable.
9. Add visual/manual QA captures for Source, standard popout, compact popout, and Settings.

## Test approach for the future code pass

- `SettingsServiceTests`: default theme, migration from old appearance fields, invalid theme ID,
  invalid hex color, override normalization, reset behavior.
- New theme policy tests: preset catalog uniqueness, token generation, derived accent contrast basics.
- `XamlInvariantTests`: required theme resources resolve; no old accent-specific settings controls
  remain after replacement; Settings has a scroll container.
- `WpfRuntimeTests`: construct `MainWindow`, `PlayerWindow`, `SettingsWindow`, and `Prompt` with each
  theme and verify style/resource resolution.
- Manual screenshots: compare `Sharp Dark`, `Minimal`, and `Soft Glass` in source window, standard
  popout, compact popout, and Settings.

## Risks

- **Settings bloat:** adding theme controls without removing old controls will make the dialog too
  tall and conceptually noisy.
- **Runtime resource drift:** partial conversion from `StaticResource` to theme tokens can leave some
  controls using old colors.
- **Opacity safety:** `Soft Glass` must still obey the existing no-click-through decision and remain
  easy to recover at the minimum UI opacity.
- **WebView2 edges:** popout rounding and transparent-looking surfaces can expose WebView2 airspace and
  resize-hit-test issues already observed in States 04 and 05.
- **Backward compatibility:** changing settings shape without migration would surprise existing users
  who already tuned Pin/Fade/opacity.

## Documentation impact

When implementation starts, update:

- `docs/CHANGELOG.md` for the user-visible settings/theme change.
- `docs/QA_Checklist.md` with theme smoke captures and opacity/fade checks.
- `docs/ui-overhaul-discovery/ui-state-notes.md` with before/after screenshots for Settings and each popout state.
- A real design spec and implementation plan once the open decisions below are settled.

## Open decisions

- Should selecting a theme overwrite active/idle opacity immediately, or keep current user opacity as
  overrides until the user chooses "use theme defaults"?
- Should `FadeDelayPreset` be theme-owned by default with an advanced override, or remain a user
  setting independent of theme?
- Should `Soft Glass` default to auto-hide top bar, or should auto-hide remain off by default for
  discoverability?
- Which initial accent chips are allowed, and what contrast threshold should derived accent tokens
  enforce?
- Should live theme changes update already-open `PlayerWindow` instances immediately, or apply on next
  popout like compact mode?

# Popout control customization - design

## Goals

Add a small, Phase 2 friendly customization surface for the controls the user sees most often:
the active color of **Pin** and **Fade**, plus the idle delay before Popout Player controls fade.
The change should preserve today's defaults, keep the dark PiPlay identity intact, and prepare the
settings shape for later Phase 4 whole-window opacity without shipping unsafe transparency behavior.

The happy path stays unchanged: Pin still controls topmost behavior, Fade still controls only the
Popout Player chrome strip, and disabling Fade still restores the MVP "controls always visible"
behavior. This pass does not make WebView2 transparent, does not add click-through, and does not make
whole-window opacity part of the Fade toggle.

This document is the implementation contract for the first customization slice. It intentionally
chooses exact settings fields, palette keys, and fade-delay presets so the code pass does not reopen
scope while wiring the UI.

## Requirements served

- Spec section 5.2 and 22.2 / `REQ-UI-01`, `REQ-UI-02` - keep dark-theme completeness, icon
  coherence, and active-color behavior verifiable while adding customization.
- Spec sections 6.2, 6.3, and 7.1 - customize Fade and Pin without changing their meanings.
- Spec section 7.3 and ADR-0006 - leave a clean path for later whole-window opacity while keeping
  click-through / pass-through transparency out of scope.
- `Q-8` - visible Popout Player remains interactable; no hidden input trap.
- Spec section 12.6 / 26.4 - settings persist atomically and recover safely from invalid values.

## Acceptance criteria

- The user can choose the active **Pin** color from a fixed set of dark-theme-safe swatches.
- The user can choose the active **Fade** color independently from the same swatch set.
- Default settings preserve the current visual behavior: Pin and Fade active states use the current
  cyan accent unless the user changes them.
- The persisted fields live under `PlayerSettings` as `PinAccent`, `FadeAccent`, and
  `FadeIdleDelayMs`, serialized as `pinAccent`, `fadeAccent`, and `fadeIdleDelayMs`.
- Valid accent keys for the first slice are `cyan`, `violet`, `green`, and `amber`.
- The Source Window Pin and Popout Player Pin use the same configured Pin accent.
- The Popout Player Fade toggle uses the configured Fade accent.
- The user can choose the controls-fade idle delay from constrained values, with the current
  2500 ms delay as the default.
- Valid fade-delay preset values are 1500 ms, 2500 ms, and 4000 ms.
- Invalid or missing settings are sanitized back to safe defaults on load.
- Reset app state restores the default Pin/Fade colors and default fade delay.
- The settings UI uses color swatches rather than free-form text, and every swatch has an accessible
  name/tooltip.
- No custom hex picker ships in this pass.
- No whole-window opacity UI ships in this pass; existing `PlayerSettings.IdleWindowOpacity` remains
  internal/deferred until Phase 4 tests cover input, rendering, and performance.
- No click-through, pointer pass-through, transparent hit testing, or transparent WebView2 behavior
  is introduced.

## Palette and presets

| Accent key | Display name | Brush resource | Token value | Notes |
|---|---|---|---|---|
| `cyan` | Cyan | `AccentCyan` | `#FF00D4FF` | Default; preserves current Pin/Fade active behavior. |
| `violet` | Violet | `AccentViolet` | `#FFA78BFA` | New customization token; do not reuse current `AccentPurple` because it is too low-contrast for active toggle glyphs on hover surfaces. |
| `green` | Green | `AccentGreen` | `#FF38D996` | New customization token. |
| `amber` | Amber | `AccentAmber` | `#FFFFC857` | New customization token. |

| Delay key | Display name | Value | Notes |
|---|---|---|---|
| `short` | Short | 1500 ms | Fast fade for users who want controls out of the way. |
| `normal` | Normal | 2500 ms | Default; matches `FadePolicy.IdleDelayMs`. |
| `long` | Long | 4000 ms | Slower fade without making controls feel stuck open. |

Implementation can store only the millisecond value, but the UI should present the named presets.
Any value outside the preset set normalizes to 2500 ms.

## Settled decisions

1. **Use a fixed palette, not arbitrary color input.** Swatches are enough for the user need and are
   testable for contrast. A hex picker would require validation, previews, bad-value recovery, and
   accessibility review that are not needed yet.

2. **Configure Pin and Fade independently.** The user explicitly wants to change those button colors;
   keeping two fields avoids a forced "one accent fits all" decision while remaining small.

3. **Pin accent is global across both PiPlay surfaces.** Pin means the same topmost state in the
   Source Window and Popout Player, so the active color should match across both.

4. **Fade accent is Popout-only.** Fade is only exposed in the Popout Player today; do not add a
   Source Window Fade concept while chrome fade remains Phase 4.

5. **Add fade-delay presets, not an unconstrained slider.** Short = 1500 ms, Normal = 2500 ms, and
   Long = 4000 ms cover the realistic tuning need, keep the UI compact, and avoid odd values that make
   fade feel broken or twitchy.

6. **Keep transparency separate.** This pass can document and preserve the Phase 4 path, but it must
   not surface opacity controls until the implementation proves `Q-8`: the player stays draggable,
   clickable, and recoverable at every allowed opacity.

7. **SettingsWindow owns input only; MainWindow owns persistence and live application.** This matches
   the current privacy-action pattern: the settings dialog gathers a user decision, while MainWindow
   applies app policy and saves with `SettingsService`.

8. **Do not add profile overrides yet.** Profiles already carry some Phase 2 playback fields, but
   control colors are a global appearance preference for this pass. Per-profile appearance would add
   precedence rules and edit UI that are not needed for the first version.

## Non-goals / out of scope

- Arbitrary custom hex/RGB color entry.
- Downloaded themes, importing/exporting themes, or a full theme editor.
- Per-profile color or fade-delay overrides.
- Source Window chrome fade.
- Whole-window opacity UI or behavior.
- Click-through / mouse pass-through windows.
- Transparent WebView2 or transparent player content.
- Global hotkeys for recovering from transparency modes.

## Testing approach

- **Logic tests:** `SettingsServiceTests` for default values, round-trip, and sanitizing invalid
  `PinAccent`, `FadeAccent`, and fade-delay values. Add focused tests for the palette/normalization
  helper.
- **Markup tests:** `XamlInvariantTests` should assert the new settings controls exist, every swatch
  has a tooltip/name, every static resource resolves, and the palette colors meet contrast thresholds
  against the dark surfaces they appear on. Active toggle glyphs should meet at least 3:1 against
  `SurfaceHover`; text labels still follow the existing 4.5:1 text threshold.
- **WPF runtime tests:** construct `SettingsWindow`, `MainWindow`, and `PlayerWindow` without showing
  them; verify the selected appearance values bind/apply to the Pin/Fade toggles and styles resolve.
- **Manual visual QA:** capture Source Window Pin, Popout Pin, and Popout Fade active states for at
  least two palettes at 100 percent and a fractional DPI. Confirm controls remain readable before and
  after idle fade, and that the player remains interactable.
- **Deferred Phase 4 QA:** when opacity is later surfaced, run the section 7.3 / QA checklist tests:
  minimum 45 percent normal floor, hover restores 100 percent, no click-through, and rendering/input
  remain correct with WebView2.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Models/AppSettings.cs` | Add `PinAccent = "cyan"`, `FadeAccent = "cyan"`, and `FadeIdleDelayMs = 2500` under `PlayerSettings`. |
| `src/PiPlay/Services/SettingsService.cs` | Sanitize invalid accent keys and fade-delay values. |
| `src/PiPlay/Services/PlayerAppearancePolicy.cs` | New small helper for palette keys, display names, brush resource keys, and normalization. |
| `src/PiPlay/Services/FadePolicy.cs` | Keep current constants as defaults/bounds; allow PlayerWindow to use the configured idle delay. |
| `src/PiPlay/Theme/Colors.xaml` | Add `AccentViolet`, `AccentGreen`, and `AccentAmber` token colors/brushes. Keep existing `AccentCyan` as the default. |
| `src/PiPlay/Theme/ControlStyles.xaml` | Refactor `PinToggle` so its checked accent is supplied per control instead of hardcoded to `AccentCyan`. |
| `src/PiPlay/MainWindow.xaml.cs` | Apply configured Pin accent to the Source Window Pin; pass appearance settings to PlayerWindow; persist SettingsWindow appearance changes. |
| `src/PiPlay/PlayerWindow.xaml` | Keep Fade/Pin controls in place; allow per-control active accent and tooltip/name updates. |
| `src/PiPlay/PlayerWindow.xaml.cs` | Apply Fade/Pin accents and configured fade idle delay; expose an internal test seam if needed. |
| `src/PiPlay/SettingsWindow.xaml` | Add an Appearance section with Pin color swatches, Fade color swatches, and fade-delay presets. |
| `src/PiPlay/SettingsWindow.xaml.cs` | Own input state and expose appearance changes back to MainWindow without writing settings directly. |
| `tests/PiPlay.Tests/SettingsServiceTests.cs` | Add defaults, round-trip, and sanitize coverage for customization fields. |
| `tests/PiPlay.Tests/FadePolicyTests.cs` | Add fade-delay bounds/normalization coverage if the policy owns those values. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | Add named-control, resource, tooltip/name, and contrast coverage. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Add runtime style/application coverage for settings and player/source toggles. |
| `docs/CHANGELOG.md` | Add a Phase 2 customization entry when implemented. |
| `docs/QA_Checklist.md` | Add manual checks for customized Pin/Fade colors and keep Phase 4 opacity checks separate. |

## Docs & changelog impact

Implementation should update `docs/CHANGELOG.md` because the user-visible settings surface changes.
It should also update `docs/QA_Checklist.md` with a small manual visual pass for customized Pin/Fade
colors. No ADR change is needed unless the implementation attempts to surface whole-window opacity or
alter ADR-0006's no-click-through decision.

## Deferred decisions

- Whether compact mode owns any separate appearance settings remains a compact-mode decision.
- Whether Phase 4 whole-window opacity needs a recovery affordance remains deferred until opacity is
  actually surfaced and tested against `Q-8`.

# PiPlay PR #21 override-model review

Date: 2026-06-11
Scope: GitHub PR #21, `fix(theme): honor nullable behavior overrides through the Settings apply path`
Head reviewed: `6a5a9abd7dba9d3cfff7e9afc527799a0cb275e9`
Source reviewed: GitHub PR diff and comments for `espensev/PiPlay#21`
Status: no blocking findings found.

## Findings

No blocking issues found in the substantive P2 fix.

### P3 - Test nit: avoid magic slider percent

Copilot's comment on `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` is reasonable: the new preset-click test asserts the soft-glass active slider value as `92`. That is currently correct, but it is a brittle magic number. Compute it from `preset.DefaultActiveWindowOpacity` and `WindowOpacityPolicy.UiFloor` with the same rounding rule the UI uses.

This is not a behavioral blocker.

### P3 - Optional consistency note: invalid writer inputs normalize differently than settings load

`ThemeSettingsWriter.Apply` normalizes invalid explicit opacity overrides through `WindowOpacityPolicy.Normalize`, so an invalid value becomes an explicit `1.0` override. `SettingsService.NormalizeOptionalOpacity` repairs invalid persisted optional opacity values to `null`, which means "follow preset default."

This does not affect the real Settings path because the dialog receives sanitized values and sliders emit valid values. Keep it as-is if the writer is considered a UI payload mapper rather than a corrupted-file repair path. If the writer is meant to be the general theme-settings repair contract, consider sharing the optional-opacity normalization behavior.

## P2 Review

The P2 issue from `p2-p3-code-review.md` is fixed.

The old failure mode was:

- `SettingsWindow` had one coarse `AppearanceChanged` flag.
- Clicking only an accent chip set that flag.
- `MainWindow.ApplyPlayerPreferences` then wrote strip auto-hide and both opacities back as concrete theme values.
- A schema-3 user who was following preset defaults became silently detached from those defaults forever.

PR #21 fixes that path:

- `SettingsWindow` now exposes `ActiveOpacityOverride`, `IdleOpacityOverride`, and `StripAutoHideOverride` as nullable values.
- Effective display and preview values are computed as `override ?? preset default`.
- Accent-only changes leave all three behavior overrides null.
- Touching one behavior control creates an override only for that control.
- Preset clicks reset behavior overrides to null and display the new preset defaults.
- `MainWindow` passes nullable overrides across the dialog boundary instead of effective values.
- `ThemeSettingsWriter.Apply` writes nullable overrides to `Theme` unchanged while mirroring effective values into legacy `Player` fields.

That directly addresses the state-model bug and answers the open design question the right way: preset behavior defaults remain live until the user deliberately touches the behavior control.

## P3 Review

The copy fix is also correct.

The old hint promised live theme/accent apply, but the code only live-previewed opacity. PR #21 changes the text to close-to-apply:

`Theme, accent, and corners apply to every open window when you close Settings - no restart needed.`

That matches the current implementation:

- Opacity sliders live-preview through `OpacityPreviewChanged`.
- The Settings dialog self-previews native corners.
- Theme/accent/corner selection applies to main and open player windows when the dialog closes.

## Test Coverage

The PR adds the right tests for the regression:

- Logic-lane writer test for accent-only null preservation.
- Writer test for effective legacy mirrors.
- WPF test for accent-only behavior staying on preset defaults.
- WPF preset-click test updated to assert null overrides plus displayed defaults.

I did not run the PR branch locally because the branch was not available in the current checkout and I did not fetch it. The GitHub connector reports PR #21 as open and mergeable; combined status returned no statuses.

## Merge Recommendation

I would merge PR #21 after either accepting or addressing the Copilot test nit. The nit is small enough that it should not block the P2 fix if you want the semantic repair landed now.

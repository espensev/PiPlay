# PiPlay theme pass code review

Date: 2026-06-11
Scope: current `main` after PR #19 merge/pull, focused on the theme resolver, schema migration, Settings UI, runtime apply path, and tests.
Status: review artifact. No code changes made.

## Findings

### P2 - Accent-only Settings changes materialize nullable preset defaults as explicit overrides

`ThemeSettings` now documents `StripAutoHide`, `ActiveWindowOpacity`, and `IdleWindowOpacity` as nullable overrides: null means "follow the selected preset default." The resolver and schema-3 migration implement that correctly.

The Settings apply path collapses that model as soon as any appearance field changes. `SettingsWindow` has one coarse `AppearanceChanged` flag; clicking only an accent chip sets it at `src/PiPlay/SettingsWindow.xaml.cs:157-161`. `MainWindow.SettingsButton_Click` then calls `ApplyPlayerPreferences` for the whole appearance payload at `src/PiPlay/MainWindow.xaml.cs:513-516`, and `ApplyPlayerPreferences` writes all three behavior values back as explicit overrides at `src/PiPlay/MainWindow.xaml.cs:573-578`.

Impact:

- A fresh schema-3 settings file can have behavior nulls that follow preset defaults.
- If the user opens Settings and changes only the accent, those nulls are replaced with concrete active opacity, idle opacity, and strip auto-hide values.
- From then on, the user no longer follows future preset behavior defaults, even though they never touched those behavior controls.
- This contradicts the comment in `SettingsWindow_preset_click_adopts_the_preset_defaults` that "Controls touched afterwards become overrides" (`tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:266-269`). In the current apply path, closing the dialog after any appearance change makes the behavior controls overrides.

Suggested fix:

- Track dirty state per behavior control, not one global `AppearanceChanged`.
- Pass nullable behavior overrides from `SettingsWindow` to `MainWindow`.
- On preset click, reset behavior overrides to null unless the user then touches the strip/opacity controls.
- Add a regression test for "accent-only apply preserves null behavior overrides" and, if desired, "preset click stores null behavior overrides until a behavior control is touched."

### P3 - Settings copy promises immediate theme/accent application, but only opacity previews live

The visible Settings text says:

`Themes and the accent apply right away to the main window and to new Popout Players.`

That is in `src/PiPlay/SettingsWindow.xaml:134-135`. The implementation does not do that. `ThemePreset_Click` and `AccentChip_Click` update only the dialog's internal state and selected chips (`src/PiPlay/SettingsWindow.xaml.cs:128-161`). `MainWindow` subscribes only to `OpacityPreviewChanged` at `src/PiPlay/MainWindow.xaml.cs:498`, and it applies theme resources only after `ShowDialog()` returns true, via `ApplyPlayerPreferences` at `src/PiPlay/MainWindow.xaml.cs:513-588`.

Impact:

- Clicking a preset or accent while Settings is open does not restyle the main window.
- Existing popouts get live opacity preview, but not live accent/theme/corner preview.
- The dialog's own native corner mode previews, but that is much narrower than what the copy promises.

Suggested fix:

- Either change the text to match the current close-to-apply behavior, or add a real theme/accent preview event from `SettingsWindow`.
- If live preview is added, also add cancel/revert logic mirroring the opacity preview rollback at `src/PiPlay/MainWindow.xaml.cs:500-504`.
- Add a focused WPF test that proves preset/accent clicks either do not claim live apply or actually call the live apply path.

## What Looks Solid

- Resolver semantics are coherent: schema-3 null behavior values use preset defaults, explicit overrides win, invalid theme ids fall back safely, and null theme blocks still fall back to legacy `PlayerSettings`.
- Schema migration is correctly two-layered: missing theme blocks seed through `ThemeSettings.FromLegacy`, while schema `< 3` theme blocks backfill null behavior fields once from `PlayerSettings`.
- Runtime popout creation and live reapply now use effective theme values rather than stale player fields.
- DWM corner mode is decoupled from opacity and covered by real-HWND tests.
- Palette/radius resources are applied by replacement, and XAML invariant tests cover `DynamicResource` reachability and hardcoded-radius drift.

## Open Questions

1. Should preset-owned behavior defaults remain "live" after a user selects a preset, or is a preset click meant to snapshot the preset's current behavior values as explicit overrides? The code snapshots them; the nullable override model and comments read like they should remain live until touched.
2. Should theme/accent selection be close-to-apply or live-preview? The current implementation is close-to-apply for theme/accent, live-preview for opacity, and live-preview only on the Settings dialog for corners.

## Review Verdict

No blocker found in the resolver, migration, palette/radius, or DWM-corner core. The pass is structurally much better than the stale draft.

The main remaining issue is the Settings state model: it has per-field override semantics in storage but only coarse dirty/apply semantics in the UI. That is the next thing I would tighten before another external review, because it is exactly the kind of mismatch a reviewer will find by tracing a simple accent-only Settings save.

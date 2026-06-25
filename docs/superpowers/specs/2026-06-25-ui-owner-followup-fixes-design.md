# UI owner follow-up fixes - design

## Goals

Close the low-risk defects from the 2026-06-23 owner appearance review that can be fixed without changing WebView2 hosting architecture or reversing shipped profile-accent behavior:

- Make primary accent buttons visually quieter by removing the special 2 DIP outline.
- Give the Source Placeholder a direct `Show popout` action that uses the existing single-popout activation path.
- Preserve the current compact-player, profile-accent, theme-preset, and DWM-corner semantics until their larger decisions are explicitly made.

## Requirements served

- `REQ-UI-01`: chrome remains readable and intentionally themed.
- `REQ-UI-02`: action labels and accessible names match the current command.
- Spec section 13.3 / source placeholder: the main window must not leave the user stuck when playback is detached.
- ADR-0005 / single-player lifecycle: while a popout exists, actions focus the existing window rather than creating a duplicate.

## Acceptance criteria

- `AccentButton` uses the shared `BorderThicknessDefault` token instead of a hardcoded 2 DIP outline.
- `PopOutButton`, Settings `Done`, and runtime-recovery primary actions keep the same dark fill and accent outline color, but with the standard 1 DIP default border.
- The Source Placeholder shows a visible `Show popout` action while playback is detached.
- Clicking the placeholder `Show popout` action calls the same activation helper as the toolbar `Show popout` state.
- Existing placeholder fallback-note behavior still surfaces and clears correctly.
- Markup/runtime tests cover the placeholder action and the quieter accent-button border contract.

## Settled decisions

1. Use `BorderThicknessDefault` for `AccentButton`.
   The theme system already defines this as the shared 1 DIP control-outline token. Reusing it fixes the "fat border" complaint without creating a new border-strength feature.
2. Add `Show popout` only in this pass.
   The owner review also requested `Restore video here`, but that is a different return/detach behavior and interacts with `REQ-RETURN-01`. The existing safe path is to focus/restore the already-open popout.
3. Keep the button text literal and direct.
   The placeholder is an active recovery state, not a tutorial surface. `Show popout` is enough.
4. Do not change profile accent behavior here.
   Current tests and shipped behavior resolve active profile colors as app accent overrides. Reversing that model needs its own sign-off and migration tests.
5. Do not change outer corner architecture here.
   The large rounded-card target remains blocked by the current HWND/WebView2 airspace shape. This pass does not attempt a DWM workaround.

## Non-goals / out of scope

- No new theme preset, including `Blackout`.
- No border mode/strength slider.
- No shadow-strength control.
- No WebView2 airspace lift or large rounded-card outer shadow.
- No profile-accent reversal.
- No main-window Browse/Cinema/Compact mode model.
- No `Restore video here` action.
- No release version bump in this pass.

## Testing approach

- Markup tests assert `AccentButton.BorderThickness` references `BorderThicknessDefault`.
- Runtime tests assert a realized `AccentButton` resolves the shared border thickness token.
- Markup/runtime tests assert the Source Placeholder contains a named `Show popout` button with the accent style, correct click handler, tooltip, and accessible name.
- Existing popout action state and placeholder fallback-note runtime tests remain green.
- Focused validation uses the UI/theme test slice plus `git diff --check`.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Theme/ControlStyles.xaml` | Change `AccentButton` border thickness from hardcoded `2` to `{DynamicResource BorderThicknessDefault}` and update the style comment. |
| `src/PiPlay/MainWindow.xaml` | Add `PlaceholderShowPopoutButton` to the Source Placeholder. |
| `src/PiPlay/MainWindow.xaml.cs` | Add a handler that calls `ActivateExistingPlayer()`. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | Add markup coverage for the placeholder action and accent-button border token. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Add runtime coverage for the placeholder action and realized accent-button border thickness. |
| `docs/CHANGELOG.md` | Add an Unreleased fix note. |

## Unresolved decisions

- Whether profile colors become identity markers only.
- Whether the app accepts DWM-limited corners or moves to a different WebView2 hosting architecture for large rounded-card silhouettes.
- Whether accent readability becomes text-only instead of a global apply/save gate.
- Whether to add `Restore video here` as a separate action.

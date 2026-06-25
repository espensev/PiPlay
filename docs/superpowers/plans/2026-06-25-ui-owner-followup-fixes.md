# UI owner follow-up fixes - implementation plan

**Spec:** `docs/superpowers/specs/2026-06-25-ui-owner-followup-fixes-design.md`

**Goal:** land the immediate owner-review UI fixes that are already decided: quieter accent-button borders and a direct Source Placeholder `Show popout` action.

**Result:** Complete in the current checkout. `AccentButton` now uses the shared border token, the Source Placeholder has a direct `Show popout` action, focused UI/theme validation passed with 281 tests, and the full test suite passed with 681 tests.

## Tasks

- [x] **Task 1 - Scope and evidence review.**
  - Review the folded owner-review summary in `docs/SPEC_GAPS_AND_OWNERSHIP.md`.
  - Confirm the latest committed source change only handled popout action text rendering.
  - Confirm the current fixable seams:
    - `AccentButton` hardcodes `BorderThickness=2`.
    - Source Placeholder has static text but no direct action.

- [x] **Task 2 - Quiet the primary action border.**
  - Change `AccentButton.BorderThickness` to `{DynamicResource BorderThicknessDefault}`.
  - Update the style comment so it no longer documents a 2 DIP outline.
  - Add markup/runtime tests proving the shared token is used and realized.

- [x] **Task 3 - Add Source Placeholder `Show popout`.**
  - Add `PlaceholderShowPopoutButton` below the placeholder copy.
  - Wire it to a handler that calls `ActivateExistingPlayer()`.
  - Keep fallback-note display/clearing unchanged.
  - Add markup/runtime tests for name, style, handler, tooltip, and accessible name.

- [x] **Task 4 - Docs and validation.**
  - Add an Unreleased changelog entry.
  - Run focused UI/theme tests.
  - Run `git diff --check`.

## Validation

- `dotnet test PiPlay.sln --configuration Debug --filter 'FullyQualifiedName~XamlInvariantTests|FullyQualifiedName~WpfRuntimeTests|FullyQualifiedName~SettingsWindowAppearanceTests|FullyQualifiedName~ProfileAccentServiceTests|FullyQualifiedName~AccentColorPickerTests|FullyQualifiedName~ThemeCatalogTests|FullyQualifiedName~AccentReadabilityPolicyTests|FullyQualifiedName~PlaybackModePolicyTests' --nologo`: passed, 281 tests.
- `dotnet test PiPlay.sln --configuration Debug --nologo`: passed, 681 tests.
- `git diff --check`: passed. PowerShell reported LF-to-CRLF warnings for edited files.

## Deferred follow-ups

- Profile color reversal: global app accent plus profile identity marker.
- Accent validation relax: any accent color with readable foreground chosen per text surface.
- Corner/card architecture: DWM-limited corners versus WebView2 airspace lift.
- Full main-window Browse/Cinema/Compact mode model.
- `Restore video here` detach/return action.

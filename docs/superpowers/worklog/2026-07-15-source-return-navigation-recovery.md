# Session worklog - Source return and navigation recovery (2026-07-15)

Saved record of the findings-first Source Window review and focused remediation pass after the
v0.11.0-b34 Focused Popout work.

## Request

> Review the normal Source Window after exit/return, compare its navigation with the overlay, evaluate
> Bring back / Pin / Popout logic, then proceed with the suggested fix.

## What was reviewed

- Deployed Stable Source and Popout window state, portable settings, and the owner screenshot.
- Main/Player XAML and lifecycle code, return replay, Auto gating, placement/native sizing, independent
  Pin persistence, Focused host actions, product spec, ADR-0005, QA, and UI priorities.
- Existing Logic/Markup/WPF tests and the current dirty-tree boundary. Unrelated local-CI work was left
  untouched.

## Decisions

- Fix the invalid sub-minimum Source restore before adding a new main-window layout or auto-hide mode.
- Keep the 42 + 50 DIP Source chrome; consolidate profile CRUD and collapse only transfer text at
  compact width.
- Keep Show Popout (focus/recovery) separate from Bring video back (capture/close/transfer).
- Preserve separate Source/Popout Pin preferences, but temporarily suspend actual Source topmost while
  the player owns playback and restore the actual pre-popout state, including profile-derived Pin.
- Hold Returning through pending replay and disable hidden Source navigation/profile commands.

## Implementation

- New: dated design/plan/review records; Source lifecycle regressions; dark menu, Pin-affordance, and
  Focused-script tests; this worklog.
- Edited: Source toolbar and lifecycle, native minimum tracking, Popout Pin copy, Focused Pin copy,
  shared dark menu styles, living spec, ADR-0005, QA, UI priorities, and Unreleased changelog.
- Review remediation: captured actual Source Topmost instead of assuming the global setting, and
  canceled/revalidated return replay when Clear Browser Data starts.

## Verification

- Focused integrated Source/menu/Pin/placement suite: **224 passed, 0 failed**.
- Full Debug solution suite: **959 passed, 0 failed**.
- No-bump RID-specific Release build: **0 warnings, 0 errors**.
- Spec preflight: pass. Final diff whitespace check: pass.
- Independent integration review: no blocker, high, or medium finding remains.
- Local visual smoke was attempted through the Windows inspection helper, but the Default-channel test
  executable did not expose a targetable window. The running Stable v0.11.0-b34 process was not changed;
  minimized/maximized return, Pin matrix, real DPI restore, menu rendering, and clear-during-live-replay
  remain deployed/manual QA rows.

## Disposition

- Working tree only; no version/build bump, commit, tag, push, or Stable publish.
- This change remains Unreleased. A sanctioned clean-source Stable publish is required before claiming
  deployed acceptance evidence.

## Commits

- None in this session.

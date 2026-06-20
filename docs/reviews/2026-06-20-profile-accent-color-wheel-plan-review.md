# Profile Accent Color Wheel Plan Review

Date: 2026-06-20
Scope:

- `docs/superpowers/specs/2026-06-20-profile-accent-color-wheel-design.md`
- `docs/superpowers/plans/2026-06-20-profile-accent-color-wheel.md`
- Current source baseline for theme apply, Settings close/apply flow, and profile selection/edit/delete.

## Verdict

The spec and plan are broadly code-grounded. The hardened color math, readability policy, global-vs-
resolved accent split, and profile persistence shape match the current code boundaries.

I found two implementation-gate gaps that would let a future implementation satisfy much of the plan
while still breaking the promised behavior. Both are now folded into the active spec/plan as explicit
test and implementation requirements.

## Findings

### P1 - Live accent preview needed a real non-applying dismissal path

Evidence:

- Current `SettingsWindow.CloseButton_Click` and `DoneButton_Click` both route through
  `DismissApplyingChanges` (`src/PiPlay/SettingsWindow.xaml.cs:140-149`).
- `DismissApplyingChanges` calls `CompleteDialog()` whenever `AppearanceChanged` is true
  (`src/PiPlay/SettingsWindow.xaml.cs:151-154`), which makes `ShowDialog()` return true.
- `MainWindow` only reverts live preview when `ShowDialog() != true`
  (`src/PiPlay/MainWindow.xaml.cs:500-504`).
- The color-wheel spec promises commit on Done and revert on dismiss-without-apply.

Why it matters:

With live preview, App-level accent resources, pin brushes, and popout appearance are changed before
persistence. If every visible close path commits once `AppearanceChanged` is true, a title-bar close
after preview is not a dismiss path. If the picker is sitting on an unreadable in-flight value, a disabled
Done button also does not protect the user from committing the last readable preview through close.

Disposition:

- Spec now requires Done as the commit affordance and requires close/Esc or an explicit Cancel path to
  dismiss without true so `MainWindow` reverts previewed accent surfaces.
- Plan Task 4 now requires failing tests for Done commit, close/Esc or Cancel revert, and construction
  seeding that does not dirty `AppearanceChanged`.

### P1 - Active-profile rename/delete/overwrite lifecycle was not pinned

Evidence:

- `LoadProfilesIntoCombo` sets `_loadingProfiles`, replaces `ItemsSource`, and resets
  `SelectedIndex = -1` (`src/PiPlay/MainWindow.xaml.cs:369-374`).
- `ProfilesCombo_SelectionChanged` exits while `_loadingProfiles` is true and only applies profile
  state on a live selected profile (`src/PiPlay/MainWindow.xaml.cs:382-387`).
- `DeleteProfileButton_Click` removes the profile, saves, and reloads the combo without any active
  profile cleanup path today (`src/PiPlay/MainWindow.xaml.cs:456-468`).
- The original plan tested selection, startup restore, and commit routing, but not active profile
  deletion, rename, or overwrite as colorless.

Why it matters:

After the feature adds `ActiveProfileName`, relying on `Sanitize` to clear dangling names at next load
is too late for the live app. Deleting or renaming the active profile can leave the persisted active name
and visible accent stale, especially because the reload path suppresses selection-change side effects.

Disposition:

- Spec now states that active profile edit/delete/overwrite must re-resolve immediately in the live
  `MainWindow` path.
- Plan Task 7 now requires tests and implementation for active delete, active rename, and active
  overwrite-as-colorless, exercising the real combo reload/selection-suppression shape.

## Verification

- Read the current source paths listed above.
- Read the new spec and implementation plan.
- Folded the two follow-ups into the active spec/plan docs.

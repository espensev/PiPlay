# Profile edit / validation — implementation plan

**Spec:** `docs/superpowers/specs/2026-06-06-profile-edit-validation-design.md`

**Goal:** Add profile **editing** (Name + URL, with inline URL validation) and **delete** to the
Source Window toolbar, satisfying spec §17 Phase 2 ("profile editing, proactive validation UI"),
without changing the MVP save/load happy path.

**Result:** implemented. Verified on 2026-06-06 with `dotnet test PiPlay.sln --configuration Debug`
= **148** passing. The implementation includes 5 service-logic update tests plus markup coverage
for the Edit/Delete profile buttons.

## Tasks

- [x] **Task 1 — `ProfileService.Update` (TDD, Layer 2).**
  - Write failing tests in `ProfileServiceTests.cs`:
    - `Update_changes_url_in_place_and_keeps_position`
    - `Update_rename_to_free_name_keeps_position`
    - `Update_rename_onto_other_profile_reports_conflict_and_does_not_mutate`
    - `Update_rename_with_overwrite_removes_collision_and_keeps_position`
    - `Update_unknown_original_returns_NotFound`
  - Add `public enum ProfileUpdateOutcome { Updated, NotFound, NameConflict }` and
    `Update(AppSettings, string originalName, Profile updated, bool overwrite = false)`:
    find original by name (case-insensitive, `List.FindIndex`); if the updated name matches a
    *different* index → return `NameConflict` unless `overwrite` (then `RemoveAt(collision)`, and
    decrement `index` if the removal was before it); replace in place `list[index] = updated`;
    return `Updated`. Missing original → `NotFound`.
  - Verify the 5 tests pass; full suite green.
  - Commit: `feat(profiles): position-preserving, collision-aware ProfileService.Update (spec 17)`

- [x] **Task 2 — `Prompt.EditProfile` editor with inline validation.**
  - Add `public static (string Name, string Url)? EditProfile(Window owner, string name, string url)`
    built on `BuildShell`: a "Name" label + `DarkTextBox` (prefilled), a "URL" label + `DarkTextBox`
    (prefilled), a collapsed error `TextBlock` (Foreground `DangerPin`), Save (default) + Cancel.
    On Save: trim name; empty name → show "Enter a name." and keep open; else
    `ProfileService.ValidateUrl(url)` — if not ok show its error and keep open; else set
    `DialogResult = true` and return the trimmed `(name, url)`. Cancel/title-bar close → `null`.
  - Build only (ShowDialog path, not unit-tested per the established convention).
  - Commit: `feat(profiles): themed Edit-profile dialog with inline URL validation (REQ-UI-01)`

- [x] **Task 3 — Toolbar buttons + handlers + enablement.**
  - `MainWindow.xaml`: after `SaveProfileButton`, add
    `EditProfileButton` (`Content="&#xE70F;"`, `ToolTip="Edit selected profile"`) and
    `DeleteProfileButton` (`Content="&#xE74D;"`, `ToolTip="Delete selected profile"`), both
    `Style="{StaticResource IconButton}"`, `IsEnabled="False"`, with `Click` handlers.
  - `MainWindow.xaml.cs`:
    - `UpdateProfileCommandState()` → `EditProfileButton.IsEnabled = DeleteProfileButton.IsEnabled =
      ProfilesCombo.SelectedItem is Profile;` call it at the end of `ProfilesCombo_SelectionChanged`
      and `LoadProfilesIntoCombo`.
    - `EditProfileButton_Click`: if `SelectedItem is not Profile p` return; call
      `Prompt.EditProfile(this, p.Name, p.Url)`; if null return; build `updated` from `p` (clone:
      keep `Mode/Topmost/FadeEnabled/Bounds`, set new `Name`/`Url`); call `ProfileService.Update`;
      on `NameConflict` → `Prompt.AskConfirm("Overwrite profile?", …, "Overwrite")`; if yes retry
      with `overwrite: true`, else return; on success `_settingsService.Save(_settings)`,
      `LoadProfilesIntoCombo()`, `Log.Info("Profile edited.")`.
    - `DeleteProfileButton_Click`: if `SelectedItem is not Profile p` return; `Prompt.AskConfirm(
      "Delete profile?", $"Delete the profile \"{p.Name}\"? This can't be undone.", "Delete",
      danger: true)`; if confirmed `ProfileService.Remove(_settings, p.Name)`,
      `_settingsService.Save(_settings)`, `LoadProfilesIntoCombo()`, `Log.Info("Profile deleted.")`.
  - Build; run `dotnet test --filter Category=Wpf` (no regression).
  - Commit: `feat(profiles): edit + delete selected profile from the Source Window (spec 17)`

- [x] **Task 4 — Markup invariant test (Layer 1).**
  - In `XamlInvariantTests.cs`, extend the required-`x:Name` assertion (or add one) to include
    `EditProfileButton` and `DeleteProfileButton` so the toolbar wiring can't silently regress.
  - Verify it passes.
  - Commit: `test(ui): assert Edit/Delete profile buttons exist in MainWindow markup`

- [x] **Task 5 — CHANGELOG + full verify + PR.**
  - `docs/CHANGELOG.md`: add an `[Unreleased]` "Added — Phase 2" bullet for profile edit/delete +
    inline validation; remove "profile edit/validation" from the "Planned — Phase 2 (remaining)"
    bullet (leaving `Auto`, release publish profiles, Phase 2 QA).
  - `dotnet test` → current verified count is 148/148.
  - Commit: `docs(changelog): Phase 2 profile edit + delete with inline URL validation`
  - Push, open PR to `main` referencing the design spec and spec §17.

## Self-review

- Spec §17 "profile editing" → Tasks 2+3. "proactive validation UI" → Task 2 inline validation.
  "duplicate names prompt overwrite/rename" → Task 1 `NameConflict` + Task 3 overwrite prompt.
  "validate URLs before saving" → Task 2 (`ValidateUrl` gate). "fail gracefully" → no close on
  invalid, no write.
- Ownership: profile commands stay in `MainWindow`; Settings window untouched.
- Risk concentrated in `Update` (covered by 5 Layer-2 tests); UI dialog follows the existing
  untested-ShowDialog convention; markup invariant guards the wiring.
- Current verified test count: 148/148.

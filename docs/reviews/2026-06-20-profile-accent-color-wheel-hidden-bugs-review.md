# Profile accent color wheel hidden-bugs review

Date: 2026-06-20

Scope: bug-focused review of the current PiPlay working tree after the per-profile accent color wheel implementation and visual/manual verification.

## Findings

### P1 - Color wheel captures the mouse and never releases it

`src/PiPlay/Controls/AccentColorPicker.xaml.cs:167-170` calls `HueSatDisc.CaptureMouse()` when the color wheel is pressed, but there is no `MouseLeftButtonUp`, `LostMouseCapture`, `MouseLeave`, or `Unloaded` path that calls `ReleaseMouseCapture()`.

Impact: after the first click/drag on the hue wheel, the image can keep mouse capture. Subsequent clicks in Settings, the profile editor, or the main window can be misrouted or feel ignored until another control steals capture. This is a classic hidden WPF input bug because static tests pass and the first drag appears to work.

Recommended fix: release capture on mouse up and on cleanup/lost-capture paths. Add a focused WPF runtime test that invokes the wheel mouse down/up path and asserts `Mouse.Captured` is no longer the disc.

Status: Addressed. `AccentColorPicker` now hooks `MouseLeftButtonUp`, releases capture on mouse-up, releases if movement continues after the left button is no longer pressed, and releases on `Unloaded` (`src/PiPlay/Controls/AccentColorPicker.xaml.cs:37-39`, `src/PiPlay/Controls/AccentColorPicker.xaml.cs:171-194`). Regression coverage: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:199-209` asserts the capture release paths. The attempted global `Mouse.Captured` runtime assertion was not stable in the headless WPF test runner, so the regression is source-invariant based.

### P2 - Profile editor can save a stale accent after invalid RGB input

`src/PiPlay/Controls/AccentColorPicker.xaml.cs:131-142` handles invalid RGB text by marking the picker unreadable and showing the warning, but it leaves `SelectedColor` unchanged. `src/PiPlay/Prompt.cs:270-278` then saves `accentPicker.SelectedColor` and validates that string only; it does not also require `accentPicker.IsSelectedReadable`.

Impact: in the profile editor, a user can enable "Profile accent", type an invalid RGB channel such as `999`, see the warning state, then click `Save`. Because `SelectedColor` still holds the previous readable color, the dialog can close and persist that stale value instead of blocking the save. Settings does not have this exact failure because its `Done` button is bound to `AccentPicker.IsSelectedReadable`.

Recommended fix: bind/disable the profile editor `Save` button from `AccentColorPicker.IsSelectedReadable`, and keep the click handler guard as `useAccent.IsChecked != true || (accentPicker.IsSelectedReadable && ProfileService.ValidateAccent(accentPicker.SelectedColor))`. Add a profile-editor WPF test for invalid RGB state.

Status: Addressed. The picker now raises `ReadabilityChanged` when RGB/hex readability changes, and the profile editor disables Save plus re-checks `CanSaveProfileAccent(...)` in the click path (`src/PiPlay/Controls/AccentColorPicker.xaml.cs:48`, `src/PiPlay/Prompt.cs:237-297`). Regression coverage: `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:898-912` verifies invalid RGB makes the profile accent unsaveable even while `SelectedColor` still holds the prior readable color.

### P2 - Saving over an existing profile drops existing profile-specific settings

`src/PiPlay/MainWindow.xaml.cs:424-433` asks for overwrite confirmation, then constructs a new `Profile` with only `Name`, `Url`, and `Topmost`. `src/PiPlay/Services/ProfileService.cs:32-39` removes the existing profile and appends the replacement.

Impact: if a profile already has `Mode`, `AccentColor`, `FadeEnabled`, or `Bounds`, using "Save profile" with the same name silently clears those fields. With per-profile accents, this is now user-visible: saving the current URL over a custom-colored profile removes the custom accent without going through the edit dialog.

Recommended fix: when overwriting from the quick-save path, merge the existing profile's non-URL settings into the replacement unless the UI explicitly says it will reset all profile options. Add a regression test for overwriting a profile that has `Mode` and `AccentColor`.

Status: Addressed. The quick-save path now finds the existing profile and saves through `ProfileService.CreateQuickSaveProfile(...)`, which refreshes URL/current source pin state while preserving `Mode`, `AccentColor`, `FadeEnabled`, and `Bounds` (`src/PiPlay/MainWindow.xaml.cs:424-434`, `src/PiPlay/Services/ProfileService.cs:29-44`). Regression coverage: `tests/PiPlay.Tests/ProfileServiceTests.cs:47-76`.

## Validation Run

- `dotnet build PiPlay.sln --configuration Debug`: passed
- `dotnet test PiPlay.sln --configuration Debug --no-build --filter "FullyQualifiedName~AccentColorPicker|FullyQualifiedName~ProfileService|FullyQualifiedName~SettingsWindow|FullyQualifiedName~MainWindow"`: 56 passed
- `dotnet test PiPlay.sln --configuration Debug --no-build`: 663 passed
- `git diff --check`: no whitespace errors; Git reported existing LF-to-CRLF normalization warnings

## Notes

The automated suite and the final visual/manual evidence remain green after the hidden-bug fixes. These findings were hidden behavioral gaps: they required specific user interaction sequences or quick-save overwrite behavior rather than build/runtime startup coverage.

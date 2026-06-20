# Profile Accent Color Wheel — Efficiency and Simplification Review

Date: 2026-06-20

Scope: review-only pass over the current PiPlay working tree after the per-profile accent color wheel, hidden-bug fixes, and build/deploy workflow adjustments. Focus: regressions, code efficiency, code blobs, and simplification targets.

## Verdict

No P1/P2 correctness blockers found in this pass.

Follow-up fix status: all review findings below are addressed in the working tree. The remaining note on drag-preview allocation is intentionally left as "monitor only" because current validation/manual behavior does not show drag stutter.

Outstanding backlog: deferred follow-ups are tracked in `docs/superpowers/plans/2026-06-20-profile-accent-color-wheel.md` under `TEST-BACKLOG-01` and `PERF-BACKLOG-01`.

## Findings

### P3 — Settings copy overstates live preview behavior

`src/PiPlay/SettingsWindow.xaml:136-138` says "Theme, accent, and corners preview live; Done applies the changes." The implemented live preview surface is narrower:

- accent preview is raised through `AccentPreviewChanged` (`src/PiPlay/SettingsWindow.xaml.cs:204-210`) and subscribed by `MainWindow` (`src/PiPlay/MainWindow.xaml.cs:530-534`);
- opacity preview is raised by theme/slider changes (`src/PiPlay/SettingsWindow.xaml.cs:198`, `src/PiPlay/SettingsWindow.xaml.cs:267`);
- theme palette and app/native window corner application still land after `ShowDialog() == true` via `ApplyPlayerPreferences(...)` (`src/PiPlay/MainWindow.xaml.cs:550-554`, `src/PiPlay/MainWindow.xaml.cs:614-617`);
- `ApplyOwnCornerMode()` previews only the Settings dialog's own corner shape (`src/PiPlay/SettingsWindow.xaml.cs:200-226`).

This is not a data-loss bug, but it is a small UX regression: a user can click a theme preset, see only partial live feedback, and the hint says the whole theme/corner change is live.

Recommended simplification: either narrow the copy to the actual contract ("Accent and opacity preview live; Done applies theme and corner changes.") or implement a full theme/corner preview plus revert path.

Status: Addressed. The copy now says "Accent and opacity preview live; Done applies theme and corner changes." (`src/PiPlay/SettingsWindow.xaml`). `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` now pins the wording and rejects stale "chips" / overbroad live-preview text.

### P3 — Profile accent normalization has three policy surfaces

Profile accent policy is currently split across:

- `MainWindow.NormalizeProfileAccent(...)` (`src/PiPlay/MainWindow.xaml.cs:668-675`), which repairs unreadable colors to nearest readable;
- `SettingsService.NormalizeProfileAccent(...)` (`src/PiPlay/Services/SettingsService.cs:214-221`), which performs the same repair during load sanitize;
- `ProfileService.ValidateAccent(...)` (`src/PiPlay/Services/ProfileService.cs:106-107`), which validates but rejects unreadable values.

The behavior is defensible by call site, but the repeated hex/readability rule is a drift target. The next accent-policy tweak will have to update multiple locations.

Recommended simplification: centralize storage normalization as one method, for example `ProfileService.NormalizeAccentForStorage(string?)`, returning `null` for missing/invalid and nearest-readable for unreadable. Keep `ValidateAccent(...)` as a thin UI-gate wrapper if the editor still needs strict "block save until readable" semantics.

Status: Addressed. `ProfileService.NormalizeAccentForStorage(...)` owns the storage repair rule; `SettingsService` and `MainWindow` both call it, while `ValidateAccent(...)` remains the strict UI gate. Active-profile resolution/commit/reconcile moved to `ProfileAccentService`.

### P3 — Git stderr hardening is duplicated across release scripts

The native stderr fix is repeated in:

- `scripts/Build-PiPlay.ps1:267-299`
- `scripts/Publish-Stable.ps1:84-94`
- `scripts/Verify-StableDeploy.ps1:60-70`
- `scripts/Preflight-SpecGate.ps1:66-75`

The regression coverage currently checks for text snippets in each script (`tests/PiPlay.Tests/ReleaseScriptPolicyTests.cs:126-138`). That catches accidental removal, but it does not exercise behavior and will be brittle if the implementation is reshaped.

Recommended simplification: if these scripts continue to grow, factor the native-command/EAP handling into a dot-sourced helper or at least align the helper function names/signatures. A stronger follow-up test would execute a small stderr-emitting native command under `ErrorActionPreference = "Stop"` and assert the wrapper uses exit code, not stderr, as the failure signal.

Status: Addressed. The EAP/native-stderr wrapper is now `scripts/NativeCommand.ps1`, dot-sourced by Build, Publish, Verify, and Preflight. `Verify-StableDeploy.ps1` also routes its `git merge-base` check through the helper. `ReleaseScriptPolicyTests` now asserts the shared helper is used and that the copied EAP block does not reappear in each script.

### P3 — UI runtime tests are becoming a feature catch-all

`tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` is now 1,975 lines, and the color-wheel work adds unrelated clusters in the same file:

- Settings accent picker behavior (`tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:392-536`);
- MainWindow profile accent routing (`tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:660-754`);
- AccentColorPicker-specific control behavior (`tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:866-946`).

This is manageable today, but it slows review because tests for a reusable control, Settings appearance, and MainWindow profile routing all live in one runtime file.

Recommended simplification: split future work into `AccentColorPickerTests`, `SettingsWindowAppearanceTests`, and `MainWindowProfileAccentTests` while keeping shared STA setup in one helper. This matches the feature plan's intended shape and makes focused runs more obvious.

Status: Addressed. The color-wheel/appearance/profile-accent WPF tests were moved into `tests/PiPlay.Tests/Ui/AccentColorPickerTests.cs`, `tests/PiPlay.Tests/Ui/SettingsWindowAppearanceTests.cs`, and `tests/PiPlay.Tests/Ui/MainWindowProfileAccentTests.cs`. `WpfRuntimeTests.cs` dropped from 1,975 lines to about 1,613 lines.

### P3 — New MainWindow test seams are doing model work

The feature adds several internal `MainWindow` test hooks:

- `ReplaceSettingsForTests(...)`
- `ResolvedAccentColorForTests`
- `ActiveProfileNameForTests`
- `SelectProfileForTests(...)`
- `DeleteProfileForTests(...)`
- `RenameProfileForTests(...)`

See `src/PiPlay/MainWindow.xaml.cs:716-747`. The repo already uses `*ForTests` seams, so this is not out of pattern, but these particular hooks mutate profile state and re-run UI loading. That makes some profile-accent behavior harder to reason about as pure model logic.

Recommended simplification: if profile-specific appearance grows again, extract a small pure resolver/writer around `ActiveProfileName`, `ResolvedAccentColor`, and commit routing. Then `MainWindow` can call that service and tests can cover the edge cases without adding more mutating window hooks.

Status: Addressed. `ProfileAccentService` now owns active profile resolution, commit routing, and active-profile reconciliation. The profile rename/delete/commit tests moved to pure service tests; the deleted `MainWindow` test seams are gone.

## Efficiency Notes

- `AccentColorPicker.RenderDisc()` (`src/PiPlay/Controls/AccentColorPicker.xaml.cs:258-294`) does a full pixel render using HSV math, but it runs on load/size/DPI changes, not per drag tick. At the current 148 DIP wheel size this is acceptable.
- Per-drag preview calls `AccentReadabilityPolicy.Evaluate(...)` and, for readable colors, `ThemeResourceApplier.ApplyAccentOnly(...)`. That allocates a small set of frozen brushes per readable tick. This is reasonable for now; throttle only if manual testing shows drag stutter.
- `_discScale` in `AccentColorPicker` was assigned but not read elsewhere. Status: Addressed; it is now a local variable inside `RenderDisc()`.

## Validation

- `dotnet build PiPlay.sln --configuration Debug`: passed after stopping the stale local PiPlay process that held the Debug exe.
- `dotnet test PiPlay.sln --configuration Debug --no-build --filter "FullyQualifiedName~ProfileAccentServiceTests|FullyQualifiedName~ProfileServiceTests|FullyQualifiedName~SettingsServiceTests|FullyQualifiedName~ReleaseScriptPolicyTests|FullyQualifiedName~SettingsWindowAppearanceTests|FullyQualifiedName~MainWindowProfileAccentTests|FullyQualifiedName~AccentColorPickerTests|FullyQualifiedName~XamlInvariantTests"`: 134 passed.
- PowerShell parser check over `NativeCommand.ps1`, Build, Preflight, Publish, and Verify scripts: passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Preflight-SpecGate.ps1 -Quiet`: passed.
- `dotnet test PiPlay.sln --configuration Debug --no-build`: 673 passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`: passed.

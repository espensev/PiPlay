# Profile edit / validation — design

## Goals

Close the Phase 2 profile gap (spec §17): today profiles are MVP **save + load** only —
`ProfilesCombo` (load-on-select) and `SaveProfileButton`. There is no way to **edit** a
saved profile or **delete** one from the UI, even though `ProfileService.Remove`/`ValidateUrl`
already exist and the `Profile` model already carries the Phase 2 fields. This pass adds
**profile editing with proactive URL validation** and **delete**, satisfying spec §17's
"profile editing, proactive validation UI" line and its quality requirements, without
touching the MVP save/load happy path.

## Requirements served

- Spec §17 "Phase 2 profile model" — profile **editing** + **proactive validation UI**.
- Spec §17 quality requirements:
  - "Duplicate profile names should prompt overwrite/rename instead of silently creating clutter."
  - "Profiles should validate URLs before saving once the Phase 2 edit path exists."
  - "Broken profile URLs must fail gracefully even in MVP."
- `REQ-PROFILE-01` (per-field override / null fall-through) — preserved: editing carries the
  existing nullable fields through unchanged; only `Name`/`Url` are user-editable here.
- `REQ-UI-01` (dark-theme completeness) — the editor reuses the borderless-dark `Prompt.BuildShell`.
- Q-6 (recover cleanly) — invalid input is surfaced inline; no crash, no partial write.

## Settled decisions

1. **UI home: MainWindow toolbar, not the Settings window.** The code-ownership table
   (`SPEC_GAPS_AND_OWNERSHIP.md`) assigns *profile commands* to `MainWindow`; the Settings
   window owns Privacy only and deliberately performs no app work. So Edit/Delete live as two
   icon buttons next to `ProfilesCombo`/`SaveProfileButton`, acting on the **currently selected**
   profile. No new window class; the editor is a `Prompt` dialog (matches the app's small-dialog
   pattern and inherits the dark/borderless shell already covered by
   `Prompt_dialogs_are_borderless_dark`).

2. **Edit is a single Name+URL dialog with inline ("proactive") URL validation.** `Prompt.EditProfile`
   prefills both fields, validates the URL **format** inline via `ProfileService.ValidateUrl` (the
   dialog does not close on an invalid URL; it shows a themed `DangerPin` error and lets the user
   fix it), and requires a non-empty name. It returns the edited `(Name, Url)` or `null` on cancel.
   The dialog stays settings-agnostic (no name-collision knowledge) so it is a pure input widget.

3. **Name-collision policy lives in `ProfileService`, surfaced with the existing overwrite prompt.**
   New `ProfileService.Update(settings, originalName, updated, overwrite=false)` updates the named
   profile **in place (position-preserving)** and reports `NameConflict` when the edited name now
   matches a *different* existing profile. `MainWindow` relays that to the same
   `Prompt.AskConfirm("Overwrite profile?", …)` already used by Save, then retries with
   `overwrite: true`. This keeps the rename/overwrite UX identical to Save and keeps the policy
   unit-testable (Layer 2), independent of any modal.

4. **Position preservation.** Editing a profile must not reorder the list (a visible annoyance on a
   curated list). `Update` replaces in place by index. The existing `Save` (capture-current-page)
   keeps its append/upsert semantics unchanged — appending a freshly captured page to the end is
   the natural behavior there and its test stays green.

5. **Delete reuses `ProfileService.Remove`** behind a danger-styled `Prompt.AskConfirm`, then
   persists and reloads the combo.

6. **Edit/Delete enablement.** Both buttons are disabled when no profile is selected (after load,
   `SelectedIndex = -1`, so they start disabled and enable once the user picks a profile). A small
   `UpdateProfileCommandState()` helper drives this from `SelectionChanged` and after reload.

## Testing approach (matches the established split)

- **Service logic (Layer 2, `ProfileServiceTests`)** — the new `Update` carries the risk, so it
  gets thorough coverage: in-place URL change, rename to a free name (position kept), rename onto a
  different existing profile → `NameConflict` (no mutation), rename with `overwrite:true` (collision
  removed, position kept), unknown original → `NotFound`.
- **The editor dialog** follows the codebase's existing "ShowDialog paths are not unit-tested"
  convention (same as `AskText`/`AskConfirm`); its dark/borderless shell is already asserted by
  `Prompt_dialogs_are_borderless_dark`, and end-to-end behavior is covered by the manual UI smoke
  / QA checklist. No live-modal test is introduced.
- **No happy-path change**: existing `ProfileServiceTests` (Find/Exists/Save/Remove/ValidateUrl)
  and the MVP save/load flow are untouched.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Services/ProfileService.cs` | Add `ProfileUpdateOutcome` enum + `Update(...)` (position-preserving, collision-aware). |
| `src/PiPlay/Prompt.cs` | Add `EditProfile(owner, name, url)` — Name+URL editor with inline URL validation. |
| `src/PiPlay/MainWindow.xaml` | Add `EditProfileButton` + `DeleteProfileButton` icon buttons by the combo. |
| `src/PiPlay/MainWindow.xaml.cs` | Wire Edit/Delete handlers + `UpdateProfileCommandState()`; enable on selection. |
| `tests/PiPlay.Tests/ProfileServiceTests.cs` | Add `Update` coverage (5 cases). |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | Assert the two new named buttons exist (markup invariant). |
| `docs/CHANGELOG.md` | `[Unreleased]` entry under Phase 2; drop "profile edit/validation" from "Planned". |

## Out of scope (explicit)

- Editing `Mode`/`FadeEnabled`/`Bounds`/`Topmost` from the editor — those Phase 2 per-field
  surfaces are carried through unchanged but not exposed here; this pass is Name + Url + delete.
- `Auto` (still blocked on the open trigger-timing decision).
- Reordering / drag-sort of profiles.

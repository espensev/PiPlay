# Review - Profile Selector Frame

**Date:** 2026-07-11

**Surface:** `origin/main...HEAD` (`5bff7d8...00df3e7`)

**Spec source:** `docs/superpowers/specs/2026-06-25-profile-color-identity-and-accent-fill-design.md`; `docs/PiPlay_Product_Engineering_Spec.md` section 17

**Standards sources:** `CLAUDE.md`; `docs/AGENTS.md`; `docs/Feature_Workflow.md`; `docs/reviews/README.md`

**Verdict:** FAIL

`HEAD` and `origin/main` currently have identical trees. The triple-dot surface is non-empty because
the branch and `origin/main` contain patch-equivalent commits (`00df3e7` and `5cb3e50`) on opposite
sides of the same merge base. This verdict audits the implementation patch; there is no remaining
tree delta to merge into `origin/main`.

## Findings

### High

No findings.

### Medium

- [axis: spec/regression] `src/PiPlay/Theme/ControlStyles.xaml:379` - A valid profile color can make
  both promised identity cues disappear. The closed selector binds the raw profile color to a 1-DIP
  frame whose inner surface is `SurfaceRaised` (`ControlStyles.xaml:384`), and the dropdown renders
  the same raw color as a 4-DIP rail (`src/PiPlay/MainWindow.xaml:100-102`). All valid RGB values are
  accepted (`src/PiPlay/Services/ProfileService.cs:106-112`). In Sharp Dark, `SurfaceRaised` is
  `#131820` (`src/PiPlay/Theme/ThemeCatalog.cs:191-192`), so a profile with that valid color produces
  a frame identical to the closed-control background and a rail identical to the selected-row
  background (`ControlStyles.xaml:353-354`). The design requires profile colors to be "visibly used"
  as the frame/marker (`docs/superpowers/specs/2026-06-25-profile-color-identity-and-accent-fill-design.md:26`),
  and the product spec requires sufficient dark-UI contrast (`docs/PiPlay_Product_Engineering_Spec.md:1160`).
  Evidence: the new runtime test samples only bright violet at
  `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:244-269`, so this degenerate but supported input remains
  green. Impact: users can save an identity color and receive no discernible identity treatment.
  Recommendation: preserve the stored color but render it on a contrast-aware backplate/outline (or
  derive a presentation-only contrasting edge), then cover a color equal to each theme surface.

- [axis: standards] `docs/superpowers/specs/2026-06-25-profile-color-identity-and-accent-fill-design.md:1` -
  The July 3 follow-up rewrites the completed June 25 change-pass contract instead of recording a
  new dated pass. `docs/AGENTS.md:10-12` requires the goals and approach "of that pass" in a dated
  design, and `docs/Feature_Workflow.md:44-49` requires a dated design and, for multi-step work, a
  dated plan before code. Git history shows the document was created by `9e58734` on 2026-06-25 and
  rewritten by `00df3e7` on 2026-07-03. The reused design still lists original-pass changes to
  `MainWindow.xaml.cs` and `Prompt.cs` at lines 68-69, neither of which is in this patch, while the
  reused plan retains the older 682-test result at
  `docs/superpowers/plans/2026-06-25-profile-color-identity-and-accent-fill.md:42-45`; the current
  equivalent commit and this review both discover 678 tests. Impact: the retained record no longer
  distinguishes the released filled-chip decision from the later frame/rail correction, weakening
  auditability and making the validation section ambiguous. Recommendation: restore the June record
  as historical truth and add a `2026-07-03-profile-selector-frame-design.md` follow-up (or append an
  explicitly dated amendment with its own scope and verification).

### Low

- [axis: standards] `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:244` - The new runtime test and the
  rewritten invariant at `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:271` do not reference a `Q-*`
  or `REQ-*` identifier, contrary to `docs/AGENTS.md:48`. Recommendation: associate both with
  `REQ-PROFILE-01` and/or `REQ-UI-01` in their names or adjacent comments.

- [axis: spec] `src/PiPlay/Models/Profile.cs:22` - The model comment still says a null identity color
  uses a neutral "profile chip," although this patch removes that chip in favor of a frame/rail.
  Impact: the nearest model documentation now describes a deleted presentation contract.
  Recommendation: describe the neutral marker/frame behavior without the obsolete chip term.

## Verification

- `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~WpfRuntimeTests|FullyQualifiedName~XamlInvariantTests" --nologo` - pass, 160 tests.
- Focused profile/theme/UI filter recorded in the implementation plan - pass, 249 tests.
- `dotnet test PiPlay.sln --configuration Debug --nologo` - pass, 678 tests.
- `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` - pass, 0 warnings and 0 errors.
- `git diff --check origin/main...HEAD` - pass.
- `rg -n '[ \t]+$' docs/reviews/review-2026-07-11-profile-selector-frame.md` - pass (no matches).
- `.\scripts\Preflight-SpecGate.ps1` - pass; the gate detects a changed dated spec but does not detect
  that it belongs to an earlier pass.
- `.\scripts\Verify-Docs.ps1` - unavailable; this repository has no such script, so no separate
  docs verifier was run.
- `git diff --quiet HEAD origin/main` - pass; current trees are identical.
- Deployed-Stable visual QA - not run. This is not a release-candidate review, and repo rules require
  manual UI testing to use a verified deployed Stable copy.

## Coverage Notes

- Files reviewed deeply: `docs/CHANGELOG.md`, `docs/PiPlay_Product_Engineering_Spec.md`,
  `docs/SPEC_GAPS_AND_OWNERSHIP.md`, the changed design and plan, `src/PiPlay/MainWindow.xaml`,
  `src/PiPlay/Theme/ControlStyles.xaml`, deleted `src/PiPlay/Theme/AccentForegroundConverter.cs`,
  `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`, and `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`.
- Direct callers/resources reviewed: `src/PiPlay/Models/Profile.cs`,
  `src/PiPlay/Services/ProfileService.cs`, `src/PiPlay/Theme/ThemeCatalog.cs`, and all
  `DarkComboBox` consumers.
- Files sampled or excluded: none from the branch diff. Three unrelated/uncommitted image files in
  the working tree are outside the branch surface; the application screenshots among them predate
  the patch and are not current release evidence.

## Open Questions

- None. The accepted-color and visible-identity requirements jointly make the surface-equal color
  case deterministic rather than a subjective styling preference.

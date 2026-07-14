# Review - Profile Identity Rail and Accent Wash (address-pass)

> **Archive note (2026-07-14):** the design spec and plan this review cites were **pruned** after their
> live content (the contrast contract; the open deployed-Stable visual-QA gate) was folded into
> `PiPlay_Product_Engineering_Spec.md` and `QA_Checklist.md` (row **UI-CHK-9**) — see
> [2026-07-14-doc-cleanup-audit.md](2026-07-14-doc-cleanup-audit.md). The citations below are left intact
> as the record of what was reviewed; the paths no longer resolve. The reviewed work shipped in v0.7.3 (b28).

**Date:** 2026-07-11

**Surface:** merge delta `origin/main..HEAD` = commit `a9bb197` exactly. `HEAD~1` and `origin/main`
have identical trees (`origin/main` tip `5cb3e50` is patch-equivalent to branch commit `00df3e7`),
so this review audits the single address-pass commit.

**Spec source:** `docs/superpowers/specs/2026-07-11-profile-identity-rail-and-accent-wash-design.md`;
`docs/superpowers/plans/2026-07-11-profile-identity-rail-and-accent-wash.md`;
`docs/PiPlay_Product_Engineering_Spec.md` sections 17 and 20;
`docs/reviews/review-2026-07-11-profile-selector-frame.md` (the FAIL review this pass answers)

**Standards sources:** `CLAUDE.md`; `docs/AGENTS.md`; `docs/Feature_Workflow.md`;
`docs/reviews/README.md`; `docs/superpowers/templates/plan-template.md`

**Verdict:** PASS WITH NOTES

## Prior Review Closure

All four findings of `review-2026-07-11-profile-selector-frame.md` are genuinely addressed:

- **M1 (surface-equal color loses both identity cues):** addressed. `ThemeColors.EnsureContrast` /
  `ContrastBrush` lift only the presentation color (`src/PiPlay/Theme/ThemeColors.cs:83-124`);
  stored hex values stay exact (asserted in `ThemeSettingsWriterTests` / `ProfileServiceTests`).
  The rail converts against the live `SurfaceHover` through a DynamicResource-backed `Tag`
  (`src/PiPlay/MainWindow.xaml:105-114`), and `DeriveAccentSet` lifts `AccentPrimary`/`AccentPressed`
  the same way (`ThemeColors.cs:172-175`). Because `SurfaceHover` is the lightest surface in all
  three shipped presets, a 3.0:1 floor there mathematically implies >= 3:1 on the darker
  `SurfaceRaised`/`SurfaceBase` as well. Coverage includes catalog-sourced surface-equal accents per
  preset (`ThemeColorsTests`) and a live surface-swap WPF rail test (`WpfRuntimeTests:275-335`).
- **M2 (rewritten June records):** addressed. The June 25 design and plan are restored
  byte-identical to their originals (`git diff 9e58734 a9bb197 --` on both files is empty), and the
  dated July 11 design + plan own this pass.
- **L1 (missing requirement IDs):** addressed. New/changed tests carry `_REQ_UI_01` /
  `_REQ_PROFILE_01` suffixes.
- **L2 (stale "profile chip" comment):** addressed. `src/PiPlay/Models/Profile.cs:22` now describes
  the transparent identity rail.

## Findings

### High

No findings.

### Medium

No findings.

### Low

- [axis: regression] `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:148` - The CON-1 regression pin was
  weakened: `HEAD~1` asserted the independent literal `Colors.White` for `OnAccentPressed`; the new
  assertion computes both sides from production (`ThemeColors.ContrastRatio(...) >= 4.5`) and passes
  by a ~0.03 margin, under an inline comment that no longer matches. Recommendation: re-pin the
  expected literal, or assert through the independent `tests/Infrastructure/Wcag.cs` oracle.
- [axis: regression] `tests/PiPlay.Tests/ThemeColorsTests.cs:176` (also `:190`) - The new
  `EnsureContrast` unit tests use production `ThemeColors.ContrastRatio` as their pass oracle, which
  is near-tautological because that predicate is `EnsureContrast`'s own termination condition.
  Recommendation: assert the ratios via the independent `Wcag.cs` oracle already used elsewhere.
- [axis: regression] `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:305` (also `:328`) -
  `GetBindingExpression(Border.BackgroundProperty)?.UpdateTarget()` is a silent no-op: the property
  is bound with a MultiBinding, for which `GetBindingExpression` returns null (the dispatcher flush
  at `:315` does the real work). The visibility assertion is also alpha-blind: `Transparent`'s RGB
  reads as white and would pass the 3:1 check (partially mitigated by `Assert.NotEqual` at `:317`
  and the explicit `Transparent` expectation at `:329`). Recommendation: use
  `BindingOperations.GetMultiBindingExpression(...)` or delete the two lines; assert `A == 0xFF`.
- [axis: spec] `docs/superpowers/specs/2026-07-11-profile-identity-rail-and-accent-wash-design.md:17` -
  The "Requirements served" gloss overstates `REQ-UI-01` ("responds visibly to the app accent");
  the product-spec requirement mandates dark-theme completeness, not accent-responsiveness.
  Recommendation: correct the gloss or cite the actual normative line.
- [axis: standards] `docs/superpowers/plans/2026-07-11-profile-identity-rail-and-accent-wash.md:8` -
  The Result records 298 focused / 690 full test counts without the commands or filters that
  produced them, contrary to the plan template's Result guidance; the focused count is not
  reproducible. Recommendation: record the exact `dotnet test --filter` invocations.

### Notes

- Pressed-state feedback collapses for floor-lifted accents: darkening then re-lifting lands
  `AccentPressed` back at the same 3.0:1 boundary as `AccentPrimary` (`ThemeColors.cs:175`), so
  extreme dark custom accents lose the pressed affordance. Accepted trade-off (an invisible pressed
  fill is what the spec forbids); worth recording in the derivation comment, or lift pressed to a
  slightly higher floor (e.g. 3.5:1).
- The title-bar wash has no realized-element runtime assertion (coverage stops at raw-XAML text and
  resource tokens), and the rail runtime path is exercised on sharp-dark only; other presets are
  inferred from unit-level `EnsureContrast` coverage.
- Null profile color now renders a fully transparent rail; the previous neutral `#2D333B` marker is
  gone. This is spec-sanctioned (design acceptance criteria) but is a visible behavior change for
  colorless profiles.
- The imperative fallbacks diverge from the converter: a missing `SurfaceHover` yields the raw,
  possibly invisible brush in `MainWindow.xaml.cs:291-294` / `PlayerWindow.xaml.cs:625-631`, but
  `Transparent` in `ContrastBrushConverter`. Unreachable today (`SurfaceHover` is always seeded).
- `ControlStyles.xaml:369` - the `DarkComboBox` style-level `BorderBrush` setter is now dead code;
  the template hardcodes the toggle border to `Transparent`.
- The design's changes-by-file table omits `src/PiPlay/Controls/AccentColorPicker.xaml` (changed
  user-facing tooltip copy), and the updated `docs/SPEC_GAPS_AND_OWNERSHIP.md:89` row drops the plan
  links its neighboring rows keep.
- Out-of-surface repo state: the double-audio mute+pause suppression stack (`2b553ee` et al.) exists
  only on the diverged, unpushed local `main` (8 commits ahead of `origin/main`; tips `cc665e3` /
  `5cb3e50` are patch-equivalent). This delta touches no audio code; the concentrated regression
  risk is the eventual local-main reconciliation, not this merge.

## Verification

- `dotnet test PiPlay.sln --configuration Debug --nologo` - pass, 690/690.
- `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` - pass, 0 warnings, 0 errors.
- `git diff --check origin/main...HEAD` - pass.
- `.\scripts\Preflight-SpecGate.ps1` - pass (dated design spec detected).
- Independent WCAG recomputation (out-of-repo, no repo build): representative dark and
  surface-equal accents lift to 3.01-3.04:1 across all three presets; shell tints land at
  1.206-1.21:1; the derived `#0F242C` shell tint and `#2492AF` pressed seed match `Colors.xaml`
  byte-for-byte; the bisection's loop invariant guarantees (not merely approaches) the floor.
- June record restoration - verified byte-identical (`git diff 9e58734 a9bb197` on both files empty).
- Deployed-Stable visual QA - not run. Release-candidate lane; the design's declared unresolved
  owner check (wash restraint across themes/DPI) remains open.

## Coverage Notes

- Method: 12-agent adversarial review workflow - six scoped reviewers (prior-finding closure, theme
  derivation logic, XAML wiring, test quality, docs/records, regression sweep), independent
  re-verification of every non-note finding (0 refuted), and a completeness critic over the full
  diff.
- Files reviewed deeply: all 24 changed files, plus unchanged consumers and context -
  `ThemeCatalog.cs`, `ProfileAccentService.cs`, `AccentReadabilityPolicy.cs`, `App.xaml.cs` startup
  ordering, `SettingsWindow` preview flow, `tests/Infrastructure/Wcag.cs`, and the standards docs.
- Repo-wide greps: no stale `AccentForegroundConverter` or `SelectedItem.AccentColor` references
  remain outside the retained historical review artifact.

## Open Questions

- Owner visual QA on deployed Stable: does the title-bar wash stay a restrained tint across all
  three themes and DPI scales?
- When and how to reconcile the diverged local `main` (mute-suppression stack) with `origin/main`.

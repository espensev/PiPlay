# Review — Accent reach dial and profile routing

**Date:** 2026-07-14
**Surface:** `stable-v0.9.0-b31...c8a9c9f`
**Spec source:** owner handoff in the 2026-07-14 session; product spec §17; retained v0.9.0 accent-reach design
**Standards sources:** `CLAUDE.md`; `docs/AGENTS.md`; `docs/Feature_Workflow.md`; `docs/reviews/README.md`
**Verdict:** FAIL

This is the immutable pre-fix review of commit `c8a9c9f`. Accepted findings are addressed by the separate
follow-up design `docs/superpowers/specs/2026-07-14-accent-reach-default-and-routing-fixes-design.md`.

## Findings

### High

- [axis: regression] `src/PiPlay/Theme/ThemeSettingsWriter.cs:21` — any accepted Appearance change under
  a colored profile writes the dialog's effective/profile color into `Theme.AccentColor` before the
  profile-aware commit runs. Evidence: Settings is seeded from `ResolvedAccentColor`
  (`MainWindow.xaml.cs:662-673`), the broad `AppearanceChanged` path reaches
  `ApplyPlayerPreferences` (`MainWindow.xaml.cs:705-765`), and the global assignment is unconditional.
  Impact: deselecting the profile permanently loses the user's global fallback. Recommendation: make the
  profile-aware service the sole commit owner and reapply the resolved accent after full theme resources.

### Medium

- [axis: spec/regression] `src/PiPlay/Theme/ThemeColors.cs:186-217` — one linear scalar drives both wash
  and glyph reach. At default 50 the wash matches v0.9.0 but the glyph is pale `#90D2E5`; the deployed
  combination of full `#2BAED0` glyph plus `#12343F` wash is unreachable. Recommendation: retain the
  0–100 wash curve and finish glyph reach at 50.
- [axis: regression] `src/PiPlay/SettingsWindow.xaml.cs:186-205` — preset switching applies the
  global-default substitution rule to a profile-owned color. If that profile color equals the prior
  preset default, it is silently replaced and stored back to the profile. Recommendation: carry accent
  ownership into the dialog; only global targets follow preset defaults.
- [axis: standards/spec] commit `c8a9c9f` changes 16 source/test files without a new dated change-pass
  design or changelog entry. Current QA/docs still describe the removed single wash constant and omit the
  reach preference. Recommendation: create a distinct follow-up spec/plan and fold current docs forward
  without rewriting the completed v0.9.0 design record.

### Low

- [axis: standards] `docs/SPEC_GAPS_AND_OWNERSHIP.md`, `docs/Theme_Preset_Differences.md`, and product
  spec §25.2 retain live identity-only/global-split or pre-dial text that contradicts product spec §17
  and current code. Recommendation: explicitly supersede historical intent and update the current-code
  resource/persistence map.

## Verification

- `git diff --check stable-v0.9.0-b31...c8a9c9f` — pass.
- Red-capable focused regression loop added in the follow-up working tree — failed as expected: global
  fallback became `#A78BFA`, default glyph was `#90D2E5`, and split-curve anchors failed.
- Full QA on the immutable review surface — not run; the follow-up implementation owns the final gate.

## Coverage Notes

- Files reviewed deeply: all 16 files changed by `c8a9c9f` — the 11 production files and five test files
  listed by `git diff --name-status stable-v0.9.0-b31...c8a9c9f`.
- Direct callers/docs reviewed: `ProfileAccentService`, product spec §17/§25.2, QA accent rows, current
  theme map, ownership notes, retained v0.9.0 design, and retained prior review guidance.
- Files sampled or excluded: none from the fixed-point diff.

## Open Questions

- None. The owner's midpoint compatibility and profile/global ownership decisions settle the fixes.

## Follow-up Disposition

- All findings were addressed in the follow-up working tree under the separate dated design/plan.
- Final verification: 769/769 Debug tests; Release build with 0 warnings/errors; spec preflight and diff
  checks passed; independent re-review found no blocking issues.
- Deployed-Stable visual QA was not run because this is not a release-candidate publish.

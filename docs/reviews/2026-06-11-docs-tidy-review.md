# PiPlay docs tidy review

Date: 2026-06-11
Scope: current repo docs and root Markdown files after PR #21 merge.
Focus: separate active docs from completed/historical review artifacts, and identify stale active-doc claims.
Status: executed in the follow-up cleanup. Theme and chrome review artifacts were moved under
`docs/reviews/`; active-doc wording was corrected; discovery notes were bannered as historical.

## Cleanup Outcome

Implemented from this review:

- Fixed the active changelog wording for theme/accent/corners close-to-apply.
- Added `docs/reviews/` as the archive lane for completed review artifacts.
- Moved completed theme review artifacts into `docs/reviews/2026-06-11-theme/`.
- Moved completed chrome review reports into `docs/reviews/2026-05-30-chrome/`.
- Updated references to the moved review artifacts.
- Added active-docs-vs-records guidance to `docs/README.md`.
- Marked `docs/ui-overhaul-discovery/` as historical discovery evidence.

## Findings

### P1 - Active changelog still says theme/accent applies live

`docs/CHANGELOG.md` is an active release note, and it still says the theme accent "applies right away" and recolors the main window controls live.

Evidence:

- `docs/CHANGELOG.md:20-23`
- `docs/superpowers/specs/2026-06-11-theme-corners-and-palettes-design.md:171-176`

PR #21 deliberately settled the opposite contract: theme, accent, and corners are close-to-apply when Settings closes; only opacity sliders live-preview, and the dialog self-previews its own native corners.

Recommendation:

Update the changelog wording before the next release/publish. Suggested wording:

```text
Theme, accent, and corners apply to every open window when Settings closes, with no restart needed.
Opacity sliders still live-preview on the open Popout Player.
```

This is the one active-doc fix I would do first. It is small, release-facing, and currently wrong.

### P1 - Completed theme review artifacts were cluttering the repo root

At review time, the repo root contained several theme review documents:

- `piplay-theme-review-and-variants.md`
- `piplay-theme-end-pass-review.md`
- `piplay-theme-claim-response-review.md`
- `piplay-theme-post-merge-disposition-review.md`
- `piplay-theme-pass-code-review.md`
- `piplay-theme-pr21-override-model-review.md`

Two of these already carry historical banners, and the active implementation disposition now lives in the checked-in design spec addenda:

- `docs/superpowers/specs/2026-06-11-theme-corners-and-palettes-design.md:102-176`

Keeping all of these in the root would make "what is active?" harder than it needs to be. The root should be for build/project entry points and a very small number of deliberate handoff docs, not every completed review pass.

Cleanup applied:

A dedicated review archive lane was created and the done theme files were moved there:

```text
docs/reviews/2026-06-11-theme/
  source-review-and-variants.md
  historical-draft-end-pass-review.md
  historical-claim-response-review.md
  post-merge-disposition-review.md
  p2-p3-code-review.md
  pr21-override-model-review.md
  README.md
```

References in the theme spec were updated from root filenames to the new paths. The design spec itself stayed in `docs/superpowers/specs/` because it is the active pass record that satisfies the workflow/spec gate.

### P2 - Top-level chrome review docs mixed active procedure with superseded findings

At review time, the chrome docs were split across:

- `docs/Chrome_UI_Spec_Review.md`
- `docs/Chrome_UI_Issue_Report.md`
- `docs/Chrome_UI_Screenshot_Test_Procedure.md`

`Chrome_UI_Spec_Review.md` said it superseded `Chrome_UI_Issue_Report.md`, but both lived as top-level docs. The screenshot test procedure is still useful as an active manual method; the old issue report is mainly provenance.

Cleanup applied:

- Kept `docs/Chrome_UI_Screenshot_Test_Procedure.md` active.
- Moved the consolidated review and the superseded issue report under `docs/reviews/2026-05-30-chrome/`.
- Updated references from the old top-level paths to the archived paths.

Suggested archive if moving:

```text
docs/reviews/2026-05-30-chrome/
  chrome-ui-spec-review.md
  superseded-chrome-ui-issue-report.md
```

### P2 - Discovery notes are not clearly marked as historical after the overhaul landed

`docs/ui-overhaul-discovery/README.md` says the folder is for the "upcoming" settings/theme/popout/appearance refactor. Much of that work has now landed, and some notes are stale by design.

Evidence:

- `docs/ui-overhaul-discovery/README.md`
- `docs/ui-overhaul-discovery/ui-state-notes.md:447-450`
- `docs/ui-overhaul-discovery/theme-system-overhaul-evaluation.md:33`

The folder is valuable raw evidence, but it should not read like an active implementation source.

Recommendation:

Either move it to:

```text
docs/archive/ui-overhaul-discovery/
```

or keep the path and update the README opening to:

```text
This folder is historical discovery evidence for the 2026-06-10/11 UI and theme overhaul.
The active contracts live in docs/superpowers/specs/ and docs/superpowers/plans/.
Individual observations may be stale after the merged passes.
```

I would prefer the banner first, not a move, because the screenshots and old notes may still be useful for visual comparison.

### P2 - The active docs index needs an explicit "active vs pass records" rule

`docs/README.md` is a good active-doc index, but it does not tell contributors what to do with the growing number of dated specs, plans, worklogs, review notes, and evidence files.

Recommendation:

Add a short section to `docs/README.md`:

```text
## Active docs vs records

Active product/source docs are this index, the product spec, QA checklist, changelog, ADRs, compliance, privacy map, and SPEC_GAPS_AND_OWNERSHIP.
Dated specs/plans/worklogs under docs/superpowers are immutable change-pass records.
Review artifacts live under docs/reviews/.
Raw discovery/evidence lives under docs/archive/ or docs/evidence/ and is not a current-state contract.
```

That keeps active docs separate without hiding the useful history.

## Recommended Tidy Layout

Keep these as active docs:

- `README.md`
- `docs/README.md`
- `docs/PiPlay_Product_Engineering_Spec.md`
- `docs/CHANGELOG.md`
- `docs/QA_Checklist.md`
- `docs/SPEC_GAPS_AND_OWNERSHIP.md`
- `docs/Feature_Workflow.md`
- `docs/AGENTS.md`
- `docs/YouTube_Compliance.md`
- `docs/Data_and_Privacy_Map.md`
- `docs/adr/`
- `docs/superpowers/specs/` for dated design contracts
- `docs/superpowers/plans/` for dated implementation plans
- `docs/superpowers/worklog/` for session summaries
- `docs/evidence/` for release and screenshot evidence

Add these lanes:

```text
docs/reviews/
  2026-05-30-chrome/
  2026-06-11-theme/

docs/archive/
  ui-overhaul-discovery/   # optional later move
```

Keep the repo root lean:

- project/build files
- `README.md`
- `VERSION`
- `BUILD_NUMBER`
- solution/scripts

Do not keep completed review artifacts in the root after their disposition is recorded in a spec, PR, or review archive.

## Suggested Cleanup Order

1. Fix the active changelog line about live accent application. Done.
2. Add `docs/reviews/README.md` with the rule: reviews are evidence/provenance, not active source-of-truth docs. Done.
3. Move the six root theme review files into `docs/reviews/2026-06-11-theme/`. Done.
4. Update `docs/superpowers/specs/2026-06-11-theme-corners-and-palettes-design.md` links to point at the archive paths. Done.
5. Add a historical banner to `docs/ui-overhaul-discovery/README.md` or move that folder under `docs/archive/`. Banner done.
6. Banner or move `docs/Chrome_UI_Issue_Report.md` as superseded by `docs/Chrome_UI_Spec_Review.md`. Moved both chrome review reports under `docs/reviews/2026-05-30-chrome/`.
7. Add the active-vs-records section to `docs/README.md`. Done.

## Bottom Line

The docs are not structurally bad; they just need lanes.

Active docs should stay small and current. Dated specs/plans/worklogs should remain under `docs/superpowers/`. Completed review artifacts should move out of the repo root into `docs/reviews/`. Discovery material should be explicitly historical. The only active-doc correctness issue I found in this pass is the changelog's stale live-apply wording.

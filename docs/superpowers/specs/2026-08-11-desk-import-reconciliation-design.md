# Desk import reconciliation — design

## Goal

Reconcile the copied SND-DESK working tree with the canonical repository by retaining its coherent documentation and comment-authority cleanup, removing only superseded historical documents, and leaving runtime behavior unchanged.

## Requirements served

- `tooling/docs`
- `docs/Feature_Workflow.md`

## Acceptance criteria

- The 18 canonical documentation and source/test comment updates remain intact.
- The nine superseded audit, review, evidence-index, template-index, and implemented-design files are removed only after their durable facts are confirmed in canonical documents or Git history.
- No runtime or test behavior changes are introduced.
- Documentation contains no conflict markers or live references to removed paths.
- `pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1` passes.
- The branch is reviewed through GitHub checks and squash-merged to `main` without a force push.

## Decisions in force

- Treat the copied changes as one documentation-authority consolidation because the source and test diffs change comments only.
- Keep unresolved measurement ownership in `docs/SPEC_GAPS_AND_OWNERSHIP.md` and durable theme behavior in `docs/Theme_Preset_Differences.md`.
- Rely on Git history for retired narrative audit/review artifacts instead of preserving duplicate current-state authorities.
- Use the PR-body `Spec-Exception` permitted by `docs/Feature_Workflow.md` because final source/test changes are comment-only and this completed coordination spec will be retired before merge.

## Constraints and warnings

- Preserve unrelated ignored local files and build output.
- Do not deploy a Stable build because no runtime behavior changes.
- Do not contact SND-DESK or REMOTE; reconciliation is against `origin` from SND-HOST.
- Push only the review branch and merge through a pull request.

## Non-goals

- Runtime, UI, URL parsing, theme, packaging, or release behavior changes.
- Restoring retired narrative reports as parallel sources of truth.
- Publishing or deploying a release artifact.

## Changes

| File, interface, or ownership seam | Required change |
|---|---|
| Canonical product, QA, gaps, theme, changelog, and asset docs | Retain the consolidated statements and authority links. |
| Source and test comments | Retain comment-only references to canonical specifications. |
| Retired audit/review/evidence/template/design artifacts | Delete after verifying that no live reference or unique durable fact remains. |
| GitHub review branch and PR | Validate, push, check, and squash-merge the bounded change. |

**Docs/changelog:** The change itself is documentation consolidation; retain the updated `docs/CHANGELOG.md` and canonical documents.

## Verification

- Documentation conflict-marker, removed-path-reference, and changed-file semantic checks.
- `git diff --check`
- `pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1`
- GitHub required checks on the pull request.
- No deployed/manual check is required because runtime behavior is unchanged.

## Unresolved work

- None.

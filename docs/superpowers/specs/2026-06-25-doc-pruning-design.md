# Docs pruning and stale-artifact cleanup - design

## Goals

Reduce the amount of retained Markdown and scratch evidence under `docs/` while preserving the
authoritative PiPlay documentation set, current open bug/backlog notes, ADRs, release evidence, and
the dated change-pass records required by the workflow.

This pass should make historical review/discovery material either folded into active docs or removed.
It should not change product behavior, release claims, or the source of truth for requirements.

## Requirements served

Docs-only cleanup serving the contributor workflow in `AGENTS.md` and `Feature_Workflow.md`.
No `REQ-*` product behavior changes.

## Acceptance criteria

- Authoritative docs remain: product spec, README, changelog, workflow, QA checklist, ADRs,
  compliance/privacy maps, theme reference, spec gaps/ownership, current change-pass records, and
  the workflow examples/templates.
- Folded owner-review and chrome-review details no longer depend on removable review artifacts.
- Historical discovery scratch material is removed after the useful findings are represented in
  active docs or dated records.
- A reference scan does not leave active docs pointing at deleted historical files.

## Settled decisions

1. Keep current `docs/superpowers/` records, workflow examples/templates, and the active Theme V2
   reference. Prune old completed implementation records once useful facts are folded into active
   docs.
2. Keep current release evidence under `docs/evidence/`. Prune stale debug/review screenshots once
   useful findings are folded forward.
3. Remove completed one-off review logs once their useful findings are already captured in active
   backlog/docs.
4. Remove the old UI discovery scratch folder instead of archiving it again; its implementation
   value has been folded forward.

## Non-goals / out of scope

- No product-code change.
- No release-note change; this cleanup is not user-visible app behavior.
- No deletion of ADRs, current spec sections, or change-pass specs/plans needed for the workflow.
  Historical release screenshots may be pruned after their facts are folded into active docs.

## Testing approach

Run reference scans for pruned paths, inspect git status, and use `git diff --check` for whitespace.
No build or app test is required for doc-only deletion.

## Changes by file

| File | Change |
|---|---|
| `docs/README.md` | Clarify that folded review/discovery scratch docs should be deleted rather than kept as active guidance. |
| `docs/SPEC_GAPS_AND_OWNERSHIP.md` | Fold raw owner-review and old chrome-review references into current notes. |
| `docs/Theme_Preset_Differences.md` | Point to the folded owner-direction summary instead of a raw review file. |
| `docs/Chrome_UI_Screenshot_Test_Procedure.md` | Keep the runnable procedure but stop treating the old 2026 chrome review files as update targets. |
| `docs/Spec_Conformance_Review.md` / `docs/Regression_Test_Suite_Design.md` | Fold historical details into the retained regression-suite plan and delete the standalone reports. |
| `docs/superpowers/...` | Replace references to pruned scratch evidence and stale completed records with folded-history notes; delete historical records no longer needed for current workflow/examples. |
| Historical review, discovery, worklog, and debug evidence files | Delete folded one-off artifacts. |

## Docs & changelog impact

No changelog entry. This is repo documentation hygiene only.

## Unresolved decisions

- None

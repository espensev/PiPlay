# Feature-workflow enforcement - plan

**Spec:** `docs/superpowers/specs/2026-06-06-feature-workflow-enforcement-design.md`

- [x] Add the `spec-check.yml` "Require design spec" gate (fail-closed paginated `gh api` file list, dated-design-spec path check, `Spec-Exception:` escape hatch).
- [x] Add `.github/pull_request_template.md` (spec link, requirement IDs, acceptance criteria, verification, docs/changelog impact).
- [x] Add `docs/superpowers/templates/feature-design-template.md` mirroring the house spec shape + DoR fields.
- [x] Add `docs/superpowers/templates/plan-template.md` and `worklog-template.md` plus a template README.
- [x] Add a "Definition of Ready" subsection to `docs/Feature_Workflow.md` and document the gate + exception.
- [x] Update `docs/AGENTS.md` to point agents/contributors at the templates, DoR, spec gate, and examples.
- [x] Delete `docs/TEMP_WORKFLOW_REVIEW_NOTE.md` once its findings are acted on.
- [x] Verify local simulation of the gate logic.
- [x] Verify live pass path via the introducing PR.
- [x] Verify live fail + exception paths via a throwaway PR.
- [x] Re-verify live paths after the paginated file-listing improvement.

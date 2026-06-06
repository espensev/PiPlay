# Change-pass templates

Skeletons for the per-change-pass records described in [`../../Feature_Workflow.md`](../../Feature_Workflow.md)
and [`../../AGENTS.md`](../../AGENTS.md). Copy a template to its dated home, fill every section, and
delete the guidance comments before committing.

| Template | Copy to | When |
|---|---|---|
| [feature-design-template.md](feature-design-template.md) | `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md` | **Always** for a non-trivial change — the design note that satisfies the Definition of Ready and the spec-check gate. |
| [plan-template.md](plan-template.md) | `docs/superpowers/plans/YYYY-MM-DD-<topic>.md` | When the work spans **multiple steps** — one committable task per step. |
| [worklog-template.md](worklog-template.md) | `docs/superpowers/worklog/YYYY-MM-DD-<topic>.md` | **Optional**, after a substantial session (a release, a multi-fix sweep) whose path is worth replaying. |

Architecture decisions use a separate template that lives with the records it governs:
[`../../adr/README.md`](../../adr/README.md).

Each skeleton mirrors the established gold-standard examples so a new note starts in the house shape:
- Design — [`../specs/2026-06-06-profile-edit-validation-design.md`](../specs/2026-06-06-profile-edit-validation-design.md)
- Plan — [`../plans/2026-06-06-profile-edit-validation.md`](../plans/2026-06-06-profile-edit-validation.md)
- Worklog — [`../worklog/2026-06-06-stable-publish-session.md`](../worklog/2026-06-06-stable-publish-session.md)

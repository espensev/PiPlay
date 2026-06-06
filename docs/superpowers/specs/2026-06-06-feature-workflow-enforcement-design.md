# Feature-workflow enforcement — design

## Goals

`docs/Feature_Workflow.md` and `docs/AGENTS.md` already *ask* contributors to write a dated design
note before non-trivial code, but enforcement is purely social: nothing in CI or the PR surface
requires a traceable design spec, and there is no shared shape for the note or the PR. The recent
stable-channel work was less traceable than the profile-edit work precisely because of this gap
(captured at the time in a temporary review note, now superseded by this spec).

This pass turns the social convention into a lightweight, self-documenting **gate plus templates**:

- A **Definition of Ready** so "non-trivial change" has a concrete checklist.
- A **feature-design template** so every spec starts from the same shape as the gold-standard
  `2026-06-06-profile-edit-validation-design.md`.
- A **PR template** that asks for the spec link, requirement IDs, acceptance criteria, verification
  output, and docs/changelog impact.
- A **CI check** that fails a PR which changes `src/`, `scripts/`, or `tests/` without either a
  changed dated spec matching `docs/superpowers/specs/YYYY-MM-DD-*-design.md` **or** an explicit
  `Spec-Exception:` line — an escape hatch so small or urgent changes are never hard-blocked, only
  made deliberate.

## Motivation / source

Findings #1–#3 of a temporary 2026-06-06 review note (a working-tree scratch file, never committed — inlined and superseded here):
1. Non-trivial changes should have an obvious dated design note before implementation.
2. Important behavior must be easy to discover and hard to misuse.
3. The workflow rules are good, but enforcement is still mostly social.

This is process tooling, not product behavior, so it serves no `REQ-*`/`Q-*` ID and adds **no**
`docs/CHANGELOG.md` entry (the changelog tracks user-visible app changes only).

## Settled decisions

1. **The gate is a soft gate with an audited escape hatch, not a hard block.** It requires a changed
   spec *or* a `Spec-Exception: <reason>` line in the PR body. A hard block would punish legitimate
   tiny changes (typo fixes, dependency bumps) and breed resentment; an audited exception keeps the
   default honest while leaving an explicit, reviewable override.

2. **The gate runs unconditionally and detects gated paths *inside* the job — no `paths:` filter.**
   This deliberately mirrors decision #3 of `2026-06-06-ci-and-feature-workflow-design.md`: a required
   check skipped by a `paths:` filter can leave a PR stuck "pending" forever. By always running and
   always emitting a conclusive pass/fail, the check is safe to mark **required** in branch protection.

3. **Changed files come from the paginated GitHub PR files API, not a local `git diff`.** The
   GitHub-native file list needs no checkout, no `fetch-depth: 0`, and no merge-base reasoning —
   eliminating the most common way such a gate silently **fails open** (an empty diff from an
   unreachable base SHA). The workflow uses `gh api --paginate /pulls/{number}/files`, not
   `gh pr view --json files`, so PRs with more than 100 files do not lose gated paths after the first
   page. The step runs under `set -euo pipefail`, so a *failure to list files* fails the gate
   (fail-closed), never silently passes.

4. **Security posture.** The job uses `pull_request` (never `pull_request_target`), `permissions:
   contents: read` + `pull-requests: read`, and passes the untrusted PR body through an **environment
   variable** that is never interpolated into the shell — so a hostile PR body cannot inject commands.

5. **Distinct, cheap check.** A separate `.github/workflows/spec-check.yml` surfaces as its own
   **"Require design spec"** status on `ubuntu-latest` (seconds; no .NET), independent of the existing
   Windows build-and-test check.

6. **Branch protection stays manual.** Making "Require design spec" a *required* check is a repository
   settings change, left to the maintainer after the first successful run — exactly as the original CI
   check was (out-of-scope item in `2026-06-06-ci-and-feature-workflow-design.md`).

## Testing approach

The gate's **decision logic** (path classification, spec detection, the `Spec-Exception:` regex, the
empty-list fail-closed branch, a null/empty body) is proven by a **local simulation** of the script
against synthetic inputs.

GitHub-Actions-context behavior cannot be proven locally, so it is verified **live**:
- The introducing PR for this change touches only `.github/` and `docs/` → it exercises the **pass**
  path (no gated paths) live, and confirms the paginated `gh api` file-listing + token/repo wiring
  works.
- The **fail** path and the **`Spec-Exception:` pass** path are exercised with a **throwaway PR** that
  adds a dummy gated file (closed and deleted after observation). They are *not* claimed verified
  until that live run is observed.

The doc/template artifacts are prose; they are checked for cross-artifact consistency (the DoR fields,
the template headings, the PR template, and the gate's spec-path expectation all agree).

## Changes by file

| File | Change |
|---|---|
| `.github/workflows/spec-check.yml` | New "Require design spec" gate: fail-closed paginated `gh api` file list, dated-design-spec path check, soft gate with `Spec-Exception:` escape hatch. |
| `.github/pull_request_template.md` | New PR template: spec link, requirement IDs, acceptance criteria, verification output, docs/changelog impact, `Spec-Exception:` guidance. |
| `docs/superpowers/templates/` | New README plus design, plan, and worklog skeletons mirroring the house spec/plan/worklog shape. |
| `docs/Feature_Workflow.md` | Add the "Definition of Ready", a phase-at-a-glance map, the optional discover (step 0) and worklog (step 6) steps, a "review the risky changes" pass, the plan-template link, and document the spec-link gate + exception mechanism. |
| `docs/AGENTS.md` | Point agent/contributor guidance at the templates, Definition of Ready, spec-check gate, and gold-standard examples. |
| `docs/superpowers/plans/2026-06-06-feature-workflow-enforcement.md` | Track this implementation pass. |

## Out of scope

- Enabling branch protection / marking the new check required (maintainer settings step).
- Auto-generating requirement IDs or validating that cited `REQ-*` IDs exist.
- Enforcing the PR template's contents (GitHub cannot require template fields; the DoR + review do).
- Linting the *content* of a spec (the gate only checks that a dated spec changed, not its quality).

## Known limitations (accepted)

- **Any changed dated `-design.md` satisfies the gate** — editing a months-old spec during a `src/`
  change passes it. The gate's job is to make the design note impossible to *forget*, not to referee
  whether the spec is the *right* one for the change; the Definition of Ready and human review do that.
- The gate proves a spec *moved*, not that it is good. That is the deliberate floor of a soft gate.

## Deliberately not pulled in (from the shared methodology)

The broader "superpowers" skill set offers heavier machinery; this pass leaves it out as over-ceremony
for a solo WPF app, keeping the repo's lightweight bias. Each remains reachable *ad hoc* via its skill
when a change genuinely needs it, without being baked into the repo's required surface:

- **Machine-readable plans + `project.toml`** (JSON plan as source of truth, runtime task state) — the
  dated markdown plan stays the human-and-machine-readable record here.
- **Agent-roster / `/manager` campaign orchestration** (file-ownership maps, conflict zones, worktree
  fan-out) — multi-agent sweeps already happen *ad hoc* (see the stable-publish worklog); they don't
  need a standing campaign schema in-repo.
- **`/observer` project-intelligence log** (`observations.jsonl`) — the worklog covers the
  session-record need without a parallel intelligence store.
- **A discovery *template*** — step 0 is a one-line pointer to `/discover`, not a new required
  artifact, because the repo has no discovery examples yet to template from.

# PiPlay feature workflow

Use this path for any non-trivial product, UI, quality, or pipeline change.

**The path at a glance:**

| Step | Output |
|---|---|
| 0. Discover *(optional)* | Answers to open questions before committing to a design. |
| 1. Orient | The spec, ADRs, and ownership boundaries that bound the change. |
| 2. Write the change note | A dated **design spec** (always) and a dated **plan** (multi-step work). |
| 3. Implement | Code inside the ownership seams; `docs/CHANGELOG.md` for user-visible changes. |
| 4. Test & review | The deterministic lane green; a review pass on risky changes. |
| 5. Open the PR | The pre-filled template, filled; both CI checks green. |
| 6. Record the session *(optional)* | A **worklog** for releases and multi-fix sweeps. |

The per-pass records start from skeletons in
[`docs/superpowers/templates/`](superpowers/templates/README.md).

> Steps below that reference a `/command` (e.g. `/discover`, `/code-review`, `/qa`) use the
> maintainer's Claude Code skills, not tooling this repo ships — the workflow stands without them.

## 0. Discover (optional)

If a change has open questions that block a confident design — feasibility, dependency reach, which
pattern to follow, where the risk concentrates — answer them *before* writing the spec rather than
mid-implementation. The `/discover` skill produces a structured findings document for exactly this.
Skip this step when the design is already obvious; it exists to de-risk the genuinely uncertain
change, not to add ceremony to the routine one.

## 1. Orient

Read these first:

- `docs/AGENTS.md` for repo rules, vocabulary, quality bar, and ownership boundaries.
- `docs/PiPlay_Product_Engineering_Spec.md` for normative behavior and requirement IDs.
- `docs/SPEC_GAPS_AND_OWNERSHIP.md` for open product decisions and code ownership.
- `docs/adr/` before changing architecture, platform, WebView2, packaging, or window policy.

If a request touches YouTube behavior, also read `docs/YouTube_Compliance.md`.

## 2. Write the change note

Before code for a non-trivial change, add:

- A dated design spec at `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`.
  Start from `docs/superpowers/templates/feature-design-template.md`.
- A dated implementation plan at `docs/superpowers/plans/YYYY-MM-DD-<topic>.md` when the work spans
  multiple steps. Start from `docs/superpowers/templates/plan-template.md`.

Link the spec from the PR.

### Definition of Ready

A change is ready to implement once its design spec settles all of:

- **Goals** — the gap closed and what "done" looks like; what stays unchanged.
- **Requirement IDs served** — `REQ-*`, `Q-*`, or spec § numbers (or "tooling/docs" with the motivating note).
- **Acceptance criteria** — observable, checkable conditions for "done".
- **Settled decisions** — each key choice plus the one-line reason it beat the alternative.
- **Non-goals** — what this pass deliberately does not do.
- **Affected files** — the expected change-by-file list.
- **Test plan** — which layer covers what, and what is verified live vs locally.
- **Docs / changelog impact** — `docs/CHANGELOG.md` for user-visible changes; any ADRs.
- **Unresolved decisions** — open questions, or "none".

CI enforces the spec half of this: any PR touching `src/`, `scripts/`, or `tests/` must ship a changed
dated spec (see step 5).

## 3. Implement inside the ownership boundaries

Keep product terms stable: Video Popout, Popout Player, Source Window, Source Placeholder,
Pin, Fade, and Auto. Do not surface internal names such as `MainWindow`, `PlayerWindow`, or
`Detach` in user-facing UI.

Prefer existing seams:

- URL parsing and YouTube target construction: `YouTubeUrlHelper`.
- JavaScript snippets: `YouTubeDomBridge`.
- Navigation policy: `NavigationPolicy`.
- Atomic settings and recovery: `SettingsService`.
- Profile persistence and validation: `ProfileService`.
- Placement math and monitor restore: `WindowPlacementService` / `PlacementMath`.
- Local diagnostics and URL redaction: `LoggingService`.

Update `docs/CHANGELOG.md` for user-visible changes.

## 4. Test & review

Run the same deterministic lane that CI runs:

```powershell
dotnet test PiPlay.sln --configuration Debug
.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
```

Use narrower filters while developing:

```powershell
dotnet test --filter Category=Logic
dotnet test --filter Category=Markup
dotnet test --filter Category=Wpf
```

Release candidates still need the manual lane — run it against the **deployed Stable copy**, never
the repo tree (stale repo binaries are the classic false pass; see `AGENTS.md` Conventions):

```powershell
# Commit VERSION/BUILD_NUMBER/CHANGELOG first.
.\scripts\Publish-Stable.ps1                 # exact-source gate + build + deploy + verify + local stable tag
.\scripts\Verify-StableDeploy.ps1            # fail-closed release proof before testing
pwsh -File scripts\Test-UiSmoke.ps1 -ExePath E:\Dev_test_implemenations\PiPlay\PiPlay.exe
```

Then run `docs/QA_Checklist.md` for shareable builds — also against the deployed exe.

### Review the risky changes

Tests prove behavior; a review pass catches the rest. For a change that concentrates risk — a
multi-fix sweep, a security-sensitive seam, or anything touching settings/persistence — make a
deliberate review pass part of the work, not an afterthought. `/code-review` flags correctness and
simplification findings on the diff; `/qa` triages failures and coverage gaps. The stable-publish
sweep (`docs/superpowers/worklog/2026-06-06-stable-publish-session.md`) is the model: four real bugs
were found and adversarially verified before they shipped, not after.

### Stable publish (deploy a differentiable copy)

To cut a **stable** copy that runs side by side with the dev app and deploy it for test use:

```powershell
.\scripts\Publish-Stable.ps1            # deploys to E:\Dev_test_implemenations\PiPlay (override with -DeployRoot)
```

This test-gates, builds the **Stable** channel (baked in via `-p:PiPlayChannel=Stable`) from the
already-committed `VERSION`/`BUILD_NUMBER`, validates publish metadata, deploys a runnable copy,
creates/verifies the local `stable-vX.Y.Z-bN` tag, and replaces binaries while **preserving** the
`PiPlayData` runtime folder. A Stable copy is differentiable from dev: data beside the exe, its own
single-instance identity, and a `PiPlay — Stable vX.Y.Z (bN)` title. After deploying, launch the
deployed `PiPlay.exe` and confirm it opens its own window with an isolated `PiPlayData` folder beside
it. Background and trade-offs: `docs/adr/0007-stable-channel-and-portable-data.md`.

**Choosing the version move** (`VERSION` is the semantic version; `BUILD_NUMBER` is monotonic):

- New user-visible feature or behavior → bump minor; breaking change / milestone → bump major.
- Fixes and small tweaks only → bump patch.
- Rebuild of identical source (re-deploy, doc-only) → keep `VERSION`, increment `BUILD_NUMBER`.
- Commit `VERSION`/`BUILD_NUMBER` and `CHANGELOG.md` before running `Publish-Stable.ps1`.

`Publish-Stable.ps1` is exact-source by default and refuses a dirty working tree. It also creates the
local `stable-vX.Y.Z-bN` tag after deploy and before final verification. Push the commit and tag only
after the verifier prints `VERDICT: RELEASE VERIFIED`. `-AllowVersionBump` and `-AllowDirty` are
diagnostic escape hatches; their manifests/verifier output are marked not release evidence and are
not valid release-candidate QA inputs. If binaries must be signed, pass `-SignScript <path>` so
signing happens before final hashes are written.

## 5. Open the PR

The PR description is pre-filled from `.github/pull_request_template.md`; fill every section:

- Design spec link.
- Requirement IDs served, such as `Q-6`, `REQ-UI-01`, or `REQ-PROFILE-01`.
- Acceptance criteria.
- Local verification commands and results.
- Docs / changelog impact, and any manual QA evidence paths under `docs/evidence/` for
  release-candidate or visual work.

Two GitHub Actions checks run on every pull request:

- **Build and test (Windows)** (`.github/workflows/ci.yml`) — the deterministic test lane and the
  non-mutating build gate.
- **Require design spec** (`.github/workflows/spec-check.yml`) — fails a PR that changes `src/`,
  `scripts/`, or `tests/` without a changed dated spec matching
  `docs/superpowers/specs/YYYY-MM-DD-*-design.md`. To override deliberately (a trivial or urgent
  change), add a line `Spec-Exception: <reason>` to the PR description; the check then passes and
  records the reason.

  **Known limitation, accepted deliberately:** *any* changed dated `-design.md` satisfies the gate. It
  proves a spec **moved**, not that the spec is good or that it describes the change in the PR. It is a
  prompt to think, not a quality bar — reviewers still have to read the spec. The gate also runs
  unconditionally (no `paths:` filter) and decides inside the job, because a path-filtered required check
  can hang a PR in "pending" forever.

  **Deliberately not built:** machine-readable plans, a JSON plan-as-source-of-truth with runtime task
  state, an agent roster, or an observer process. The workflow is intentionally a set of conventions plus
  one CI gate, not a framework.

After a check's first successful run on GitHub, make it a required branch-protection check for `main`
so red commits cannot merge unnoticed.

## 6. Record the session (optional)

After a substantial session — a release, a multi-fix sweep, anything whose *path* (not just its
result) is worth replaying — add a worklog at `docs/superpowers/worklog/YYYY-MM-DD-<topic>.md`. Start
from `docs/superpowers/templates/worklog-template.md`. It captures the request, what was reviewed, the
decisions and why, the verification, and the disposition (branch, PR, tag, deploy). The full Claude
Code transcript is auto-persisted under the session directory; the worklog is the human-readable
summary that survives it. Routine single-task changes don't need one — the spec and plan already cover
them.

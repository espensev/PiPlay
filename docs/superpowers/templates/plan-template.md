# <Feature> — implementation plan

<!--
Copy this file to docs/superpowers/plans/YYYY-MM-DD-<topic>.md and fill it in.
Delete this comment and any guidance comments before committing.
This skeleton captures the house plan shape (see 2026-06-06-profile-edit-validation.md): every task
is a self-contained, independently committable step with its own verification and commit message.
A plan is for multi-step work; a one- or two-step change can live in the design spec alone.
-->

**Spec:** `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`

**Goal:** <one or two sentences: the gap closed and what stays unchanged. Mirror the spec's Goals.>

**Result:** <fill in when done — "implemented", the verification command + counts (e.g. `dotnet test
PiPlay.sln --configuration Debug` = N passing), and anything deferred. Leave a placeholder until then.>

## Tasks

<!-- Each task: a verb-led title, the concrete change, how it's verified, and the commit message it
     produces. Order so each task leaves the tree green and committable. Check boxes as you land them. -->

- [ ] **Task 1 — <title>.**
  - <the concrete change: files, symbols, behavior.>
  - <verification for this step (which test filter / build), expected result.>
  - Commit: `<type(scope): summary referencing the requirement ID>`

- [ ] **Task 2 — <title>.**
  - ...

## Self-review

<!-- Before opening the PR, trace the spec's acceptance criteria to the tasks above, confirm ownership
     boundaries held, name where the risk is concentrated and what covers it, and record the final
     verified test count. -->

- Requirements → tasks: <map each acceptance criterion / requirement ID to the task that satisfies it.>
- Ownership: <which seams/owners were touched; what was deliberately left alone.>
- Risk: <where the risk concentrates, and the tests/checks that cover it.>
- Verified: <final test count / gate result.>

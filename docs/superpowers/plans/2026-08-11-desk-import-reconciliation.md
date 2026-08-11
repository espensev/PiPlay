# Desk import reconciliation — implementation plan

**Spec:** `docs/superpowers/specs/2026-08-11-desk-import-reconciliation-design.md`

**Goal:** Validate and integrate the copied documentation-authority cleanup without changing runtime behavior or losing unrelated local state.

## Tasks

- [ ] **Task 1 — Prove the reconciliation boundary**
  - Files/interfaces: all changed tracked paths, canonical docs, retired artifacts, and Git history
  - Change: classify every path, confirm source/test diffs are comment-only, and confirm removed facts remain canonical or historical
  - Verify: `git diff --check`, removed-path reference scan, and focused diff review
  - Commit: `docs(workflow): record desk import reconciliation design`

- [ ] **Task 2 — Validate the reconciled tree**
  - Files/interfaces: documentation surfaces and full solution
  - Change: resolve only confirmed contradictions or invalid references
  - Verify: documentation consistency checks and `pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1`
  - Commit: `docs: consolidate canonical project guidance`

- [ ] **Task 3 — Integrate and clean coordination artifacts**
  - Files/interfaces: temporary design/plan, GitHub branch, PR, and `main`
  - Change: retire completed coordination files, push the bounded branch, pass checks, squash-merge, and remove the review branch
  - Verify: clean local `main`, matching `origin/main`, merged PR, and no remaining review branch
  - Commit: `docs(workflow): retire completed reconciliation records`

## Unresolved blockers

- None.

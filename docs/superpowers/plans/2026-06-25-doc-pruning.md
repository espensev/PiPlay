# Docs pruning and stale-artifact cleanup - implementation plan

**Spec:** `docs/superpowers/specs/2026-06-25-doc-pruning-design.md`

**Goal:** Remove historical review/discovery clutter from `docs/` while keeping authoritative docs,
release evidence, ADRs, and required change-pass records intact.

**Result:** Implemented in this cleanup pass; verification recorded in the final assistant summary.

## Tasks

- [x] **Task 1 - Classify retained docs.**
  - Preserve product/spec/workflow/release/ADR/superpowers surfaces.
  - Verify by inventorying the docs tree and checking current references.
  - Commit: `docs: classify retained documentation surfaces`

- [x] **Task 2 - Fold stale references forward.**
  - Replace active links to removable review/discovery artifacts with current-doc summaries.
  - Verify with reference scans for pruned path names.
  - Commit: `docs: fold stale review references into active docs`

- [x] **Task 3 - Delete removable artifacts.**
  - Remove folded one-off review logs and old UI discovery scratch evidence.
  - Verify with git status and path scans.
  - Commit: `docs: prune stale review and discovery artifacts`

- [x] **Task 4 - Prune completed historical change-pass records.**
  - Remove old `superpowers` implementation plans/specs after their durable decisions were retained
    in the product spec, changelog, QA checklist, spec-gap summary, or current Theme V2 docs.
  - Keep workflow examples/templates, ADRs, the active Theme V2 reference, and 2026-06-25 working
    records.
  - Commit: `docs: prune completed historical change records`

## Self-review

- Requirements -> tasks: the spec's preservation and pruning criteria map to Tasks 1-3.
- Ownership: docs-only changes under `docs/`; no product code, release evidence, ADRs, or current
  product spec behavior changed.
- Risk: stale or broken references; covered by targeted `rg` scans and `git diff --check`.
- Verified: to be reported in the final cleanup summary.

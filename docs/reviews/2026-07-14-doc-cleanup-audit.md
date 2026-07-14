# PiPlay doc cleanup audit — 2026-07-14

Second pruning pass, run right after the v0.8.0-b29 branch reconciliation. It follows the method and the
retention policy already set by [2026-07-02-doc-clarity-pruning-audit.md](2026-07-02-doc-clarity-pruning-audit.md)
and [README.md](README.md): keep dated change-pass specs/plans **only** when they still explain a current
code boundary, owner decision, or workflow example that active docs would otherwise lose.

## Why now

The reconciliation merged three long-diverged lines and released v0.8.0 (build 29). That made a set of
documented claims false — most importantly in `CLAUDE.md`, which every agent session loads. A pruning pass
that only shortened the docs, without first making them true, would have compressed the errors rather than
removed them. So this pass is **truth first, volume second**.

## Part 1 — Claims the reconciliation made false (repaired)

| Surface | Was | Now |
|---|---|---|
| `CLAUDE.md` (repo root) | Pointed agents at "the `run-piplay` skill's change-verification smoke". | The skill was **deleted** (`b9a9859`); nothing is tracked under `.claude/` and there is no user-level copy. Every session was being pointed at a tool it cannot invoke. Reference removed. |
| `CLAUDE.md` | "update `CHANGELOG.md`" | No root changelog exists — it is `docs/CHANGELOG.md`, and that is a deliberate decision. Corrected, so a literal reading cannot create a stray root file. |
| `docs/SPEC_GAPS_AND_OWNERSHIP.md` (bring-back row) | "Implemented in working tree." | **Released in v0.8.0 (b29)** and deployed. "Working tree" reads as unlanded and invites a re-implementation hunt. |
| `docs/SPEC_GAPS_AND_OWNERSHIP.md` (double-audio row) | Status `NEEDS STABLE SMOKE`, with no indication the fix had shipped. | Status is still correct — the fix is **not** confirmed — but the row now says the fix is released in b29 and deployed, so the smoke is finally *runnable*. For weeks it was not: the fix existed only on an unmerged branch, so every Stable the owner tested lacked it. |
| `docs/SPEC_GAPS_AND_OWNERSHIP.md` (test count) | "687/687" asserted as current. | Re-run and dated: **731/731**. |
| `docs/PiPlay_UI_Priority_Improvements.md` | Framed P4 ("bring video back") as a *current problem*. | P4's core dock/undock shipped in v0.8.0. Status banner added. Deliberately does **not** claim P4 is fully accepted — its remaining acceptance bullets are unmet. |
| `docs/PiPlay_Product_Engineering_Spec.md` | Settings example showed `"schemaVersion": 2`. | `AppSettings.CurrentSchemaVersion` is **4**. Pre-existing drift, unrelated to the merge; fixed while here. |

### The signing contradiction (three surfaces, one decision)

`docs/AGENTS.md` carried an unconditional imperative — *"Sign release binaries with the SevIQ
code-signing certificate before sharing"* — and `docs/QA_Checklist.md` treated signing as a gate. But
signing is **deliberately not a release gate**: the owner signs locally with a self-signed certificate,
which proves nothing that a commit hash does not already prove. v0.8.0-b29 released unsigned. The repo
already carried the correct model in `docs/README.md` ("signing is optional") — it simply contradicted
itself.

The normative root is `REQ-RELEASE-01` in the product spec. **The requirement text is left intact.** It is
not this pass's place to retire a requirement; instead its *status* is now recorded where deferred
requirements belong — `SPEC_GAPS_AND_OWNERSHIP.md` — as deferred and non-gating, with the rationale and
the condition that would revive it (public distribution under a real, non-self-signed certificate). The
operational surfaces (`AGENTS.md`, `QA_Checklist.md`) now describe reality and point at that row.

Release provenance comes from the exact-source commit, the `stable-vX.Y.Z-bN` tag, and
`Verify-StableDeploy.ps1` — not from a signature. **Do not "fix" this by adding an Authenticode gate.**

## Part 2 — Retention decisions

### Kept, and why

| File | Why it survives |
|---|---|
| `specs/2026-06-14-theme-v2-tight-scope-design.md` + plan | Canonical Theme V2 reference; `CON-1 AccentMuted` is still an open owner decision. |
| `specs/2026-06-06-profile-edit-validation-design.md` + plan | Named by `AGENTS.md` as the gold-standard spec/plan example. Deleting them breaks the workflow's own teaching material. |
| `worklog/2026-06-06-stable-publish-session.md` | Named by `AGENTS.md` as the gold-standard worklog. |
| `worklog/2026-07-14-release-pipeline-hardening.md` | Sole carrier of the deploy-swap / publish-lock / tag-preflight rationale. |
| `reviews/2026-06-26-*` pair, `reviews/2026-06-25-claude-dependency-audit.md` | Pinned as current by `reviews/README.md`. |

### Folded forward, then deleted

Nothing was deleted until its live content had a home in an active doc.

| Deleted | What was folded out first, and to where |
|---|---|
| `plans/2026-06-25-b25-full-review-followup.md` | **This plan was not all-closed** — four items were still open in code. X-close capture still samples from the timer rather than taking a fresh DOM read; `PlayerReturnState` carries only a video id, so a different-video return can drop playlist context; the 4-DIP band's visual QA had no row; and the pure-function extraction / locale-regression test never landed. All four moved to `SPEC_GAPS_AND_OWNERSHIP.md` (open items) and `QA_Checklist.md` (the DPI visual row). **See "Parked" below for the P2 conflict it also carried.** |
| `specs/2026-06-11-theme-corners-and-palettes-design.md` | `AccentForThemeSwitch` — *a preset click adopts the new preset's default accent only when the current accent is the previous preset's default, so a custom accent survives theme switches* — was live code with **zero** coverage in any active doc. Folded to `Theme_Preset_Differences.md`, along with the note that `ControlCornerRadius`/`ButtonCornerRadius` are migration aliases with no remaining consumers. |
| `specs/2026-07-11-profile-identity-rail-and-accent-wash-design.md` + plan | Its contrast contract (`SurfaceHover` is the reference surface; 3:1 rail floor; tint floor against `SurfaceBase`; a null profile color yields a transparent rail with the gutter retained) went to the product spec, which previously said only that colors "may be minimally contrast-adjusted". Its open deployed-Stable visual-QA gate went to `QA_Checklist.md`. |
| `specs/2026-06-06-feature-workflow-enforcement-design.md` + plan, `specs/2026-06-06-ci-and-feature-workflow-design.md` + plan | The accepted known limitation — *any changed dated `-design.md` satisfies the gate; it proves a spec moved, not that it is good* — went to `Feature_Workflow.md`. `.github/workflows/spec-check.yml` cited the spec **by path**; that pointer was repaired first. The two pairs referenced each other, so they were removed together. |
| `specs/2026-06-25-p1-borderless-design.md` + plan | Nothing live. The load-bearing constraint (the WebView2 child HWND swallows `WM_NCHITTEST`, so the top-level window must own the edge pixels) is already in the product spec and in comments at the seam; the 4/32 DIP decision is already in `SPEC_GAPS`. |
| `specs/2026-06-25-popout-look-cleanup-and-drop-compact-design.md` + plan | Nothing live. Compact's keep-dormant rationale is already in `SPEC_GAPS`, `QA_Checklist`, and the code's XML docs. |
| `specs/2026-06-25-profile-color-identity-and-accent-fill-design.md` + plan | Superseded by the July-11 pass. `SPEC_GAPS` named it "design of record"; that pointer was repaired first. |
| `specs/2026-07-14-efficiency-and-customization-hardening-design.md` + plan | Superseded: its unresolved decisions are all settled and carried by the retained release-pipeline worklog. Its plan's "Result: diagnostic Stable copy deployed" was already stale. |
| `specs/2026-06-25-ui-owner-followup-fixes-design.md` + plan | **Actively misleading.** Its settled decision #4 asserts that profile colors resolve as app-accent overrides — reversed by the very next pass — and its "Show popout" action no longer exists. Nothing cited it. |

### Parked — an owner decision that must not die with a deleted file

The b25 follow-up plan was the only live carrier of a **direct contradiction between two active docs**:

- `docs/PiPlay_Product_Engineering_Spec.md` — a profile's `accentColor` *"must not replace the global app accent"* (the v0.6.0 split; this is what the code does today).
- `docs/PiPlay_UI_Priority_Improvements.md` **P2** — *"the selected profile color should become the application's primary accent"*, carrying its own `CONFLICT: this reverses the v0.6.0 decision — confirm before implementing.`

This pass deliberately **does not resolve it** — it is the owner's call, and guessing would silently
rewrite either shipped behavior or the roadmap. It is now recorded as an explicit open owner-decision in
`SPEC_GAPS_AND_OWNERSHIP.md`, so it survives the prune and stays visible until called.

## Method note

Both halves were checked against the code, not against memory. That mattered twice: the b25 plan was
*assumed* closed and was not, and an agent memory asserting P2 had already been settled was contradicted
by the docs on disk. Disk won.

## Result

**59 files / 7,969 lines → 42 files / 6,293 lines** (−17 files, −1,676 lines net). The 18 deleted files
carried ~2,700 lines; roughly 1,000 came back as folded-forward content in the active docs and this audit,
which is the trade the pass exists to make — the surviving text is the text that is still true. Test gate
green at 731/731 throughout. Every deletion is recoverable from git history.

## Next candidate (not done here)

`docs/reviews/` itself. The b25 June-26 pair is now four builds stale and demoted to historical. The
July-11 selector-frame review is retained **only** because its low-severity follow-ups were never closed
out one by one — closing or explicitly waiving them would free it for pruning. That is an owner call, not
a janitorial one, so this pass left it alone.

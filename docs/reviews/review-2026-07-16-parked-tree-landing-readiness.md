# Parked working-tree — landing-readiness & commit-grouping review

**Date:** 2026-07-16
**Surface reviewed:** current `main` working tree — 35 changed paths (22 tracked modifications, 13 untracked
additions), no staged changes, at `d11eac5` (release v0.11.0-b34)
**Verdict:** **Landing-ready as two clean commits.** Both parked threads partition to disjoint files; no
hunk-level split is required and either thread can be committed first.

This is a *landing aid*, not a per-feature review — each thread was already reviewed in isolation
(`review-2026-07-15-source-window-return-navigation.md`, `review-2026-07-15-local-ci-runner-audit-map.md`).
The gap those left is **cross-thread**: the two bodies of work sit intermixed in one dirty tree, and the
standing rule is "group the commit, don't `git add -A`." This review closes that gap.

> **No commit was made.** Both threads remain owner-gated. The `git add` lists below are the recommended
> grouping for when the owner chooses to land; nothing here was executed.

## Evidence

- **Whole-tree deterministic gate** (`scripts/Test-LocalCI.ps1`, 2026-07-16): `dotnet test PiPlay.sln`
  (Debug) **959 passed / 0 failed / 0 skipped**; Release `win-x64` build **0 warnings / 0 errors**;
  `LOCAL CI: PASS`. The two threads build and test green *together*.
- **Cross-thread separability** (independent verification): grep for each thread's symbols across the
  other thread's files is **empty in both directions** — no source-return production/test references
  `Test-LocalCI` / `LocalCiPlan` / `ci.yml` / `nodeMajor` / `PIPLAY_WINDOWS_RUNNER`, and no local-ci
  script/test references `MainWindow` / `PlayerWindow` / `BorderlessWindow` / `PopoutAction` /
  `DarkContextMenu` / `EnsureMinSize`. The threads touch disjoint surfaces, so each commit builds and
  tests independently.
- **D-001 (prior audit thread) — closed, no code change.** The `ResizeSubclassStates` static dict retains
  no closed `PlayerWindow`: the entry (the only strong-ref chain to the window) is removed on
  `WM_NCDESTROY`, and `WpfRuntimeTests.Native_move_size_messages_drive_player_activity_and_destroy_cleanup`
  builds a real PlayerWindow, calls the real `Close()`, and asserts the dict entry is gone. Ran in
  isolation 2026-07-16: **Passed 1/1**. This matches the already-committed deep-audit doc's
  *Confirmed-sound paths* line; the newer finding was a conservative re-flag of a closed path.

## Commit grouping

Both groups stage at **file granularity** — no `git add -p`. One rule only: the local-ci commit must be
staged as a whole so its 6 gate-path docs never reference `scripts/Test-LocalCI.ps1` before that file
exists in-tree (all such docs are already inside the local-ci group, so staging the group satisfies it).

### Commit 1 — `source-return` (Source Window return/navigation recovery, Unreleased feature)

Source (6):
```
src/PiPlay/MainWindow.xaml
src/PiPlay/MainWindow.xaml.cs
src/PiPlay/PlayerWindow.xaml.cs
src/PiPlay/Services/BorderlessWindowHelper.cs
src/PiPlay/Services/YouTubeDomBridge.cs
src/PiPlay/Theme/ControlStyles.xaml
```
Tests (7):
```
tests/PiPlay.Tests/BorderlessWindowHelperTests.cs
tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs
tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
tests/PiPlay.Tests/FocusedPinAffordanceScriptTests.cs
tests/PiPlay.Tests/Ui/ContextMenuStyleTests.cs
tests/PiPlay.Tests/Ui/MainWindowLifecycleTests.cs
tests/PiPlay.Tests/Ui/PlayerPinAffordanceTests.cs
```
Docs (9):
```
docs/CHANGELOG.md
docs/PiPlay_Product_Engineering_Spec.md
docs/adr/0005-single-player.md
docs/QA_Checklist.md
docs/PiPlay_UI_Priority_Improvements.md
docs/reviews/review-2026-07-15-source-window-return-navigation.md
docs/superpowers/plans/2026-07-15-source-return-navigation-recovery.md
docs/superpowers/specs/2026-07-15-source-return-navigation-recovery-design.md
docs/superpowers/worklog/2026-07-15-source-return-navigation-recovery.md
```

What it does: single-flight visible Source return (Ready/Open/Returning states), Show Popout (activate
existing) split from Bring video back (capture/close/transfer), independent Source/Popout Pin
suspend-restore across popout, 760×480 DIP minimum-size restore, hidden-Source command gating, a shared
dark profile-actions ContextMenu, and Ctrl+L/F6 URL focus — with its tests and living-spec/ADR/QA/UI
updates. `CHANGELOG.md`'s single `[Unreleased]` block is wholly this thread.

### Commit 2 — `local-ci` (local-first deterministic CI runner, dev tooling)

Code + CI (5):
```
.github/workflows/ci.yml
.github/pull_request_template.md
scripts/Test-LocalCI.ps1
tests/PiPlay.Tests/LocalCiPlanTests.cs
tests/PiPlay.Tests/ReleaseScriptPolicyTests.cs
```
Gate-path docs (8):
```
CLAUDE.md
docs/AGENTS.md
docs/Feature_Workflow.md
docs/README.md
tests/README.md
docs/discovery-local-runner-default.md
docs/superpowers/specs/2026-07-15-local-first-ci-runner-design.md
docs/reviews/review-2026-07-15-local-ci-runner-audit-map.md
```

What it does: a one-command deterministic gate (`Test-LocalCI.ps1`) with a side-effect-free `-Plan`
projection, per-run GUID `PIPLAY_DATA_ROOT`, fail-closed exit checks, env save/restore, and best-effort
cleanup; `ci.yml` collapses to a single wrapper call with `push.branches:[main]` (tag-dedup),
`case()`-based `runs-on` routing, `timeout-minutes:20`, SHA-pinned actions, `persist-credentials:false`;
plus `LocalCiPlanTests` (pins the plan contract) and `ReleaseScriptPolicyTests` (+69 additive, pins the
gate/ci.yml policy). `ReleaseScriptPolicyTests.cs` is the only pre-existing tracked file here and its diff
is purely additive.

> This review doc (`review-2026-07-16-parked-tree-landing-readiness.md`) is a cross-thread session
> artifact outside both groups — commit it separately or discard; it belongs to neither feature.

## Residual notes (all accepted / non-blocking)

- **local-ci Node-24 rejection guard** is exercised only by source-text/plan assertions, not by an
  executable non-24 run — the accepted residual of the local-ci audit's Note 2.
- **local-ci self-hosted routing seam is inert** until `vars.PIPLAY_WINDOWS_RUNNER` is set (every event
  resolves to `windows-latest` today) — accepted Note 4; `case()` is a real post-cutoff GHA function.
- **`FocusedPinAffordanceScriptTests` / `PlayerSurfaceScriptTests`** assert exact full-line substrings of
  generated JS (change-detector brittleness), but verify a real accessibility contract, consistent with
  existing script-string tests.
- Both threads independently satisfy `spec-check.yml`'s dated-design-spec gate (each ships its own
  `docs/superpowers/specs/2026-07-15-*-design.md`).

## Limits

- This is a **static + build/test** landing review. It certifies the two-commit split and green gate; it
  is **not** deployed manual-QA evidence. Both threads' own docs keep their manual/deployed QA rows open,
  and deployed Stable remains `v0.11.0-b34`. A sanctioned clean-source `Publish-Stable.ps1` run is still
  required before any deployed-acceptance claim (ADR-0007 / `QA_Checklist.md`).
- No commit, tag, push, or Stable publish was performed.

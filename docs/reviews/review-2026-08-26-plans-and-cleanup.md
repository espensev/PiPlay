# Review - Plans, cleanup, and runtime deadlines

**Date:** 2026-08-26
**Surface:** working-tree runtime-deadline patch against local `main` `c5ba6a4`, plus `docs/SPEC_GAPS_AND_OWNERSHIP.md` and live PR state
**Spec source:** `docs/PiPlay_Product_Engineering_Spec.md`; current user request to find remaining plan or cleanup work
**Standards sources:** `CLAUDE.md`, `docs/AGENTS.md`, `.gitignore`
**Verdict:** FAIL - publication remains blocked; the local deadline patch is commit-ready, but one remote integration hazard remains

## Findings

### High

- [axis: regression] PR #36 remains open and appears mergeable only against stale remote `main`.
  Evidence: live `gh pr view 36` reports `OPEN`, `MERGEABLE/CLEAN`, base `a9bbe37`, and head `667661e`; the fixed-point takeover review already proved 17 conflicts against the released local line (`docs/reviews/review-2026-08-26-pr-36-takeover.md:13-26`).
  Impact: the green GitHub badge can invite an obsolete merge that bypasses the released b39 corrections and current canonical documentation.
  Recommendation: do not merge or rebase PR #36. After this patch is committed and explicit publication approval is received, push current `main`, then close PR #36 as superseded. Both operations are external mutations and remain intentionally unperformed.

### Low

- [axis: standards] Legacy plan identifiers remain embedded throughout the implementation after their owning plan documents were removed.
  Evidence: representative stale or user-visible examples remain at `src/PiPlay/PlayerWindow.xaml:40` ("always visible in MVP; fade is Phase 2" although Fade is current product behavior) and `scripts/Verify-StableDeploy.ps1:201` (asks the operator to republish with an undefined "Phase 0 provenance pipeline").
  Impact: comments and failure guidance send maintainers toward deleted planning context and make current behavior harder to navigate.
  Recommendation: perform a bounded comment/message-only cleanup: retain the rationale, replace useful anchors with current spec/Q-section references, and remove historical phase/task labels. Do not introduce a replacement worklog.

- [axis: regression] A blanket ignored-file cleanup would mix disposable build caches with local recovery state and release evidence.
  Evidence: `git clean -ndX` would include `.remember/`, `.superpowers/`, `docs/.remember/`, `docs/evidence/`, `Archive/`, and `.claude/` alongside caches. Measured disposable candidates are `.vs/` at 143.68 MiB and current source/test `bin`/`obj` output at 28.59 MiB. Root `bin/` is 107.32 MiB and contains published copies plus 42 release archives (96.84 MiB); one old `PiPlay-20260716-130215-v0.12.1-b36.zip` is 62.90 MiB with 420 framework-heavy entries, unlike the current non-self-contained release contract.
  Impact: `git clean -fdX` can erase handoff/recovery data or evidence together with regenerable output; indiscriminate archive pruning can remove the only convenient copy of an old artifact.
  Recommendation: never clean ignored content as one set. Cache cleanup may target `.vs/`, project/test `bin`/`obj`, and stale `TestResults` explicitly. Treat root `bin/publish/archive` as a separate retention decision; the 62.90 MiB legacy archive is the first candidate after backup/ownership is confirmed.

## Reconciled Canonical Snapshot

The working tree resolves the two highest-priority implementation gaps: normal-page DOM execution now has a `5 s` foreground deadline and remains single-flight per WebView, while a connected single-instance client has `2 s` to finish its pipe payload. The shared deadline adapter cancels token-aware work and observes late completion from non-cancellable work (`AsyncOperationDeadline`, `YouTubeDomBridge`, `SingleInstancePipePolicy`, `RuntimeFailurePolicyTests`).

The sole canonical backlog now contains five verified implementation gaps, one pending visual-acceptance evidence item, and four deferred product decisions in `docs/SPEC_GAPS_AND_OWNERSHIP.md`. This review does not duplicate or reorder that backlog. The four deferred choices remain non-blocking until explicitly selected.

## Verification

- `git status --short --branch` - pass; the expected nine-path runtime-deadline/review surface is dirty before commit.
- `git rev-list --left-right --count 'HEAD...@{upstream}'` - pass; local `main` is ahead `6`, behind `0` before this patch is committed.
- `gh pr view 36 --json ...` - pass; PR open at `667661e`, green only against remote base `a9bbe37`.
- `gh issue list --state open --limit 100 --json ...` - pass; no open GitHub issues.
- Source checks - pass; five canonical implementation gaps remain, the profile-selector visual check is now tracked as acceptance evidence, and the two removed deadline gaps are implemented in this patch.
- Focused runtime-policy plus contended-dispatcher test under process-only `WINDIR=$env:SystemRoot` - pass, 29/29.
- Full direct Debug suite under the same process-only correction - pass, 1045/1045.
- `scripts/Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` under the same process-only correction - pass; Release build has 0 warnings and 0 errors.
- `scripts/Test-LocalCI.ps1` - blocked before tests by the previously documented SDK 10.0.400 `dotnet --info` host exception; the wrapper is not reported as a pass.
- `git diff --check` - pass.
- `git clean -ndX` plus size/archive inventory - read-only pass; no files deleted.
- Real WebView2 renderer-stall and connected silent-client integration checks - not run; deterministic injected seams cover the foreground deadline, adapter cancellation, single-flight recovery, dispatcher affinity, and constants.

## Coverage Notes

- Files reviewed deeply: `src/PiPlay/Services/AsyncOperationDeadline.cs`, `src/PiPlay/Services/SingleInstancePipePolicy.cs`, `src/PiPlay/Services/YouTubeDomBridge.cs`, `src/PiPlay/App.xaml.cs`, `tests/PiPlay.Tests/RuntimeFailurePolicyTests.cs`, `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`, `docs/SPEC_GAPS_AND_OWNERSHIP.md`, `docs/AGENTS.md`, `docs/PiPlay_Product_Engineering_Spec.md`, `docs/QA_Checklist.md`, both other 2026-08-26 review/QA records, and `.gitignore`.
- Repository-wide searches covered TODO/fix markers, provisional/open-status wording, legacy plan identifiers, ignored paths, build output, release archives, branch/upstream state, open PRs, and open issues.
- Excluded: full behavioral review of earlier unpublished commits, destructive cleanup, publication, PR changes, deployed UI/runtime changes, real renderer/pipe integration fault injection, and machine configuration.

## Open Questions

- After this patch is committed, does the owner authorize pushing current `main` and closing PR #36 as superseded despite the already documented host-only `dotnet --info` blocker?
- When should the now-tracked profile-selector Stable visual check be run?
- Is the 62.90 MiB legacy self-contained b36 archive still intentionally retained elsewhere?

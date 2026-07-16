# Session worklog — runtime failure hardening (2026-07-16)

Saved record of the deep-audit evaluation and focused remediation working session.

## Request

> "EVALUET AND ADRESS"

The request followed the PiPlay runtime-efficiency discovery pass. It authorized focused product-code remediation while live profiling, deployment, and manual runtime mutation remained out of scope.

## What was reviewed

- Shared machine/root/docs instructions, feature workflow, product engineering spec, privacy/compliance docs, and single-player/shared-environment ADRs.
- Current `main` at `8015ba4`, the pre-existing dirty-tree boundary, and the deployed Stable identity/hashes. Stable remained at `d11eac5`, so it was not used for current-source runtime claims.
- Startup/single-instance handoff, WebView initialization, Source suppression, Popout state sync, Focused DOM work, privacy clearing, settings persistence, logging, WPF accent preview, return, and shutdown ownership.
- Eight discovery traces in independent read-only lanes, plus independent design/test reviews before implementation.
- Current Microsoft WebView2 documentation for `ClearBrowsingDataAsync` and `CoreWebView2Environment.CreateAsync` task/ownership semantics.

## Decisions

- Fix the session-mismatched named-pipe identity and unbounded failure retry loop.
- Retain an underlying browser-data clear after the 30-second foreground timeout and block only a second Clear until terminal completion.
- Require a true post-command mute+pause acknowledgement before constructing a Popout; keep the 1 Hz Q-1 guard unchanged.
- Coalesce WebView host errors per WebView and exact operation, so unrelated success cannot reopen a persistent failure log episode.
- Preserve equal frozen accent resources and replace only actual derived-color changes.
- Reject removal of the two Popout-return settings saves because spec section 14 requires both durability checkpoints.
- Reject shared-environment single-flight work for the current runtime because no credible parallel caller is reachable.
- Leave Focused DOM cost and repeated Popout lifecycle settling measurement-bound; no static leak/observer rewrite was justified.

## Implementation

- New:
  - `src/PiPlay/Services/BrowserDataClearCoordinator.cs`
  - `src/PiPlay/Services/ConsecutiveFailureGate.cs`
  - `src/PiPlay/Services/PopoutLaunchPolicy.cs`
  - `src/PiPlay/Services/SingleInstancePipePolicy.cs`
  - `tests/PiPlay.Tests/RuntimeFailurePolicyTests.cs`
  - dated design/plan and `.audit/deep-audit/piplay-runtime-2026-07-16/` trace artifacts
- Edited:
  - `App.xaml.cs`, `MainWindow.xaml.cs`, `YouTubeDomBridge.cs`, privacy/Settings surfaces, and `ThemeResourceApplier.cs`
  - WPF runtime tests and the Unreleased changelog

## Verification

- Red gate: focused tests initially failed to compile on the intentionally absent policies/seams.
- Focused green gate: 26/26 passed after the final orchestration/race regressions were added.
- Full Debug solution: 985/985 passed (baseline was 959/959).
- Canonical `scripts/Test-LocalCI.ps1`: PASS; restore, 985 tests, and Release build completed with 0 warnings / 0 errors.
- `scripts/Preflight-SpecGate.ps1`: PASS with the dated design present.
- Live/manual WebView, cross-session, fault-injection, and profiling checks were not run. They require a current-source Stable deployment and explicit authority.
- Two independent final reviews: PASS after their timeout-boundary, live-Settings, and orchestration-coverage findings were addressed; no actionable production/test issue remained.

## Disposition

- Working tree on local `main`; no commit, push, PR, release, or deployment was requested or performed.
- The pre-existing untracked `docs/reviews/review-2026-07-16-parked-tree-landing-readiness.md` was not modified.

## Commits

- None; changes remain uncommitted for owner review.

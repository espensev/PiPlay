# Local-first CI runner — design

## Goals

Make PiPlay's deterministic Windows gate one command locally and in GitHub Actions, then provide a
safe routing seam for the already-proven isolated runner pattern. The canonical pull-request job for
this public repository must continue to run on disposable GitHub-hosted Windows machines. Trusted
`main` pushes and manual dispatches may use a PiPlay-specific self-hosted label only after a
disposable, isolated runner is registered.

No WPF, WebView2, playback, settings, packaging, or Stable-deploy behavior changes in this pass.

## Requirements served

This is developer tooling and CI policy rather than a product-spec requirement. It serves the
deterministic local gate in `docs/Feature_Workflow.md` and preserves the protected check context
`Build and test (Windows)`.

## Acceptance criteria

- One repo-owned PowerShell command runs SDK diagnostics, restore, the full Debug test suite, and the
  non-mutating Release build gate in the same order locally and in Actions.
- Tests use a unique `PIPLAY_DATA_ROOT`; the prior process environment and working directory are
  restored, and the temporary root is removed on success or failure unless explicitly preserved.
- A side-effect-free JSON plan exposes the exact command vectors and receives executable regression
  coverage without recursively running the full gate from inside the test suite.
- Pull-request runs of the canonical job select `windows-latest`.
- Trusted `main` pushes and `workflow_dispatch` runs select `PIPLAY_WINDOWS_RUNNER` when configured,
  otherwise they fall back to `windows-latest`.
- Stable-tag pushes no longer launch a duplicate build of the same commit.
- The job name remains `Build and test (Windows)` so branch protection keeps the same check context.
- Official actions are pinned to immutable commits, and checkout does not persist its token.
- The runner variable remains unset until a distinct disposable PiPlay runner is online and a manual
  dispatch proves its label and workload. Registration itself is an exposure boundary in a public
  repository because contributed workflows can propose targeting the label directly.

## Settled decisions

1. **One job owns both executor choices.** Dynamic `runs-on` preserves one check identity and avoids
   duplicating the gate definition.
2. **The canonical public-PR job stays hosted.** Its expression is routing policy, not an access
   control boundary: a contributed workflow can propose targeting a registered label directly.
3. **Trusted events are local-first only after configuration.** An unset repository variable is a
   safe deployment and maintenance fallback; it is not automatic runtime failover for an offline
   selected runner.
4. **`case(...)` expresses the routing rule directly.** The first predicate forces PRs to hosted
   Windows; the default branch resolves the configured label or hosted fallback.
5. **The wrapper owns only the existing CI payload.** Spec preflight and `git diff --check` remain
   separate because they use working-tree/PR context not present in a clean Actions checkout.
6. **Tags do not rerun main validation.** `push.branches: [main]` keeps post-merge evidence while
   eliminating the observed main-plus-Stable-tag duplicate.
7. **Runner registration is infrastructure, not source configuration.** Tokens, guest credentials,
   VM scripts, service setup, and runner diagnostics remain outside Git.

## Non-goals / out of scope

- Registering a PiPlay runner before a separate, disposable guest service boundary is ready.
- Sending `pull_request` or `pull_request_target` code to a persistent local runner.
- Automatic fallback after a self-hosted job has already queued.
- Moving the Ubuntu design-spec check to Windows.
- Changing branch protection, repository visibility, release stamps, or Stable deployment.
- Installing machine-global Git hooks without an explicit opt-in.

## Testing approach

- Logic: invoke `Test-LocalCI.ps1 -Plan -AsJson` twice and assert schema, requirements, exact Node
  check and gate step order/arguments, unique uncreated data roots, and build-gate flags.
- Static policy: assert the wrapper fails closed on native exit codes and restores/cleans state; assert
  the workflow retains its check name, hosted-PR routing, variable fallback, main-only push filter,
  pinned non-persisting checkout, and single wrapper invocation.
- Execution: run the wrapper itself, then run spec preflight and diff checks.
- Live follow-up: after runner registration, set `PIPLAY_WINDOWS_RUNNER=piplay-ci`, manually dispatch
  CI, and verify the Actions Jobs API reports the intended runner. Clear the variable and rerun on
  hosted Windows for recovery proof.

## Changes by file

| File | Change |
|---|---|
| `scripts/Test-LocalCI.ps1` | Add the canonical executable gate and side-effect-free plan contract. |
| `.github/workflows/ci.yml` | Route PRs hosted, trusted events by variable, filter tag pushes, pin actions, and call the wrapper. |
| `tests/PiPlay.Tests/LocalCiPlanTests.cs` | Execute and validate the JSON plan. |
| `tests/PiPlay.Tests/ReleaseScriptPolicyTests.cs` | Pin cleanup/fail-closed and workflow-routing invariants. |
| `docs/README.md` | Make the wrapper the local front door. |
| `docs/Feature_Workflow.md` | Describe the shared command and executor boundary. |
| `docs/AGENTS.md` | Update the mandatory pre-PR gate. |
| `tests/README.md` | Record the wrapper as CI's deterministic lane owner. |
| `.github/pull_request_template.md` | Pre-fill the canonical verification command. |
| `CLAUDE.md` | Keep the repository execution boundary aligned with the shared gate. |
| `docs/discovery-local-runner-default.md` | Link the accepted implementation direction. |

## Docs & changelog impact

Contributor and test documentation changes with the workflow. `docs/CHANGELOG.md` is unchanged
because this does not alter the shipped application or release identity.

## Unresolved decisions

- The proven `SQ-CI-WIN` guest remains exclusive to its repository-scoped SQ-Control service; it is
  not a PiPlay candidate because sharing would cross the isolation boundary and two services could
  overlap on the same 8-vCPU guest. Its provisioning administrators are also retired. Enabling the
  repository variable therefore waits on a separate disposable PiPlay VM, not same-guest reuse.

# Discovery — Local Runner as the Default

**Goal:** Look at implementing the local runner more as the default for PiPlay.
**Date:** 2026-07-15
**Status:** complete
**Implementation outcome:** the local-first developer gate and trusted-event routing seam are now in
the working tree. Keep the repository variable unset; provision a self-hosted lane only on a separate
disposable host that is safe to expose to contributed workflow definitions.

---

## Questions

1. What does “local runner” map to in the current PiPlay repository?
2. Where is runner selection defaulted, persisted, and overridden?
3. What compatibility, availability, and security constraints apply?
4. Does this affect any PiPlay runtime or remote/sub-panel UI?
5. What is the smallest safe implementation and verification surface?

---

## Findings

The inventory below records the pre-implementation state that motivated the accepted direction.

### Q1: What does “local runner” map to in the current PiPlay repository?

**Answer:** It maps most naturally to a GitHub Actions self-hosted runner, not to a PiPlay runtime feature. PiPlay currently has no self-hosted runner configuration or app feature named “runner.” It does have a documented local CI-equivalent command pair, but no one-command wrapper or installed Git hook.

**Evidence:**

- `.github/workflows/ci.yml:15-18` — the only Windows build/test job is hard-coded to `windows-latest`.
- `.github/workflows/spec-check.yml:24-29` — the separate design-spec check is hard-coded to hosted Ubuntu and uses Bash/GitHub CLI tooling.
- `docs/README.md:65-70` and `docs/Feature_Workflow.md:88-103` — local verification is documented as direct `dotnet test` and `Build-PiPlay.ps1` commands.
- Repository-wide search found no `self-hosted`, Actions runner registration, `act`, or app/runtime runner surface. Other literal runner hits are `${{ runner.temp }}` and the xUnit runner package.
- Live `gh api repos/espensev/PiPlay/actions/runners` returned `total_count: 0`.

**Implications:**

- No current PiPlay setting can simply be flipped from remote to local.
- A GitHub self-hosted rollout requires both external runner registration and a workflow routing change.
- A local-first developer gate can be implemented entirely inside the repository without an always-on service.

### Q2: Where is runner selection defaulted, persisted, and overridden?

**Answer:** Runner selection is a literal workflow value. There is no repository variable, runner group, reusable workflow, or fallback mechanism today.

**Evidence:**

- `.github/workflows/ci.yml:18` — `runs-on: windows-latest` is the Windows selection point.
- `.github/workflows/spec-check.yml:27` — `runs-on: ubuntu-latest` is the spec-check selection point.
- Live repository Actions state reported zero self-hosted runners and zero Actions variables.
- GitHub routes self-hosted jobs by matching labels; every label in an array must match. An unavailable match queues rather than falling through to a hosted runner.

**Implications:**

- `runs-on: [self-hosted, Windows, X64, piplay-ci]` is mechanically sufficient only after a matching runner exists.
- A repository variable can choose a label for new runs, but it is configuration fallback, not runtime failover. If a selected runner is offline, the run must be cancelled/retried after changing the selection.
- The Ubuntu spec check should remain hosted unless a separate Linux runner is provisioned or the gate is ported.

### Q3: What compatibility, availability, and security constraints apply?

**Answer:** Windows x64 is the correct host shape, but making a persistent local runner the normal PR executor is not appropriate while PiPlay is public. Hosted standard runners are already free for this repository, and the current job is short and reliable.

**Evidence:**

- `global.json:1-5` requires .NET SDK 10.0.300; `.github/workflows/ci.yml:29-41` provisions that SDK and Node 24.
- `src/PiPlay/PiPlay.csproj:3-6` and `scripts/Build-PiPlay.ps1:17-25` establish the Windows/WPF, win-x64 build boundary.
- Live repository state: `espensev/PiPlay` is public; fork workflow approval is only `first_time_contributors`; all actions are allowed and SHA pinning is not required.
- Live branch protection requires `Build and test (Windows)`, is non-strict, and does not enforce administrators. A similarly named ruleset also exists but is disabled.
- The latest `main` job completed successfully in 86 seconds. The last 44 main CI runs were 42 successful and 2 concurrency-cancelled, with no failures.
- GitHub warns that fork pull requests against public repositories can run dangerous code on self-hosted infrastructure, recommends self-hosting primarily for private repositories, and states that unmatched jobs can queue for up to 24 hours.
- Standard GitHub-hosted runners are free and unlimited for public repositories.
- The existing `snd-host-sq-ci-win` runner is online for `espensev/SQ-Control`, but repository-level runners are dedicated to one repository. `espensev` is a user account rather than an organization, so that runner is not an organization-level pool PiPlay can select.

**Implications:**

- Do not register the interactive `SND-DESK` workstation as PiPlay's public runner.
- Do not co-locate a public PiPlay runner with the existing SQ-Control runner; compromise or residue would cross the intended repository boundary.
- If self-hosting is still desired, use a separate low-privilege, disposable/ephemeral Windows VM with no user data, deploy directories, signing material, SSH keys, PATs, or unrelated drive access.
- Keep the canonical pull-request check on a disposable GitHub-hosted runner. A trusted push/manual self-hosted lane can be added later, but registering any persistent runner to a public personal repository still increases exposure because a contributed workflow can target its label.

Official references:

- [Choosing a runner for a job](https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job)
- [Self-hosted runner routing and queue behavior](https://docs.github.com/en/actions/reference/runners/self-hosted-runners)
- [GitHub warning for self-hosted runners on public repositories](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners)
- [GitHub Actions billing for public repositories](https://docs.github.com/en/billing/concepts/product-billing/github-actions)

### Q4: Does this affect any PiPlay runtime or remote/sub-panel UI?

**Answer:** No. Runner routing is repository automation only. It does not affect PiPlay's Standard, Focused, or Compact playback modes, the local `piplay.local` shell, the remote controls, or sub-panel placement.

**Evidence:**

- The only product-side “local” playback surface is the Compact shell described by `src/PiPlay/Services/PlaybackModePolicy.cs:8-16` and `docs/YouTube_Compliance.md:9-27`; it is consistently called a shell/mode, not a runner.
- Repo-wide runner hits are confined to workflow context and test-runner dependencies.

**Implications:**

- This change can be designed and verified without touching WPF, WebView2, playback policy, or UI assets.

### Q5: What is the smallest safe implementation and verification surface?

**Answer:** Make the repository's deterministic local gate one command and reduce redundant hosted triggers first. Treat self-hosting as a separate infrastructure step, not the default PR path.

**Evidence:**

- `.github/workflows/ci.yml:3-6` currently runs on every PR, every push, every tag push, and manual dispatch.
- The `main` and `stable-v0.11.0-b34` pushes launched two identical Windows jobs for commit `d11eac56325404553b59f4515f6cae81b54cdf36`: run `29406379846` took 86 seconds and run `29406379901` took 93 seconds.
- `docs/README.md:65-70` already defines the exact local payload, so a wrapper would consolidate existing policy rather than invent a new gate.

**Implications:**

1. Add a repo-local `scripts/Test-LocalCI.ps1` (or equivalently named wrapper) for restore, deterministic tests, non-mutating Release build, spec preflight, and diff checks.
2. Make that wrapper the documented default before push; offer an opt-in pre-push hook rather than silently installing a machine-global hook.
3. Narrow hosted workflow triggers to pull requests and `main` pushes, excluding release-tag duplication unless tag-only validation is deliberately required.
4. Keep `Build and test (Windows)` hosted and keep its check name stable for branch protection.
5. If a dedicated runner is later provisioned, add a distinct trusted-event self-hosted lane labelled `piplay-ci`; keep hosted recovery explicit because GitHub has no automatic cross-runner fallback.

---

## Cross-Cutting Analysis

### Constraints

- PiPlay has no available self-hosted runner today; flipping `runs-on` now would only queue jobs.
- The repository is public and personally owned, so organization runner-group workflow restrictions are not available in the current ownership shape.
- The current required check context is `Build and test (Windows)` and should remain stable.
- The self-hosted Windows host must support .NET 10.0.300, Node 24, PowerShell, WPF/win-x64 builds, outbound GitHub/NuGet access, and writable work/temp/tool-cache paths.

### Risks

| Risk | Likelihood | Impact | Notes |
|---|---:|---:|---|
| Public PR code compromises a persistent runner | Medium | High | GitHub explicitly warns about this boundary. |
| Offline runner stalls a required job | Medium | High | No automatic hosted fallback; unmatched jobs can queue for 24 hours. |
| Co-locating PiPlay and SQ-Control leaks cross-repo state | Medium | High | Persistent self-hosted jobs do not receive a fresh VM by default. |
| Local wrapper drifts from CI | Low | Medium | Make the workflow call the same wrapper once stabilized. |
| Duplicate branch/tag CI wastes time | High | Low | Proven on the v0.11.0 release commit. |

### Open Questions

- Whether the desired “local runner” means a one-command local developer gate or a GitHub-connected self-hosted service. The evidence favors the latter wording, but the safe immediate implementation is the former.
- Whether PiPlay should remain public. If it becomes private or moves into an organization with restricted runner groups, the self-hosted recommendation can be revisited.

---

## Recommendation

Proceed directly with a **local-first developer gate plus CI trigger cleanup**. This is a small repository change with a clear verification path and no infrastructure exposure.

Do **not** replace the hosted PR executor with a persistent local runner in the current public, personal-repository setup. If trusted self-hosted execution is still wanted afterward, provision a dedicated disposable Windows VM, harden Actions/fork policy and action pinning, register it specifically for PiPlay, then add it as a separate trusted push/manual lane while the hosted PR check remains canonical.

**Accepted direction (2026-07-15):** implement the shared local gate and trusted-event routing now,
with the runner variable deliberately unset until a PiPlay-specific service is ready. The proven
`SQ-CI-WIN` pattern supplies the host baseline, but its existing repository service remains scoped to
SQ-Control and public pull requests remain hosted.

# Review — Local-first CI runner: deep audit (executed)

**Date:** 2026-07-15
**Surface:** working tree (unstaged, uncommitted) — the `Test-LocalCI.ps1` change set
**Spec source:** `docs/superpowers/specs/2026-07-15-local-first-ci-runner-design.md` (+ `docs/discovery-local-runner-default.md`)
**Standards sources:** `CLAUDE.md`, `docs/AGENTS.md`
**Verdict:** PASS WITH NOTES — no merge-blocker; workflow payload demonstrated sound (940 green + full gate PASS). Four notes (Note 1 LOW–MEDIUM robustness, Note 2 LOW coverage, Note 3 design observation, Note 4 forward-risk) plus two minor. The headline `case()` "merge-blocker" from the first pass was a stale-knowledge false alarm, executed to ground and retracted.

> **Correction up front (epistemic honesty).** The first scoping pass flagged `runs-on: ${{ case(...) }}` as a near-certain **merge-blocking defect** on the belief that `case()` is not a GitHub Actions function. **That was wrong.** Executing the check overturned it (evidence below). The lesson stands but inverts: the danger here wasn't the code — it was a confident, stale prior (mine and the advisor's) that a text-only test could neither confirm nor refute. I verified before asserting; the report reflects the verified result.

---

## Executed verifications (what actually ran)

| Check | Command / method | Result |
|---|---|---|
| `case()` is a real GHA function | `actionlint 1.7.12` on `ci.yml` **and** on a probe file mixing `case()` with a bogus fn | **CONFIRMED real.** actionlint lists available functions incl. `"case"`; flags `totallyBogusFn` but accepts `case`. Two docs fetches (one neutral, verbatim) also list `case`. |
| `case()` routing *logic* | manual trace vs documented semantics `case(pred1,val1,…,default)` | **Reads correct, but currently inert** — see Note 4. PR → `windows-latest`; other events → `vars.PIPLAY_WINDOWS_RUNNER || 'windows-latest'`, and that var is deliberately unset, so **every event resolves to `windows-latest` today** regardless of how `case()` resolves its default. |
| Action SHA-pins match their tags | `gh api repos/<a>/commits/<tag>` vs pinned SHA ×3 | **All MATCH** (checkout v6, setup-dotnet v5, setup-node v6). |
| `persist-credentials: false` safe | read `Build-PiPlay.ps1` Build path | **Safe.** `-Stage Build` exits at L907 before any publish; only git touch is read-only `rev-parse` (no token needed). |
| Node-24 gate regex | ran `^v?24\.` against `v24.3.0/v22/v240/v26/''` | **Correct** (accepts 24.x, rejects others). |
| Env save/restore | replicated `Set-/Restore-ProcessEnvironment`, exercised existed / not-existed / two-layer overlap | **Correct for the cases that matter** (existing vars detected + restored; two-layer nesting unwinds base→common→test correctly). |
| `Test-Path Env:` reliability | clean-room fresh-GUID probe | Works (`never-set → null/False`); earlier anomaly was tool-process name pollution, not a script bug. |
| `-Plan` runs side-effect-free | `Test-LocalCI.ps1 -Plan` | Clean; prints exact vectors; no temp root created. |
| `finally` exception masking | control-flow probe (throw-in-try + throw-in-finally) | **Reproduced** → Note 1. |

---

## Notes (non-blocking)

### Note 1 — LOW–MEDIUM (robustness): fatal cleanup can both mask a failure *and* flip a passing run to failed
`scripts/Test-LocalCI.ps1:228-229` — the innermost `finally` runs `Remove-Item -LiteralPath $testDataRoot -Recurse -Force -ErrorAction Stop`. The tests write real files under this root, so a transient Windows lock (testhost handle, Defender scan) at cleanup time is plausible. Two failure modes, both confirmed by control-flow reading + a PowerShell semantics probe:
- **Masking:** `dotnet test` fails → the cleanup `Remove-Item` throws from the `finally` → that exception *replaces* the test-failure exception. The log shows "access denied," not "N tests failed."
- **False-negative:** `dotnet test` *passes* → cleanup throws → the exception propagates out of the `foreach`, so the **`build` step is skipped and a fully green run is reported as FAILED.**

The gate still fails **closed** in both cases (good), but one masks the real cause and the other is flaky-red on healthy code. The old CI wrote to `${{ runner.temp }}` and let the runner discard it; this change makes cleanup a hard gate — and `ReleaseScriptPolicyTests.cs:267` (`DoesNotContain("Could not remove local CI test data")`) actively **forbids** the graceful catch-and-warn fix. Recommendation: best-effort the removal (warn, don't throw) when unwinding, and drop the assertion that bans it.

### Note 2 — LOW (test coverage / two-sources-of-truth): Node-24 enforcement is untested and decoupled from the plan
The enforced rule lives in an execution-only literal — `if ($nodeVersion -notmatch '^v?24\.')` (`:174`) — which **no test exercises** (`LocalCiPlanTests` only runs `-Plan -AsJson`; it never runs the real gate; `ReleaseScriptPolicyTests` only string-matches the wrapper). Meanwhile the tested/declared value is `requirements.nodeMajor = 24` in the *plan*, which execution never reads. The two can silently diverge (bump one, forget the other) and the suite stays green. Low impact today (a broken regex would fail loudly in CI), but it's a real guard-confidence gap.

### Note 3 — DESIGN OBSERVATION: the `-Plan` JSON is partly decorative
The safety story ("the plan exposes the exact command vectors") holds for what execution actually consumes: top-level `environment`, and each step's `name` / `filePath` / `arguments`, plus the **test** step's `environment`. But several advertised, test-pinned fields are **never read by execution** — it re-derives them from its own variables/literals:
- `workingDirectory` → execution uses `$repoRoot` directly (`Push-Location`, `:208`).
- `cleanupTestDataRoot` → cleanup is gated on `$KeepTestDataRoot` (`:226`), not this field.
- `requirements.*` → preflight checks literals `global.json` / `pwsh` / the `^v?24\.` regex, not these values.
- non-test steps' `environment` → only the test step's env is applied (`:217`); the others are inert `{}`.

Not a bug (values currently agree), but readers/tests treat the plan as the contract when parts of it are documentation that can drift from behavior. If the plan is meant to be authoritative, execution should consume it (e.g. enforce Node from `requirements.nodeMajor`, cleanup from `cleanupTestDataRoot`).

### Note 4 — FORWARD RISK: the self-hosted routing is unproven and inert today
`case()` *exists* and *reads* correctly, but the routing it enables is not actually exercised. `PIPLAY_WINDOWS_RUNNER` is deliberately unset, so `vars.PIPLAY_WINDOWS_RUNNER || 'windows-latest'` collapses to `windows-latest` for **every** event — the self-hosted branch never resolves to a real label until someone sets that variable. So what is proven today is "the expression is valid and always yields hosted Windows"; the *live routing to a self-hosted runner* — the whole point of the seam — is unverified and cannot be verified from the repo. That is also exactly where the real future risk sits: the design doc itself concedes a contributed workflow can target the runner label directly, outside this expression. Treat the day the variable goes live as its own review gate (the design's own "Live follow-up" testing step), not as something this change already validated. One latent constraint for that day: `vars.PIPLAY_WINDOWS_RUNNER` is a single scalar, so `runs-on` can carry only **one** label — it cannot express the `[self-hosted, Windows, X64, piplay-ci]` array the discovery doc recommends (`docs/discovery-local-runner-default.md:57`) without switching to a `fromJSON` form. A single unique label can still route, but the multi-label precision isn't reachable as written.

### Minor
- **Custom step-failure messages are mostly dead on the CI shell.** On pwsh 7.4 (`windows-latest`), `$PSNativeCommandUseErrorActionPreference=$true` + `$ErrorActionPreference='Stop'` makes a native non-zero exit auto-throw `NativeCommandExitException` *before* `Test-LocalCI.ps1:119-120/171` read `$LASTEXITCODE`, so the `"Local CI step 'X' failed…"` messages rarely surface. Fail-closed is preserved and the explicit checks remain the correct fallback for the `#Requires -Version 5.1` (Windows PowerShell) path — cosmetic only.
- **The routing test pins exact expression *syntax*, not behavior** (`ReleaseScriptPolicyTests.cs:287` asserts the literal `case(...)` string). `case()` is valid so this isn't defending a bug — but a maintainer who refactors to the equally-valid `&& / ||` idiom would break the test for no behavioral reason. Brittle guard, low stakes.

---

## The one durable theme (correctly scoped)

Every new guard here asserts on **source text** (YAML/PS substrings and positions), never on GitHub/PowerShell **behavior**. That property is real and is why the `case()` question could only be settled with `actionlint` + primary docs, and why Node-24 enforcement (Note 2) is unprotected. It cuts both ways: it can hide a broken workflow, *and* it can make a correct workflow look broken to a stale reader. Where behavior matters, verify behavior (`actionlint` in CI would be the durable fix for the workflow half).

## Verified clean (do not re-flag)
Root `Build-PiPlay.ps1` is a shim → `scripts\Build-PiPlay.ps1` (path resolves correctly). Trigger matrix: `pull_request` (all) + `push.branches:[main]` + `workflow_dispatch`; tags correctly no longer trigger (kills the stable-tag duplicate); concurrency groups PR (`-<prnum>`) and main (`-refs/heads/main`) separately, so no cross-cancellation. Fork PRs route to hosted `windows-latest` regardless of `vars`. SHA-pins correct. `persist-credentials:false` safe. `-AsJson` requires `-Plan` (guarded).

## Verification log
- `git diff` (working tree) — all 12 files read
- `Test-LocalCI.ps1 -Plan` — pass
- **`Test-LocalCI.ps1` full gate — PASS** (`node v24.18.0` → accepted; `dotnet test` **940 passed / 0 failed**; Release build 0 warn/0 err; `LOCAL CI: PASS`). This live-exercised Note 2's untested `^v?24\.` path — it works.
- `actionlint 1.7.12` on `ci.yml` — **exit 0**; on `case()`+bogus probe — flags only the bogus fn, lists `case` as available
- `gh api .../commits/{v6,v5,v6}` — 3/3 SHA MATCH
- pwsh probes — node regex, env save/restore, Test-Path Env, finally-masking (all as tabulated)
- Read `Build-PiPlay.ps1` (Build-stage git/credential surface)

## Coverage notes
- Deep-read + executed: `Test-LocalCI.ps1` (incl. a full live run), `ci.yml`, `Build-PiPlay.ps1`, `LocalCiPlanTests.cs`, `ReleaseScriptPolicyTests.cs` (diff), both new docs, all doc diffs.
- The workflow *payload* is now demonstrated sound (940 green + gate PASS), not merely inferred. What remains unverifiable from the repo is the live self-hosted routing (Note 4) — by construction, not by omission.

### Independent adversarial pass (folded in)
A second reviewer audited the same diff. **Caveat:** it was briefed under this review's *original* premise that `case()` is a hallucinated function, so its top finding ("the test pins `case()`, so any fix goes red") and its "merge-blocked / unverifiable" conclusion are **VOID** — `case()` is valid (verified above) and the gate passes. Setting those aside, it independently **confirmed**: env save/restore is correct on every failure path (env is restored *before* the fatal cleanup, so the process env is clean even when Note 1 fires); Push/Pop pairing; the Node-24 three-sources gap (Note 2); the decorative plan fields (Note 3); trigger soundness incl. that removing the tag trigger breaks nothing and stops PR branches double-firing; and all three SHA-pins correct (via `git ls-remote`: v6.0.3 / v5.4.0 / v6.5.0). It **added** the three items now integrated: Note 1's false-negative mode, Note 4's single-label constraint, and the two Minor findings.

## Recommended next
1. ✅ **Done** — Note 1 addressed (cleanup no longer masks a step failure or flips green→red). See Resolution below.
2. ✅ **Done** — Note 2 closed (Node enforcement driven from `requirements.nodeMajor`). See Resolution below.
3. Merge-ready from a correctness standpoint. Gate the day `PIPLAY_WINDOWS_RUNNER` is set as its own review (Note 4).

---

## Resolution (2026-07-15) — Notes 1 & 2 hardened

Both actionable notes were implemented in the same working tree, test-first (RED→GREEN, each guard mutation-proven):

- **Note 1 (cleanup masking / flaky-red).** The cleanup `Remove-Item` in the test-step `finally` (`Test-LocalCI.ps1`) is now wrapped `try { … -ErrorAction Stop } catch { Write-Warning "Could not remove local CI test data …" }`, so a transient lock **degrades to a warning** instead of throwing out of the `finally` — it can no longer mask a real test failure nor flip a fully green run to FAILED (the leak is still surfaced, just non-fatally). `ReleaseScriptPolicyTests` was flipped from **forbidding** that warning to **requiring** the best-effort structure (remove → `} catch {` → warning, in order); the reverted-catch mutation re-failed with *"cleanup remove must be wrapped in a try/catch."*
- **Note 2 (Node enforcement decoupled from the plan).** `Invoke-NodeVersionStep` now takes `[int]$RequiredMajor`, fed `-RequiredMajor $localCiPlan.requirements.nodeMajor` at the call site, and checks `-notmatch "^v?$RequiredMajor\."`. The standalone `'^v?24\.'` literal is gone, so the **enforced value is the tested/declared plan value** — bump `requirements.nodeMajor` and both follow. The RED baseline failed on the missing `[int]$RequiredMajor`.

Re-verified: full gate green — `node v24.18.0` accepted through the `$RequiredMajor` path, **940 passed / 0 failed**, Release build 0 warn / 0 err, `LOCAL CI: PASS`.

Notes 3 (decorative plan fields) and 4 (inert self-hosted routing) are unchanged — design observation / forward-risk, gated to the day `PIPLAY_WINDOWS_RUNNER` goes live.

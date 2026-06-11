# Test-deploy provenance and the outside-repo testing rule — design

Date: 2026-06-11
Status: implemented in the same pass

## Goals

1. Make the testing-location rule unmissable across the repo: all manual/human testing — anything
   beyond the automated in-repo smoke runs — happens OUTSIDE the repo, against the deployed Stable
   copy at `E:\Dev_test_implemenations\PiPlay\PiPlay.exe` (ADR-0007's deploy root), never against
   repo build output. Stale repo binaries presenting as "the change" is the failure mode this
   eliminates.
2. Give the tester one-command certainty about WHAT is deployed (version, build number, source
   commit, artifact integrity) without hand-rolled PowerShell hash checking.
3. Document the version policy: when `VERSION` must move (and how far) versus a
   `BUILD_NUMBER`-only rebuild, how normal script bumps are committed/tagged after deploy, and how
   to publish an exact pre-stamped source identity with `-NoVersionBump -NoBuildNumberBump`.

## Requirements served

None formal (tooling/process). Anchored on `docs/adr/0007-stable-channel-and-portable-data.md`
(Stable channel, deploy root, portable data) and the release-gate language in `docs/AGENTS.md` /
`docs/Feature_Workflow.md`.

## Settled decisions

- The deployed Stable copy is the ONLY sanctioned manual-test target; `scripts/Publish-Stable.ps1`
  remains the only promote path (ADR-0007). Repo builds stay sanctioned for the automated dev loop
  (deterministic gate, `run-piplay` change-verification smoke).
- Verification is a standalone read-only script, `scripts/Verify-StableDeploy.ps1`, also invoked as
  `Publish-Stable.ps1`'s final step (closes the no-post-copy-integrity-check gap). Exit 0 with
  warnings = intact deploy, read the drift warnings; exit 1 = do not test from it.
- Verifier checks: manifest presence + legacy `BUILDINFO.json` identity; `.piplay.publish.marker`
  agreement; SHA256/size re-hash of every listed artifact; exe `FileVersion` vs manifest;
  `sourceCommit` existence/ancestry vs HEAD (behind-count) + tag presence; repo
  `VERSION`/`BUILD_NUMBER` and working-tree cleanliness.
- Version policy: user-visible feature → `-Version minor`; breaking/milestone → `major`; fixes only
  → default patch; same-semver rebuild → `-NoVersionBump` (build number still moves). Normal
  publishes bump `VERSION`/`BUILD_NUMBER` in the working tree; commit the bumped stamps + CHANGELOG
  and tag `stable-vX.Y.Z-bN`. Exact current-HEAD publishes pre-commit `VERSION`/`BUILD_NUMBER`,
  then run `Publish-Stable.ps1 -NoVersionBump -NoBuildNumberBump` and tag that committed source.
- Rule placement: a new repo-root `CLAUDE.md` (read by AI agents every session) is the primary
  surface; `docs/AGENTS.md` Conventions, `docs/QA_Checklist.md` header (+ deployed-exe and
  source-commit fields), `docs/Feature_Workflow.md` manual lane + version-move criteria,
  `tests/README.md` Lane B, `docs/Chrome_UI_Screenshot_Test_Procedure.md`,
  `docs/README.md` Build & run carve-out, `.github/pull_request_template.md` Manual QA evidence,
  and the `run-piplay` skill scope note all state or link it.

## Changes by file

- `scripts/Verify-StableDeploy.ps1` — NEW: one-command deploy verification (see checks above).
- `scripts/Publish-Stable.ps1` — step 6 added: post-copy verification via the new script; help text
  documents the deploy root as the only manual-test target, the commit+tag follow-up, and the exact
  pre-stamped source path (`-NoVersionBump -NoBuildNumberBump`).
- `CLAUDE.md` — NEW (repo root): the two absolute rules (outside-repo testing; version discipline).
- `docs/AGENTS.md`, `docs/README.md`, `docs/Feature_Workflow.md`, `docs/QA_Checklist.md`,
  `tests/README.md`, `docs/Chrome_UI_Screenshot_Test_Procedure.md`,
  `.github/pull_request_template.md`, `.claude/skills/run-piplay/SKILL.md` — rule ingrained as
  described under Settled decisions.

## Verification

- `Verify-StableDeploy.ps1` run live against the real E: deploy and correctly rejected the old
  v0.4.3 b18 copy: deployed `PiPlay.exe` hash did not match `build-info.json`, the manifest source
  commit was `3d530cd` (12 commits behind HEAD), and the working tree was dirty. This is the unsafe
  false-pass case the verifier is meant to catch.
- `dotnet test PiPlay.sln --configuration Debug`: 555 passed.
- `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`: passed with pre-stamped
  `VERSION`/`BUILD_NUMBER` (`0.4.3`/`19`).

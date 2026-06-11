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
  `Publish-Stable.ps1`'s final step (closes the no-post-copy-integrity-check gap). After the
  2026-06-12 hardening addendum, release mode is fail-closed: source drift, dirty trees, missing
  tags, version/build mismatch, or non-release manifests exit 1.
- Verifier checks: manifest presence + legacy `BUILDINFO.json` identity; `.piplay.publish.marker`
  agreement; SHA256/size re-hash of every listed artifact; exe `FileVersion` and `ProductVersion`
  vs manifest; `sourceCommit` equals `HEAD`; expected `stable-vX.Y.Z-bN` tag presence; repo
  `VERSION`/`BUILD_NUMBER` and working-tree cleanliness.
- Version policy: user-visible feature → bump minor; breaking/milestone → bump major; fixes only
  → bump patch; same-semver rebuild → keep `VERSION` and increment `BUILD_NUMBER`. Commit the
  stamps and changelog first, then run exact-source `Publish-Stable.ps1`; diagnostic
  `-AllowVersionBump` / `-AllowDirty` publishes are explicitly not release evidence.
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

## Addendum: Phase 0 release provenance hardening (2026-06-12)

Review of the first provenance pass found that warning-only drift was still too permissive for
release-candidate evidence. This addendum hardens the release lane while preserving explicit
diagnostic escape hatches.

### Goals

1. A Stable deploy cannot be reported as release-verified when it was built from uncommitted or
   untracked source changes.
2. The official release-candidate path is one exact source commit, one deployed Stable copy, one
   matching manifest, and one matching stable tag.
3. Signed binaries can pass verification because signing happens before final manifest hashes are
   written.
4. The verifier checks exactly what it claims: artifact hashes, `FileVersion`, `ProductVersion`,
   manifest/marker identity, source commit, stable tag, repo stamps, and source cleanliness.

### Settled decisions

- `Publish-Stable.ps1` is exact-source by default. It refuses dirty trees, passes
  `-NoVersionBump -NoBuildNumberBump` to the build script, deploys Stable, runs a pre-tag
  verification (`Verify-StableDeploy.ps1 -AllowMissingStableTag`), creates the local
  `stable-vX.Y.Z-bN` tag on the manifest `sourceCommit` ONLY after that passes, then runs a final
  full verification that requires the tag. A verification failure therefore never leaves a
  release-looking tag behind.
- `-AllowDirty` and `-AllowVersionBump` are explicit non-release escape hatches. They can be useful
  for local diagnostics, but `Publish-Stable.ps1` passes an explicit `-NonReleaseReason` to the build
  so the manifest records `releaseEvidence=false` (and `sourceDirty=true` when applicable) even from
  a clean no-op run; `sourceDirty` is recorded independently. The verifier prints a yellow
  diagnostics-only verdict when `-AllowNonReleaseEvidence` is used.
- `Build-PiPlay.ps1 -SignScript <path>` runs after publish output is produced and before
  `build-info.json` / `BUILDINFO.json` hashes are generated. Manual signing after manifest
  generation is intentionally treated as hash drift.
- `Verify-StableDeploy.ps1` is fail-closed by default. Drift that used to warn (dirty tree,
  deploy behind `HEAD`, missing stable tag, repo stamp mismatch, manifest marked non-release) is now
  a failure unless explicitly running in non-release diagnostics mode.

### Changes by file

- `scripts/Build-PiPlay.ps1` — adds optional pre-manifest signing hook; records
  `releaseEvidence`, `sourceDirty`, `sourceDirtyEntries`, and signing timing in build metadata.
  Adds `-NonReleaseReason` so a publish can be forced non-release independent of source dirtiness.
- `scripts/Publish-Stable.ps1` — defaults to exact-source release deploys; adds `-AllowDirty`,
  `-AllowVersionBump`, and `-SignScript`; passes `-NonReleaseReason` for diagnostic escape hatches;
  verifies the deployed copy before creating the stable tag, then runs a final tag-required pass.
- `scripts/Verify-StableDeploy.ps1` — checks `ProductVersion`, manifest release-evidence fields,
  exact `HEAD`, matching stable tag, clean repo, and matching repo stamps as release failures by
  default. Adds `-AllowMissingStableTag` for the pre-tag release gate (only the missing expected
  tag is downgraded to a warning; every other release check stays fail-closed).
- `tests/PiPlay.Tests/ReleaseScriptPolicyTests.cs` — static regression coverage for the release
  script policy hooks.
- Docs and changelog — update the official release-candidate path and signing guidance.

### Verification plan

- Dirty guard: run `Publish-Stable.ps1 -SkipTests -SkipDeploy` with a dirty tree and confirm it
  refuses without `-AllowDirty`.
- Diagnostic non-release evidence: run
  `Publish-Stable.ps1 -AllowVersionBump -NoVersionBump -NoBuildNumberBump -SkipTests -SkipDeploy`
  and confirm the produced `bin\publish\latest\build-info.json` records `releaseEvidence=false` with
  a `releaseEvidenceReason` naming the escape hatch (independent of source dirtiness).
- Tag-after-verify ordering: confirm `Publish-Stable.ps1` runs `-AllowMissingStableTag` pre-tag
  verification before `Assert-StableTag`, and a final tag-required verification after.
- Verifier fail-closed behavior: run `Verify-StableDeploy.ps1` against the current older deploy and
  confirm drift is a failure rather than green release evidence.
- Deterministic gate:
  - `dotnet test PiPlay.sln --configuration Debug`
  - `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`

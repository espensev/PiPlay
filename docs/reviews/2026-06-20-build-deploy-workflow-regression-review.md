# Build/deploy/workflow regression review

Date: 2026-06-20

Scope: regression review of the current PiPlay working tree's build, Stable publish/deploy, deploy verifier, local spec preflight, and GitHub workflow paths after the profile accent color-wheel changes.

## Findings

### P2 - Local spec preflight could abort on benign Git line-ending warnings

`scripts/Preflight-SpecGate.ps1` runs under `$ErrorActionPreference = "Stop"` and gathers changed files with native `git` commands. In the current working tree, `git diff --name-only` emitted LF-to-CRLF normalization warnings on stderr. Windows PowerShell surfaced that stderr as `NativeCommandError`, causing the local preflight to exit before it could report the spec-gate verdict.

Impact: contributors could see the local workflow gate fail for a PowerShell/Git warning even though the actual changed-file model is valid. That makes the preflight unreliable exactly when the tree has ordinary line-ending normalization warnings.

Status: Addressed. The Git helper pattern now temporarily lowers `$ErrorActionPreference` to `Continue` around redirected native Git calls and still relies on `$LASTEXITCODE` for real command failure:

- `scripts/Preflight-SpecGate.ps1:66-74`
- `scripts/Publish-Stable.ps1:84-93`
- `scripts/Verify-StableDeploy.ps1:60-69`
- `scripts/Build-PiPlay.ps1:267-297`

Regression coverage: `tests/PiPlay.Tests/ReleaseScriptPolicyTests.cs:131-138` pins the guard across the build, publish, verify, and preflight scripts.

## Non-Findings

- CI workflow shape still matches the documented gate: Windows restore, deterministic `dotnet test`, then `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`.
- GitHub action references resolve upstream: `actions/checkout@v6` and `actions/setup-dotnet@v5` both have matching tags.
- Stable publish exact-source behavior still fail-closes on dirty trees by default. The diagnostic run with `-AllowDirty -SkipDeploy -SkipTests` produced `releaseEvidence=false` and `sourceDirty=true`.
- The existing deployed Stable copy re-hashes clean and remains internally release-verified, but it is not current for this working tree: diagnostics verification reports it is 33 commits behind current `HEAD` and the repo is dirty. That is expected until the current work is committed and republished.

## Validation Run

- PowerShell parser pass over `Build-PiPlay.ps1` and `scripts/*.ps1`: passed
- `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~ReleaseScriptPolicyTests"`: 10 passed
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Preflight-SpecGate.ps1 -Quiet`: passed
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`: passed, 0 warnings / 0 errors
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Stable.ps1 -SkipTests -SkipDeploy -AllowDirty`: passed; Stable publish and metadata validation completed without deploying
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-StableDeploy.ps1 -AllowNonReleaseEvidence`: passed for diagnostics; 2 expected warnings for source drift/dirty tree
- `dotnet test PiPlay.sln --configuration Debug --no-build`: 669 passed
- `git diff --check`: no whitespace errors; Git reported LF-to-CRLF normalization warnings

# Review - PR #36 takeover

**Date:** 2026-08-26
**Surface:** PR #36 `hardening/desk-candidate-automation` at `667661e`, reviewed against local `main` at `970a472`; merge base and current remote base `a9bbe37`
**Spec source:** `docs/PiPlay_Product_Engineering_Spec.md`; current user request to review and take over
**Standards sources:** `CLAUDE.md`, `docs/AGENTS.md`, `docs/DECISIONS.md`
**Verdict:** FAIL — do not merge PR #36; close it as superseded after explicit approval

## Findings

### High

- [axis: regression] PR #36's green GitHub merge badge is stale relative to the actual local main line.
  Evidence: `gh pr view 36` reports `MERGEABLE/CLEAN` only against remote base `a9bbe37`; local `main` is five commits ahead at `970a472`. `git merge-tree --write-tree --name-only --messages main origin/hardening/desk-candidate-automation` exits 1 with exactly 17 conflicted paths: `.github/pull_request_template.md`, `CLAUDE.md`, `README.md`, `docs/AGENTS.md`, `docs/CHANGELOG.md`, `docs/DECISIONS.md`, `docs/Data_and_Privacy_Map.md`, `docs/PiPlay_Product_Engineering_Spec.md`, `docs/QA_Checklist.md`, `docs/SPEC_GAPS_AND_OWNERSHIP.md`, `docs/Theme_Preset_Differences.md`, `docs/YouTube_Compliance.md`, `scripts/Publish-Stable.ps1`, `scripts/Test-UiSmoke.ps1`, `scripts/Verify-StableDeploy.ps1`, `src/PiPlay/MainWindow.xaml.cs`, and `tests/PiPlay.Tests/ReleaseScriptPolicyTests.cs`.
  Impact: a normal PR merge is unavailable after the five local commits are published. Manual conflict resolution would combine two superseding release/process designs and could silently regress the already released b39 behavior.
  Recommendation: do not rebase or merge this branch. Publish the current main line only after its host verification gate is resolved or explicitly accepted, then close PR #36 as superseded.

- [axis: standards] The PR deletes authorities that current main explicitly requires and that now contain live b39 facts.
  Evidence: `git diff --name-status a9bbe37..667661e` deletes `docs/DECISIONS.md`, `docs/Data_and_Privacy_Map.md`, `docs/QA_Checklist.md`, `docs/SPEC_GAPS_AND_OWNERSHIP.md`, `docs/Theme_Preset_Differences.md`, and `docs/YouTube_Compliance.md`. Current `docs/AGENTS.md:3-11` assigns canonical ownership to those files; `docs/PiPlay_Product_Engineering_Spec.md:3`, `:208`, `:238`, and `:242` depend on them. The current gap record at `docs/SPEC_GAPS_AND_OWNERSHIP.md:7` and QA record at `docs/reviews/qa-2026-08-26-stable-v0.13.2-b39-listening.md:1-22` postdate the PR.
  Impact: accepting the PR's document deletions would erase accepted ADRs, privacy/compliance detail, the manual QA contract, current unresolved work, and the newly observed Q-1 result.
  Recommendation: retain the current canonical surfaces. Any future documentation consolidation must begin from current main and preserve their owned facts.

- [axis: regression] The PR predates two released behavior corrections and conflicts directly in their implementation/evidence surfaces.
  Evidence: local `d287315` adds playlist-only timestamp normalization via `src/PiPlay/Services/PopoutTargetResolver.cs:45-58` and uses it at `src/PiPlay/MainWindow.xaml.cs:1520`; tests pin it at `tests/PiPlay.Tests/PopoutTargetResolverTests.cs:55-78`. Local `e1f45d6` makes the deployed smoke fail closed when foregrounding or capture quality is untrustworthy at `scripts/Test-UiSmoke.ps1:112` and `:137`. The merge simulation conflicts in `MainWindow.xaml.cs`, `Test-UiSmoke.ps1`, and `ReleaseScriptPolicyTests.cs`; PR #36 is based before both commits.
  Impact: resolving these conflicts in favor of the older PR can restore unrelated playlist/miniplayer timestamps or false-positive UI-smoke evidence. Both corrections are already in Stable v0.13.2 b39 (`e948c79`).
  Recommendation: keep `d287315` and `e1f45d6` as authoritative. Do not transplant the PR wholesale around them.

### Medium

- [axis: spec] The desk-candidate acceptance flow in PR #36 no longer matches the accepted Stable verification route.
  Evidence: `667661e:.github/workflows/ci.yml:54-164` creates and uploads a non-release Stable-channel desk candidate, and `667661e:docs/AGENTS.md:14-15` reserves a human acceptance cycle around it. Current `docs/AGENTS.md:38-44`, `CLAUDE.md:17`, and `docs/PiPlay_Product_Engineering_Spec.md:216-229` instead require exact-source Stable deployment through an explicit `PIPLAY_STABLE_ROOT`, manifest verification of that deployed copy, and only result-dependent end-user checks afterward. Local commit `d287315` deliberately replaced PR/spec ceremony with that deterministic route.
  Impact: merging the PR would restore a second artifact/acceptance plane whose evidence is explicitly non-release and whose lifecycle is not the current documented release contract.
  Recommendation: close the desk-candidate design with the PR. If remote candidate artifacts become desirable later, specify them anew against the current exact-source Stable contract.

## Verification

- `gh pr view 36 --json ...` — pass; PR open at `667661e`, CI successful, GitHub base `a9bbe37`, remote merge state clean only against that base.
- `git status --short --branch` — pass before this report; local `main` clean and five commits ahead of `origin/main`.
- `git merge-tree --write-tree --name-only --messages main origin/hardening/desk-candidate-automation` — expected fail; 17 conflicts prove the current integration surface is not mergeable.
- `git diff --check a9bbe37..667661e` and `git diff --check origin/main..HEAD` — pass.
- `pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1` — blocked before tests: SDK 10.0.400 `dotnet --info` exits 1 in `Microsoft.DotNet.Cli.Installer.Windows.InstallerBase..cctor()`.
- Direct `dotnet test PiPlay.sln --configuration Debug --no-restore` — initial host-environment fail: 160 WPF initialization failures and 880 passes because this process omitted `WINDIR`.
- Focused WPF rerun with process-only `WINDIR=$env:SystemRoot` — pass, 2/2.
- Full direct test rerun with process-only `WINDIR=$env:SystemRoot` — pass, 1040/1040.
- `pwsh -NoProfile -File .\scripts\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` under the same process-only correction — pass; Release build 0 warnings, 0 errors.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass` plus `Get-FileHash .\VERSION` — pass on Windows PowerShell 5.1 with the currently inherited `PSModulePath`; PR #36's older module-autoload reproduction was not reproduced in this environment.

The canonical gate remains formally blocked because `dotnet --info` still fails after restoring `WINDIR`, although the complete product test and build surfaces pass. The exception matches open dotnet/sdk issue #55862; no SDK, registry, installer, or machine repair was attempted in this review.

## Coverage Notes

- Files reviewed deeply: `.github/workflows/ci.yml`, `.github/pull_request_template.md`, `CLAUDE.md`, `README.md`, `docs/AGENTS.md`, `docs/CHANGELOG.md`, `docs/DECISIONS.md`, `docs/Data_and_Privacy_Map.md`, `docs/PiPlay_Product_Engineering_Spec.md`, `docs/QA_Checklist.md`, `docs/SPEC_GAPS_AND_OWNERSHIP.md`, `docs/Theme_Preset_Differences.md`, `docs/YouTube_Compliance.md`, `scripts/Build-PiPlay.ps1`, `scripts/DeploySwap.ps1`, `scripts/Publish-Stable.ps1`, `scripts/PublishLock.ps1`, `scripts/StableDeployRoot.ps1`, `scripts/Test-DeploySwap.ps1`, `scripts/Test-Documentation.ps1`, `scripts/Test-LocalCI.ps1`, `scripts/Test-PublishMetadata.ps1`, `scripts/Test-StableDeployRoot.ps1`, `scripts/Test-UiSmoke.ps1`, `scripts/Verify-StableDeploy.ps1`, `src/PiPlay/MainWindow.xaml.cs`, `src/PiPlay/Services/PopoutTargetResolver.cs`, `tests/PiPlay.Tests/LocalCiPlanTests.cs`, `tests/PiPlay.Tests/PopoutTargetResolverTests.cs`, and `tests/PiPlay.Tests/ReleaseScriptPolicyTests.cs`.
- Reviewed for deletion/authority impact: `.github/workflows/spec-check.yml`, `docs/Feature_Workflow.md`, `docs/assets/README.md`, `docs/superpowers/templates/feature-design-template.md`, `docs/superpowers/templates/plan-template.md`, `scripts/Preflight-SpecGate.ps1`, and `tests/README.md`.
- Sampled mechanical source/comment cleanup across every remaining changed product file: `src/PiPlay/MainWindow.xaml`, `src/PiPlay/Models/AppSettings.cs`, `src/PiPlay/Models/PlacementData.cs`, `src/PiPlay/Models/PlayerReturnState.cs`, `src/PiPlay/Models/Profile.cs`, `src/PiPlay/Models/YouTubeTarget.cs`, `src/PiPlay/PiPlay.csproj`, `src/PiPlay/PlayerShell/player-shell.js`, `src/PiPlay/PlayerShell/player.html`, `src/PiPlay/PlayerWindow.xaml`, `src/PiPlay/PlayerWindow.xaml.cs`, `src/PiPlay/Prompt.cs`, `src/PiPlay/Services/AppChannel.cs`, `src/PiPlay/Services/AppPaths.cs`, `src/PiPlay/Services/AutoPopoutPolicy.cs`, `src/PiPlay/Services/FadePolicy.cs`, `src/PiPlay/Services/LoggingService.cs`, `src/PiPlay/Services/NavigationPolicy.cs`, `src/PiPlay/Services/PlacementMath.cs`, `src/PiPlay/Services/PlaybackModePolicy.cs`, `src/PiPlay/Services/PlayerShellBridge.cs`, `src/PiPlay/Services/PlayerShellErrorPolicy.cs`, `src/PiPlay/Services/PlayerShellProtocol.cs`, `src/PiPlay/Services/PopoutLaunchPolicy.cs`, `src/PiPlay/Services/PrivacyService.cs`, `src/PiPlay/Services/ProfileAccentService.cs`, `src/PiPlay/Services/ProfileService.cs`, `src/PiPlay/Services/ReturnPolicy.cs`, `src/PiPlay/Services/SettingsService.cs`, `src/PiPlay/Services/WebViewEnvironmentService.cs`, `src/PiPlay/Services/WindowOpacityApplier.cs`, `src/PiPlay/Services/WindowOpacityPolicy.cs`, `src/PiPlay/Services/WindowPlacementService.cs`, `src/PiPlay/Services/YouTubeDomBridge.cs`, `src/PiPlay/Services/YouTubeUrlHelper.cs`, `src/PiPlay/SettingsWindow.xaml`, `src/PiPlay/SettingsWindow.xaml.cs`, `src/PiPlay/Theme/AccentWashBrushConverter.cs`, `src/PiPlay/Theme/Colors.xaml`, `src/PiPlay/Theme/ControlStyles.xaml`, `src/PiPlay/Theme/ThemeCatalog.cs`, `src/PiPlay/Theme/ThemeColors.cs`, `src/PiPlay/Theme/ThemePreferenceResolver.cs`, and `src/PiPlay/Theme/ThemeResourceApplier.cs`.
- Sampled matching comment/contract churn across every remaining changed test: `tests/PiPlay.Tests/AccentWashBrushConverterTests.cs`, `tests/PiPlay.Tests/AppPathsTests.cs`, `tests/PiPlay.Tests/ContrastReportTests.cs`, `tests/PiPlay.Tests/LoggingServiceTests.cs`, `tests/PiPlay.Tests/NavigationPolicyTests.cs`, `tests/PiPlay.Tests/PlaybackModePolicyTests.cs`, `tests/PiPlay.Tests/PlayerShellAssetTests.cs`, `tests/PiPlay.Tests/PlayerShellErrorPolicyTests.cs`, `tests/PiPlay.Tests/PlayerShellProtocolTests.cs`, `tests/PiPlay.Tests/ProfileAccentServiceTests.cs`, `tests/PiPlay.Tests/ProfileServiceTests.cs`, `tests/PiPlay.Tests/ReturnPolicyTests.cs`, `tests/PiPlay.Tests/RuntimeFailurePolicyTests.cs`, `tests/PiPlay.Tests/SettingsServiceTests.cs`, `tests/PiPlay.Tests/ThemeCatalogTests.cs`, `tests/PiPlay.Tests/ThemeColorsTests.cs`, `tests/PiPlay.Tests/ThemePreferenceResolverTests.cs`, `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`, `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`, `tests/PiPlay.Tests/WindowOpacityPolicyTests.cs`, and `tests/PiPlay.Tests/YouTubeUrlHelperTests.cs`.

## Open Questions

- External disposition requires explicit approval: close PR #36 as superseded, then push the five local main commits after deciding whether the host-only `dotnet --info` blocker is acceptable or must be cleared first.
- PR #36 contains isolated hardening ideas, especially module-independent hashing for Windows PowerShell 5.1. They should be reconsidered only as a new bounded change against current main; the claimed failure did not reproduce in the current PowerShell 5.1 environment.

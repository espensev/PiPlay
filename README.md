# PiPlay

PiPlay is a Windows WPF app that moves YouTube playback into one native Popout Player and returns it to the Source Window; the [product contract](docs/PiPlay_Product_Engineering_Spec.md) defines its supported behavior and boundaries.

## Run

PiPlay needs the .NET 10 Desktop Runtime and Microsoft Edge WebView2 Evergreen. The project is framework-dependent and references WebView2 SDK `1.0.3967.48` (`src/PiPlay/PiPlay.csproj`). Microsoft documents that production WebView2 apps require the WebView2 Runtime: [WebView2 distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

1. Start PiPlay and open a YouTube video or playlist.
2. Select **Pop out video**.
3. Move, resize, Pin, Fade, or change the presentation.
4. Select **Bring video back** or close the player to return playback. **Show Popout** only restores or focuses the existing player (`MainWindow.ActivateExistingPlayer`, `MainWindow.BringVideoBackAsync`).

## Develop and verify

Install the SDK selected by `global.json`, PowerShell 7, and Node 24 or newer. GitHub Actions pins Node 24; the local gate accepts later majors (`.github/workflows/ci.yml`; `scripts/Test-LocalCI.ps1:New-LocalCiPlan`).

```powershell
dotnet run --project .\src\PiPlay\PiPlay.csproj

# Inspect or run the same gate used by CI.
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1 -Plan
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
```

The gate checks the Stable-root policy, documentation links and paths, restore, the Debug test suite, executable DOM behavior, and a non-mutating Release build. Test state uses a unique temporary `PIPLAY_DATA_ROOT` and is cleaned in `finally` (`scripts/Test-LocalCI.ps1`; `LocalCiPlanTests`).

## Data and privacy

`PIPLAY_DATA_ROOT` overrides all app data locations. Otherwise Stable stores `PiPlayData` beside its executable and Default uses the operating system's Local Application Data folder. `AppPaths` defines `settings.json`, `logs/piplay.log`, and `WebView2UserData`; `AppPathsTests` pins the precedence.

**Reset app state** atomically replaces settings while preserving logs and browser data. **Clear browser data** separately clears WebView2 `AllProfile` data and signs the user out (`SettingsService.Reset`, `PrivacyService.ClearBrowserDataAsync`, `SettingsServiceTests`, `PrivacyServiceTests`). Application diagnostics stay in the bounded local log; adding telemetry, credential collection, or uploads is outside the contract (`src/PiPlay/Services/LoggingService.cs:Log`).

## Desk candidate and release

A manually dispatched CI run attaches `PiPlay-desk-candidate-<commit>` as a non-release artifact. Download and extract it on SND-DESK, open PowerShell 7 in the extracted root, then run:

```powershell
pwsh -NoProfile -File .\scripts\Test-UiSmoke.ps1 -Mode DeskCandidate -KeepOpen
```

The packaged exact preflight and automated desktop smoke run first. Child data comes from `PIPLAY_DESK_CANDIDATE_DATA_ROOT` or Local Application Data, is passed as `PIPLAY_DATA_ROOT`, and must be disjoint from the extracted payload, so candidate bytes stay unchanged (`scripts/Test-UiSmoke.ps1:Resolve-DeskCandidateDataRoot`, `scripts/Test-UiSmoke.ps1:New-SmokeProcessStartInfo`). PiPlay then remains open until the tester closes it; the script records no human result (`scripts/Test-UiSmoke.ps1:-KeepOpen`). Use [Unresolved verification](docs/PiPlay_Product_Engineering_Spec.md#unresolved-verification) as the sole live checklist.

Acceptance is release-decision evidence, not release provenance or tested-byte identity for later Stable output. Only after acceptance, publish from clean committed source to an environment-derived Stable root:

```powershell
$env:PIPLAY_STABLE_ROOT = Join-Path $env:LOCALAPPDATA "PiPlayStable"
pwsh -NoProfile -File .\scripts\Publish-Stable.ps1
```

The command requires committed `VERSION` and `BUILD_NUMBER`, runs the shared gate, builds, performs the verified staged deployment, verifies the deployed bytes, and tags `stable-vX.Y.Z-bN` (`scripts/Publish-Stable.ps1`).

## Maintained documents

- [Product and architecture contract](docs/PiPlay_Product_Engineering_Spec.md)
- [Repository rules](docs/AGENTS.md)
- [Current unreleased changes](docs/CHANGELOG.md)

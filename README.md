# PiPlay

PiPlay is a Windows WPF desktop app that plays YouTube in one movable, resizable Video Popout. It returns playback to the Source Window and supports Pin, Fade, Auto, profiles, playlists, mixes, privacy actions, and theme/accent changes. The product contract is in [`docs/PiPlay_Product_Engineering_Spec.md`](docs/PiPlay_Product_Engineering_Spec.md); implementation evidence is the source and tests named there.

## Run

The app targets `net10.0-windows`, uses WPF, and references Microsoft WebView2 Evergreen (`src/PiPlay/PiPlay.csproj`, `global.json`). Open a YouTube video or playlist, select **Pop out video**, then use **Bring video back** or close the Popout to return playback.

For the deterministic, non-interactive gate:

```powershell
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
```

That script owns Node/version checks, restore, Debug tests, temporary test data, and the non-mutating Release build. Use `-Plan` to inspect its exact commands.

## Stable acceptance

Set `PIPLAY_STABLE_ROOT` to the machine-local Stable deployment directory. The scripts do not choose a user-specific path for you:

```powershell
$stableRoot = $env:PIPLAY_STABLE_ROOT
if ([string]::IsNullOrWhiteSpace($stableRoot)) { throw 'Set PIPLAY_STABLE_ROOT first.' }
.\scripts\Publish-Stable.ps1 -DeployRoot $stableRoot
.\scripts\Verify-StableDeploy.ps1 -DeployRoot $stableRoot
pwsh -NoProfile -File .\scripts\Test-UiSmoke.ps1 -ExePath (Join-Path $stableRoot 'PiPlay.exe')
```

`Publish-Stable.ps1` runs the deterministic gate and staged deployment; `Verify-StableDeploy.ps1` checks the deployed manifest, hashes, version, tag, source commit, and tree state. The remaining real-user check is the final playback/audio acceptance in [`docs/QA_Checklist.md`](docs/QA_Checklist.md).

## Canonical docs

- [`docs/AGENTS.md`](docs/AGENTS.md): repository rules and terminology.
- [`docs/DECISIONS.md`](docs/DECISIONS.md): accepted architecture.
- [`docs/Theme_Preset_Differences.md`](docs/Theme_Preset_Differences.md): current theme values.
- [`docs/Data_and_Privacy_Map.md`](docs/Data_and_Privacy_Map.md): local data boundaries.
- [`docs/YouTube_Compliance.md`](docs/YouTube_Compliance.md): page-script and platform-safety policy.
- [`docs/SPEC_GAPS_AND_OWNERSHIP.md`](docs/SPEC_GAPS_AND_OWNERSHIP.md): verified open work only.
- [`docs/CHANGELOG.md`](docs/CHANGELOG.md): shipped user-visible changes.

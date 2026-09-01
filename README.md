# PiPlay

Windows WPF app (`net10.0-windows`, WebView2 Evergreen: `src/PiPlay/PiPlay.csproj`, `global.json`) that plays YouTube in one Video Popout. Product contract: [`docs/PiPlay_Product_Engineering_Spec.md`](docs/PiPlay_Product_Engineering_Spec.md).

Command-line help is owned by the executable:

```powershell
& .\PiPlay.exe --help
```

`-h` and `/?` are equivalent exact aliases. PowerShell `Get-Help` does not inspect native executables, so invoke PiPlay with one of those arguments instead.

```powershell
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
```

`-Plan` prints the gate commands. The script owns Node/version checks, restore, Debug tests, temporary test data, and the non-mutating Release build.

## Development and promotion

SND-HOST owns routine feature work: start from current `main`, use a machine-namespaced `snd-host/...` branch, run the local gate, and open a pull request. GitHub-hosted `Build and test (Windows)` is the required merge check.

SND-DESK owns clean validation and Stable acceptance. Fast-forward a clean test worktree, keep automated test/UI data disposable through the repository scripts, and never replace the Stable runtime with source or feature-branch output. Promote only a merged, committed release through the Stable scripts below.

## Stable acceptance

Set `PIPLAY_STABLE_ROOT` to the machine-local Stable directory:

```powershell
$stableRoot = $env:PIPLAY_STABLE_ROOT
if ([string]::IsNullOrWhiteSpace($stableRoot)) { throw 'Set PIPLAY_STABLE_ROOT first.' }
.\scripts\Publish-Stable.ps1 -DeployRoot $stableRoot
.\scripts\Verify-StableDeploy.ps1 -DeployRoot $stableRoot
pwsh -NoProfile -File .\scripts\Test-UiSmoke.ps1 -ExePath (Join-Path $stableRoot 'PiPlay.exe')
```

Do not use source or `bin` output as release evidence. On that verified copy: pop out a playing video and listen through launch and return/close (Q-1); repeat once with a playlist or mix when available; record unavailable ads/account/profile states as not run.

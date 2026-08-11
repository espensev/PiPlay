# PiPlay tests

## Deterministic lane

No network, visible desktop, or WebView2 runtime. Run the complete local/Actions gate from repo root:

```powershell
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
```

Useful filters:

```powershell
dotnet test
dotnet test --filter Category=Markup
dotnet test --filter Category=Logic
dotnet test --filter Category=Wpf
dotnet test --filter FullyQualifiedName~YouTubeDomBehaviorTests
```

- `Markup`: parses XAML/manifest invariants—dark resources, `AllowsTransparency=False`, `UseLayoutRounding=False`, names, icon fallback, contrast, and PerMonitorV2.
- `Logic`: pure URL/navigation/settings/profile/placement/return/fade/theme/protocol/DOM-script policies.
- `Wpf`: constructs real windows on one shared STA thread without showing them or initializing WebView2; checks resources, styles, accessibility contracts, and fractional-DPI URL rendering.
- `YouTubeDomBehaviorTests`: executes generated scripts against a dependency-free fake DOM for trusted/stale input, ad-safe actions, drag thresholds/exclusions, selectors, and recovery.

Executable DOM tests require Node 24 and built-in modules only: no `npm install`, package manifest, browser, network, or desktop.

The suite is serial because it owns process-global `PIPLAY_DATA_ROOT` and one WPF `Application`. `Infrastructure/TestDataRoot.cs` redirects runtime files to a temporary root so tests never touch `%LOCALAPPDATA%\PiPlay`.

`scripts/Test-LocalCI.ps1` runs SDK diagnostics, restore, Debug tests, and the non-mutating Release build. Inspect without executing:

```powershell
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1 -Plan
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1 -Plan -AsJson
```

## Deployed manual lane

This is a release gate and runs only against the verified Stable copy:

```powershell
.\scripts\Publish-Stable.ps1
.\scripts\Verify-StableDeploy.ps1
pwsh -File .\scripts\Test-UiSmoke.ps1 -ExePath E:\Dev_test_implemenations\PiPlay\PiPlay.exe
```

The UI Automation smoke captures true rendering under `docs/evidence/`; use fractional DPI. Complete `docs/QA_Checklist.md` for WebView2, live YouTube/ad/account behavior, mixed-DPI windows, and `UI-CHK-1…12`. Commit `VERSION`, `BUILD_NUMBER`, and `docs/CHANGELOG.md` before release publishing; diagnostic/dirty deploys are not evidence.

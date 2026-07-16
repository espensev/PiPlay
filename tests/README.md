# PiPlay tests

Layered regression suite. See `docs/AGENTS.md` for the test conventions and quality bar, and
`docs/SPEC_GAPS_AND_OWNERSHIP.md` plus the product spec for the requirement-by-requirement status.

## Lane A — `dotnet test` (fast, deterministic, headless)

No WebView2, no network, no visible desktop. Runs in well under a second.
The full deterministic gate is owned by `scripts/Test-LocalCI.ps1`: it runs this lane and then the
non-mutating release build gate. `.github/workflows/ci.yml` invokes that same wrapper so local and
remote command sequences cannot drift.

```bash
dotnet test                              # everything in PiPlay.Tests
dotnet test --filter Category=Markup     # Layer 1: XAML invariants (parsed as XML, no WPF runtime)
dotnet test --filter Category=Logic      # Layer 2: pure services
dotnet test --filter Category=Wpf        # Layer 3: live WPF on a shared STA thread
dotnet test --filter FullyQualifiedName~YouTubeDomBehaviorTests  # executable generated DOM scripts
```

Run the complete shared gate from the repository root with:

```powershell
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
```

The executable DOM slice needs Node 24, but uses only built-in modules: there is no `npm install`,
package manifest, browser runtime, network access, or visible desktop. CI pins the Node version so
the generated scripts run in the same deterministic VM contract locally and remotely.

- **Layer 1 (`Markup`)** — `Ui/XamlInvariantTests.cs`. Asserts the burned-in markup that breaks
  the app if it silently flips: `UseLayoutRounding="False"` (the "rounding = 0" guard),
  `AllowsTransparency="False"`, `WindowChrome CornerRadius=0`, required `x:Name` controls, glyph
  icon-font fallback, tooltips, that every `{StaticResource}` resolves, WCAG contrast, and the
  PerMonitorV2 manifest.
- **Layer 2 (`Logic`)** — pure services: `NavigationPolicy`, `YouTubeUrlHelper`,
  `SettingsService`, `ProfileService`, `PlacementMath`, `ReturnPolicy`, `Log.RedactUrl`,
  `FadePolicy`, `AppPaths`, the DPI-aware `RoundedWindowRegionPolicy` geometry,
  `PopoutPresentationPolicy`, the closed Focused/drag message protocols, and deterministic
  `YouTubeDomBridge` script contracts (threshold ordering, real-control/caption exclusions,
  selective unused-chrome inclusion, no-crop `contain`, accessible overlay actions, and no
  coordinate-bearing drag payload). `YouTubeDomBehaviorTests` then executes those generated scripts
  against a dependency-free fake DOM to prove passive-drag threshold/exclusion behavior, trusted-event
  gates, ad-safe Focused seek/Next behavior, document-token rotation, and selector-failure recovery.
- **Layer 3 (`Wpf`)** — `Ui/WpfRuntimeTests.cs`. Constructs the real windows on a shared STA
  thread (never shown, so WebView2/network are untouched) to prove every resource resolves at
  runtime, the DependencyProperty invariants hold, the dark styles are applied, and a
  `RenderTargetBitmap` shows the URL text is not clipped to a band at 150% DPI.

The whole suite runs serially (`Infrastructure/AssemblyConfig.cs`): a few tests touch
process-global state (`PIPLAY_DATA_ROOT`, the single WPF `Application` on one STA thread). That
data root is auto-redirected to a temp dir for the whole run (`Infrastructure/TestDataRoot.cs`),
so tests never touch your real `%LOCALAPPDATA%\PiPlay`.

## Lane B — manual E2E smoke (release gate, NOT in `dotnet test`)

Needs an interactive desktop, the WebView2 runtime, and network. As a release gate this lane runs
against the **deployed Stable copy**, not the repo tree (root `CLAUDE.md` / `docs/AGENTS.md`):

```powershell
.\scripts\Publish-Stable.ps1                 # exact-source gate + build + deploy + verify + stable tag
.\scripts\Verify-StableDeploy.ps1            # fail-closed release proof
pwsh -File scripts/Test-UiSmoke.ps1 -ExePath E:\Dev_test_implemenations\PiPlay\PiPlay.exe
```

It launches the given exe, asserts the key UI elements exist via UI Automation, and saves a
window screenshot to `docs/evidence/`. Capture at a fractional DPI (e.g. 150%) — integer-scale
captures hide the rounding/clipping class of bug (`docs/AGENTS.md`). This is the only lane that
confirms true pixel rendering (the chrome `UI-CHK-*` gates in `docs/QA_Checklist.md` §8).
For release-candidate QA, commit `VERSION`/`BUILD_NUMBER` and `CHANGELOG.md` before publishing; dirty
or script-stamped deploys are diagnostic only and the verifier labels them as not release evidence.

# PiPlay tests

Layered regression suite. See `docs/Regression_Test_Suite_Design.md` for the design and
`docs/Spec_Conformance_Review.md` for the requirement-by-requirement status.

## Lane A — `dotnet test` (fast, deterministic, headless)

No WebView2, no network, no visible desktop. Runs in well under a second.
This is the lane run by `.github/workflows/ci.yml` before the non-mutating release build gate.

```bash
dotnet test                              # everything in PiPlay.Tests
dotnet test --filter Category=Markup     # Layer 1: XAML invariants (parsed as XML, no WPF runtime)
dotnet test --filter Category=Logic      # Layer 2: pure services
dotnet test --filter Category=Wpf        # Layer 3: live WPF on a shared STA thread
```

- **Layer 1 (`Markup`)** — `Ui/XamlInvariantTests.cs`. Asserts the burned-in markup that breaks
  the app if it silently flips: `UseLayoutRounding="False"` (the "rounding = 0" guard),
  `AllowsTransparency="False"`, `WindowChrome CornerRadius=0`, required `x:Name` controls, glyph
  icon-font fallback, tooltips, that every `{StaticResource}` resolves, WCAG contrast, and the
  PerMonitorV2 manifest.
- **Layer 2 (`Logic`)** — pure services: `NavigationPolicy`, `YouTubeUrlHelper`,
  `SettingsService`, `ProfileService`, `PlacementMath`, `ReturnPolicy`, `Log.RedactUrl`,
  `FadePolicy`, `AppPaths`.
- **Layer 3 (`Wpf`)** — `Ui/WpfRuntimeTests.cs`. Constructs the real windows on a shared STA
  thread (never shown, so WebView2/network are untouched) to prove every resource resolves at
  runtime, the DependencyProperty invariants hold, the dark styles are applied, and a
  `RenderTargetBitmap` shows the URL text is not clipped to a band at 150% DPI.

The whole suite runs serially (`Infrastructure/AssemblyConfig.cs`): a few tests touch
process-global state (`PIPLAY_DATA_ROOT`, the single WPF `Application` on one STA thread). That
data root is auto-redirected to a temp dir for the whole run (`Infrastructure/TestDataRoot.cs`),
so tests never touch your real `%LOCALAPPDATA%\PiPlay`.

## Lane B — manual E2E smoke (release gate, NOT in `dotnet test`)

Needs an interactive desktop, the WebView2 runtime, and network. Build a publish first, then:

```powershell
.\Build-PiPlay.ps1 -Stage Publish
pwsh -File scripts/Test-UiSmoke.ps1
```

It launches the built exe, asserts the key UI elements exist via UI Automation, and saves a
window screenshot to `docs/evidence/`. Capture at a fractional DPI (e.g. 150%) — integer-scale
captures hide the rounding/clipping class of bug (`docs/AGENTS.md`). This is the only lane that
confirms true pixel rendering (the chrome `UI-CHK-*` gates in `docs/QA_Checklist.md` §8).

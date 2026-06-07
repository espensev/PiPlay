# Regression Test Suite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a layered regression-test suite (markup invariants, expanded logic, live-WPF runtime, manual E2E smoke) plus a committed spec-conformance review, so the UI/markup regression class (e.g. the `UseLayoutRounding` "rounding = 0" URL-text clipping) and logic regressions are caught automatically.

**Architecture:** Layers 1–3 live in the existing `PiPlay.Tests` xUnit project, separated by `[Trait("Category", …)]`; Layer 4 is a manual `pwsh` UIA+screenshot harness in `scripts/`. Three tiny, pre-approved test-isolation seams are added to `src` (an `AppPaths` env override, a pure `PlacementMath.Clamp`, a pure `ReturnPolicy`). Markup tests parse `.xaml` as XML (no WPF runtime); runtime tests boot an `Application` on an STA thread via `Xunit.StaFact` and construct the real windows without ever calling `Show()` (so WebView2/network are never touched).

**Tech Stack:** .NET 10 (`net10.0-windows`), WPF, xUnit 2.9, `Xunit.StaFact`, `System.Xml.Linq`, `System.Windows.Media.Imaging.RenderTargetBitmap`, PowerShell 7 + `System.Windows.Automation`.

**Reference spec:** `docs/Regression_Test_Suite_Design.md` (approved 2026-05-31).

---

## File structure

**New (src seams):**
- `src/PiPlay/Services/PlacementMath.cs` — pure `RectI` + `Clamp` (extracted from `WindowPlacementService`).
- `src/PiPlay/Services/ReturnPolicy.cs` — pure return-resume decision (extracted from `MainWindow`).

**Modified (src):**
- `src/PiPlay/Services/AppPaths.cs` — `PIPLAY_DATA_ROOT` env override; `Root` becomes computed.
- `src/PiPlay/Services/WindowPlacementService.cs` — delegate clamp math to `PlacementMath`.
- `src/PiPlay/MainWindow.xaml.cs` — `Player_OnClosed` calls `ReturnPolicy.Decide`.

**New (tests):**
- `tests/PiPlay.Tests/Infrastructure/TestCategories.cs` — trait-name constants.
- `tests/PiPlay.Tests/Infrastructure/TestDataRoot.cs` — `[ModuleInitializer]` redirecting `PIPLAY_DATA_ROOT` to a temp dir for the whole test run.
- `tests/PiPlay.Tests/Infrastructure/XamlTestFiles.cs` — locate + load the src `.xaml` as `XDocument` with the WPF namespaces.
- `tests/PiPlay.Tests/Infrastructure/Wcag.cs` — contrast-ratio math.
- `tests/PiPlay.Tests/Infrastructure/WpfAppFixture.cs` — STA `Application` bootstrap + collection.
- `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` — Layer 1.
- `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` — Layer 3.
- `tests/PiPlay.Tests/PlacementMathTests.cs`, `ReturnPolicyTests.cs`, `AppPathsTests.cs`, `ProfileServiceTests.cs` — Layer 2 additions.
- `scripts/Test-UiSmoke.ps1` — Layer 4.
- `tests/README.md` — run-lanes doc.

**Modified (tests):**
- `tests/PiPlay.Tests/PiPlay.Tests.csproj` — add `Xunit.StaFact`.
- `tests/PiPlay.Tests/YouTubeUrlHelperTests.cs` — gap cases.

**New (docs):**
- `docs/Spec_Conformance_Review.md` — Phase 0 deliverable.

---

## Phase 0 — Spec-conformance review (produces `docs/Spec_Conformance_Review.md`)

This phase is analysis, not TDD. Under ultracode it is run as a multi-agent fan-out (one agent per requirement area); otherwise do it sequentially. Its output drives which **failing-gap** tests get written later.

- [ ] **Step 0.1: Enumerate requirement areas.** Read `docs/PiPlay_Product_Engineering_Spec.md` and extract every `REQ-*` id, every `Q-*` quality gate (§22), and every `UI-CHK-*` / `MUI-*` case. Group into areas: Navigation, Popout lifecycle/Return, Settings/Profiles, Window/Placement/DPI, Chrome/Visual identity, Fade/Opacity, Single-instance, Recovery/Errors, Packaging.

- [ ] **Step 0.2: Review each area** against `src/` + existing tests. For each requirement record: `id`, `status ∈ {met, partial, gap, untested, intentional-deviation}`, `evidence (file:line)`, `covering test (or none)`, `proposed test / action`.
  - Known finding to record up front: **NAV — Google auth allowed on _both_ surfaces** (`NavigationPolicy.cs:43-46`, comment + `CHANGELOG` rationale) is an *intentional deviation* from a strict reading of REQ-NAV-02 ("player never wanders"), justified because a sign-in redirect must not dead-end the player. Tests assert the real behavior; do **not** "fix" it.

- [ ] **Step 0.3: Write `docs/Spec_Conformance_Review.md`** — a table per area (`id | status | evidence | test | notes`), a "Gaps → tests" backlog section, and a "Confirmed bugs (await approval)" section (empty unless a real bug is found).

- [ ] **Step 0.4: Commit.**
```bash
git add docs/Spec_Conformance_Review.md
git commit -m "docs: spec-conformance review (requirement-by-requirement status + gap backlog)"
```

> **Gate:** If Step 0.2 finds a **real bug** (not a missing test), STOP after writing its failing test (in the relevant phase below) and the report entry; surface it and wait for approval before changing app behavior. Test-enabling seams (Phase 1) are pre-approved and excluded from this gate.

---

## Phase 1 — Test-isolation seams + infrastructure

### Task 1.1: `AppPaths` data-root override

**Files:**
- Modify: `src/PiPlay/Services/AppPaths.cs`
- Test: `tests/PiPlay.Tests/AppPathsTests.cs`

- [ ] **Step 1: Write the failing test**
```csharp
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class AppPathsTests
{
    [Fact]
    public void Root_honors_PIPLAY_DATA_ROOT_override()
    {
        var prev = Environment.GetEnvironmentVariable("PIPLAY_DATA_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", @"C:\some\override");
            Assert.Equal(@"C:\some\override", AppPaths.Root);
            Assert.Equal(@"C:\some\override\settings.json", AppPaths.SettingsFile);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", prev);
        }
    }

    [Fact]
    public void Root_falls_back_to_localappdata_when_unset()
    {
        var prev = Environment.GetEnvironmentVariable("PIPLAY_DATA_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", null);
            Assert.EndsWith("PiPlay", AppPaths.Root);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", prev);
        }
    }
}
```
> Note: `TestCategories` is created in Task 1.4; if executing 1.1 first, temporarily drop the `[Trait]` line and re-add it after 1.4. (Recommended order: do Task 1.4 first, then 1.1–1.3.)

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test tests/PiPlay.Tests --filter FullyQualifiedName~AppPathsTests`
Expected: FAIL — `Root` ignores the env var (currently a cached `%LOCALAPPDATA%` path).

- [ ] **Step 3: Implement** — replace the cached `Root` property with a computed one:
```csharp
using System.IO;

namespace PiPlay.Services;

/// <summary>Central definition of every on-disk location PiPlay uses (spec 11, 18, Data &amp; Privacy Map).</summary>
public static class AppPaths
{
    /// <summary>
    /// Data root. Honors the <c>PIPLAY_DATA_ROOT</c> environment variable when set (used by
    /// tests to stay out of the real user profile); otherwise %LOCALAPPDATA%\PiPlay.
    /// Computed per access so an override set at process start is always picked up.
    /// </summary>
    public static string Root =>
        Environment.GetEnvironmentVariable("PIPLAY_DATA_ROOT") is { Length: > 0 } overrideRoot
            ? overrideRoot
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiPlay");

    public static string LogsDir => Path.Combine(Root, "logs");
    public static string LogFile => Path.Combine(LogsDir, "piplay.log");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string WebView2UserDataDir => Path.Combine(Root, "WebView2UserData");
}
```

- [ ] **Step 4: Run test to verify it passes**
Run: `dotnet test tests/PiPlay.Tests --filter FullyQualifiedName~AppPathsTests`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add src/PiPlay/Services/AppPaths.cs tests/PiPlay.Tests/AppPathsTests.cs
git commit -m "feat(seam): PIPLAY_DATA_ROOT override for AppPaths (test isolation)"
```

### Task 1.2: Extract `PlacementMath.Clamp`

**Files:**
- Create: `src/PiPlay/Services/PlacementMath.cs`
- Modify: `src/PiPlay/Services/WindowPlacementService.cs:115-128` (the private `Clamp`)
- Test: `tests/PiPlay.Tests/PlacementMathTests.cs`

- [ ] **Step 1: Write the failing test**
```csharp
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PlacementMathTests
{
    private static readonly RectI Work = new(0, 0, 1920, 1080);

    [Fact]
    public void Inside_work_area_is_unchanged()
    {
        var r = new RectI(100, 100, 1060, 640); // 960x540
        Assert.Equal(r, PlacementMath.Clamp(r, Work));
    }

    [Fact]
    public void Offscreen_right_is_pulled_back_onto_the_monitor()
    {
        var r = new RectI(1900, 100, 2860, 640); // starts past the right edge
        var c = PlacementMath.Clamp(r, Work);
        Assert.True(c.Right <= Work.Right);
        Assert.Equal(960, c.Width); // size preserved
        Assert.Equal(140, c.Top);   // wait: top unchanged
    }

    [Fact]
    public void Negative_origin_is_clamped_to_work_origin()
    {
        var r = new RectI(-500, -500, 460, 40);
        var c = PlacementMath.Clamp(r, Work);
        Assert.Equal(0, c.Left);
        Assert.Equal(0, c.Top);
    }

    [Fact]
    public void Window_larger_than_work_area_is_shrunk_to_fit()
    {
        var r = new RectI(0, 0, 4000, 3000);
        var c = PlacementMath.Clamp(r, Work);
        Assert.Equal(1920, c.Width);
        Assert.Equal(1080, c.Height);
    }
}
```
> Fix the stray comment: the second test's `Assert.Equal(140, c.Top)` is wrong — remove that line; top is `100` and unchanged. Correct version asserts only `c.Right <= Work.Right` and `c.Width == 960`.

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test tests/PiPlay.Tests --filter FullyQualifiedName~PlacementMathTests`
Expected: FAIL — `RectI`/`PlacementMath` do not exist (compile error).

- [ ] **Step 3: Implement** — create `src/PiPlay/Services/PlacementMath.cs`:
```csharp
namespace PiPlay.Services;

/// <summary>Integer pixel rectangle (Left/Top inclusive, Right/Bottom exclusive-style bounds).</summary>
public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>
/// Pure placement geometry, extracted from <see cref="WindowPlacementService"/> so the
/// "never restore a window off-screen" clamp (spec 16.4, REQ-PROFILE-02) is unit-testable
/// without a live <see cref="System.Windows.Window"/> or monitor enumeration.
/// </summary>
public static class PlacementMath
{
    /// <summary>Clamp <paramref name="r"/> into <paramref name="work"/>: shrink to fit, then keep fully on-screen.</summary>
    public static RectI Clamp(RectI r, RectI work)
    {
        var w = Math.Min(r.Width, work.Width);
        var h = Math.Min(r.Height, work.Height);

        var x = r.Left;
        var y = r.Top;
        if (x < work.Left) x = work.Left;
        if (y < work.Top) y = work.Top;
        if (x + w > work.Right) x = work.Right - w;
        if (y + h > work.Bottom) y = work.Bottom - h;

        return new RectI(x, y, x + w, y + h);
    }
}
```

- [ ] **Step 4: Refactor `WindowPlacementService` to delegate.** Replace its private `Clamp` (lines 115-128) with a thin adapter so behavior is identical and the math lives in one place:
```csharp
    private static RECT Clamp(RECT r, RECT work)
    {
        var c = PlacementMath.Clamp(
            new RectI(r.Left, r.Top, r.Right, r.Bottom),
            new RectI(work.Left, work.Top, work.Right, work.Bottom));
        return new RECT { Left = c.Left, Top = c.Top, Right = c.Right, Bottom = c.Bottom };
    }
```

- [ ] **Step 5: Run tests + build**
Run: `dotnet test tests/PiPlay.Tests --filter FullyQualifiedName~PlacementMathTests`
Expected: PASS. Then `dotnet build src/PiPlay` — Expected: builds clean.

- [ ] **Step 6: Commit**
```bash
git add src/PiPlay/Services/PlacementMath.cs src/PiPlay/Services/WindowPlacementService.cs tests/PiPlay.Tests/PlacementMathTests.cs
git commit -m "refactor(seam): extract pure PlacementMath.Clamp + tests"
```

### Task 1.3: Extract `ReturnPolicy`

**Files:**
- Create: `src/PiPlay/Services/ReturnPolicy.cs`
- Modify: `src/PiPlay/MainWindow.xaml.cs:392-404` (`Player_OnClosed` resume block)
- Test: `tests/PiPlay.Tests/ReturnPolicyTests.cs`

- [ ] **Step 1: Write the failing test** — encodes REQ-RETURN-01 (resume only if the source was playing; `0` is a valid timestamp distinct from unknown):
```csharp
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ReturnPolicyTests
{
    [Theory]
    [InlineData(120, true, ReturnAction.SeekAndPlay)]
    [InlineData(120, false, ReturnAction.Seek)]
    [InlineData(0, true, ReturnAction.SeekAndPlay)]   // 0 is a valid timestamp
    [InlineData(0, false, ReturnAction.Seek)]
    [InlineData(null, true, ReturnAction.Play)]       // unknown timestamp, was playing
    [InlineData(null, false, ReturnAction.None)]      // unknown + paused: do nothing
    public void Decide_matches_REQ_RETURN_01(int? lastKnownSeconds, bool wasPlaying, ReturnAction expected)
    {
        Assert.Equal(expected, ReturnPolicy.Decide(lastKnownSeconds, wasPlaying));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test tests/PiPlay.Tests --filter FullyQualifiedName~ReturnPolicyTests`
Expected: FAIL — `ReturnPolicy`/`ReturnAction` do not exist.

- [ ] **Step 3: Implement** — create `src/PiPlay/Services/ReturnPolicy.cs`:
```csharp
namespace PiPlay.Services;

/// <summary>What the Source Window should do on return from the Popout Player (spec 14).</summary>
public enum ReturnAction
{
    /// <summary>Do nothing: timestamp unknown and the source was paused at popout.</summary>
    None,
    /// <summary>Resume playback at the current position: timestamp unknown but the source was playing.</summary>
    Play,
    /// <summary>Seek to the last-known timestamp and stay paused.</summary>
    Seek,
    /// <summary>Seek to the last-known timestamp and resume.</summary>
    SeekAndPlay,
}

/// <summary>
/// Pure decision for REQ-RETURN-01, extracted from <see cref="MainWindow"/> so it is testable
/// without WebView2. Resume only if the source was playing when popout started; treat a null
/// <paramref name="lastKnownSeconds"/> as "unknown" (0 is a valid timestamp, not unknown).
/// </summary>
public static class ReturnPolicy
{
    public static ReturnAction Decide(int? lastKnownSeconds, bool sourceWasPlaying)
    {
        if (lastKnownSeconds is not null)
            return sourceWasPlaying ? ReturnAction.SeekAndPlay : ReturnAction.Seek;
        return sourceWasPlaying ? ReturnAction.Play : ReturnAction.None;
    }
}
```

- [ ] **Step 4: Refactor `MainWindow.Player_OnClosed`** — replace the inline `if/else` resume block (lines 394-404) with a switch over the policy, preserving exact behavior:
```csharp
                if (core is not null)
                {
                    switch (ReturnPolicy.Decide(state.LastKnownSeconds, _sourceWasPlayingAtPopout))
                    {
                        case ReturnAction.SeekAndPlay:
                            await YouTubeDomBridge.SeekAndPlayAsync(core, state.LastKnownSeconds!.Value);
                            break;
                        case ReturnAction.Seek:
                            await YouTubeDomBridge.SeekAsync(core, state.LastKnownSeconds!.Value);
                            break;
                        case ReturnAction.Play:
                            await YouTubeDomBridge.PlayAsync(core);
                            break;
                        case ReturnAction.None:
                            break;
                    }
                }
```

- [ ] **Step 5: Run tests + build**
Run: `dotnet test tests/PiPlay.Tests --filter FullyQualifiedName~ReturnPolicyTests` — Expected: PASS.
Run: `dotnet build src/PiPlay` — Expected: builds clean.

- [ ] **Step 6: Commit**
```bash
git add src/PiPlay/Services/ReturnPolicy.cs src/PiPlay/MainWindow.xaml.cs tests/PiPlay.Tests/ReturnPolicyTests.cs
git commit -m "refactor(seam): extract pure ReturnPolicy (REQ-RETURN-01) + tests"
```

### Task 1.4: Test infrastructure — traits, data-root init, STA fixture, package

**Files:**
- Modify: `tests/PiPlay.Tests/PiPlay.Tests.csproj`
- Create: `tests/PiPlay.Tests/Infrastructure/TestCategories.cs`
- Create: `tests/PiPlay.Tests/Infrastructure/TestDataRoot.cs`
- Create: `tests/PiPlay.Tests/Infrastructure/WpfAppFixture.cs`

- [ ] **Step 1: Add `Xunit.StaFact`**
Run: `dotnet add tests/PiPlay.Tests package Xunit.StaFact`
Then confirm the `<PackageReference Include="Xunit.StaFact" ... />` line is present in the csproj (pin whatever version resolves).

- [ ] **Step 2: Trait constants** — `tests/PiPlay.Tests/Infrastructure/TestCategories.cs`:
```csharp
namespace PiPlay.Tests;

/// <summary>xUnit trait names so lanes can be filtered: `dotnet test --filter Category=Markup`.</summary>
public static class TestCategories
{
    public const string Key = "Category";
    public const string Markup = "Markup"; // Layer 1 — XAML parsed as XML, no WPF runtime
    public const string Logic = "Logic";   // Layer 2 — pure services
    public const string Wpf = "Wpf";       // Layer 3 — live WPF on STA
}
```

- [ ] **Step 3: Redirect the data root for the whole test run** — `tests/PiPlay.Tests/Infrastructure/TestDataRoot.cs`:
```csharp
using System.IO;
using System.Runtime.CompilerServices;

namespace PiPlay.Tests;

/// <summary>
/// Point PiPlay's on-disk root at a throwaway temp dir for the entire test process, so any
/// code that touches AppPaths (e.g. constructing MainWindow in Layer 3) never reads or writes
/// the developer's real %LOCALAPPDATA%\PiPlay. Runs before any test via [ModuleInitializer].
/// </summary>
internal static class TestDataRoot
{
    [ModuleInitializer]
    public static void Init()
    {
        // Don't clobber an override the caller set deliberately.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PIPLAY_DATA_ROOT")))
            return;
        var dir = Path.Combine(Path.GetTempPath(), "PiPlayTests", "data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", dir);
    }
}
```

- [ ] **Step 4: STA `Application` fixture** — `tests/PiPlay.Tests/Infrastructure/WpfAppFixture.cs`:
```csharp
using System.Windows;
using PiPlay;

namespace PiPlay.Tests;

/// <summary>
/// Boots a single WPF <see cref="Application"/> for the test process and ensures the PiPlay
/// assembly's resources (App.xaml merged dictionaries, the app icon) resolve. Crucially sets
/// <see cref="Application.ResourceAssembly"/> to the PiPlay assembly: the windows use
/// short-form pack URIs (`pack://application:,,,/Assets/piplay.ico`, `{StaticResource ...}`)
/// that otherwise resolve against the test host and fail. Windows are constructed but never
/// shown, so WebView2 (created in Loaded) and the network are never touched.
/// </summary>
public sealed class WpfAppFixture
{
    public WpfAppFixture()
    {
        if (Application.Current is not null) return;

        // Must be set before any pack:// resource is resolved.
        Application.ResourceAssembly = typeof(MainWindow).Assembly;

        var app = new App();      // PiPlay.App
        app.InitializeComponent(); // loads Theme/Colors.xaml + Theme/ControlStyles.xaml into App.Resources
    }
}

/// <summary>Single shared WPF app across all STA UI tests (one Application per process).</summary>
[CollectionDefinition(Name)]
public sealed class WpfCollection : ICollectionFixture<WpfAppFixture>
{
    public const string Name = "WPF";
}
```
> If `App.InitializeComponent()` is not accessible from the test assembly, replace the two `app` lines with manual loading:
> ```csharp
> var app = new Application();
> foreach (var src in new[] { "Theme/Colors.xaml", "Theme/ControlStyles.xaml" })
>     app.Resources.MergedDictionaries.Add(new ResourceDictionary
>     {
>         Source = new Uri($"pack://application:,,,/PiPlay;component/{src}", UriKind.Absolute)
>     });
> ```

- [ ] **Step 5: Verify build + existing tests still green**
Run: `dotnet test tests/PiPlay.Tests` — Expected: all existing tests PASS, new infra compiles. (No new tests asserted yet here.)

- [ ] **Step 6: Backfill `[Trait]` on existing test classes** so lanes are complete: add `[Trait(TestCategories.Key, TestCategories.Logic)]` to `FadePolicyTests`, `NavigationPolicyTests`, `SettingsServiceTests`, `YouTubeUrlHelperTests`.

- [ ] **Step 7: Commit**
```bash
git add tests/PiPlay.Tests
git commit -m "test(infra): Xunit.StaFact, trait categories, temp data-root, STA app fixture"
```

---

## Phase 2 — Layer 1: XAML markup invariants

### Task 2.1: XAML file loader

**Files:**
- Create: `tests/PiPlay.Tests/Infrastructure/XamlTestFiles.cs`

- [ ] **Step 1: Implement the loader** (no separate test — exercised by 2.2+):
```csharp
using System.IO;
using System.Xml.Linq;

namespace PiPlay.Tests;

/// <summary>Locates and loads the source .xaml files as XML for markup-invariant assertions.</summary>
internal static class XamlTestFiles
{
    public static readonly XNamespace Pres = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    public static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Absolute path to `src/PiPlay`, found by walking up to the repo root (PiPlay.sln).</summary>
    public static string SrcDir { get; } = ResolveSrcDir();

    public static XDocument Load(string fileName) =>
        XDocument.Load(Path.Combine(SrcDir, fileName), LoadOptions.SetLineInfo);

    private static string ResolveSrcDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PiPlay.sln")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("Could not locate repo root (PiPlay.sln) from " + AppContext.BaseDirectory);
        return Path.Combine(dir.FullName, "src", "PiPlay");
    }
}
```

- [ ] **Step 2: Commit** (with Task 2.2, see below) — no standalone commit.

### Task 2.2: Window property invariants

**Files:**
- Create: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`

- [ ] **Step 1: Write the failing test** — the heart of the "rounding = 0" guard:
```csharp
using System.Xml.Linq;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Markup)]
public class XamlInvariantTests
{
    private static XElement Window(string file) =>
        XamlTestFiles.Load(file).Root!;

    private static string? Attr(XElement e, string name) => e.Attribute(name)?.Value;

    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData("PlayerWindow.xaml")]
    public void Window_layout_and_airspace_invariants_hold(string file)
    {
        var w = Window(file);

        // The rounding = 0 regression: layout rounding MUST be off on both windows (UI-CHK-5).
        Assert.Equal("False", Attr(w, "UseLayoutRounding"));
        // WebView2 airspace hard constraint (ADR-0004): a transparent window breaks the HwndHost.
        Assert.Equal("False", Attr(w, "AllowsTransparency"));
        // Custom chrome + crisp scaling.
        Assert.Equal("None", Attr(w, "WindowStyle"));
        Assert.Equal("True", Attr(w, "SnapsToDevicePixels"));
    }

    [Theory]
    [InlineData("MainWindow.xaml", "42")]
    [InlineData("PlayerWindow.xaml", "0")]
    public void WindowChrome_invariants_hold(string file, string expectedCaptionHeight)
    {
        var chrome = Window(file)
            .Descendants(XamlTestFiles.Pres + "WindowChrome")
            .Single();

        Assert.Equal("0", chrome.Attribute("CornerRadius")?.Value);
        Assert.Equal("0", chrome.Attribute("GlassFrameThickness")?.Value);
        Assert.Equal("False", chrome.Attribute("UseAeroCaptionButtons")?.Value);
        Assert.Equal(BorderlessResizeHitTestPolicy.ResizeBorderDip.ToString(),
            chrome.Attribute("ResizeBorderThickness")?.Value);
        Assert.Equal(expectedCaptionHeight, chrome.Attribute("CaptionHeight")?.Value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**, then passes against current XAML.
Run: `dotnet test tests/PiPlay.Tests --filter FullyQualifiedName~XamlInvariantTests`
Expected: first PASS immediately (the XAML already satisfies these). To prove the guard bites, temporarily set `UseLayoutRounding="True"` in `MainWindow.xaml`, re-run → FAIL, then revert → PASS. (This is the regression-catch proof; record it in the commit message, don't leave the edit.)

- [ ] **Step 3: Commit**
```bash
git add tests/PiPlay.Tests/Infrastructure/XamlTestFiles.cs tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
git commit -m "test(markup): window layout/airspace + WindowChrome invariants (re-catches rounding=0)"
```

### Task 2.3: Required named controls present

**Files:**
- Modify: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`

- [ ] **Step 1: Add the test** — guards against a rename/removal silently breaking code-behind `FindName`/generated fields:
```csharp
    [Theory]
    [MemberData(nameof(RequiredNames))]
    public void Required_named_controls_exist(string file, string[] names)
    {
        var doc = XamlTestFiles.Load(file);
        var present = doc.Descendants()
            .Select(e => e.Attribute(XamlTestFiles.X + "Name")?.Value)
            .Where(n => n is not null)
            .ToHashSet();

        foreach (var name in names)
            Assert.Contains(name, present!);
    }

    public static IEnumerable<object[]> RequiredNames() => new[]
    {
        new object[] { "MainWindow.xaml", new[]
        {
            "Browser", "UrlBox", "ProfilesCombo", "PinToggle", "PinnedHint", "PopOutButton",
            "BackButton", "ReloadButton", "HomeButton", "SaveProfileButton",
            "MinimizeButton", "MaximizeButton", "CloseButton",
            "SourcePlaceholder", "RuntimeErrorPanel", "RuntimeErrorText",
        }},
        new object[] { "PlayerWindow.xaml", new[]
        {
            "ChromeStrip", "FadeToggle", "PinToggle", "CloseButton", "Player",
        }},
    };
```

- [ ] **Step 2: Run** → PASS. **Step 3: Commit**
```bash
git add tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
git commit -m "test(markup): assert required x:Name controls exist on both windows"
```

### Task 2.4: Glyph icon-font + tooltips

**Files:**
- Modify: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`

- [ ] **Step 1: Add the tests** — guards the `.notdef` empty-box regression (REQ-UI-02) and tooltip presence (UI-CHK-4):
```csharp
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";

    // Styles that themselves set the icon FontFamily (so a glyph Content renders correctly).
    private static readonly HashSet<string> IconFontStyles = new()
    {
        "{StaticResource IconButton}", "{StaticResource CloseIconButton}", "{StaticResource PinToggle}",
    };

    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData("PlayerWindow.xaml")]
    public void Glyph_controls_use_the_icon_font(string file)
    {
        var doc = XamlTestFiles.Load(file);

        // Any TextBlock whose Text is a single PUA glyph (>= U+E000) must declare the icon font inline.
        foreach (var tb in doc.Descendants(XamlTestFiles.Pres + "TextBlock"))
        {
            var text = tb.Attribute("Text")?.Value;
            if (text is null || text.Length == 0 || text[0] < '') continue;
            var font = tb.Attribute("FontFamily")?.Value;
            Assert.True(font is not null && font.Contains("Segoe Fluent Icons"),
                $"Glyph TextBlock '{text}' in {file} is missing the icon FontFamily.");
        }

        // Any Button/ToggleButton carrying a glyph Content must use an icon-font style.
        foreach (var btn in doc.Descendants().Where(e =>
                     e.Name == XamlTestFiles.Pres + "Button" || e.Name == XamlTestFiles.Pres + "ToggleButton"))
        {
            var content = btn.Attribute("Content")?.Value;
            if (content is null || content.Length == 0 || content[0] < '') continue;
            var style = btn.Attribute("Style")?.Value;
            Assert.True(style is not null && IconFontStyles.Contains(style),
                $"Glyph button '{content}' in {file} must use an icon-font style (was '{style}').");
        }
    }

    [Fact]
    public void Caption_and_toolbar_controls_have_tooltips()
    {
        var doc = XamlTestFiles.Load("MainWindow.xaml");
        var byName = doc.Descendants()
            .Where(e => e.Attribute(XamlTestFiles.X + "Name") is not null)
            .ToDictionary(e => e.Attribute(XamlTestFiles.X + "Name")!.Value);

        foreach (var name in new[]
        {
            "MinimizeButton", "MaximizeButton", "CloseButton", "BackButton", "ReloadButton",
            "HomeButton", "UrlBox", "ProfilesCombo", "SaveProfileButton", "PinToggle",
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(byName[name].Attribute("ToolTip")?.Value),
                $"{name} is missing a ToolTip (UI-CHK-4).");
        }
    }
```
> Note: `PopOutButton` carries its glyph in a nested `TextBlock` with an explicit icon `FontFamily`, so it's covered by the TextBlock check, not the button-style check.

- [ ] **Step 2: Run** → PASS. **Step 3: Commit**
```bash
git add tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
git commit -m "test(markup): glyph icon-font fallback + tooltip presence (REQ-UI-02, UI-CHK-4)"
```

### Task 2.5: Resource integrity (every `{StaticResource}` resolves)

**Files:**
- Modify: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`

- [ ] **Step 1: Add the test** — catches a renamed/deleted theme token before it becomes a runtime crash:
```csharp
    [Fact]
    public void Every_StaticResource_reference_is_defined()
    {
        var files = new[] { "App.xaml", "MainWindow.xaml", "PlayerWindow.xaml",
                            "Theme/ControlStyles.xaml", "Theme/Colors.xaml" };

        var defined = new HashSet<string>();
        var referenced = new HashSet<string>();
        var rx = new System.Text.RegularExpressions.Regex(
            @"\{StaticResource\s+([^}]+)\}", System.Text.RegularExpressions.RegexOptions.Compiled);

        foreach (var f in files)
        {
            var doc = XamlTestFiles.Load(f);
            foreach (var el in doc.Descendants())
            {
                if (el.Attribute(XamlTestFiles.X + "Key")?.Value is { } key) defined.Add(key.Trim());
                foreach (var a in el.Attributes())
                    foreach (System.Text.RegularExpressions.Match m in rx.Matches(a.Value))
                        referenced.Add(m.Groups[1].Value.Trim());
            }
        }

        var missing = referenced.Where(r => !defined.Contains(r)).OrderBy(x => x).ToArray();
        Assert.True(missing.Length == 0,
            "Undefined StaticResource keys: " + string.Join(", ", missing));
    }
```

- [ ] **Step 2: Run** → PASS. **Step 3: Commit**
```bash
git add tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
git commit -m "test(markup): every {StaticResource} reference resolves to a defined key"
```

### Task 2.6: Theme contrast (WCAG)

**Files:**
- Create: `tests/PiPlay.Tests/Infrastructure/Wcag.cs`
- Modify: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`

- [ ] **Step 1: Implement the contrast helper** — `tests/PiPlay.Tests/Infrastructure/Wcag.cs`:
```csharp
using System.Globalization;

namespace PiPlay.Tests;

/// <summary>WCAG 2.x relative-luminance contrast ratio from #AARRGGBB / #RRGGBB hex strings.</summary>
internal static class Wcag
{
    public static double ContrastRatio(string hexA, string hexB)
    {
        var (la, lb) = (Luminance(hexA), Luminance(hexB));
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 8) hex = hex.Substring(2); // drop alpha
        var r = Channel(hex.Substring(0, 2));
        var g = Channel(hex.Substring(2, 2));
        var b = Channel(hex.Substring(4, 2));
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double Channel(string twoHex)
    {
        var v = int.Parse(twoHex, NumberStyles.HexNumber) / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}
```

- [ ] **Step 2: Add the contrast test** to `XamlInvariantTests.cs` — reads the actual tokens from `Colors.xaml`:
```csharp
    private static Dictionary<string, string> ColorTokens()
    {
        var doc = XamlTestFiles.Load("Theme/Colors.xaml");
        return doc.Descendants(XamlTestFiles.Pres + "Color")
            .Where(e => e.Attribute(XamlTestFiles.X + "Key") is not null)
            .ToDictionary(
                e => e.Attribute(XamlTestFiles.X + "Key")!.Value,
                e => e.Value.Trim());
    }

    [Theory]
    // foreground token, background token, min ratio (4.5 = WCAG AA normal text)
    [InlineData("TextPrimaryColor", "SurfaceRaisedColor", 4.5)]   // URL box (UI-CHK-5)
    [InlineData("TextPrimaryColor", "AppBackgroundColor", 4.5)]
    [InlineData("TextPrimaryColor", "SurfaceBaseColor", 4.5)]
    [InlineData("TextSecondaryColor", "SurfaceBaseColor", 4.5)]   // secondary text / empty state
    public void Theme_contrast_meets_minimum(string fg, string bg, double min)
    {
        var t = ColorTokens();
        var ratio = Wcag.ContrastRatio(t[fg], t[bg]);
        Assert.True(ratio >= min, $"{fg} on {bg} = {ratio:F2}:1, below {min}:1.");
    }

    [Fact]
    public void Accent_button_text_is_readable_on_accent_fill()
    {
        // AccentButton: foreground #FF06141A literal on AccentCyan fill (ControlStyles.xaml).
        var ratio = Wcag.ContrastRatio("#FF06141A", ColorTokens()["AccentCyanColor"]);
        Assert.True(ratio >= 4.5, $"Accent button text contrast = {ratio:F2}:1.");
    }
```

- [ ] **Step 3: Run** → PASS (TextPrimary/SurfaceRaised computes ~14.98:1 per the existing review). **Step 4: Commit**
```bash
git add tests/PiPlay.Tests/Infrastructure/Wcag.cs tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
git commit -m "test(markup): WCAG contrast floor for theme token pairs (UI-CHK-5)"
```

---

## Phase 3 — Layer 2: logic gap tests

### Task 3.1: `ProfileService` tests

**Files:**
- Create: `tests/PiPlay.Tests/ProfileServiceTests.cs`

- [ ] **Step 1: Write the tests**
```csharp
using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ProfileServiceTests
{
    private static AppSettings WithProfiles(params string[] names)
    {
        var s = new AppSettings();
        foreach (var n in names)
            s.Profiles.Add(new Profile { Name = n, Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" });
        return s;
    }

    [Fact]
    public void Find_and_Exists_are_case_insensitive()
    {
        var s = WithProfiles("Lo-fi");
        Assert.True(ProfileService.Exists(s, "lo-fi"));
        Assert.NotNull(ProfileService.Find(s, "LO-FI"));
        Assert.False(ProfileService.Exists(s, "jazz"));
    }

    [Fact]
    public void Save_new_appends_and_returns_false()
    {
        var s = WithProfiles();
        var replaced = ProfileService.Save(s, new Profile { Name = "Lo-fi", Url = "https://youtu.be/dQw4w9WgXcQ" });
        Assert.False(replaced);
        Assert.Single(s.Profiles);
    }

    [Fact]
    public void Save_existing_overwrites_by_name_and_returns_true()
    {
        var s = WithProfiles("Lo-fi");
        var replaced = ProfileService.Save(s, new Profile { Name = "lo-fi", Url = "https://youtu.be/new12345678", Topmost = true });
        Assert.True(replaced);
        Assert.Single(s.Profiles);
        Assert.Equal("https://youtu.be/new12345678", s.Profiles[0].Url);
    }

    [Fact]
    public void Remove_returns_true_only_when_present()
    {
        var s = WithProfiles("Lo-fi");
        Assert.True(ProfileService.Remove(s, "Lo-fi"));
        Assert.False(ProfileService.Remove(s, "Lo-fi"));
        Assert.Empty(s.Profiles);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("not a url", false)]
    [InlineData("https://example.com/watch?v=dQw4w9WgXcQ", false)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", true)]
    public void ValidateUrl_accepts_only_supported_youtube_urls(string? url, bool ok)
    {
        Assert.Equal(ok, ProfileService.ValidateUrl(url).Ok);
    }
}
```

- [ ] **Step 2: Run** → PASS. **Step 3: Commit**
```bash
git add tests/PiPlay.Tests/ProfileServiceTests.cs
git commit -m "test(logic): ProfileService find/save/overwrite/remove/validate"
```

### Task 3.2: `YouTubeUrlHelper` gap cases

**Files:**
- Modify: `tests/PiPlay.Tests/YouTubeUrlHelperTests.cs`

- [ ] **Step 1: Add the gap tests** (existing file already covers the common shapes; add the untested ones):
```csharp
    [Theory]
    [InlineData("https://www.youtube.com/v/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    public void Parses_path_based_v_and_live_ids(string url, string expected)
    {
        Assert.True(YouTubeUrlHelper.TryParse(url, out var t));
        Assert.Equal(expected, t.VideoId);
    }

    [Fact]
    public void Parses_youtu_be_with_list_and_timestamp()
    {
        Assert.True(YouTubeUrlHelper.TryParse("https://youtu.be/dQw4w9WgXcQ?list=PLabc&t=1m", out var t));
        Assert.Equal("dQw4w9WgXcQ", t.VideoId);
        Assert.Equal("PLabc", t.PlaylistId);
        Assert.Equal(60, t.StartSeconds);
    }

    [Fact]
    public void Parses_watch_with_start_param_alias()
    {
        Assert.True(YouTubeUrlHelper.TryParse("https://www.youtube.com/watch?v=dQw4w9WgXcQ&start=45", out var t));
        Assert.Equal(45, t.StartSeconds);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("")]
    public void ParseTime_rejects_garbage_and_negatives(string value)
    {
        Assert.Null(YouTubeUrlHelper.ParseTime(value));
    }

    [Fact]
    public void BuildWatchUrl_for_playlist_only_target()
    {
        var t = new PiPlay.Models.YouTubeTarget { PlaylistId = "PLabc", IsPlaylistOnly = true };
        Assert.Equal("https://www.youtube.com/playlist?list=PLabc", YouTubeUrlHelper.BuildWatchUrl(t));
    }

    [Fact]
    public void BuildEmbedUrl_includes_autoplay_playlist_and_start()
    {
        var t = new PiPlay.Models.YouTubeTarget { VideoId = "dQw4w9WgXcQ", PlaylistId = "PLabc" };
        Assert.Equal(
            "https://www.youtube.com/embed/dQw4w9WgXcQ?autoplay=1&list=PLabc&start=30",
            YouTubeUrlHelper.BuildEmbedUrl(t, 30));
    }
```

- [ ] **Step 2: Run** → PASS. **Step 3: Commit**
```bash
git add tests/PiPlay.Tests/YouTubeUrlHelperTests.cs
git commit -m "test(logic): YouTubeUrlHelper path ids, start alias, embed/playlist-only, ParseTime guards"
```

### Task 3.3: `NavigationPolicy` scheme cases

**Files:**
- Modify: `tests/PiPlay.Tests/NavigationPolicyTests.cs`

- [ ] **Step 1: Add the tests** (the both-surfaces Google-auth behavior is already covered; add the in-app scheme rules and null-guard, which lock REQ-NAV behavior the handlers rely on):
```csharp
    [Theory]
    [InlineData("about:blank")]
    [InlineData("data:text/html,<p>x</p>")]
    [InlineData("blob:https://www.youtube.com/abc")]
    public void Inapp_runtime_schemes_are_allowed(string url)
    {
        Assert.True(NavigationPolicy.IsAllowed(new Uri(url), NavigationSurface.Source));
        Assert.True(NavigationPolicy.IsAllowed(new Uri(url), NavigationSurface.Player));
    }

    [Fact]
    public void Null_uri_is_not_allowed()
    {
        Assert.False(NavigationPolicy.IsAllowed(null, NavigationSurface.Source));
    }
```

- [ ] **Step 2: Run** → PASS. **Step 3: Commit**
```bash
git add tests/PiPlay.Tests/NavigationPolicyTests.cs
git commit -m "test(logic): NavigationPolicy in-app schemes + null-uri guard"
```

---

## Phase 4 — Layer 3: live WPF on STA

> All tests in this phase are in the `WpfCollection` and use `[StaFact]`/`[StaTheory]` from `Xunit.StaFact`. Construction never calls `Show()`, so WebView2 (`Loaded`) and the network are never hit. The `WpfAppFixture` provides the `Application` + `ResourceAssembly`; `TestDataRoot` keeps settings I/O in a temp dir.

### Task 4.1 + 4.2: Windows construct cleanly; resolved DP invariants

**Files:**
- Create: `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`

- [ ] **Step 1: Write the tests**
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using Microsoft.Web.WebView2.Core;
using PiPlay;
using PiPlay.Models;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Wpf)]
[Collection(WpfCollection.Name)]
public class WpfRuntimeTests
{
    private static PlayerWindow NewPlayer() =>
        // environment is only used in InitializePlayerAsync (Loaded), never in the ctor.
        new(environment: null!, url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            topmost: false, placement: null, defaultWidth: 960, defaultHeight: 540, fadeEnabled: true);

    [StaFact]
    public void MainWindow_constructs_without_throwing()
    {
        // Proves every {StaticResource} resolves at runtime and all templates compile.
        var ex = Record.Exception(() => new MainWindow());
        Assert.Null(ex);
    }

    [StaFact]
    public void PlayerWindow_constructs_without_throwing()
    {
        var ex = Record.Exception(() => NewPlayer());
        Assert.Null(ex);
    }

    [StaFact]
    public void MainWindow_holds_layout_and_airspace_invariants()
    {
        var w = new MainWindow();
        Assert.False(w.UseLayoutRounding);
        Assert.False(w.AllowsTransparency);
        Assert.Equal(WindowStyle.None, w.WindowStyle);
        var chrome = WindowChrome.GetWindowChrome(w);
        Assert.NotNull(chrome);
        Assert.Equal(new CornerRadius(0), chrome!.CornerRadius);
    }

    [StaFact]
    public void PlayerWindow_holds_layout_and_airspace_invariants()
    {
        var w = NewPlayer();
        Assert.False(w.UseLayoutRounding);
        Assert.False(w.AllowsTransparency);
        Assert.Equal(WindowStyle.None, w.WindowStyle);
        Assert.Equal(new CornerRadius(0), WindowChrome.GetWindowChrome(w)!.CornerRadius);
    }
}
```
> If `PlayerWindow(null!, …)` throws in the ctor (it should not — `_environment` is only stored), pass a real env via `await CoreWebView2Environment.CreateAsync(...)` in a `[StaFact] async` test guarded by WebView2 runtime availability. Default path assumes the null is fine because the ctor only assigns it.

- [ ] **Step 2: Run** → first run proves nothing fails on construction.
Run: `dotnet test tests/PiPlay.Tests --filter Category=Wpf`
Expected: PASS. If `MainWindow_constructs_without_throwing` fails on the pack-URI `Icon` line, verify `WpfAppFixture` set `Application.ResourceAssembly = typeof(MainWindow).Assembly` (this is the fix).

- [ ] **Step 3: Commit**
```bash
git add tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs
git commit -m "test(wpf): windows construct cleanly + resolved layout/airspace DP invariants"
```

### Task 4.3: Named element types + styled-control template applies

**Files:**
- Modify: `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`

- [ ] **Step 1: Add the tests**
```csharp
    [StaFact]
    public void Named_controls_resolve_to_expected_types()
    {
        var w = new MainWindow();
        Assert.IsType<TextBox>(w.FindName("UrlBox"));
        Assert.IsType<Button>(w.FindName("PopOutButton"));
        Assert.IsType<ComboBox>(w.FindName("ProfilesCombo"));
        Assert.IsType<Border>(w.FindName("SourcePlaceholder"));
        Assert.IsType<Border>(w.FindName("RuntimeErrorPanel"));
    }

    [StaFact]
    public void DarkTextBox_template_applies_and_resolves_part_content_host()
    {
        var w = new MainWindow();
        var url = (TextBox)w.FindName("UrlBox")!;
        url.Measure(new Size(400, 32));
        url.Arrange(new Rect(0, 0, 400, 32));
        url.ApplyTemplate();
        Assert.NotNull(url.Template);
        Assert.NotNull(url.Template.FindName("PART_ContentHost", url)); // template wired correctly
    }
```

- [ ] **Step 2: Run** → PASS. **Step 3: Commit**
```bash
git add tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs
git commit -m "test(wpf): named-control types + DarkTextBox template/PART_ContentHost"
```

### Task 4.4: DPI characterization render test (the affirmative rounding guard)

**Files:**
- Modify: `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`

- [ ] **Step 1: Add the render test** — renders a styled `TextBox` at 150 % DPI and asserts the text's inked rows are not collapsed to a thin band (what `UseLayoutRounding=True` did at fractional DPI):
```csharp
    [StaTheory]
    [InlineData(false)] // production setting: text must render at full height
    public void UrlText_is_not_clipped_to_a_band_at_150pct_dpi(bool useLayoutRounding)
    {
        const double dpi = 144; // 150%
        var host = new Border
        {
            Width = 320,
            Height = 32,
            UseLayoutRounding = useLayoutRounding,
            Background = System.Windows.Media.Brushes.Black,
            Child = new TextBox
            {
                Text = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                Style = (Style)Application.Current.FindResource("DarkTextBox"),
            },
        };

        host.Measure(new Size(320, 32));
        host.Arrange(new Rect(0, 0, 320, 32));
        host.UpdateLayout();

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)Math.Ceiling(320 * dpi / 96), (int)Math.Ceiling(32 * dpi / 96), dpi, dpi,
            System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(host);

        var inkedRows = CountInkedRows(rtb);
        // Real text occupies many rows; the clipping bug collapsed it to ~1-2 device rows.
        Assert.True(inkedRows >= 8, $"Only {inkedRows} inked rows — text appears clipped to a band.");
    }

    // Count horizontal scanlines that contain a meaningfully non-black pixel (text ink).
    private static int CountInkedRows(System.Windows.Media.Imaging.RenderTargetBitmap rtb)
    {
        var w = rtb.PixelWidth;
        var h = rtb.PixelHeight;
        var stride = w * 4;
        var px = new byte[h * stride];
        rtb.CopyPixels(px, stride, 0);

        var rows = 0;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = y * stride + x * 4;
                // Pbgra32: B,G,R,A. Text is light (#F3F5F7) on near-black; look for bright pixels.
                if (px[i] > 80 || px[i + 1] > 80 || px[i + 2] > 80) { rows++; break; }
            }
        }
        return rows;
    }
```
> Assertion is structural (inked-row count), never exact pixels, so it is robust across machines/fonts (design §7). The `[InlineData(true)]` (reproducing the clip) is intentionally omitted from CI because the failure mode depends on font metrics; the `false` case is the guarantee we ship.

- [ ] **Step 2: Run** → PASS.
Run: `dotnet test tests/PiPlay.Tests --filter FullyQualifiedName~UrlText_is_not_clipped`
Expected: PASS (inked rows well above 8 for ~24px text at 150%).

- [ ] **Step 3: Commit**
```bash
git add tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs
git commit -m "test(wpf): RenderTargetBitmap proves URL text not clipped at 150% DPI"
```

---

## Phase 5 — Layer 4: manual E2E smoke + run docs

### Task 5.1: UIA + screenshot smoke harness

**Files:**
- Create: `scripts/Test-UiSmoke.ps1`

- [ ] **Step 1: Write the harness** (manual; needs a desktop session + WebView2 + network):
```powershell
#requires -Version 7
<#
.SYNOPSIS
  Manual end-to-end UI smoke for PiPlay: launches the built exe, asserts key UI elements via
  UI Automation, and captures screenshots for the spec section 22.2 chrome acceptance review.
.NOTES
  Not part of `dotnet test`. Run on an interactive desktop. Capture at a fractional DPI (e.g.
  150%) to expose the rounding/clipping class of bug (see docs/AGENTS.md).
#>
param(
    [string]$ExePath = "$PSScriptRoot\..\bin\publish\latest\PiPlay.exe",
    [string]$EvidenceDir = "$PSScriptRoot\..\docs\evidence",
    [int]$ReadyTimeoutSec = 30
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ExePath)) { throw "PiPlay.exe not found at $ExePath. Build first: .\Build-PiPlay.ps1 -Stage Publish" }
New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null

$proc = Start-Process -FilePath $ExePath -PassThru
try {
    $deadline = (Get-Date).AddSeconds($ReadyTimeoutSec)
    $root = $null
    while ((Get-Date) -lt $deadline -and -not $root) {
        Start-Sleep -Milliseconds 400
        $root = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)))
    }
    if (-not $root) { throw "PiPlay main window did not appear within $ReadyTimeoutSec s." }

    function Assert-Element([string]$automationId, [string]$label) {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
        $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if (-not $el) { throw "MISSING UI element: $label (AutomationId=$automationId)" }
        Write-Host "OK  $label" -ForegroundColor Green
    }

    # WPF maps x:Name -> AutomationId, so these match the named controls in MainWindow.xaml.
    Assert-Element 'PopOutButton' 'Pop out video button'
    Assert-Element 'UrlBox'       'URL / address box'
    Assert-Element 'CloseButton'  'Close caption button'
    Assert-Element 'ProfilesCombo' 'Profiles dropdown'

    # Screenshot the window region for the chrome acceptance review.
    $rect = $root.Current.BoundingRectangle
    $bmp = New-Object System.Drawing.Bitmap([int]$rect.Width, [int]$rect.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
    $dpi = [int]([System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width) # informational label
    $shot = Join-Path $EvidenceDir ("ui-smoke-{0}.png" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "Saved screenshot: $shot" -ForegroundColor Cyan
    Write-Host "SMOKE PASS" -ForegroundColor Green
}
finally {
    if (-not $proc.HasExited) { $proc.CloseMainWindow() | Out-Null; Start-Sleep 1; if (-not $proc.HasExited) { $proc.Kill() } }
}
```

- [ ] **Step 2: Smoke-run it manually** (requires a publish build):
Run: `pwsh -File scripts/Test-UiSmoke.ps1`
Expected: `OK` lines for each element + `SMOKE PASS` + a PNG in `docs/evidence/`. If the build isn't published, it instructs you to build first (not a test failure).

- [ ] **Step 3: Commit**
```bash
git add scripts/Test-UiSmoke.ps1
git commit -m "test(e2e): manual UIA + screenshot smoke harness (Layer 4, release gate)"
```

### Task 5.2: Run-lanes doc

**Files:**
- Create: `tests/README.md`

- [ ] **Step 1: Write** `tests/README.md`:
````markdown
# PiPlay tests

See `docs/Regression_Test_Suite_Design.md` for the full design.

## Lane A — `dotnet test` (fast, deterministic, headless)

```bash
dotnet test                                  # everything in PiPlay.Tests
dotnet test --filter Category=Markup         # Layer 1: XAML invariants (no WPF runtime)
dotnet test --filter Category=Logic          # Layer 2: pure services
dotnet test --filter Category=Wpf            # Layer 3: live WPF on STA (no Show/WebView2/network)
```

Layer 3 runs an `Application` on an STA thread and constructs the windows without showing them.
`PIPLAY_DATA_ROOT` is auto-redirected to a temp dir for the whole run (see `TestDataRoot`).

## Lane B — manual E2E smoke (release gate, NOT in `dotnet test`)

Needs an interactive desktop, the WebView2 runtime, and network. Build a publish first, then:

```powershell
pwsh -File scripts/Test-UiSmoke.ps1
```

Capture at a fractional DPI (e.g. 150%) — integer-scale captures hide the rounding/clipping
class of bug (`docs/AGENTS.md`). Evidence lands in `docs/evidence/`.
````

- [ ] **Step 2: Commit**
```bash
git add tests/README.md
git commit -m "docs(test): run-lanes README"
```

---

## Phase 6 — Verify + finalize

- [ ] **Step 1: Full green run**
Run: `dotnet test tests/PiPlay.Tests`
Expected: all PASS (existing + new). Record the count. If any **failing-gap** test exists from Phase 0, it must be explicitly marked `[Fact(Skip="<review id>: awaiting fix approval")]` with a comment pointing at the review entry — never left red.

- [ ] **Step 2: Update `docs/CHANGELOG.md`** under `[Unreleased]` → add a `### Tests` (or `### Added`) bullet describing the four-layer regression suite + the conformance review, and note the three seams.

- [ ] **Step 3: Cross-link the QA checklist** — in `docs/QA_Checklist.md` §8, note that UI-CHK-5 (URL legibility) and the resource/contrast checks now have automated coverage (Layer 1/3), so manual focus shifts to true-render verification (Layer 4).

- [ ] **Step 4: Commit + summary**
```bash
git add docs/CHANGELOG.md docs/QA_Checklist.md
git commit -m "docs: changelog + QA cross-refs for the regression suite"
```

- [ ] **Step 5: Report** to the user: test counts per lane, the conformance-review findings (esp. any confirmed bugs awaiting approval), and the branch state. Do NOT open a PR or merge unless asked.

---

## Self-review (against `docs/Regression_Test_Suite_Design.md`)

- **§3 layers/lanes** → Phases 2 (L1), 3 (L2), 4 (L3), 5 (L4); traits in Task 1.4. ✓
- **§4.1 markup invariants** (window props, WindowChrome, named controls, glyph font, tooltips, resource integrity, contrast) → Tasks 2.2–2.6. ✓
- **§4.2 logic gaps** → Tasks 3.1–3.3; note existing coverage already strong (nav both-surfaces, url shapes, settings recovery). ✓
- **§4.3 live WPF** (construct, DP values, FindName, template, DPI render) → Tasks 4.1–4.4. ✓
- **§4.4 E2E** → Task 5.1. ✓
- **§5 seams** (AppPaths override, ReturnPolicy, + the design's "placement clamp extraction" risk in §7) → Tasks 1.1, 1.3, 1.2. ✓
- **§6 deliverables** (review doc, layers, smoke + run-doc, seams, failing-gap protocol) → Phase 0, Phases 2–5, Phase 6. ✓
- **Placeholder scan:** the stray `Assert.Equal(140, c.Top)` in Task 1.2 Step 1 is flagged inline with its fix. No `TBD`/`TODO`. ✓
- **Type consistency:** `TestCategories`, `RectI`/`PlacementMath.Clamp`, `ReturnAction`/`ReturnPolicy.Decide`, `XamlTestFiles.{Pres,X,Load}`, `Wcag.ContrastRatio`, `WpfAppFixture`/`WpfCollection.Name` are defined once and used consistently. ✓

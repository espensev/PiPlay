# Per-profile Accent Color-Wheel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable full color-wheel accent picker, make it the global accent picker (closing TG-4), and let each profile carry an accent override that re-themes the app live when the profile is active (remembered across restart).

**Architecture:** A pure foundation (`ThemeCatalog.IsValidHex` + `ColorMath` HSV↔RGB + `AccentReadabilityPolicy` gating any hex against the WCAG accent contract across all three presets) underpins a reusable `AccentColorPicker` UserControl. The picker replaces the six Settings swatches (live preview via a new public `ApplyAccentOnly`, revert on dismiss) and is embedded in the profile editor; profiles gain a nullable `AccentColor` resolved as `ResolvedAccentColor = activeProfile?.AccentColor ?? globalAccent`, with the active profile persisted (`AppSettings.ActiveProfileName`).

**Tech Stack:** .NET 10 WPF, System.Text.Json settings, xUnit (STA WPF runtime tests + XML-parse markup tests), existing `ThemeColors`/`ThemeResourceApplier`/`ThemeCatalog`/`ThemePreferenceResolver`.

**Spec:** `docs/superpowers/specs/2026-06-20-profile-accent-color-wheel-design.md` — read it first; §4 is the contrast contract every task serves, §5.3 the accent value model, §2 the seven decisions.

## Global Constraints

- **Accent format:** opaque `#RRGGBB`; validate via the new public `ThemeCatalog.IsValidHex`. No alpha.
- **`DeriveAccentSet` takes a STRING:** its signature is `DeriveAccentSet(string? baseAccent, ThemePreset preset)` (`ThemeColors.cs:108`); pass the hex string, never a `Color` (proven call: `ThemeCatalogTests.cs:358-359`).
- **`ParseColor`/`NormalizeAccentColor` never throw** — they fall back to `#00D4FF`. Use `IsValidHex` to detect malformed input; do NOT wrap `ParseColor` in try/catch for validation.
- **Contrast contract (spec §4):** readable iff for ALL `{sharp-dark, minimal, soft-glass}`: OnAccent ≥ 4.5 on Primary & Hover; OnAccentPressed ≥ 4.5 on Pressed; Border ≥ 3.0 on SurfaceBase & SurfaceRaised; Primary ≥ 3.0 on SurfaceHover. Pipeline is fail-closed (`PickReadableForeground` throws) — never hand it an unreadable accent, **including on the live-preview path**.
- **Live preview is readability-gated:** the picker raises `PreviewColorChanged` only for readable values (holds last readable otherwise); commit on Done, revert to `ResolvedAccentColor` on dismiss-without-apply (mirror `OpacityPreviewChanged` / `MainWindow.xaml.cs:498-504`).
- **Apply vs dismiss must be explicit:** the current checkout has `CloseButton` sharing the apply path with the footer `DoneButton`. For this feature, a live accent preview must have a non-applying dismissal path: either split `Done` = apply and title-bar close/Esc = cancel, or add an explicit Cancel and update the copy/tests. Do not leave all visible close paths committing a previewed accent.
- **Do NOT overload `MainWindow.EffectiveAccentColor`** (global-only, 5 call sites). Add `ResolvedAccentColor` for the visible accent; re-point visible consumers (pin `:286`, player `:614`, startup `:747`, Settings seed `:484`, test seam `:623`).
- **Replace-not-mutate:** restyle by replacing App-level frozen brush + companion `*Color` tokens; open windows re-resolve via `{DynamicResource …}`.
- **TDD:** every production change has a test that failed first.
- **Tests stay green:** full suite green after every task — **re-measure the current green count at branch time** (do not hardcode; was 617 on main, +Done-button work if present). Never weaken an existing gate to pass.
- **No magic literals in tests:** derive expected contrast/thresholds from the catalog/policy.
- **Commits:** one per task, conventional-commit; never `--no-verify`.

---

## Phase 0 — Pure foundation (no UI)

### Task 1: Public hex validator + `ColorMath` (RGB↔HSV)

**Files:**
- Modify: `src/PiPlay/Theme/ThemeCatalog.cs` (expose `IsValidHex`; near the private `NormalizeHex6` ~:376)
- Create: `src/PiPlay/Theme/ColorMath.cs`
- Test: `tests/PiPlay.Tests/Theme/ColorMathTests.cs`, plus an `IsValidHex` case in `tests/PiPlay.Tests/ThemeCatalogTests.cs`

**Interfaces:**
- Produces: `static bool ThemeCatalog.IsValidHex(string? hex)` (true for a well-formed `#RRGGBB`, case-insensitive; false for null/blank/malformed/wrong-length); `static (double H,double S,double V) ColorMath.RgbToHsv(Color)`; `static Color ColorMath.HsvToRgb(double h,double s,double v)`.

- [ ] **Step 1: Write the failing `IsValidHex` test** (in `ThemeCatalogTests.cs`)

```csharp
[Theory]
[InlineData("#00D4FF", true)]
[InlineData("#abcdef", true)]
[InlineData("not-a-color", false)]
[InlineData("#12345", false)]
[InlineData("#1234567", false)]
[InlineData("00D4FF", true)]   // canonical NormalizeHex6 accepts a 6-hex string with the '#' optional
[InlineData(null, false)]
[InlineData("", false)]
public void IsValidHex_matches_the_canonical_normalizer(string? hex, bool expected)
    => Assert.Equal(expected, ThemeCatalog.IsValidHex(hex));
```

- [ ] **Step 2: Write the failing `ColorMath` tests**

```csharp
using System.Windows.Media;
using PiPlay.Theme;
namespace PiPlay.Tests;

public class ColorMathTests
{
    [Theory]
    [InlineData(0xFF,0x00,0x00,0.0)] [InlineData(0x00,0xFF,0x00,120.0)] [InlineData(0x00,0x00,0xFF,240.0)]
    public void RgbToHsv_maps_primary_hues(byte r, byte g, byte b, double hue)
    {
        var (h,s,v) = ColorMath.RgbToHsv(Color.FromRgb(r,g,b));
        Assert.Equal(hue,h,3); Assert.Equal(1.0,s,3); Assert.Equal(1.0,v,3);
    }

    [Fact]
    public void Gray_has_zero_saturation_and_hue_zero()
    {
        var (h,s,v) = ColorMath.RgbToHsv(Color.FromRgb(0x80,0x80,0x80));
        Assert.Equal(0.0,s,3); Assert.Equal(0x80/255.0,v,3); Assert.Equal(0.0,h,3);
    }

    [Theory]
    [InlineData(0xFF,0x50,0xC8)] [InlineData(0x00,0xD4,0xFF)] [InlineData(0x12,0x34,0x56)]
    public void HsvToRgb_inverts_RgbToHsv(byte r, byte g, byte b)
    {
        var o = Color.FromRgb(r,g,b);
        var (h,s,v) = ColorMath.RgbToHsv(o);
        var round = ColorMath.HsvToRgb(h,s,v);
        Assert.Equal(o.R,round.R); Assert.Equal(o.G,round.G); Assert.Equal(o.B,round.B);
    }

    [Fact]
    public void HsvToRgb_wraps_hue_and_clamps_sv()
    {
        Assert.Equal(ColorMath.HsvToRgb(0,1,1), ColorMath.HsvToRgb(360,1,1));
        Assert.Equal(Colors.White, ColorMath.HsvToRgb(0,-1,2));
    }
}
```

- [ ] **Step 3: Run, verify fail** — `--filter "FullyQualifiedName~ColorMathTests|FullyQualifiedName~IsValidHex"`. Expected FAIL (members missing).

- [ ] **Step 4: Implement `IsValidHex`** — make it the public face of the canonical private `NormalizeHex6` (ThemeCatalog.cs:376), which trims, allows the leading `#` to be optional, is case-insensitive, and requires exactly 6 hex digits. Delegating keeps ONE definition of "valid accent hex" (DRY) and matches `NormalizeAccentColor`'s acceptance:

```csharp
// In ThemeCatalog (public): true iff NormalizeHex6 can parse it (6 hex digits, '#' optional, trimmed).
public static bool IsValidHex(string? hex) => NormalizeHex6(hex) is not null;
```

- [ ] **Step 5: Implement `ColorMath`** (see spec §5.1 ranges)

```csharp
using System;
using System.Windows.Media;
namespace PiPlay.Theme;

/// <summary>RGB↔HSV for the accent wheel. H in [0,360) (0 when achromatic), S/V in [0,1].</summary>
public static class ColorMath
{
    public static (double H, double S, double V) RgbToHsv(Color c)
    {
        double r=c.R/255.0, g=c.G/255.0, b=c.B/255.0;
        double max=Math.Max(r,Math.Max(g,b)), min=Math.Min(r,Math.Min(g,b)), d=max-min;
        double h=0;
        if (d>1e-9)
        {
            if (max==r) h=60*(((g-b)/d)%6);
            else if (max==g) h=60*(((b-r)/d)+2);
            else h=60*(((r-g)/d)+4);
        }
        if (h<0) h+=360;
        double s = max<=1e-9 ? 0 : d/max;
        return (h, s, max);
    }

    public static Color HsvToRgb(double h, double s, double v)
    {
        h=((h%360)+360)%360; s=Math.Clamp(s,0,1); v=Math.Clamp(v,0,1);
        double c=v*s, x=c*(1-Math.Abs((h/60%2)-1)), m=v-c;
        (double r,double g,double b) = h switch
        {
            <60 => (c,x,0.0), <120 => (x,c,0.0), <180 => (0.0,c,x),
            <240 => (0.0,x,c), <300 => (x,0.0,c), _ => (c,0.0,x),
        };
        return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));
    }
}
```

- [ ] **Step 6: Run, verify pass. Step 7: Commit**

```bash
git add src/PiPlay/Theme/ColorMath.cs src/PiPlay/Theme/ThemeCatalog.cs tests/PiPlay.Tests/Theme/ColorMathTests.cs tests/PiPlay.Tests/ThemeCatalogTests.cs
git commit -m "feat(theme): public hex validator + RGB<->HSV color math for the accent wheel"
```

---

### Task 2: `AccentReadabilityPolicy` — gate + nearest-readable (closes TG-4 core)

**Files:**
- Create: `src/PiPlay/Theme/AccentReadabilityPolicy.cs`
- Test: `tests/PiPlay.Tests/Theme/AccentReadabilityPolicyTests.cs`

**Interfaces:**
- Consumes: `ThemeCatalog.IsValidHex`, `ThemeColors.DeriveAccentSet(string, ThemePreset)`, `ThemeColors.ContrastRatio`, `ThemeColors.ParseColor` (for surface palette colors only), `ThemeCatalog.Presets/AccentOptions/DefaultAccentColor`, `ColorMath`.
- Produces: `enum AccentGate { None, OnAccent, OnAccentPressed, Border, GlyphOnHover, Invalid }`; `record AccentReadability(bool IsReadable, AccentGate FailingGate)`; `static AccentReadability Evaluate(string? hex)`; `static string NearestReadable(string? hex)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using PiPlay.Theme;
namespace PiPlay.Tests;

public class AccentReadabilityPolicyTests
{
    [Fact]
    public void Every_curated_accent_is_readable()
    {
        foreach (var o in ThemeCatalog.AccentOptions)
            Assert.True(AccentReadabilityPolicy.Evaluate(o.HexColor).IsReadable, $"{o.Key} ({o.HexColor})");
    }

    [Fact]
    public void Malformed_hex_is_unreadable()
    {
        var r = AccentReadabilityPolicy.Evaluate("not-a-color");
        Assert.False(r.IsReadable);
        Assert.Equal(AccentGate.Invalid, r.FailingGate);
    }

    [Fact]
    public void A_dim_gray_is_unreadable()
        => Assert.False(AccentReadabilityPolicy.Evaluate("#404040").IsReadable);

    [Theory]
    [InlineData("#404040")] [InlineData("#1A1A1A")] [InlineData("#202060")] [InlineData("not-a-color")]
    public void NearestReadable_always_returns_a_readable_color(string input)
        => Assert.True(AccentReadabilityPolicy.Evaluate(AccentReadabilityPolicy.NearestReadable(input)).IsReadable);

    [Fact]
    public void NearestReadable_is_identity_for_a_readable_color()
        => Assert.Equal("#00D4FF", AccentReadabilityPolicy.NearestReadable("#00D4FF"));

    [Fact]
    public void NearestReadable_closes_a_dense_hue_sweep()   // TG-4
    {
        for (int hue = 0; hue < 360; hue += 5)
        {
            var raw = $"#{ColorMath.HsvToRgb(hue,1,1).R:X2}{ColorMath.HsvToRgb(hue,1,1).G:X2}{ColorMath.HsvToRgb(hue,1,1).B:X2}";
            Assert.True(AccentReadabilityPolicy.Evaluate(AccentReadabilityPolicy.NearestReadable(raw)).IsReadable, $"hue {hue}");
        }
    }
}
```

- [ ] **Step 2: Run, verify fail.** Expected FAIL — type missing.

- [ ] **Step 3: Implement** (note: hex string into `DeriveAccentSet`; `IsValidHex` first; no dead try/catch)

```csharp
using System;
using System.Windows.Media;
namespace PiPlay.Theme;

public enum AccentGate { None, OnAccent, OnAccentPressed, Border, GlyphOnHover, Invalid }
public record AccentReadability(bool IsReadable, AccentGate FailingGate);

/// <summary>Single source of truth for "is this hex a WCAG-safe accent" across ALL presets (spec §4).
/// Closes TG-4: generalizes the curated-palette gates to arbitrary input.</summary>
public static class AccentReadabilityPolicy
{
    private const double TextMin = 4.5, UiMin = 3.0;

    public static AccentReadability Evaluate(string? hex)
    {
        if (!ThemeCatalog.IsValidHex(hex)) return new AccentReadability(false, AccentGate.Invalid);
        foreach (var preset in ThemeCatalog.Presets)
        {
            var gate = FirstFailingGate(hex!, preset);
            if (gate != AccentGate.None) return new AccentReadability(false, gate);
        }
        return new AccentReadability(true, AccentGate.None);
    }

    private static AccentGate FirstFailingGate(string hex, ThemePreset preset)
    {
        DerivedAccentSet d;
        try { d = ThemeColors.DeriveAccentSet(hex, preset); }   // takes the STRING; may throw via PickReadableForeground
        catch { return AccentGate.OnAccent; }

        Color sBase   = ThemeColors.ParseColor(preset.Palette.SurfaceBase);
        Color sRaised = ThemeColors.ParseColor(preset.Palette.SurfaceRaised);
        Color sHover  = ThemeColors.ParseColor(preset.Palette.SurfaceHover);

        if (ThemeColors.ContrastRatio(d.OnAccent, d.Primary) < TextMin) return AccentGate.OnAccent;
        if (ThemeColors.ContrastRatio(d.OnAccent, d.Hover) < TextMin) return AccentGate.OnAccent;
        if (ThemeColors.ContrastRatio(d.OnAccentPressed, d.Pressed) < TextMin) return AccentGate.OnAccentPressed;
        if (ThemeColors.ContrastRatio(d.Border, sBase) < UiMin) return AccentGate.Border;
        if (ThemeColors.ContrastRatio(d.Border, sRaised) < UiMin) return AccentGate.Border;
        if (ThemeColors.ContrastRatio(d.Primary, sHover) < UiMin) return AccentGate.GlyphOnHover;
        return AccentGate.None;
    }

    public static string NearestReadable(string? hex)
    {
        if (!ThemeCatalog.IsValidHex(hex)) return ThemeCatalog.DefaultAccentColor;
        if (Evaluate(hex).IsReadable) return Normalize(ThemeColors.ParseColor(hex!));

        var (h, s, v) = ColorMath.RgbToHsv(ThemeColors.ParseColor(hex!));
        for (double nv = v; nv <= 1.0 + 1e-9; nv += 0.04)
        {
            var c = Normalize(ColorMath.HsvToRgb(h, s, Math.Min(nv, 1.0)));
            if (Evaluate(c).IsReadable) return c;
        }
        for (double ns = s; ns >= -1e-9; ns -= 0.04)
        {
            var c = Normalize(ColorMath.HsvToRgb(h, Math.Max(ns, 0.0), 1.0));
            if (Evaluate(c).IsReadable) return c;
        }
        return NearestPresetByHue(h);
    }

    private static string NearestPresetByHue(double hue)
    {
        string best = ThemeCatalog.DefaultAccentColor; double bestDelta = double.MaxValue;
        foreach (var o in ThemeCatalog.AccentOptions)
        {
            var (ph, _, _) = ColorMath.RgbToHsv(ThemeColors.ParseColor(o.HexColor));
            double delta = Math.Abs(((ph - hue + 540) % 360) - 180);
            if (delta < bestDelta) { bestDelta = delta; best = o.HexColor; }
        }
        return best;
    }

    private static string Normalize(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
```

> CONFIRM during implementation against `src/PiPlay/Theme/ThemeColors.cs`/`ThemeCatalog.cs`: `DerivedAccentSet` member names (`OnAccent/Primary/Hover/Pressed/OnAccentPressed/Border`) and `ThemePalette.SurfaceBase/SurfaceRaised/SurfaceHover` — verified present at review time; the gates/thresholds are fixed by spec §4.

- [ ] **Step 4: Run, verify pass. Step 5: Commit**

```bash
git add src/PiPlay/Theme/AccentReadabilityPolicy.cs tests/PiPlay.Tests/Theme/AccentReadabilityPolicyTests.cs
git commit -m "feat(theme): accent readability policy + nearest-readable (closes TG-4 core)"
```

---

## Phase 1 — `AccentColorPicker` UserControl

### Task 3: Picker control — HSV state, readability-gated preview, DPI-correct disc

**Files:**
- Create: `src/PiPlay/Controls/AccentColorPicker.xaml(.cs)`
- Test: `tests/PiPlay.Tests/Ui/AccentColorPickerTests.cs` (STA runtime), `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` (markup name)
- Verify: `XamlTestFiles` loader resolves `Controls/AccentColorPicker.xaml` (add the subfolder if it filters by directory); WPF SDK auto-globs the control into the build.

**Interfaces:** (spec §5.2)
- `SelectedColor` (`string`, two-way DP, normalized); `event Action<string>? PreviewColorChanged` (readable values only); `bool IsSelectedReadable` (read-only); `void UseNearestReadable()`.
- Named parts: `HueSatDisc`, `ValueSlider`, `RInput`, `GInput`, `BInput`, `HexInput`, `PresetRow`, `PreviewSwatch`, `ReadabilityWarning`, `UseNearestReadableButton`.

- [ ] **Step 1: Write the failing runtime tests**

```csharp
using System.Windows.Controls;
using PiPlay.Controls; using PiPlay.Theme;
namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Wpf)]
public class AccentColorPickerTests
{
    [Fact] public void Seeds_and_reads_selected_color() => StaTestThread.Invoke(() =>
    {
        var p = new AccentColorPicker { SelectedColor = "#38D996" };
        Assert.Equal("#38D996", p.SelectedColor);
        Assert.True(p.IsSelectedReadable);
    });

    [Fact] public void Flags_unreadable_and_fixes_it() => StaTestThread.Invoke(() =>
    {
        var p = new AccentColorPicker { SelectedColor = "#404040" };
        Assert.False(p.IsSelectedReadable);
        p.UseNearestReadable();
        Assert.True(p.IsSelectedReadable);
        Assert.NotEqual("#404040", p.SelectedColor);
    });

    [Fact] public void Raises_preview_for_readable_only() => StaTestThread.Invoke(() =>
    {
        var p = new AccentColorPicker { SelectedColor = "#00D4FF" };
        string? last = null; p.PreviewColorChanged += h => last = h;
        p.SelectedColor = "#A78BFA"; Assert.Equal("#A78BFA", last);   // readable -> fires
        p.SelectedColor = "#404040"; Assert.Equal("#A78BFA", last);   // unreadable -> holds last readable
    });

    [Fact] public void Presets_match_the_catalog() => StaTestThread.Invoke(() =>
    {
        var p = new AccentColorPicker();
        var presets = (System.Collections.IEnumerable)((ItemsControl)p.FindName("PresetRow")!).ItemsSource;
        Assert.Equal(ThemeCatalog.AccentOptions, presets);   // catalog-drift guard (runtime; markup can't see code-set ItemsSource)
    });
}
```

- [ ] **Step 2: Add a markup name invariant** (in `XamlInvariantTests.cs`): assert `Controls/AccentColorPicker.xaml` contains a named `PresetRow` and `HueSatDisc` element (existence only — the catalog match is the runtime test above, not a vacuous name check).

- [ ] **Step 3: Run, verify fail** (`~AccentColorPicker`). Expected FAIL — control missing.

- [ ] **Step 4: Implement the control** (spec §5.2 — authoritative `(H,S,V)`; DPI-physical disc bitmap; preview gating)
  - XAML: `UserControl` with `Image x:Name="HueSatDisc"` + overlay `Canvas` thumb, `Slider x:Name="ValueSlider"` (0–1, black→hue track), `TextBox` `RInput/GInput/BInput/HexInput`, `ItemsControl x:Name="PresetRow"`, `Border x:Name="PreviewSwatch"`, `TextBlock x:Name="ReadabilityWarning"` (collapsed when readable), `Button x:Name="UseNearestReadableButton"`. Theme tokens; tooltips + `AutomationProperties.Name` on each.
  - Code-behind: keep authoritative `_h/_s/_v` doubles. `PresetRow.ItemsSource = ThemeCatalog.AccentOptions`. A `_origin` guard distinguishes disc/slider drags (update H/S/V → derive RGB/hex/`SelectedColor`, do NOT recompute HSV) from R/G/B/hex text edits (recompute H/S/V from typed RGB). `SelectedColor` DP callback: refresh inputs/thumb/slider/preview, set `IsSelectedReadable = AccentReadabilityPolicy.Evaluate(value).IsReadable`, toggle warning/button, and **raise `PreviewColorChanged(value)` only when readable**. `UseNearestReadable()` + the button → `SelectedColor = AccentReadabilityPolicy.NearestReadable(SelectedColor)`. Preset click → that hex.
  - Disc bitmap: render once into a frozen `WriteableBitmap` at `logicalSize * VisualTreeHelper.GetDpi(this).DpiScaleX` (bitmap DPI `96*scale`); cache keyed `(size, dpiScale)`; regenerate on `OnDpiChanged`; map pointer through the same scale.

- [ ] **Step 5: Run, verify pass.** Expected PASS (4 runtime + markup).

- [ ] **Step 6: Commit** (`feat(controls): add reusable AccentColorPicker (HSV wheel + value + rgb/hex + presets)`).

---

## Phase 2 — Global accent uses the picker (closes TG-4 / Task 9)

### Task 4: Accent value model, `ApplyAccentOnly`, live preview + revert, swap Settings swatches

**Files:**
- Modify: `src/PiPlay/Theme/ThemeResourceApplier.cs` (add public `ApplyAccentOnly`), `src/PiPlay/SettingsWindow.xaml` (+`.xaml.cs`), `src/PiPlay/MainWindow.xaml.cs` (add `ResolvedAccentColor`, `LivePreviewAccent`, `CommitAccent`; re-point visible consumers; subscribe preview + revert).
- Modify tests: `XamlInvariantTests.cs` (RequiredNames + a11y/tooltip sweep + relocate catalog match), `WpfRuntimeTests.cs` (re-point FOUR chip tests + new live-preview/revert/Done-gating tests + ThemeHintText copy test).

**Interfaces:**
- Produces: `static void ThemeResourceApplier.ApplyAccentOnly(ResourceDictionary, string accentHex, ThemePreset)`; `string MainWindow.ResolvedAccentColor`; `void MainWindow.LivePreviewAccent(string hex)`; `void MainWindow.CommitAccent(string hex)`; `event Action<string>? SettingsWindow.AccentPreviewChanged`.

- [ ] **Step 1: Write failing tests**
  - `ApplyAccentOnly` replaces `AccentPrimary` (+ companion `AccentPrimaryColor`) on a dict without touching a palette token (e.g. `AppBackground` unchanged) — pure-ish runtime test mirroring `ThemeResourceApplier` tests.
  - `SettingsWindow_previews_accent_live_and_gates_done`:
```csharp
[Fact] public void SettingsWindow_previews_accent_live_and_gates_done() => StaTestThread.Invoke(() =>
{
    var w = new SettingsWindow(isBrowserReady: true);
    string? prev = null; w.AccentPreviewChanged += h => prev = h;
    var p = (PiPlay.Controls.AccentColorPicker)w.FindName("AccentPicker")!;
    p.SelectedColor = "#A78BFA";
    Assert.Equal("#A78BFA", prev); Assert.True(w.AppearanceChanged);
    p.SelectedColor = "#404040";   // unreadable
    Assert.False(((Button)w.FindName("DoneButton")!).IsEnabled);
});
```
  - `MainWindow` revert: drive `LivePreviewAccent("#A78BFA")` then a revert call; assert `Application.Current.Resources["AccentPrimary"]` is back to the `ResolvedAccentColor` brush. Drag-through-deadzone: `LivePreviewAccent` is only called with readable hex (gated upstream), but add a guard test that `LivePreviewAccent` no-ops/uses-NearestReadable if handed an unreadable hex (defense in depth) and never throws.
  - Apply/dismiss contract: after a readable preview, **Done** closes with `DialogResult == true` and commits; title-bar close/Esc or the chosen Cancel path closes without true and `MainWindow` reverts App resources, pin brush, and popout appearance to `ResolvedAccentColor`. This must fail against the current close-applies path before implementation.
  - Construction guard: seeding `AccentPicker.SelectedColor` from the ctor must not raise `AccentPreviewChanged`, set `AppearanceChanged`, or dirty the app resources. Subscribe after seeding or use an explicit `_seedingAccentPicker` guard, and pin it with a runtime test.
  - **ThemeHintText copy test** (NEW — none exists): assert `ThemeHintText.Text` reflects live accent preview and does not contain "chips".

- [ ] **Step 2: Re-point the FOUR existing chip tests** in `WpfRuntimeTests.cs` — replace each `((ToggleButton)w.FindName("AccentChip…")!).RaiseEvent(Click)` with `((PiPlay.Controls.AccentColorPicker)w.FindName("AccentPicker")!).SelectedColor = "<hex>"`, preserving every assertion:
  - `SettingsWindow_reflects_and_updates_theme_and_accent_input` (Violet → `#A78BFA`).
  - `SettingsWindow_accent_only_change_keeps_behavior_on_preset_defaults` (Amber → `#FFC857`).
  - `SettingsWindow_preset_click_preserves_a_custom_accent` (Amber/Violet/Cyan → their hexes; keep the theme-switch-survives assertions).
  - `SettingsWindow_done_button_dismisses_the_dialog` (Amber → `#FFC857`).

- [ ] **Step 3: Update markup tests** — `Required_named_controls_exist` (SettingsWindow): remove the six `AccentChip*`, add `AccentPicker`; `Settings_appearance_controls_have_tooltips_and_accessible_names`: chips → `AccentPicker`; in `Settings_theme_and_accent_controls_match_the_catalog` remove the accent-Tags assertion (now covered by the Task 3 runtime `Presets_match_the_catalog`), keep the preset + corner assertions.

- [ ] **Step 4: Run, verify fail.** Expected FAIL (new tests + edited theories).

- [ ] **Step 5: Implement**
  - `ThemeResourceApplier.ApplyAccentOnly`: extract the body of the private `ApplyAccent` into a public static taking `(resources, accentHex, preset)`; have the existing `Apply` call it.
  - `MainWindow`: add `ResolvedAccentColor => ActiveProfileAccent ?? EffectiveAccentColor` (ActiveProfileAccent comes in Task 7; until then it is null, so `ResolvedAccentColor == EffectiveAccentColor`). Re-point pin (`:286`), player (`:614`), startup (`:747`), Settings seed (`:484`), test seam (`:623`) to `ResolvedAccentColor`. Add `LivePreviewAccent(hex)`: `ApplyAccentOnly(Application.Current.Resources, hex, currentPreset)` + set pin brush `Brush(hex)` + `_player?.ApplyAppearance(hex, …)` — refactor `ApplySourceAppearance`/`ApplyOpenPlayerAppearance` to take an accent arg shared by preview & commit. Add `CommitAccent(hex)` (Task 7 routes to profile vs global; for now global). Subscribe `dialog.AccentPreviewChanged += LivePreviewAccent`; on `ShowDialog() != true` revert all surfaces to `ResolvedAccentColor`; on apply call `CommitAccent`.
  - `SettingsWindow.xaml`: swap the swatch StackPanel for `<controls:AccentColorPicker x:Name="AccentPicker"/>` + a context-note `TextBlock`; `xmlns:controls`. Update `ThemeHintText` copy.
  - `SettingsWindow.xaml.cs`: seed picker without firing preview/dirty state; then subscribe `PreviewColorChanged` → set `AccentColor`/`AppearanceChanged`/raise `AccentPreviewChanged`/`DoneButton.IsEnabled = picker.IsSelectedReadable`; preset/theme clicks push into the picker. Split the apply/dismiss paths so Done applies and the chosen dismiss path returns without true for `MainWindow` revert.

- [ ] **Step 6: Run the full suite, verify green.**
- [ ] **Step 7: Dev smoke** (`run-piplay` → Settings → drag wheel → accent recolors live incl. pin/popout; drag through a mid-gray → no error dialog; "Use nearest readable" works). Verified-renders only.
- [ ] **Step 8: Commit** (`feat(settings): color-wheel accent picker with live preview (closes TG-4)`).

---

## Phase 3 — Per-profile accent + active-profile persistence

### Task 5: `Profile.AccentColor` + `AppSettings.ActiveProfileName` + persistence/sanitize

**Files:** `src/PiPlay/Models/Profile.cs`, `src/PiPlay/Models/AppSettings.cs` (schema 3→4 + `ActiveProfileName`), `src/PiPlay/Services/SettingsService.cs:161-202`; test `tests/PiPlay.Tests/SettingsServiceTests.cs`.

**Interfaces:** `Profile.AccentColor` (`string?`, null=global); `AppSettings.ActiveProfileName` (`string?`). Sanitize: profile color invalid→null, unreadable→`NearestReadable`; `ActiveProfileName` cleared if no matching profile.

- [ ] **Step 1: failing tests** — accent roundtrip; sanitize drops `"not-a-color"`→null and clamps `"#404040"` to readable; `ActiveProfileName` roundtrip; sanitize clears a dangling `ActiveProfileName` ("Ghost" with no such profile → null). Reuse the file's temp-path + `ValidYouTubeUrl` helpers.
- [ ] **Step 2: run/fail. Step 3: implement** (`Profile.AccentColor`; `AppSettings.ActiveProfileName`; bump `CurrentSchemaVersion=4`; in `Sanitize`: `p.AccentColor = NormalizeProfileAccent(p.AccentColor)` where null→null, `!IsValidHex`→null, readable→as-is, else `NearestReadable`; clear `ActiveProfileName` if `Profiles` has no match). **Step 4: run/pass. Step 5: commit** (`feat(profile): per-profile accent + persisted active profile`).

---

### Task 6: `ProfileService.ValidateAccent` + edit-dialog picker (thread the accent through)

**Files:** `src/PiPlay/Services/ProfileService.cs`, `src/PiPlay/Prompt.cs:166-238`, `src/PiPlay/MainWindow.xaml.cs:423-435` (the edit handler); tests `ProfileServiceTests.cs`, `WpfRuntimeTests.cs`.

**Interfaces:** `static bool ProfileService.ValidateAccent(string? hex)` (null OK; else `IsValidHex` && readable). `Prompt.EditProfile` returns `(string Name, string Url, string? Mode, string? AccentColor)?`.

- [ ] **Step 1: failing tests** — `ValidateAccent(null)`=true, `("#404040")`=false, `("not-a-color")`=false, `("#00D4FF")`=true; a runtime test that the EditProfile dialog hosts an `AccentColorPicker` (build via the same path as `Prompt_dialogs_are_borderless_dark`).
- [ ] **Step 2: run/fail. Step 3: implement** — add `ValidateAccent`; add the picker to the `EditProfile` shell + extend the tuple; **in `EditProfileButton_Click` (`:427-435`) set `AccentColor = NormalizeProfileAccent(edited.Value.AccentColor)` on the reconstructed Profile** (the compiler flags the tuple arity but NOT this dropped assignment — do not miss it); `MainWindow` passes a preview callback (→ `LivePreviewAccent`) and reverts to `ResolvedAccentColor` on cancel. Update every other `EditProfile(` call site for the new arity (grep first). **Step 4: run/pass. Step 5: commit** (`feat(profile): accent picker in the profile editor`).

---

### Task 7: `ProfilesCombo` swatch + activation + persistence/restore + context-sensitive Settings target

**Files:** `src/PiPlay/MainWindow.xaml:91` (combo `ItemTemplate`), `src/PiPlay/MainWindow.xaml.cs:373,382-388` (load/select/restore + `ActiveProfileAccent`/`CommitAccent` routing), `src/PiPlay/SettingsWindow.xaml.cs` (context note + commit-target awareness); tests `WpfRuntimeTests.cs` + resolver unit test.

**Interfaces:** `MainWindow.ResolvedAccentColor` now resolves `ActiveProfileAccent ?? EffectiveAccentColor`; `CommitAccent(hex)` routes to `profile.AccentColor` when an active profile overrides, else global; startup restores `ActiveProfileName`. Test seams: `SelectProfileForTests(name)` (must NOT trigger `NavigateInternal`), `ResolvedAccentColorForTests`.

- [ ] **Step 1: failing tests**
```csharp
[Fact] public void Active_profile_color_resolves_else_falls_back_to_global() => StaTestThread.Invoke(() =>
{
    var w = new MainWindow();
    w.ReplaceSettingsForTests(new AppSettings {
        Theme = new ThemeSettings { AccentColor = "#00D4FF" },
        Profiles = { new Profile { Name="Violet", Url=Url, AccentColor="#A78BFA" },
                     new Profile { Name="Plain",  Url=Url, AccentColor=null } } });
    w.SelectProfileForTests("Violet"); Assert.Equal("#A78BFA", w.ResolvedAccentColorForTests);
    w.SelectProfileForTests("Plain");  Assert.Equal("#00D4FF", w.ResolvedAccentColorForTests);
});
```
  - Activation apply/revert isolated from navigation: after `SelectProfileForTests("Violet")` the App `AccentPrimary` brush equals the violet-derived brush; after `SelectProfileForTests("Plain")` it reverts to the global cyan — and `SelectProfileForTests` does not navigate the browser (no live `CoreWebView2` required, like `Reset_clears_dirty_ui_without_a_live_browser`).
  - Persistence: selecting a profile sets `ActiveProfileName`; a `MainWindow` constructed over settings with `ActiveProfileName="Violet"` resolves to the violet accent at startup (seam asserting the restore path without navigation).
  - Commit routing (D7): with an active overriding profile, `CommitAccent("#38D996")` writes the profile's `AccentColor` (not global); with no override, it writes global.
  - Active-profile lifecycle: deleting the active profile clears `ActiveProfileName`, applies the global accent immediately, and saves; renaming the active profile moves `ActiveProfileName` to the new name; overwriting an active profile as colorless re-resolves/applies global. These tests should exercise the real `LoadProfilesIntoCombo`/selection-suppression shape so the cleanup does not rely on `Sanitize` at next restart.
- [ ] **Step 2: run/fail. Step 3: implement** — combo `ItemTemplate` (swatch `Border` from `AccentColor`, neutral dot when null, + Name); `ProfilesCombo_SelectionChanged` records `ActiveProfileName` + applies `ResolvedAccentColor` (commit-apply) + reverts on colorless/none; isolate accent-apply from `NavigateInternal` behind the test seam; startup restore of `ActiveProfileName`; explicit active-profile rename/delete/overwrite cleanup; `CommitAccent` routing (D7) + the Settings context note ("Editing accent for profile 'X'" vs "Editing the app accent"). **Step 4: run/pass.**
- [ ] **Step 5: full suite green; dev smoke** (`run-piplay`: save two profiles with different colors via the editor, switch between them → app accent re-themes live incl. pin/popout, reverts on the colorless one; expand the combo → swatches; restart → last profile's accent restored). **Step 6: commit** (`feat(profile): re-theme the app accent from the active profile`).

---

## Final verification (after all tasks)

- [ ] Full suite green: `dotnet test tests/PiPlay.Tests/PiPlay.Tests.csproj`.
- [ ] TG-4 closed: hue-sweep + readability gates pass; mark TG-4 / Task 9 done in `docs/reviews/2026-06-14-theme-v2-spec-eval.md` + the Theme-V2 plan.
- [ ] Docs: `docs/CHANGELOG.md`; refresh `docs/Theme_Preset_Differences.md` if accent copy changed.
- [ ] Version: **minor** bump at publish (per `CLAUDE.md`); manual/visual QA on the deployed Stable copy is owner-gated.

## Backlog / deferred follow-ups

- [ ] **TEST-BACKLOG-01 — Behavior-test the native stderr wrapper.**
  Add a PowerShell-level test or script smoke for `scripts/NativeCommand.ps1` proving that a native command which writes stderr under `$ErrorActionPreference = "Stop"` does not abort when it exits `0`, and that a non-zero native exit code still reaches the caller as the failure signal. Current coverage is policy/static plus real script smoke; this is the remaining stronger test suggested by the efficiency review.

- [ ] **PERF-BACKLOG-01 — Watch accent drag preview for stutter before adding throttling.**
  Current manual and automated checks do not show a drag-performance issue, and the wheel bitmap render is not per-tick. If visual QA later shows stutter while dragging readable colors, coalesce/throttle `AccentColorPicker.PreviewColorChanged` / `MainWindow.LivePreviewAccent` so per-tick `ThemeResourceApplier.ApplyAccentOnly(...)` brush replacement is capped to a frame-rate-safe cadence.

## Spec coverage check

- D1 → Tasks 5-7 · D2 → Task 3 · D3 → Task 4 · D4 → Tasks 2-4 (block + nearest) · D5 → Tasks 3-4,6-7 (preview gated + revert) · D6 → Tasks 5,7 (persist active profile) · D7 → Task 7 + Task 4 seed/commit routing.
- Contrast contract (spec §4) implemented once in Task 2, reused everywhere. Fail-closed never hit (preview gated). `EffectiveAccentColor` not overloaded (new `ResolvedAccentColor`). Four chip tests re-pointed; catalog-drift moved to runtime; revert + ThemeHintText covered; edited accent threaded into the reconstructed Profile; DPI-correct disc; persisted+sanitized active profile.

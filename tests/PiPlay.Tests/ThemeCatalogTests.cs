using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ThemeCatalogTests
{
    [Fact]
    public void Presets_have_unique_stable_ids_and_required_first_pass_options()
    {
        var ids = ThemeCatalog.Presets.Select(p => p.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("sharp-dark", ids);
        Assert.Contains("minimal", ids);
        Assert.Contains("soft-glass", ids);
        Assert.All(ids, id => Assert.Equal(id, ThemeCatalog.NormalizeThemeId(id.ToUpperInvariant())));
    }

    [Fact]
    public void Accent_options_store_normalized_hex_colors()
    {
        var colors = ThemeCatalog.AccentOptions.Select(o => o.HexColor).ToArray();

        Assert.Equal(colors.Length, colors.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(colors, color =>
        {
            Assert.Equal(color, ThemeCatalog.NormalizeAccentColor(color));
            Assert.StartsWith("#", color);
            Assert.Equal(7, color.Length);
        });
    }

    [Theory]
    [InlineData("#a78bfa", "#A78BFA")]
    [InlineData("38d996", "#38D996")]
    [InlineData("not-a-color", "#00D4FF")]
    [InlineData("#12345", "#00D4FF")]
    public void Accent_hex_is_normalized_or_reset_to_default(string input, string expected)
    {
        Assert.Equal(expected, ThemeCatalog.NormalizeAccentColor(input));
    }

    [Theory]
    [InlineData("cyan", "#00D4FF")]
    [InlineData("violet", "#A78BFA")]
    [InlineData("green", "#38D996")]
    [InlineData("amber", "#FFC857")]
    [InlineData("bogus", "#00D4FF")]
    public void Legacy_accent_keys_map_to_hex_seed_values(string legacyAccent, string expected)
    {
        Assert.Equal(expected, ThemeCatalog.AccentColorForLegacyAccent(legacyAccent));
    }

    [Theory]
    [InlineData(1500, "short")]
    [InlineData(2500, "normal")]
    [InlineData(4000, "long")]
    [InlineData(777, "normal")]
    public void Fade_delay_preset_maps_existing_millisecond_values(int milliseconds, string expected)
    {
        Assert.Equal(expected, ThemeCatalog.FadeDelayPresetForMilliseconds(milliseconds));
        Assert.Equal(milliseconds == 777 ? 2500 : milliseconds, ThemeCatalog.FadeDelayMillisecondsForPreset(expected));
    }

    // --- Visual token sets: radii + native corner mode (review doc §8.8) ---

    private static IEnumerable<double> AllRadii(ThemeRadii r) =>
        [r.MainWindowFrame, r.PopoutFrame, r.TitleBar, r.Button, r.IconButton, r.Input,
         r.Panel, r.Popup, r.Thumbnail, r.Swatch, r.ScrollbarThumb, r.ToolTip];

    [Fact]
    public void Preset_radii_are_sane_and_ordered_by_softness()
    {
        foreach (var preset in ThemeCatalog.Presets)
        {
            Assert.All(AllRadii(preset.Radii), v =>
                Assert.True(v is >= 0 and <= 24, $"{preset.Id}: radius {v} outside 0..24."));
            Assert.True(Enum.IsDefined(preset.DwmCorners), $"{preset.Id}: undefined DwmCornerMode.");
        }

        // Soft Glass is the floating overlay theme: its popout frame is its roundest window shape.
        var softGlass = ThemeCatalog.PresetFor("soft-glass");
        Assert.True(softGlass.Radii.PopoutFrame >= softGlass.Radii.MainWindowFrame);

        // Theme personality ordering (review doc §8.1): sharp <= minimal <= soft-glass per token.
        var sharp = AllRadii(ThemeCatalog.PresetFor("sharp-dark").Radii).ToArray();
        var minimal = AllRadii(ThemeCatalog.PresetFor("minimal").Radii).ToArray();
        var soft = AllRadii(softGlass.Radii).ToArray();
        for (var i = 0; i < sharp.Length; i++)
        {
            Assert.True(sharp[i] <= minimal[i], $"sharp token {i} ({sharp[i]}) above minimal ({minimal[i]}).");
            Assert.True(minimal[i] <= soft[i], $"minimal token {i} ({minimal[i]}) above soft-glass ({soft[i]}).");
        }
    }

    [Theory]
    [InlineData(null, "theme")]
    [InlineData("", "theme")]
    [InlineData("THEME", "theme")]
    [InlineData("Square", "square")]
    [InlineData("small", "small")]
    [InlineData("soft", "soft")]
    [InlineData("round", "round")]
    [InlineData("bogus", "theme")]
    public void Corner_style_normalizes_to_the_catalog_vocabulary(string? input, string expected)
    {
        Assert.Equal(expected, ThemeCatalog.NormalizeCornerStyle(input));
        Assert.Contains(expected, ThemeCatalog.CornerStyleOptions.Select(o => o.Key));
    }

    [Fact]
    public void Corner_style_override_swaps_the_whole_profile_for_every_preset()
    {
        foreach (var preset in ThemeCatalog.Presets)
        {
            // "theme" follows the preset; the named styles replace radii AND native mode together.
            Assert.Equal(preset.Radii, ThemeCatalog.RadiiFor(preset, "theme"));
            Assert.Equal(preset.DwmCorners, ThemeCatalog.DwmCornersFor(preset, "theme"));

            Assert.All(AllRadii(ThemeCatalog.RadiiFor(preset, "square")), v => Assert.Equal(0, v));
            Assert.Equal(DwmCornerMode.Square, ThemeCatalog.DwmCornersFor(preset, "square"));

            Assert.Equal(ThemeCatalog.PresetFor("sharp-dark").Radii, ThemeCatalog.RadiiFor(preset, "small"));
            Assert.Equal(DwmCornerMode.SmallRound, ThemeCatalog.DwmCornersFor(preset, "small"));

            Assert.Equal(ThemeCatalog.PresetFor("minimal").Radii, ThemeCatalog.RadiiFor(preset, "soft"));
            Assert.Equal(DwmCornerMode.Round, ThemeCatalog.DwmCornersFor(preset, "soft"));

            Assert.Equal(ThemeCatalog.PresetFor("soft-glass").Radii, ThemeCatalog.RadiiFor(preset, "round"));
            Assert.Equal(DwmCornerMode.Round, ThemeCatalog.DwmCornersFor(preset, "round"));
        }
    }

    [Fact]
    public void Default_theme_leaves_native_corners_untouched()
    {
        // The pristine-window guarantee: a fresh install (sharp-dark, corner style "theme") must
        // not write any DWM corner preference, keeping default windows byte-identical.
        Assert.Equal(DwmCornerMode.Default,
            ThemeCatalog.DwmCornersFor(ThemeCatalog.PresetFor(ThemeCatalog.DefaultThemeId), ThemeCatalog.DefaultCornerStyle));
    }

    // --- Accent switch rule (end-pass review §3.3, claim-response review R4) ---

    [Theory]
    [InlineData("#00D4FF", "sharp-dark", "soft-glass", "#A78BFA")]   // on previous default → adopt next default
    [InlineData("#00d4ff", "sharp-dark", "soft-glass", "#A78BFA")]   // lowercase normalizes before comparison
    [InlineData("#FFC857", "sharp-dark", "soft-glass", "#FFC857")]   // custom accent survives the switch
    [InlineData("#ffc857", "sharp-dark", "soft-glass", "#FFC857")]   // …and comes back normalized
    [InlineData("#A78BFA", "soft-glass", "sharp-dark", "#00D4FF")]   // works in both directions
    public void Accent_for_theme_switch_preserves_custom_accents(
        string current, string fromTheme, string toTheme, string expected)
    {
        Assert.Equal(expected, ThemeCatalog.AccentForThemeSwitch(
            current, ThemeCatalog.PresetFor(fromTheme), ThemeCatalog.PresetFor(toTheme)));
    }

    // --- Per-preset palette readability (review doc §7 + §8.8), same Wcag gates as the
    // Colors.xaml seed theories in XamlInvariantTests but across EVERY preset palette. ---

    public static IEnumerable<object[]> PresetIds() =>
        ThemeCatalog.Presets.Select(p => new object[] { p.Id });

    [Theory]
    [MemberData(nameof(PresetIds))]
    public void Preset_palettes_meet_contrast_minimums(string presetId)
    {
        var p = ThemeCatalog.PresetFor(presetId).Palette;

        Assert.True(Wcag.ContrastRatio(p.TextPrimary, p.AppBackground) >= 4.5, $"{presetId}: TextPrimary on AppBackground.");
        Assert.True(Wcag.ContrastRatio(p.TextPrimary, p.SurfaceBase) >= 4.5, $"{presetId}: TextPrimary on SurfaceBase.");
        Assert.True(Wcag.ContrastRatio(p.TextPrimary, p.SurfaceRaised) >= 4.5, $"{presetId}: TextPrimary on SurfaceRaised.");
        Assert.True(Wcag.ContrastRatio(p.TextSecondary, p.SurfaceBase) >= 4.5, $"{presetId}: TextSecondary on SurfaceBase.");

        // DangerButton renders white text on the Danger fill. Gated at the 3.0:1 UI-component
        // level: white-on-red has never met 4.5:1 (the rose #E45D75 used by sharp-dark/soft-glass
        // is 3.43:1; minimal's warmer #E8564C is 3.58:1) and the button is a large bold CTA.
        Assert.True(Wcag.ContrastRatio("#FFFFFF", p.Danger) >= 3.0, $"{presetId}: white text on Danger.");

        // Every offered accent stays readable on every preset's hover surface (glyph gate) and
        // keeps carrying the dark AccentButton text (fill gate).
        foreach (var option in ThemeCatalog.AccentOptions)
        {
            var glyph = Wcag.ContrastRatio(option.HexColor, p.SurfaceHover);
            Assert.True(glyph >= 3.0, $"{presetId}: accent {option.Key} on hover surface = {glyph:F2}:1.");
            var text = Wcag.ContrastRatio("#FF06141A", option.HexColor);
            Assert.True(text >= 4.5, $"{presetId}: dark button text on accent {option.Key} = {text:F2}:1.");
        }
    }

    // --- TG-2 / TG-10: spec-literal exact-value gates (theme-v2 tight-scope, 2026-06-14) ---
    // These pin the spec's target tables as HARDCODED literals, NOT derived from the catalog. The
    // existing XamlInvariantTests seed test only proves Colors.xaml ↔ catalog consistency; changing
    // the catalog and that test together stays green and enforces nothing against the spec. These
    // gates fail if any shipped catalog value drifts from the agreed v2 target, independently.

    private static void AssertPresetMatchesSpec(
        string id, string displayName, string defaultAccent, string fadeDelay, bool stripAutoHide,
        double activeOpacity, double idleOpacity, DwmCornerMode dwm, ThemePalette palette, ThemeRadii radii)
    {
        var p = ThemeCatalog.PresetFor(id);
        Assert.Equal(id, p.Id);
        Assert.Equal(displayName, p.DisplayName);
        Assert.Equal(defaultAccent, p.DefaultAccentColor);
        Assert.Equal(fadeDelay, p.DefaultFadeDelayPreset);
        Assert.Equal(stripAutoHide, p.DefaultStripAutoHide);
        Assert.Equal(activeOpacity, p.DefaultActiveWindowOpacity);
        Assert.Equal(idleOpacity, p.DefaultIdleWindowOpacity);
        Assert.Equal(dwm, p.DwmCorners);
        Assert.Equal(palette, p.Palette);   // record equality covers all nine surface/text tokens
        Assert.Equal(radii, p.Radii);       // record equality covers all twelve semantic radii
    }

    [Fact]
    public void Sharp_dark_matches_the_v2_spec_literals() => AssertPresetMatchesSpec(
        "sharp-dark", "Sharp Dark", "#00D4FF", "normal", stripAutoHide: false,
        activeOpacity: 1.0, idleOpacity: 1.0, DwmCornerMode.Default,
        new ThemePalette(
            AppBackground: "#050609", SurfaceBase: "#0B0E12", SurfaceRaised: "#131820",
            SurfaceHover: "#1E2630", BorderSubtle: "#2B3645", BorderStrong: "#3E4B5C",
            TextPrimary: "#F4F7FA", TextSecondary: "#9AA2AD", Danger: "#E45D75"),
        new ThemeRadii(
            MainWindowFrame: 2, PopoutFrame: 2, TitleBar: 2, Button: 3, IconButton: 3, Input: 3,
            Panel: 2, Popup: 4, Thumbnail: 2, Swatch: 4, ScrollbarThumb: 3, ToolTip: 4));

    [Fact]
    public void Minimal_matches_the_v2_spec_literals() => AssertPresetMatchesSpec(
        "minimal", "Minimal", "#5AA9E6", "long", stripAutoHide: false,
        activeOpacity: 1.0, idleOpacity: 1.0, DwmCornerMode.SmallRound,
        new ThemePalette(
            AppBackground: "#14120F", SurfaceBase: "#1C1A16", SurfaceRaised: "#26231E",
            SurfaceHover: "#312D27", BorderSubtle: "#3C372F", BorderStrong: "#50493E",
            TextPrimary: "#F4F1EC", TextSecondary: "#B0A99E", Danger: "#E8564C"),
        new ThemeRadii(
            MainWindowFrame: 8, PopoutFrame: 12, TitleBar: 8, Button: 8, IconButton: 8, Input: 8,
            Panel: 10, Popup: 10, Thumbnail: 6, Swatch: 8, ScrollbarThumb: 5, ToolTip: 8));

    [Fact]
    public void Soft_glass_matches_the_v2_spec_literals() => AssertPresetMatchesSpec(
        "soft-glass", "Soft Glass", "#A78BFA", "short", stripAutoHide: true,
        activeOpacity: 0.92, idleOpacity: 0.78, DwmCornerMode.Round,
        new ThemePalette(
            AppBackground: "#0B1018", SurfaceBase: "#121A26", SurfaceRaised: "#1B2738",
            SurfaceHover: "#26354B", BorderSubtle: "#44526E", BorderStrong: "#66799E",
            TextPrimary: "#F6F8FC", TextSecondary: "#C4CEDC", Danger: "#E45D75"),
        new ThemeRadii(
            MainWindowFrame: 14, PopoutFrame: 22, TitleBar: 14, Button: 12, IconButton: 12, Input: 12,
            Panel: 16, Popup: 16, Thumbnail: 10, Swatch: 12, ScrollbarThumb: 6, ToolTip: 10));

    [Fact]
    public void Theme_palette_and_radii_use_structural_equality()
    {
        // The *_matches_the_v2_spec_literals gates above pin all nine palette tokens and twelve radii
        // through Assert.Equal(expectedRecord, preset.Palette/Radii) — which only compares per-field
        // because ThemePalette/ThemeRadii are records. If either were ever refactored to a class,
        // those asserts would silently degrade to reference equality and stop enforcing the literals.
        // This guard turns that refactor red instead of letting it gut the spec gates unnoticed.
        Assert.Equal(
            new ThemePalette("#1", "#2", "#3", "#4", "#5", "#6", "#7", "#8", "#9"),
            new ThemePalette("#1", "#2", "#3", "#4", "#5", "#6", "#7", "#8", "#9"));
        Assert.NotEqual(
            new ThemePalette("#1", "#2", "#3", "#4", "#5", "#6", "#7", "#8", "#9"),
            new ThemePalette("#0", "#2", "#3", "#4", "#5", "#6", "#7", "#8", "#9"));
        Assert.Equal(
            new ThemeRadii(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12),
            new ThemeRadii(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12));
        Assert.NotEqual(
            new ThemeRadii(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12),
            new ThemeRadii(0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12));
    }

    // --- Task 2 / TG-1: anti-collapse identity gates. The ordering test guarantees only <=, which a
    // regression collapsing presets back to near-identical values would still pass. These assert the
    // presets stay visibly distinct on the axes v2 newly diverges. ---

    [Fact]
    public void Preset_identities_stay_visually_distinct()
    {
        var sharp = ThemeCatalog.PresetFor("sharp-dark");
        var minimal = ThemeCatalog.PresetFor("minimal");
        var soft = ThemeCatalog.PresetFor("soft-glass");

        // Popout-frame rounding strictly increases sharp < minimal < soft-glass.
        Assert.True(sharp.Radii.PopoutFrame < minimal.Radii.PopoutFrame,
            $"sharp PopoutFrame {sharp.Radii.PopoutFrame} not below minimal {minimal.Radii.PopoutFrame}.");
        Assert.True(minimal.Radii.PopoutFrame < soft.Radii.PopoutFrame,
            $"minimal PopoutFrame {minimal.Radii.PopoutFrame} not below soft-glass {soft.Radii.PopoutFrame}.");

        // Soft Glass popout reads as the overlay-soft shape: at least 16 DIP rounder than Sharp.
        Assert.True(soft.Radii.PopoutFrame - sharp.Radii.PopoutFrame >= 16,
            $"soft-glass PopoutFrame only {soft.Radii.PopoutFrame - sharp.Radii.PopoutFrame} DIP above sharp.");

        // Native corner mode is distinct per preset: Sharp pristine, Minimal small, Soft Glass round.
        Assert.Equal(DwmCornerMode.Default, sharp.DwmCorners);
        Assert.Equal(DwmCornerMode.SmallRound, minimal.DwmCorners);
        Assert.Equal(DwmCornerMode.Round, soft.DwmCorners);
        Assert.Equal(3, new[] { sharp.DwmCorners, minimal.DwmCorners, soft.DwmCorners }.Distinct().Count());

        // Each preset ships a distinct default accent identity.
        Assert.Equal(3, new[]
        {
            sharp.DefaultAccentColor, minimal.DefaultAccentColor, soft.DefaultAccentColor,
        }.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Preset_behavior_defaults_stay_differentiated()
    {
        var sharp = ThemeCatalog.PresetFor("sharp-dark");
        var minimal = ThemeCatalog.PresetFor("minimal");
        var soft = ThemeCatalog.PresetFor("soft-glass");

        // Fade delay diverges across all three. It is NON-MONOTONIC (normal/long/short =
        // 2500/4000/1500), so an ordering inequality would be wrong — assert pairwise distinctness.
        Assert.Equal(3, new[]
        {
            sharp.DefaultFadeDelayPreset, minimal.DefaultFadeDelayPreset, soft.DefaultFadeDelayPreset,
        }.Distinct(StringComparer.Ordinal).Count());

        // Soft Glass auto-hides its popout strip by default; Sharp and Minimal do not.
        Assert.True(soft.DefaultStripAutoHide);
        Assert.False(sharp.DefaultStripAutoHide);
        Assert.False(minimal.DefaultStripAutoHide);

        // Soft Glass is the only translucent shell; Sharp and Minimal stay fully opaque.
        Assert.True(soft.DefaultActiveWindowOpacity < 1.0 && soft.DefaultIdleWindowOpacity < 1.0,
            "soft-glass should default to translucent active and idle opacity.");
        Assert.Equal(1.0, sharp.DefaultActiveWindowOpacity);
        Assert.Equal(1.0, sharp.DefaultIdleWindowOpacity);
        Assert.Equal(1.0, minimal.DefaultActiveWindowOpacity);
        Assert.Equal(1.0, minimal.DefaultIdleWindowOpacity);
    }
}

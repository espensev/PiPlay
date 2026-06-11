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
        // level: the white-on-red ratio has never met 4.5:1 (the pre-theme #FF4B55 was 3.29:1;
        // the rose #E45D75 is 3.43:1 — a slight improvement) and the button is a large bold CTA.
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
}

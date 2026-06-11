using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class AccentPaletteTests
{
    [Fact]
    public void Derivation_is_deterministic_and_normalizes_input()
    {
        Assert.Equal(AccentPalette.Derive("#a78bfa"), AccentPalette.Derive("A78BFA"));
    }

    [Fact]
    public void Invalid_accent_falls_back_to_the_default_accent()
    {
        Assert.Equal(
            AccentPalette.Derive(ThemeCatalog.DefaultAccentColor),
            AccentPalette.Derive("not-a-color"));
    }

    [Fact]
    public void Derived_variants_are_distinct_from_the_accent()
    {
        var p = AccentPalette.Derive(ThemeCatalog.DefaultAccentColor);
        Assert.NotEqual(p.Accent, p.Hover);
        Assert.NotEqual(p.Accent, p.Pressed);
        Assert.NotEqual(p.Accent, p.Dim);
        Assert.NotEqual(p.Accent, p.Border);
        Assert.NotEqual(p.Hover, p.Pressed);
    }

    [Fact]
    public void Foreground_contrast_is_safe_for_every_catalog_accent_and_preset()
    {
        // White-or-black by contrast: the two ratios against any color multiply to 21, so the
        // winner is always >= sqrt(21) ~ 4.58 — this also holds for any future color-wheel pick.
        var accents = ThemeCatalog.AccentOptions.Select(o => o.HexColor)
            .Concat(ThemeCatalog.Presets.Select(p => p.DefaultAccentColor))
            .Distinct();
        foreach (var hex in accents)
        {
            var p = AccentPalette.Derive(hex);
            var ratio = AccentPalette.ContrastRatio(p.Foreground, p.Accent);
            Assert.True(ratio >= 4.5, $"{hex}: foreground contrast {ratio:F2}:1, below 4.5:1.");
        }
    }
}

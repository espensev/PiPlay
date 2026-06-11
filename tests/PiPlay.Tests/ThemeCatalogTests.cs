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
}

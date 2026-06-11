using System.Windows.Media;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ThemeColorsTests
{
    [Fact]
    public void ParseColor_reads_a_normalized_hex()
    {
        Assert.Equal(Color.FromRgb(0x00, 0xD4, 0xFF), ThemeColors.ParseColor("#00D4FF"));
        Assert.Equal(Color.FromRgb(0xA7, 0x8B, 0xFA), ThemeColors.ParseColor("a78bfa"));
    }

    [Fact]
    public void ParseColor_falls_back_to_the_catalog_default_for_junk()
    {
        // Delegates to ThemeCatalog.NormalizeAccentColor, so invalid input can never throw.
        Assert.Equal(ThemeColors.ParseColor(ThemeCatalog.DefaultAccentColor), ThemeColors.ParseColor("not-a-color"));
        Assert.Equal(ThemeColors.ParseColor(ThemeCatalog.DefaultAccentColor), ThemeColors.ParseColor(null));
    }

    [Fact]
    public void Lighten_blends_toward_white_and_clamps_the_amount()
    {
        var accent = Color.FromRgb(0x00, 0xD4, 0xFF);

        Assert.Equal(accent, ThemeColors.Lighten(accent, 0.0));
        Assert.Equal(Colors.White, ThemeColors.Lighten(accent, 1.0));
        Assert.Equal(Colors.White, ThemeColors.Lighten(accent, 5.0));   // clamped

        var lighter = ThemeColors.Lighten(accent, 0.30);
        Assert.True(lighter.R > accent.R && lighter.G >= accent.G && lighter.B >= accent.B);
    }

    [Fact]
    public void Brush_builds_a_frozen_accent_brush()
    {
        var brush = ThemeColors.Brush("#38D996");

        Assert.True(brush.IsFrozen);   // safe to share across the source window and popout
        Assert.Equal(Color.FromRgb(0x38, 0xD9, 0x96), brush.Color);
    }
}

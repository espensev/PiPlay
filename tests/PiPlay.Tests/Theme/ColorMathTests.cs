using System.Windows.Media;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ColorMathTests
{
    [Theory]
    [InlineData(0xFF, 0x00, 0x00, 0.0)]
    [InlineData(0x00, 0xFF, 0x00, 120.0)]
    [InlineData(0x00, 0x00, 0xFF, 240.0)]
    public void RgbToHsv_maps_primary_hues(byte r, byte g, byte b, double hue)
    {
        var (h, s, v) = ColorMath.RgbToHsv(Color.FromRgb(r, g, b));

        Assert.Equal(hue, h, 3);
        Assert.Equal(1.0, s, 3);
        Assert.Equal(1.0, v, 3);
    }

    [Fact]
    public void Gray_has_zero_saturation_and_hue_zero()
    {
        var (h, s, v) = ColorMath.RgbToHsv(Color.FromRgb(0x80, 0x80, 0x80));

        Assert.Equal(0.0, s, 3);
        Assert.Equal(0x80 / 255.0, v, 3);
        Assert.Equal(0.0, h, 3);
    }

    [Theory]
    [InlineData(0xFF, 0x50, 0xC8)]
    [InlineData(0x00, 0xD4, 0xFF)]
    [InlineData(0x12, 0x34, 0x56)]
    public void HsvToRgb_inverts_RgbToHsv(byte r, byte g, byte b)
    {
        var original = Color.FromRgb(r, g, b);
        var (h, s, v) = ColorMath.RgbToHsv(original);

        Assert.Equal(original, ColorMath.HsvToRgb(h, s, v));
    }

    [Fact]
    public void HsvToRgb_wraps_hue_and_clamps_sv()
    {
        Assert.Equal(ColorMath.HsvToRgb(0, 1, 1), ColorMath.HsvToRgb(360, 1, 1));
        Assert.Equal(Colors.White, ColorMath.HsvToRgb(0, -1, 2));
    }
}

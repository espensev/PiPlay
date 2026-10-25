using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ContrastBrushConverterTests
{
    private static readonly Color DarkSurface = Color.FromRgb(0x0B, 0x0E, 0x12);

    private static object ConvertRail(object?[] values) =>
        new ContrastBrushConverter().Convert(values!, typeof(Brush), null, CultureInfo.InvariantCulture);

    [Fact]
    public void Valid_readable_hex_with_solid_color_brush_surface_preserves_exact_color_and_freezes()
    {
        var brush = Assert.IsType<SolidColorBrush>(ConvertRail(["#A78BFA", new SolidColorBrush(DarkSurface)]));

        Assert.Equal(Color.FromRgb(0xA7, 0x8B, 0xFA), brush.Color);
        Assert.True(brush.IsFrozen);
    }

    [Fact]
    public void Dark_hex_is_lifted_to_meet_3_to_1_contrast_against_adjacent_color_surface()
    {
        var brush = Assert.IsType<SolidColorBrush>(ConvertRail(["#0B0E11", DarkSurface]));

        Assert.NotEqual(Color.FromRgb(0x0B, 0x0E, 0x11), brush.Color);
        Assert.True(ThemeColors.ContrastRatio(brush.Color, DarkSurface) >= 3.0);
        Assert.True(brush.IsFrozen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-color")]
    [InlineData("#12345")]
    public void Null_or_invalid_hex_returns_transparent_brush(string? hex)
    {
        Assert.Same(Brushes.Transparent, ConvertRail([hex, DarkSurface]));
    }

    [Fact]
    public void Missing_or_non_color_adjacent_surface_returns_transparent_brush()
    {
        Assert.Same(Brushes.Transparent, ConvertRail(["#A78BFA"]));
        Assert.Same(Brushes.Transparent, ConvertRail(["#A78BFA", null]));
        Assert.Same(Brushes.Transparent, ConvertRail(["#A78BFA", DependencyProperty.UnsetValue]));
        Assert.Same(Brushes.Transparent, ConvertRail([DependencyProperty.UnsetValue, DarkSurface]));
    }

    [Fact]
    public void ConvertBack_returns_do_nothing_for_both_bindings()
    {
        var result = new ContrastBrushConverter().ConvertBack(
            Brushes.White, [typeof(string), typeof(Brush)], null, CultureInfo.InvariantCulture);

        Assert.Equal([Binding.DoNothing, Binding.DoNothing], result);
    }
}

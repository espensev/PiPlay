using System.Globalization;
using System.Windows.Media;
using PiPlay.Theme;

namespace PiPlay.Tests;

/// <summary>
/// The profile dropdown row wash (2026-08-09 profile-backgrounds design): each row wears its OWN
/// accent at the active theme's subtle alpha, published by ThemeResourceApplier. Null/invalid
/// accents render no wash at all — the plain row surface is the fallback, matching the identity
/// rail's contract.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Logic)]
public class AccentWashBrushConverterTests
{
    private static object ConvertRow(object?[] values) =>
        new AccentWashBrushConverter().Convert(values!, typeof(Brush), null, CultureInfo.InvariantCulture);

    [Fact]
    public void Valid_accent_washes_at_the_published_alpha()
    {
        var brush = Assert.IsType<SolidColorBrush>(ConvertRow(["#A78BFA", (byte)0x22]));
        Assert.Equal(Color.FromArgb(0x22, 0xA7, 0x8B, 0xFA), brush.Color);
        Assert.True(brush.IsFrozen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-color")]
    public void Missing_or_invalid_accent_renders_no_wash(string? accent)
    {
        Assert.Same(Brushes.Transparent, ConvertRow([accent, (byte)0x22]));
    }

    [Fact]
    public void Missing_alpha_renders_no_wash()
    {
        // Pre-Apply instant or a template outside the themed tree: fail to the plain surface.
        Assert.Same(Brushes.Transparent, ConvertRow(["#A78BFA", null]));
    }
}

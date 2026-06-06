using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PlayerAppearancePolicyTests
{
    [Theory]
    [InlineData(null, "cyan")]
    [InlineData("", "cyan")]
    [InlineData("cyan", "cyan")]
    [InlineData("VIOLET", "violet")]
    [InlineData(" green ", "green")]
    [InlineData("not-a-color", "cyan")]
    public void NormalizeAccent_returns_allowed_keys_or_default(string? input, string expected)
    {
        Assert.Equal(expected, PlayerAppearancePolicy.NormalizeAccent(input));
    }

    [Theory]
    [InlineData("cyan", "AccentCyan")]
    [InlineData("violet", "AccentViolet")]
    [InlineData("green", "AccentGreen")]
    [InlineData("amber", "AccentAmber")]
    [InlineData("bad", "AccentCyan")]
    public void BrushResourceKeyFor_maps_accent_to_brush_resource(string? input, string expected)
    {
        Assert.Equal(expected, PlayerAppearancePolicy.BrushResourceKeyFor(input));
    }

    [Theory]
    [InlineData(1500, 1500)]
    [InlineData(2500, 2500)]
    [InlineData(4000, 4000)]
    [InlineData(0, 2500)]
    [InlineData(1234, 2500)]
    public void NormalizeFadeIdleDelayMs_accepts_only_presets(int input, int expected)
    {
        Assert.Equal(expected, PlayerAppearancePolicy.NormalizeFadeIdleDelayMs(input));
    }
}

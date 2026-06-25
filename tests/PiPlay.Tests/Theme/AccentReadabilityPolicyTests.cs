using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class AccentReadabilityPolicyTests
{
    [Fact]
    public void Every_curated_accent_is_readable()
    {
        foreach (var option in ThemeCatalog.AccentOptions)
            Assert.True(AccentReadabilityPolicy.Evaluate(option.HexColor).IsReadable,
                $"{option.Key} ({option.HexColor})");
    }

    [Fact]
    public void Malformed_hex_is_unreadable()
    {
        var result = AccentReadabilityPolicy.Evaluate("not-a-color");

        Assert.False(result.IsReadable);
        Assert.Equal(AccentGate.Invalid, result.FailingGate);
    }

    [Fact]
    public void A_mid_gray_is_accepted()
    {
        Assert.True(AccentReadabilityPolicy.Evaluate("#787878").IsReadable);
    }

    [Theory]
    [InlineData("#404040")]
    [InlineData("#787878")]
    [InlineData("#202060")]
    [InlineData("not-a-color")]
    public void NearestReadable_normalizes_valid_hex_and_defaults_invalid(string input)
    {
        var value = AccentReadabilityPolicy.NearestReadable(input);

        Assert.True(AccentReadabilityPolicy.Evaluate(value).IsReadable);
        Assert.Equal(ThemeCatalog.IsValidHex(input)
            ? ThemeCatalog.NormalizeAccentColor(input)
            : ThemeCatalog.DefaultAccentColor, value);
    }

    [Fact]
    public void NearestReadable_is_identity_for_a_readable_color()
    {
        // A readable NON-default color returns unchanged — a constant/default-returning stub fails this.
        Assert.Equal("#2DB57F", AccentReadabilityPolicy.NearestReadable("#2DB57F"));
    }

    [Fact]
    public void NearestReadable_preserves_valid_dim_colors()
    {
        Assert.Equal("#202060", AccentReadabilityPolicy.NearestReadable("#202060"));
    }

    [Fact]
    public void NearestReadable_closes_a_dense_hue_sweep()
    {
        for (var hue = 0; hue < 360; hue += 5)
        {
            var color = ColorMath.HsvToRgb(hue, 1, 1);
            var raw = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            Assert.Equal(raw, AccentReadabilityPolicy.NearestReadable(raw));
        }
    }
}

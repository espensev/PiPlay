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
    public void A_mid_gray_is_unreadable()
    {
        Assert.False(AccentReadabilityPolicy.Evaluate("#787878").IsReadable);
    }

    [Theory]
    [InlineData("#404040")]
    [InlineData("#787878")]
    [InlineData("#202060")]
    [InlineData("not-a-color")]
    public void NearestReadable_always_returns_a_readable_color(string input)
    {
        Assert.True(AccentReadabilityPolicy.Evaluate(AccentReadabilityPolicy.NearestReadable(input)).IsReadable);
    }

    [Fact]
    public void NearestReadable_is_identity_for_a_readable_color()
    {
        // A readable NON-default color returns unchanged — a constant/default-returning stub fails this.
        Assert.Equal("#2DB57F", AccentReadabilityPolicy.NearestReadable("#2DB57F"));
    }

    [Fact]
    public void NearestReadable_preserves_hue_when_brightening_a_dim_color()
    {
        // The fix raises Value/Saturation on the SAME hue before any curated-anchor snap, so a
        // dim-but-fixable color keeps its hue — a stub that snapped to a constant or nearest preset
        // would drift the hue and fail this. (#202060 is a dim blue ~hue 240.)
        var inputHue = ColorMath.RgbToHsv(ThemeColors.ParseColor("#202060")).H;
        var fixedHue = ColorMath.RgbToHsv(
            ThemeColors.ParseColor(AccentReadabilityPolicy.NearestReadable("#202060"))).H;
        Assert.True(Math.Abs(fixedHue - inputHue) <= 12.0, $"hue drifted {inputHue:F1} -> {fixedHue:F1}");
    }

    [Fact]
    public void NearestReadable_closes_a_dense_hue_sweep()
    {
        for (var hue = 0; hue < 360; hue += 5)
        {
            var color = ColorMath.HsvToRgb(hue, 1, 1);
            var raw = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            var fixedColor = AccentReadabilityPolicy.NearestReadable(raw);

            Assert.True(AccentReadabilityPolicy.Evaluate(fixedColor).IsReadable, $"hue {hue}: {fixedColor}");
        }
    }
}

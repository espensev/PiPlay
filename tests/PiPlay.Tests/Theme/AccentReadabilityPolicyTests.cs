using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class AccentReadabilityPolicyTests
{
    [Theory]
    [InlineData("#2DB57F", true, AccentGate.None)]
    [InlineData("787878", true, AccentGate.None)]
    [InlineData(null, false, AccentGate.Invalid)]
    [InlineData("", false, AccentGate.Invalid)]
    [InlineData("not-a-color", false, AccentGate.Invalid)]
    public void Evaluate_accepts_valid_hex_and_rejects_malformed(
        string? input, bool expectedReadable, AccentGate expectedGate)
    {
        var result = AccentReadabilityPolicy.Evaluate(input);

        Assert.Equal(expectedReadable, result.IsReadable);
        Assert.Equal(expectedGate, result.FailingGate);
    }

    [Theory]
    [InlineData("#2DB57F", "#2DB57F")]
    [InlineData("2db57f", "#2DB57F")]
    [InlineData("#202060", "#202060")]
    [InlineData("not-a-color", "#2BAED0")]
    [InlineData(null, "#2BAED0")]
    public void NearestReadable_normalizes_valid_hex_and_defaults_invalid(string? input, string expected)
    {
        var value = AccentReadabilityPolicy.NearestReadable(input);

        Assert.True(AccentReadabilityPolicy.Evaluate(value).IsReadable);
        Assert.Equal(expected, value);
    }
}

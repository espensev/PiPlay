using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class AppChannelTests
{
    [Theory]
    [InlineData("Stable", PiPlayChannel.Stable)]
    [InlineData("stable", PiPlayChannel.Stable)]
    [InlineData("STABLE", PiPlayChannel.Stable)]
    [InlineData("  Stable  ", PiPlayChannel.Stable)]
    [InlineData("Default", PiPlayChannel.Default)]
    [InlineData("nonsense", PiPlayChannel.Default)]
    [InlineData("", PiPlayChannel.Default)]
    [InlineData(null, PiPlayChannel.Default)]
    public void Parse_maps_only_stable_case_insensitively(string? raw, PiPlayChannel expected)
        => Assert.Equal(expected, AppChannel.Parse(raw));

    [Theory]
    [InlineData("Stable", "Default", PiPlayChannel.Stable)]
    [InlineData(null, "Stable", PiPlayChannel.Stable)]
    [InlineData("   ", "Stable", PiPlayChannel.Stable)]
    [InlineData(null, null, PiPlayChannel.Default)]
    public void Resolve_prefers_env_override_then_baked_channel_then_default(
        string? envOverride, string? bakedChannel, PiPlayChannel expected)
        => Assert.Equal(expected, AppChannel.Resolve(envOverride, bakedChannel));

    [Theory]
    [InlineData(PiPlayChannel.Stable, true)]
    [InlineData(PiPlayChannel.Default, false)]
    public void IsPortableChannel_is_true_only_for_stable(PiPlayChannel channel, bool expected)
        => Assert.Equal(expected, AppChannel.IsPortableChannel(channel));
}

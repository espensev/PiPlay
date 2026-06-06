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

    [Fact]
    public void Resolve_env_override_beats_the_baked_channel()
        => Assert.Equal(PiPlayChannel.Stable, AppChannel.Resolve("Stable", "Default"));

    [Fact]
    public void Resolve_falls_back_to_the_baked_channel_when_no_override()
        => Assert.Equal(PiPlayChannel.Stable, AppChannel.Resolve(null, "Stable"));

    [Fact]
    public void Resolve_whitespace_override_is_ignored()
        => Assert.Equal(PiPlayChannel.Stable, AppChannel.Resolve("   ", "Stable"));

    [Fact]
    public void Resolve_defaults_when_nothing_is_set()
        => Assert.Equal(PiPlayChannel.Default, AppChannel.Resolve(null, null));
}

using System.IO;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class AppPathsTests
{
    [Fact]
    public void Root_honors_PIPLAY_DATA_ROOT_override()
    {
        var prev = Environment.GetEnvironmentVariable("PIPLAY_DATA_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", @"C:\some\override");
            Assert.Equal(@"C:\some\override", AppPaths.Root);
            Assert.Equal(@"C:\some\override\settings.json", AppPaths.SettingsFile);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", prev);
        }
    }

    [Fact]
    public void Root_falls_back_to_localappdata_when_unset()
    {
        var prev = Environment.GetEnvironmentVariable("PIPLAY_DATA_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", null);
            Assert.EndsWith("PiPlay", AppPaths.Root);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", prev);
        }
    }

    // --- Pure resolver: every (env, channel) combination, no real environment dependency ---

    [Theory]
    [InlineData(PiPlayChannel.Default)]
    [InlineData(PiPlayChannel.Stable)]
    public void ResolveRoot_env_override_wins_for_any_channel(PiPlayChannel channel)
    {
        var root = AppPaths.ResolveRoot(@"X:\override", channel, @"Y:\app", @"Z:\LocalAppData");
        Assert.Equal(@"X:\override", root);
    }

    [Fact]
    public void ResolveRoot_empty_env_override_is_ignored()
    {
        var root = AppPaths.ResolveRoot("", PiPlayChannel.Default, @"Y:\app", @"Z:\local");
        Assert.Equal(Path.Combine(@"Z:\local", "PiPlay"), root);
    }

    [Fact]
    public void ResolveRoot_default_channel_uses_localappdata()
    {
        var root = AppPaths.ResolveRoot(null, PiPlayChannel.Default, @"Y:\app", @"Z:\LocalAppData");
        Assert.Equal(Path.Combine(@"Z:\LocalAppData", "PiPlay"), root);
    }

    [Fact]
    public void ResolveRoot_stable_channel_is_portable_beside_the_exe()
    {
        // A deployed stable copy keeps its data self-contained next to the exe (isolated from dev).
        var root = AppPaths.ResolveRoot(null, PiPlayChannel.Stable, @"Y:\Stable", @"Z:\local");
        Assert.Equal(Path.Combine(@"Y:\Stable", "PiPlayData"), root);
    }
}

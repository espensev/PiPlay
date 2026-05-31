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
}

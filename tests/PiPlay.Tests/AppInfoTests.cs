using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class AppInfoTests
{
    [Fact]
    public void Reads_version_and_build_number_from_piplay_assembly()
    {
        // The csproj stamps the plain build; the committed VERSION / BUILD_NUMBER are the source of truth.
        Assert.Equal(RepoStamps.Version, AppInfo.Version);
        Assert.Equal(RepoStamps.BuildNumber, AppInfo.BuildNumber);
    }

    [Theory]
    [InlineData(PiPlayChannel.Default, "0.3.0", 8, "PiPlay")]
    [InlineData(PiPlayChannel.Stable, "0.3.0", 8, "PiPlay — Stable v0.3.0 (b8)")]
    [InlineData(PiPlayChannel.Stable, "0.3.0", 0, "PiPlay — Stable v0.3.0")]
    [InlineData(PiPlayChannel.Stable, "  ", 0, "PiPlay — Stable")]
    public void FormatTitle_formats_channel_version_and_build(
        PiPlayChannel channel, string version, int buildNumber, string expected)
        => Assert.Equal(expected, AppInfo.FormatTitle(channel, version, buildNumber));
}

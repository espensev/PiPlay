using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class AppInfoTests
{
    [Fact]
    public void Default_channel_title_stays_plain_PiPlay()
        => Assert.Equal("PiPlay", AppInfo.FormatTitle(PiPlayChannel.Default, "0.3.0", 8));

    [Fact]
    public void Stable_channel_title_includes_channel_version_and_build()
        => Assert.Equal("PiPlay — Stable v0.3.0 (b8)", AppInfo.FormatTitle(PiPlayChannel.Stable, "0.3.0", 8));

    [Fact]
    public void Stable_channel_title_drops_the_build_segment_when_zero()
        => Assert.Equal("PiPlay — Stable v0.3.0", AppInfo.FormatTitle(PiPlayChannel.Stable, "0.3.0", 0));

    [Fact]
    public void Stable_channel_title_omits_the_version_when_blank()
        => Assert.Equal("PiPlay — Stable", AppInfo.FormatTitle(PiPlayChannel.Stable, "  ", 0));
}

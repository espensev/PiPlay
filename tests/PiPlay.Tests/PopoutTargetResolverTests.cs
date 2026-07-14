using PiPlay.Services;

namespace PiPlay.Tests;

public class PopoutTargetResolverTests
{
    [Fact]
    public void Current_source_video_wins_over_a_stale_spa_canonical()
    {
        var target = PopoutTargetResolver.Resolve(
            "https://www.youtube.com/watch?v=BBBBBBBBBBB",
            "https://www.youtube.com/watch?v=AAAAAAAAAAA");

        Assert.NotNull(target);
        Assert.Equal("BBBBBBBBBBB", target!.VideoId);
    }

    [Fact]
    public void Canonical_is_the_fallback_when_current_source_has_no_playable_target()
    {
        var target = PopoutTargetResolver.Resolve(
            "https://www.youtube.com/",
            "https://www.youtube.com/watch?v=AAAAAAAAAAA");

        Assert.NotNull(target);
        Assert.Equal("AAAAAAAAAAA", target!.VideoId);
    }

    [Fact]
    public void Source_playlist_fallback_is_preserved_when_neither_surface_has_a_video()
    {
        var target = PopoutTargetResolver.Resolve(
            "https://www.youtube.com/playlist?list=PLabc",
            "https://www.youtube.com/");

        Assert.NotNull(target);
        Assert.True(target!.IsPlaylistOnly);
        Assert.Equal("PLabc", target.PlaylistId);
    }
}

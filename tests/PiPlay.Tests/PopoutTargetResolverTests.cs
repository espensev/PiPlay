using PiPlay.Models;
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

    // --- Playlist-only launches start the page's first playable item (spec 13.1 / 22.1) ---

    [Fact]
    public void A_playlist_only_target_adopts_the_first_items_video_id_and_keeps_its_own_list()
    {
        var target = PopoutTargetResolver.WithFirstPlaylistItem(
            Target("https://www.youtube.com/playlist?list=PLabc"),
            "https://www.youtube.com/watch?v=AAAAAAAAAAA&list=PLabc&index=1");

        Assert.Equal("AAAAAAAAAAA", target.VideoId);
        Assert.Equal("PLabc", target.PlaylistId);
        Assert.True(target.IsPlaylistOnly);
    }

    /// <summary>
    /// The link is page-provided (DOM read), so only the charset-validated video id may be adopted.
    /// A link into ANOTHER list must not swap the playlist the user chose to launch.
    /// </summary>
    [Fact]
    public void A_first_item_link_into_another_list_cannot_swap_the_launched_playlist()
    {
        var target = PopoutTargetResolver.WithFirstPlaylistItem(
            Target("https://www.youtube.com/playlist?list=PLabc"),
            "https://www.youtube.com/watch?v=AAAAAAAAAAA&list=PLother");

        Assert.Equal("AAAAAAAAAAA", target.VideoId);
        Assert.Equal("PLabc", target.PlaylistId);
    }

    /// <summary>
    /// A link that doesn't strictly parse to a YouTube video (off-host, malformed id, another
    /// browse page, or nothing rendered at all) leaves the target unchanged — the popout then
    /// degrades to the playlist page itself instead of trusting a page string.
    /// </summary>
    [Theory]
    [InlineData("https://evil.example.com/watch?v=AAAAAAAAAAA")]
    [InlineData("https://www.youtube.com/watch?v=tooshort")]
    [InlineData("https://www.youtube.com/playlist?list=PLother")]
    [InlineData("not a url")]
    [InlineData(null)]
    public void An_unparsable_first_item_link_leaves_the_playlist_target_unchanged(string? link)
    {
        var target = PopoutTargetResolver.WithFirstPlaylistItem(
            Target("https://www.youtube.com/playlist?list=PLabc"), link);

        Assert.Null(target.VideoId);
        Assert.Equal("PLabc", target.PlaylistId);
        Assert.True(target.IsPlaylistOnly);
    }

    [Fact]
    public void A_target_that_already_names_a_video_ignores_the_first_item_overlay()
    {
        var target = PopoutTargetResolver.WithFirstPlaylistItem(
            Target("https://www.youtube.com/watch?v=BBBBBBBBBBB&list=PLabc"),
            "https://www.youtube.com/watch?v=AAAAAAAAAAA&list=PLabc");

        Assert.Equal("BBBBBBBBBBB", target.VideoId);
        Assert.False(target.IsPlaylistOnly);
    }

    // --- The captured identity must not outlive the Source it was captured from ---

    /// <summary>
    /// The race this rule exists for: Auto reads the video id off the address, awaits a DOM read, and the
    /// SPA advances the Source in that window. Popping the captured video would put video A on screen
    /// while the Source sits on B — and, worse, latch A as the Source's video, so the return seeks the
    /// Source (on B) to A's timestamp.
    /// </summary>
    [Fact]
    public void A_captured_identity_is_abandoned_once_the_live_source_has_moved_on()
    {
        Assert.False(PopoutTargetResolver.CapturedTargetStillMatchesSource(
            Target("https://www.youtube.com/watch?v=AAAAAAAAAAA"),
            "https://www.youtube.com/watch?v=BBBBBBBBBBB"));
    }

    [Fact]
    public void A_captured_identity_stands_while_the_live_source_still_shows_it()
    {
        // Same video, different address shape (the SPA rewrites the URL with playlist/index params).
        Assert.True(PopoutTargetResolver.CapturedTargetStillMatchesSource(
            Target("https://www.youtube.com/watch?v=AAAAAAAAAAA"),
            "https://www.youtube.com/watch?v=AAAAAAAAAAA&list=PLabc&index=2"));
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLabc", true)]
    [InlineData("https://www.youtube.com/playlist?index=2&list=PLabc", true)]
    [InlineData("https://www.youtube.com/playlist?list=PLother", false)]
    [InlineData("https://www.youtube.com/watch?v=AAAAAAAAAAA&list=PLabc", false)]
    [InlineData(null, false)]
    public void A_captured_playlist_stands_only_while_the_live_source_shows_the_same_playlist_page(
        string? liveSource,
        bool expected)
    {
        Assert.Equal(expected, PopoutTargetResolver.CapturedTargetStillMatchesSource(
            Target("https://www.youtube.com/playlist?list=PLabc"), liveSource));
    }

    /// <summary>
    /// An address with no video id is not evidence of a move. Pages whose URL hides the id are the whole
    /// reason the caller captured an identity instead of re-resolving, so treating "no id" as a move
    /// would silently disable Auto on exactly those pages.
    /// </summary>
    [Theory]
    [InlineData("https://www.youtube.com/")]
    [InlineData("https://www.youtube.com/playlist?list=PLabc")]
    [InlineData(null)]
    [InlineData("")]
    public void An_address_with_no_video_id_does_not_abandon_the_captured_identity(string? liveSource)
    {
        Assert.True(PopoutTargetResolver.CapturedTargetStillMatchesSource(
            Target("https://www.youtube.com/watch?v=AAAAAAAAAAA"), liveSource));
    }

    [Fact]
    public void There_is_nothing_to_honor_without_a_captured_video()
    {
        Assert.False(PopoutTargetResolver.CapturedTargetStillMatchesSource(
            null, "https://www.youtube.com/watch?v=AAAAAAAAAAA"));
        Assert.False(PopoutTargetResolver.CapturedTargetStillMatchesSource(
            Target("https://www.youtube.com/playlist?list=PLabc"), "https://www.youtube.com/watch?v=AAAAAAAAAAA"));
    }

    private static YouTubeTarget Target(string url)
    {
        Assert.True(YouTubeUrlHelper.TryParse(url, out var target));
        return target;
    }
}

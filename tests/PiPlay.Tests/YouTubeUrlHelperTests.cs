using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class YouTubeUrlHelperTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    public void Parses_video_id_from_common_url_shapes(string url, string expectedId)
    {
        Assert.True(YouTubeUrlHelper.TryParse(url, out var target));
        Assert.Equal(expectedId, target.VideoId);
    }

    [Fact]
    public void Parses_watch_with_playlist_and_timestamp()
    {
        Assert.True(YouTubeUrlHelper.TryParse(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL1234567890&t=123s", out var t));
        Assert.Equal("dQw4w9WgXcQ", t.VideoId);
        Assert.Equal("PL1234567890", t.PlaylistId);
        Assert.Equal(123, t.StartSeconds);
        Assert.Null(t.FallbackReason);
    }

    [Fact]
    public void Parses_playlist_only_page()
    {
        Assert.True(YouTubeUrlHelper.TryParse("https://www.youtube.com/playlist?list=PLabc", out var t));
        Assert.True(t.IsPlaylistOnly);
        Assert.Equal("PLabc", t.PlaylistId);
        Assert.Null(t.VideoId);
    }

    [Fact]
    public void Mix_radio_list_falls_back_to_current_video_with_note()
    {
        // list=RD... is a mix/radio: keep the video, drop the list, set a non-blocking note (spec 22.1).
        Assert.True(YouTubeUrlHelper.TryParse(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=RDdQw4w9WgXcQ", out var t));
        Assert.Equal("dQw4w9WgXcQ", t.VideoId);
        Assert.Null(t.PlaylistId);
        Assert.False(string.IsNullOrEmpty(t.FallbackReason));
    }

    [Theory]
    [InlineData("90", 90)]
    [InlineData("90s", 90)]
    [InlineData("1m30s", 90)]
    [InlineData("1h2m3s", 3723)]
    [InlineData("0", 0)]
    public void Parses_timestamp_formats(string value, int expected)
    {
        Assert.Equal(expected, YouTubeUrlHelper.ParseTime(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("https://example.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/")]
    public void Rejects_unsupported_input(string url)
    {
        Assert.False(YouTubeUrlHelper.TryParse(url, out _));
    }

    [Fact]
    public void Builds_watch_url_with_timestamp_and_playlist()
    {
        var target = new PiPlay.Models.YouTubeTarget
        {
            VideoId = "dQw4w9WgXcQ",
            PlaylistId = "PLabc",
        };
        var url = YouTubeUrlHelper.BuildWatchUrl(target, 42);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLabc&t=42s", url);
    }

    [Fact]
    public void Builds_watch_url_omits_zero_timestamp()
    {
        var target = new PiPlay.Models.YouTubeTarget { VideoId = "dQw4w9WgXcQ" };
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", YouTubeUrlHelper.BuildWatchUrl(target, 0));
    }

    // --- Gap cases surfaced by the spec-conformance review ---

    [Theory]
    [InlineData("https://www.youtube.com/v/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    public void Parses_path_based_v_and_live_ids(string url, string expected)
    {
        Assert.True(YouTubeUrlHelper.TryParse(url, out var t));
        Assert.Equal(expected, t.VideoId);
    }

    [Fact]
    public void Parses_youtu_be_with_list_and_timestamp()
    {
        Assert.True(YouTubeUrlHelper.TryParse("https://youtu.be/dQw4w9WgXcQ?list=PLabc&t=1m", out var t));
        Assert.Equal("dQw4w9WgXcQ", t.VideoId);
        Assert.Equal("PLabc", t.PlaylistId);
        Assert.Equal(60, t.StartSeconds);
    }

    [Fact]
    public void Parses_watch_with_start_param_alias()
    {
        Assert.True(YouTubeUrlHelper.TryParse("https://www.youtube.com/watch?v=dQw4w9WgXcQ&start=45", out var t));
        Assert.Equal(45, t.StartSeconds);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("99999999999h")]   // 11-digit hours: int.Parse used to THROW OverflowException
    [InlineData("999999999h")]     // 9-digit hours: int.Parse succeeded but the sum silently overflowed
    [InlineData("9999999999")]     // plain seconds past int range
    public void ParseTime_rejects_garbage_negatives_and_out_of_range(string value)
    {
        Assert.Null(YouTubeUrlHelper.ParseTime(value));
    }

    [Fact]
    public void TryParse_keeps_video_when_timestamp_is_out_of_range()
    {
        // A real link carrying an out-of-range t= must degrade to "no offset" and still navigate -
        // never throw into the generic crash dialog (spec 17: broken URLs fail gracefully).
        Assert.True(YouTubeUrlHelper.TryParse(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=99999999999h", out var t));
        Assert.Equal("dQw4w9WgXcQ", t.VideoId);
        Assert.Null(t.StartSeconds);
    }

    [Fact]
    public void Builds_watch_url_for_playlist_only_target()
    {
        var t = new PiPlay.Models.YouTubeTarget { PlaylistId = "PLabc", IsPlaylistOnly = true };
        Assert.Equal("https://www.youtube.com/playlist?list=PLabc", YouTubeUrlHelper.BuildWatchUrl(t));
    }

    [Fact]
    public void Builds_embed_url_with_autoplay_playlist_and_start()
    {
        var t = new PiPlay.Models.YouTubeTarget { VideoId = "dQw4w9WgXcQ", PlaylistId = "PLabc" };
        Assert.Equal(
            "https://www.youtube.com/embed/dQw4w9WgXcQ?autoplay=1&list=PLabc&start=30",
            YouTubeUrlHelper.BuildEmbedUrl(t, 30));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ&t=30s", true)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", true)]            // share link is a watch video
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", false)]  // Shorts must not pop
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", false)]
    [InlineData("https://www.youtube.com/playlist?list=PLabc", false)]
    [InlineData("https://www.youtube.com/results?search_query=lofi", false)]
    [InlineData("https://www.youtube.com/", false)]
    [InlineData("https://example.com/watch?v=dQw4w9WgXcQ", false)]  // not a YouTube host
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWatchUrl_is_true_only_for_watch_pages_and_share_links(string? url, bool expected)
    {
        Assert.Equal(expected, YouTubeUrlHelper.IsWatchUrl(url));
    }
}

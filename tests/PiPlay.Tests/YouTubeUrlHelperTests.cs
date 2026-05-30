using PiPlay.Services;

namespace PiPlay.Tests;

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
}

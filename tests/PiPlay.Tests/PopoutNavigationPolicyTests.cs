using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PopoutNavigationPolicyTests
{
    // Playable YouTube targets stay in the player (overhaul Task 3): the URL-shape proxy is the
    // only workable gate because NewWindowRequested carries no window-open disposition.
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=90", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ&list=PL123abc", "dQw4w9WgXcQ")]
    public void Playable_targets_retarget_in_place(string uri, string expectedVideoId)
    {
        var action = PopoutNavigationPolicy.DecideNewWindow(uri, out var target);

        Assert.Equal(PopoutNewWindowAction.RetargetInPlace, action);
        Assert.Equal(expectedVideoId, target!.VideoId);
    }

    [Fact]
    public void Mix_radio_lists_retarget_with_the_mix_kept()
    {
        var action = PopoutNavigationPolicy.DecideNewWindow(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=RDdQw4w9WgXcQ", out var target);

        Assert.Equal(PopoutNewWindowAction.RetargetInPlace, action);
        Assert.Equal("dQw4w9WgXcQ", target!.VideoId);
        Assert.Equal("RDdQw4w9WgXcQ", target.PlaylistId);   // mixes ride Normal like any playlist
        Assert.Null(target.FallbackReason);
    }

    // Everything without a concrete video id goes external: whole-site YouTube pages cannot be
    // hosted by the compact shell, and non-YouTube must never replace the player (REQ-NAV-02).
    [Theory]
    [InlineData("https://www.youtube.com/@SomeChannel")]
    [InlineData("https://www.youtube.com/results?search_query=test")]
    [InlineData("https://www.youtube.com/playlist?list=PL123abc")]
    [InlineData("https://www.youtube.com/")]
    [InlineData("https://example.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("about:blank")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_opens_externally(string? uri)
    {
        var action = PopoutNavigationPolicy.DecideNewWindow(uri, out var target);

        Assert.Equal(PopoutNewWindowAction.OpenExternal, action);
        Assert.Null(target);
    }
}

using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class AutoPopoutPolicyTests
{
    private const string A = "aaaaaaaaaaa";
    private const string B = "bbbbbbbbbbb";

    [Theory]
    // enabled + playing + /watch + a new id + no active player => Pop
    [InlineData(true,  true,  true,  A,    null, false, AutoPopDecision.Pop)]
    [InlineData(true,  true,  true,  A,    B,    false, AutoPopDecision.Pop)]   // a different video re-pops
    // off => Skip
    [InlineData(false, true,  true,  A,    null, false, AutoPopDecision.Skip)]
    // not playing => Skip
    [InlineData(true,  false, true,  A,    null, false, AutoPopDecision.Skip)]
    // non-/watch surface (Shorts / embed) => Skip
    [InlineData(true,  true,  false, A,    null, false, AutoPopDecision.Skip)]
    // no resolvable video id => Skip
    [InlineData(true,  true,  true,  null, null, false, AutoPopDecision.Skip)]
    [InlineData(true,  true,  true,  "",   null, false, AutoPopDecision.Skip)]
    // a player is already open / a popout is in flight => Skip (single-player, ADR-0005)
    [InlineData(true,  true,  true,  A,    null, true,  AutoPopDecision.Skip)]
    // same id as last handled: return-resume, in-source pause/resume, or the just-enabled current => Skip
    [InlineData(true,  true,  true,  A,    A,    false, AutoPopDecision.Skip)]
    public void Decide_pops_only_a_new_watch_video_playing_with_no_active_player(
        bool autoEnabled, bool isPlaying, bool isWatchVideo,
        string? currentVideoId, string? lastHandledVideoId, bool popoutActive,
        AutoPopDecision expected)
    {
        Assert.Equal(expected, AutoPopoutPolicy.Decide(
            autoEnabled, isPlaying, isWatchVideo, currentVideoId, lastHandledVideoId, popoutActive));
    }

    [Theory]
    [InlineData(true,  true,  A,    null, false, true)]
    [InlineData(true,  true,  A,    B,    false, true)]
    [InlineData(false, true,  A,    null, false, false)]
    [InlineData(true,  false, A,    null, false, false)]
    [InlineData(true,  true,  null, null, false, false)]
    [InlineData(true,  true,  "",   null, false, false)]
    [InlineData(true,  true,  A,    A,    false, false)]
    [InlineData(true,  true,  A,    null, true,  false)]
    public void NeedsPlayerState_rejects_skip_cases_before_the_WebView_probe(
        bool autoEnabled, bool isWatchVideo, string? currentVideoId,
        string? lastHandledVideoId, bool popoutActive, bool expected)
    {
        Assert.Equal(expected, AutoPopoutPolicy.NeedsPlayerState(
            autoEnabled, isWatchVideo, currentVideoId, lastHandledVideoId, popoutActive));
    }
}

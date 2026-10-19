using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PlayerShellErrorPolicyTests
{
    // --- Code -> user-facing message (spec 10.3 / Q-6) ---

    [Theory]
    [InlineData("101", "embedded")]
    [InlineData("150", "embedded")]   // documented as identical to 101
    [InlineData("100", "unavailable")]
    [InlineData("2", "isn't valid")]
    [InlineData("5", "can't play")]
    [InlineData(null, "error")]
    [InlineData("unknown", "error")]
    [InlineData("9999", "error")]
    [InlineData("", "error")]
    public void Describe_maps_documented_and_unknown_iframe_api_error_codes(string? code, string expectedPhrase)
    {
        var message = PlayerShellErrorPolicy.Describe(code);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Contains(expectedPhrase, message);
    }

    [Fact]
    public void Every_known_code_message_is_distinct_from_the_generic_one()
    {
        var generic = PlayerShellErrorPolicy.Describe("unknown");
        Assert.NotEqual(generic, PlayerShellErrorPolicy.Describe(PlayerShellErrorPolicy.CodeEmbedDisabled));
        Assert.NotEqual(generic, PlayerShellErrorPolicy.Describe(PlayerShellErrorPolicy.CodeNotFound));
        Assert.NotEqual(generic, PlayerShellErrorPolicy.Describe(PlayerShellErrorPolicy.CodeInvalidParam));
        Assert.NotEqual(generic, PlayerShellErrorPolicy.Describe(PlayerShellErrorPolicy.CodeHtml5Error));
    }

    // --- Auto-dismiss on recovery ---

    [Fact]
    public void Playing_state_auto_dismisses()
    {
        Assert.True(PlayerShellErrorPolicy.ShouldAutoDismiss(PlayerShellErrorPolicy.StatePlaying));
    }

    [Theory]
    [InlineData(-1)]  // unstarted
    [InlineData(0)]   // ended
    [InlineData(2)]   // paused
    [InlineData(3)]   // buffering
    [InlineData(5)]   // cued
    public void Non_playing_states_do_not_auto_dismiss(int playerState)
    {
        Assert.False(PlayerShellErrorPolicy.ShouldAutoDismiss(playerState));
    }

    // --- Ready timeout ---

    [Fact]
    public void Ready_timeout_is_generous_but_bounded()
    {
        // Must outlast a slow cold start (>= 10s) yet still fire within a usable wait (<= 60s).
        Assert.InRange(PlayerShellErrorPolicy.ReadyTimeout,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60));
    }
}

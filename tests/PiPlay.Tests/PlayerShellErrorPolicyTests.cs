using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PlayerShellErrorPolicyTests
{
    // --- Code -> user-facing message (spec 10.3 / Q-6) ---

    [Theory]
    [InlineData("101")]
    [InlineData("150")]   // documented as identical to 101
    public void Embed_disabled_codes_describe_embedding_restriction(string code)
    {
        Assert.Contains("embedded", PlayerShellErrorPolicy.Describe(code));
    }

    [Fact]
    public void Not_found_describes_unavailable_video()
    {
        Assert.Contains("unavailable", PlayerShellErrorPolicy.Describe("100"));
    }

    [Fact]
    public void Invalid_param_describes_invalid_reference()
    {
        Assert.Contains("isn't valid", PlayerShellErrorPolicy.Describe("2"));
    }

    [Fact]
    public void Html5_error_describes_playback_problem()
    {
        Assert.Contains("can't play", PlayerShellErrorPolicy.Describe("5"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    [InlineData("9999")]
    [InlineData("")]
    public void Unknown_codes_get_a_generic_but_meaningful_message(string? code)
    {
        var message = PlayerShellErrorPolicy.Describe(code);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Contains("error", message);
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

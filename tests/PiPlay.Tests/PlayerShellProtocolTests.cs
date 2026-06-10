using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PlayerShellProtocolTests
{
    // --- Inbound (shell -> host) parsing ---

    [Fact]
    public void Parses_ready()
    {
        var msg = PlayerShellProtocol.Parse("{\"v\":1,\"type\":\"ready\"}");
        Assert.Equal(ShellMessageKind.Ready, msg.Kind);
    }

    [Fact]
    public void Parses_state_fields()
    {
        var msg = PlayerShellProtocol.Parse(
            "{\"v\":1,\"type\":\"state\",\"currentTime\":42,\"playerState\":1,\"duration\":300}");
        Assert.Equal(ShellMessageKind.State, msg.Kind);
        Assert.Equal(42, msg.CurrentTime);
        Assert.Equal(1, msg.PlayerState);
        Assert.Equal(300, msg.Duration);
    }

    [Fact]
    public void Parses_state_with_missing_fields_using_safe_defaults()
    {
        var msg = PlayerShellProtocol.Parse("{\"v\":1,\"type\":\"state\"}");
        Assert.Equal(ShellMessageKind.State, msg.Kind);
        Assert.Equal(0, msg.CurrentTime);
        Assert.Equal(-1, msg.PlayerState);   // YT "unstarted"
        Assert.Null(msg.Duration);           // live/unknown duration stays null
        Assert.Null(msg.VideoId);            // pre-v3 senders never carry it (overhaul Task 3)
    }

    [Fact]
    public void Parses_state_video_id_when_present()
    {
        // Protocol v3 (overhaul Task 3): the shell reports the CURRENT video so playlist
        // auto-advance and in-iframe clicks survive into the return state.
        var msg = PlayerShellProtocol.Parse(
            "{\"v\":3,\"type\":\"state\",\"currentTime\":42,\"playerState\":1,\"videoId\":\"dQw4w9WgXcQ\"}");
        Assert.Equal(ShellMessageKind.State, msg.Kind);
        Assert.Equal("dQw4w9WgXcQ", msg.VideoId);
    }

    [Fact]
    public void Parses_error_code()
    {
        var msg = PlayerShellProtocol.Parse("{\"v\":1,\"type\":\"error\",\"code\":\"150\"}");
        Assert.Equal(ShellMessageKind.Error, msg.Kind);
        Assert.Equal("150", msg.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]                       // not an object
    [InlineData("\"a string\"")]                  // not an object
    [InlineData("{\"v\":1}")]                     // no type
    [InlineData("{\"type\":42}")]                 // non-string type
    [InlineData("{\"v\":1,\"type\":\"frobnicate\"}")] // unknown type
    public void Malformed_or_unknown_messages_parse_to_unknown(string? json)
    {
        Assert.Equal(ShellMessageKind.Unknown, PlayerShellProtocol.Parse(json).Kind);
    }

    [Fact]
    public void Recognized_type_with_wrong_typed_fields_degrades_to_defaults_not_a_throw()
    {
        // The ValueKind guards must keep "never throws; malformed -> safe default": a wrong-typed
        // PRESENT field (string/bool/array where a number is expected) falls back, it does not crash.
        var state = PlayerShellProtocol.Parse(
            "{\"v\":1,\"type\":\"state\",\"currentTime\":\"x\",\"playerState\":true,\"duration\":[1]}");
        Assert.Equal(ShellMessageKind.State, state.Kind);
        Assert.Equal(0, state.CurrentTime);
        Assert.Equal(-1, state.PlayerState);
        Assert.Null(state.Duration);

        // A numeric error code is not a string, so it degrades to null (no exception).
        var error = PlayerShellProtocol.Parse("{\"v\":1,\"type\":\"error\",\"code\":150}");
        Assert.Equal(ShellMessageKind.Error, error.Kind);
        Assert.Null(error.ErrorCode);
    }

    // --- Request kind (Phase 4): allowlisted shell -> host window actions ---

    [Theory]
    [InlineData("close")]
    [InlineData("pinToggle")]
    [InlineData("fullscreenToggle")]
    public void Parses_allowlisted_request_actions(string action)
    {
        var msg = PlayerShellProtocol.Parse($"{{\"v\":2,\"type\":\"request\",\"action\":\"{action}\"}}");
        Assert.Equal(ShellMessageKind.Request, msg.Kind);
        Assert.Equal(action, msg.Action);
    }

    [Theory]
    [InlineData("{\"v\":2,\"type\":\"request\"}")]                           // no action at all
    [InlineData("{\"v\":2,\"type\":\"request\",\"action\":\"minimize\"}")]   // off-allowlist action
    [InlineData("{\"v\":2,\"type\":\"request\",\"action\":\"Close\"}")]      // exact tokens only (case)
    [InlineData("{\"v\":2,\"type\":\"request\",\"action\":42}")]             // wrong-typed action
    [InlineData("{\"v\":2,\"type\":\"request\",\"action\":\"\"}")]           // empty action
    public void Off_allowlist_requests_degrade_to_unknown(string json)
    {
        // The closed set is the security property (design 2026-06-10 §2): an injected or future
        // action must die at the parse layer, before any host handler can see it.
        Assert.Equal(ShellMessageKind.Unknown, PlayerShellProtocol.Parse(json).Kind);
    }

    // --- Outbound (host -> shell) command shapes ---

    [Theory]
    [InlineData("play")]
    [InlineData("pause")]
    [InlineData("requestState")]
    public void Commands_carry_the_version_and_type(string type)
    {
        var json = type switch
        {
            "play" => PlayerShellProtocol.Play(),
            "pause" => PlayerShellProtocol.Pause(),
            _ => PlayerShellProtocol.RequestState(),
        };
        Assert.Contains("\"v\":" + PlayerShellProtocol.Version, json);
        Assert.Contains("\"type\":\"" + type + "\"", json);

        // Round-trippable as a valid command object, and not mistaken for an inbound message.
        Assert.Equal(ShellMessageKind.Unknown, PlayerShellProtocol.Parse(json).Kind);
    }

    [Fact]
    public void Seek_includes_seconds_and_clamps_negative()
    {
        Assert.Contains("\"type\":\"seek\"", PlayerShellProtocol.Seek(90));
        Assert.Contains("\"seconds\":90", PlayerShellProtocol.Seek(90));
        Assert.Contains("\"seconds\":0", PlayerShellProtocol.Seek(-5));   // never seek to a negative time
    }
}

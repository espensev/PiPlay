using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PlayerSurfaceProtocolTests
{
    private const string Nonce = "0123456789abcdef0123456789abcdef";
    private const string DocumentToken = "fedcba9876543210fedcba9876543210";
    private const string YouTube = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    [Fact]
    public void Drag_protocol_accepts_only_the_exact_nonce_and_document_token_youtube_message()
    {
        var valid = Drag(DocumentToken);

        Assert.True(PlayerSurfaceDragProtocol.TryParse(valid, YouTube, YouTube, Nonce, DocumentToken));
        Assert.False(PlayerSurfaceDragProtocol.TryParse(valid, YouTube, YouTube, "wrong", DocumentToken));
        Assert.False(PlayerSurfaceDragProtocol.TryParse(valid, YouTube, YouTube, Nonce, "wrong"));
        Assert.False(PlayerSurfaceDragProtocol.TryParse(valid.Replace("dragStart", "close"), YouTube, YouTube, Nonce, DocumentToken));
        Assert.False(PlayerSurfaceDragProtocol.TryParse(valid.Replace("\"v\":1", "\"v\":2"), YouTube, YouTube, Nonce, DocumentToken));
        Assert.False(PlayerSurfaceDragProtocol.TryParse("not-json", YouTube, YouTube, Nonce, DocumentToken));
        Assert.False(PlayerSurfaceDragProtocol.TryParse(
            valid.Replace("}", ",\"unexpected\":true}"), YouTube, YouTube, Nonce, DocumentToken));
        Assert.False(PlayerSurfaceDragProtocol.TryParse(
            valid.Replace("}", $",\"nonce\":\"{Nonce}\"}}"), YouTube, YouTube, Nonce, DocumentToken));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=x", true)]
    [InlineData("https://www.youtube-nocookie.com/embed/x", true)]
    [InlineData("http://www.youtube.com/watch?v=x", false)]
    [InlineData("https://accounts.google.com/", false)]
    [InlineData("https://piplay.local/player.html", false)]
    [InlineData("https://youtube.com.evil.test/watch?v=x", false)]
    public void Drag_protocol_source_gate_is_precise(string source, bool expected)
    {
        Assert.Equal(expected, PlayerSurfaceDragProtocol.IsTrustedYouTubeSource(source));
    }

    [Fact]
    public void Drag_protocol_rejects_a_delayed_message_after_the_player_navigates_away()
    {
        var valid = Drag(DocumentToken);
        Assert.False(PlayerSurfaceDragProtocol.TryParse(
            valid, YouTube, "https://accounts.google.com/signin", Nonce, DocumentToken));
    }

    [Fact]
    public void Page_protocols_reject_a_delayed_message_from_a_different_youtube_document()
    {
        const string replacement = "https://www.youtube.com/watch?v=BBBBBBBBBBB";
        var drag = Drag(DocumentToken);
        var focused = Focused(PlayerFirstSurfaceProtocol.ActionClose);
        var state = FocusedState(active: true, DocumentToken);

        Assert.False(PlayerSurfaceDragProtocol.TryParse(drag, YouTube, replacement, Nonce, DocumentToken));
        Assert.False(PlayerFirstSurfaceProtocol.TryParse(
            focused, YouTube, replacement, Nonce, DocumentToken, out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParseState(
            state, YouTube, replacement, Nonce, DocumentToken, out _));
    }

    [Fact]
    public void Page_protocol_source_identity_ignores_only_the_same_document_fragment()
    {
        const string messageSource = "https://www.youtube.com/watch?v=dQw4w9WgXcQ#player";
        const string currentSource = "https://www.youtube.com/watch?v=dQw4w9WgXcQ#controls";
        var drag = Drag(DocumentToken);

        Assert.True(PlayerSurfaceDragProtocol.TryParse(
            drag, messageSource, currentSource, Nonce, DocumentToken));
    }

    [Fact]
    public void Page_protocols_reject_the_previous_document_token_after_an_exact_url_reload()
    {
        const string previousDocumentToken = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        Assert.False(PlayerSurfaceDragProtocol.TryParse(
            Drag(previousDocumentToken), YouTube, YouTube, Nonce, DocumentToken));
        Assert.False(PlayerFirstSurfaceProtocol.TryParse(
            Focused(PlayerFirstSurfaceProtocol.ActionClose, previousDocumentToken),
            YouTube, YouTube, Nonce, DocumentToken, out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParseState(
            FocusedState(active: true, previousDocumentToken),
            YouTube, YouTube, Nonce, DocumentToken, out _));

        Assert.True(PlayerSurfaceDragProtocol.TryParse(
            Drag(DocumentToken), YouTube, YouTube, Nonce, DocumentToken));
    }

    [Fact]
    public void Focused_appearance_updates_are_single_flight_and_latest_wins()
    {
        var first = new PlayerFirstSurfaceAppearance("#111111", true, 1000, false);
        var middle = new PlayerFirstSurfaceAppearance("#222222", false, 2000, false);
        var latest = new PlayerFirstSurfaceAppearance("#333333", true, 3000, true);
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<PlayerFirstSurfaceAppearance>();
        var callLock = new object();

        using var pump = new PlayerFirstSurfaceAppearancePump(config =>
        {
            int count;
            lock (callLock)
            {
                calls.Add(config);
                count = calls.Count;
            }
            return count == 1 ? firstGate.Task : Task.CompletedTask;
        }, _ => { });

        pump.NavigationCompleted(succeeded: true);
        pump.Enqueue(first);
        pump.Enqueue(middle);
        pump.Enqueue(latest);

        lock (callLock) Assert.Equal(new[] { first }, calls);
        firstGate.SetResult();
        Assert.True(SpinWait.SpinUntil(() => !pump.IsRunningForTests, TimeSpan.FromSeconds(2)));
        lock (callLock) Assert.Equal(new[] { first, latest }, calls);
    }

    [Fact]
    public void Focused_appearance_updates_dedupe_identical_values_and_log_failure_once()
    {
        var first = new PlayerFirstSurfaceAppearance("#111111", true, 1000, false);
        var second = new PlayerFirstSurfaceAppearance("#222222", false, 2000, true);
        var calls = 0;
        var failures = 0;
        using var pump = new PlayerFirstSurfaceAppearancePump(
            _ =>
            {
                calls++;
                return Task.FromException(new InvalidOperationException("document unavailable"));
            },
            _ => failures++);

        pump.NavigationCompleted(succeeded: true);
        pump.Enqueue(first);
        pump.Enqueue(first);
        pump.Enqueue(second);

        Assert.Equal(2, calls);
        Assert.Equal(1, failures);
    }

    [Fact]
    public void Focused_appearance_update_waits_for_the_replacement_navigation()
    {
        var oldDocument = new PlayerFirstSurfaceAppearance("#111111", true, 1000, false);
        var newDocument = new PlayerFirstSurfaceAppearance("#222222", false, 2000, true);
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<PlayerFirstSurfaceAppearance>();
        var callLock = new object();
        using var pump = new PlayerFirstSurfaceAppearancePump(config =>
        {
            int count;
            lock (callLock)
            {
                calls.Add(config);
                count = calls.Count;
            }
            return count == 1 ? firstGate.Task : Task.CompletedTask;
        }, _ => { });

        pump.NavigationCompleted(succeeded: true);
        pump.Enqueue(oldDocument);
        pump.NavigationStarting();
        pump.Enqueue(newDocument);
        firstGate.SetResult();

        Assert.True(SpinWait.SpinUntil(() => !pump.IsRunningForTests, TimeSpan.FromSeconds(2)));
        lock (callLock) Assert.Equal(new[] { oldDocument }, calls);

        pump.NavigationCompleted(succeeded: true);
        lock (callLock) Assert.Equal(new[] { oldDocument, newDocument }, calls);
    }

    [Fact]
    public void Focused_appearance_update_drops_pending_work_after_disposal()
    {
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var pump = new PlayerFirstSurfaceAppearancePump(_ =>
        {
            var count = Interlocked.Increment(ref calls);
            return count == 1 ? firstGate.Task : Task.CompletedTask;
        }, _ => { });

        pump.NavigationCompleted(succeeded: true);
        pump.Enqueue(new PlayerFirstSurfaceAppearance("#111111", true, 1000, false));
        pump.Enqueue(new PlayerFirstSurfaceAppearance("#222222", false, 2000, true));
        pump.Dispose();
        firstGate.SetResult();

        Assert.True(SpinWait.SpinUntil(() => !pump.IsRunningForTests, TimeSpan.FromSeconds(2)));
        Assert.Equal(1, Volatile.Read(ref calls));
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    public void Native_drag_gate_requires_live_normal_window_and_pressed_button(
        bool closing, bool normal, bool leftDown, bool expected)
    {
        Assert.Equal(expected, PlayerSurfaceDragPolicy.CanBegin(closing, normal, leftDown));
    }

    [Fact]
    public void Focused_protocol_accepts_only_its_four_allowlisted_actions()
    {
        foreach (var action in PlayerFirstSurfaceProtocol.AllowedActions)
        {
            var json = Focused(action);
            Assert.True(PlayerFirstSurfaceProtocol.TryParse(
                json, YouTube, YouTube, Nonce, DocumentToken, out var parsed));
            Assert.Equal(action, parsed);
        }

        Assert.False(PlayerFirstSurfaceProtocol.TryParse(Focused("openDevTools"), YouTube, YouTube, Nonce, DocumentToken, out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParse(Focused("close"), "https://evil.test", YouTube, Nonce, DocumentToken, out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParse(Focused("close"), YouTube, YouTube, "wrong", DocumentToken, out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParse(Focused("close"), YouTube, YouTube, Nonce, "wrong", out _));
    }

    [Fact]
    public void Drag_message_does_not_parse_as_a_focused_window_action()
    {
        var drag = Drag(DocumentToken);
        Assert.False(PlayerFirstSurfaceProtocol.TryParse(
            drag, YouTube, YouTube, Nonce, DocumentToken, out _));
    }

    [Fact]
    public void Focused_protocol_rejects_unknown_and_duplicate_properties()
    {
        var request = Focused(PlayerFirstSurfaceProtocol.ActionClose);
        var state = FocusedState(active: true, DocumentToken);

        Assert.False(PlayerFirstSurfaceProtocol.TryParse(
            request.Replace("}", ",\"unexpected\":true}"), YouTube, YouTube, Nonce, DocumentToken, out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParse(
            request.Replace("}", ",\"action\":\"close\"}"), YouTube, YouTube, Nonce, DocumentToken, out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParseState(
            state.Replace("}", ",\"unexpected\":true}"), YouTube, YouTube, Nonce, DocumentToken, out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParseState(
            state.Replace("}", ",\"active\":true}"), YouTube, YouTube, Nonce, DocumentToken, out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Focused_surface_state_requires_the_closed_nonce_and_youtube_source(bool active)
    {
        var state = FocusedState(active, DocumentToken);

        Assert.True(PlayerFirstSurfaceProtocol.TryParseState(
            state, YouTube, YouTube, Nonce, DocumentToken, out var parsed));
        Assert.Equal(active, parsed);
        Assert.False(PlayerFirstSurfaceProtocol.TryParseState(
            state, YouTube, YouTube, "wrong", DocumentToken, out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParseState(
            state, "https://evil.test", YouTube, Nonce, DocumentToken, out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParseState(
            state, YouTube, YouTube, Nonce, "wrong", out _));
        Assert.False(PlayerFirstSurfaceProtocol.TryParse(
            state, YouTube, YouTube, Nonce, DocumentToken, out _));
    }

    private static string Drag(string documentToken) =>
        $$"""{"channel":"{{PlayerSurfaceDragProtocol.Channel}}","v":1,"type":"dragStart","nonce":"{{Nonce}}","documentToken":"{{documentToken}}"}""";

    private static string Focused(string action, string documentToken = DocumentToken) =>
        $$"""{"channel":"{{PlayerFirstSurfaceProtocol.Channel}}","v":1,"type":"request","nonce":"{{Nonce}}","documentToken":"{{documentToken}}","action":"{{action}}"}""";

    private static string FocusedState(bool active, string documentToken) =>
        $$"""{"channel":"{{PlayerFirstSurfaceProtocol.Channel}}","v":1,"type":"state","nonce":"{{Nonce}}","documentToken":"{{documentToken}}","active":{{active.ToString().ToLowerInvariant()}}}""";
}

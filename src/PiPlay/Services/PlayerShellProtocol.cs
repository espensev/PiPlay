using System.Text.Json;

namespace PiPlay.Services;

/// <summary>Kind of an inbound (shell -> host) message; <see cref="Unknown"/> for anything unrecognized.</summary>
public enum ShellMessageKind
{
    Unknown,
    Ready,
    State,
    Error,
    Request,
}

/// <summary>A parsed inbound message from the compact shell (spec 10.3). Nullable/defaulted fields
/// carry only what the <see cref="ShellMessageKind"/> defines; the rest stay at safe defaults.</summary>
public sealed record InboundShellMessage(
    ShellMessageKind Kind,
    int CurrentTime = 0,
    int PlayerState = -1,
    int? Duration = null,
    string? ErrorCode = null,
    string? Action = null,
    string? VideoId = null);

/// <summary>
/// The pure, versioned host&lt;-&gt;shell message contract for compact (PiPlay shell) mode
/// (spec 10.3). The single source of truth both <see cref="PlayerShellBridge"/> (host) and
/// <c>player-shell.js</c> (shell) follow. Minimal and local-only: the shell reports ready / state /
/// error; the host commands play / pause / seek / requestState. No credentials, cookies, or tokens
/// ever cross this channel. All parsing is best-effort (Q-3/Q-6): malformed input yields
/// <see cref="ShellMessageKind.Unknown"/>, never an exception.
/// </summary>
public static class PlayerShellProtocol
{
    // v3: state messages additionally carry the current videoId (overhaul Task 3 — the shell can
    // move off its launch video via playlist auto-advance or in-iframe clicks, and the host needs
    // the CURRENT video for return). Additive and parse-compatible; v2 senders simply yield null.
    public const int Version = 3;

    // Shell -> host message types.
    public const string TypeReady = "ready";
    public const string TypeState = "state";
    public const string TypeError = "error";
    public const string TypeRequest = "request";

    // Host -> shell command types.
    public const string TypePlay = "play";
    public const string TypePause = "pause";
    public const string TypeSeek = "seek";
    public const string TypeRequestState = "requestState";

    // Wire field names — the single source of truth for the JS<->host payload contract. Both this
    // parser and player-shell.js use these exact names; PlayerShellAssetTests pins them in the JS.
    public const string KeyVersion = "v";
    public const string KeyType = "type";
    public const string FieldCurrentTime = "currentTime";
    public const string FieldPlayerState = "playerState";
    public const string FieldDuration = "duration";
    public const string FieldCode = "code";
    public const string FieldSeconds = "seconds";
    public const string FieldAction = "action";
    public const string FieldVideoId = "videoId";

    // Allowlisted shell -> host window actions (Phase 4, design 2026-06-10 §2): the shell may
    // REQUEST these; the host validates against this closed set and maps each to the existing
    // native handler. Anything else parses to Unknown — never an exception, never a new
    // capability. Both sides allowlist (player-shell.js mirrors this set).
    public const string ActionClose = "close";
    public const string ActionPinToggle = "pinToggle";
    public const string ActionFullscreenToggle = "fullscreenToggle";

    /// <summary>Parse one inbound JSON string from the shell. Never throws; unrecognized -> Unknown.</summary>
    public static InboundShellMessage Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new InboundShellMessage(ShellMessageKind.Unknown);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new InboundShellMessage(ShellMessageKind.Unknown);
            if (!root.TryGetProperty(KeyType, out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
                return new InboundShellMessage(ShellMessageKind.Unknown);

            return typeEl.GetString() switch
            {
                TypeReady => new InboundShellMessage(ShellMessageKind.Ready),
                TypeState => new InboundShellMessage(
                    ShellMessageKind.State,
                    CurrentTime: ReadInt(root, FieldCurrentTime, 0),
                    PlayerState: ReadInt(root, FieldPlayerState, -1),
                    Duration: ReadNullableInt(root, FieldDuration),
                    VideoId: ReadVideoId(root)),
                TypeError => new InboundShellMessage(ShellMessageKind.Error, ErrorCode: ReadString(root, FieldCode)),
                TypeRequest => ParseRequest(root),
                _ => new InboundShellMessage(ShellMessageKind.Unknown),
            };
        }
        catch (JsonException)
        {
            return new InboundShellMessage(ShellMessageKind.Unknown);
        }
    }

    /// <summary>Host -> shell: resume playback.</summary>
    public static string Play() => Command(TypePlay);

    /// <summary>Host -> shell: pause playback.</summary>
    public static string Pause() => Command(TypePause);

    /// <summary>Host -> shell: request an immediate state message.</summary>
    public static string RequestState() => Command(TypeRequestState);

    /// <summary>Host -> shell: seek to <paramref name="seconds"/> (clamped to non-negative).</summary>
    public static string Seek(int seconds) =>
        $"{{\"{KeyVersion}\":{Version},\"{KeyType}\":\"{TypeSeek}\",\"{FieldSeconds}\":{Math.Max(0, seconds)}}}";

    private static string Command(string type) => $"{{\"{KeyVersion}\":{Version},\"{KeyType}\":\"{type}\"}}";

    /// <summary>A request is only a request when its action is on the allowlist; anything else
    /// degrades to Unknown so an injected or future action can never reach a host handler.</summary>
    private static InboundShellMessage ParseRequest(JsonElement root)
    {
        var action = ReadString(root, FieldAction);
        return action is ActionClose or ActionPinToggle or ActionFullscreenToggle
            ? new InboundShellMessage(ShellMessageKind.Request, Action: action)
            : new InboundShellMessage(ShellMessageKind.Unknown);
    }

    private static int ReadInt(JsonElement root, string name, int fallback) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v)
            ? v : fallback;

    private static int? ReadNullableInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v)
            ? v : null;

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    /// <summary>
    /// FieldVideoId carries a YouTube video id BY CONTRACT: a value that is not the 11-char id
    /// shape is malformed input from the (untrusted) shell and parses as absent — the gate lives
    /// in the parser so EVERY consumer of the parsed message is protected, not just the current
    /// one (the host later turns this string into a source navigation target on close).
    /// </summary>
    private static string? ReadVideoId(JsonElement root)
    {
        var raw = ReadString(root, FieldVideoId);
        return YouTubeUrlHelper.IsVideoId(raw) ? raw : null;
    }
}

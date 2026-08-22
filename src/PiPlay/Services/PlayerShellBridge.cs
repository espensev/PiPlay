using Microsoft.Web.WebView2.Core;

namespace PiPlay.Services;

/// <summary>
/// Host side of the compact (PiPlay shell) playback bridge. Wraps the single
/// CoreWebView2 hosting the local shell: it parses inbound shell messages via
/// <see cref="PlayerShellProtocol"/> and raises typed events, and posts host commands to the shell.
/// In compact mode the shell (YouTube IFrame API), not the YouTube page DOM, is the source of truth
/// for the timestamp/state used on return; normal mode keeps <see cref="YouTubeDomBridge"/>.
/// Best-effort throughout (Q-3/Q-6): a malformed message or a failed post is swallowed and logged,
/// never thrown.
/// </summary>
public sealed class PlayerShellBridge : IDisposable
{
    private readonly CoreWebView2 _core;
    private bool _disposed;

    /// <summary>Raised when the shell's player has loaded and is ready.</summary>
    public event EventHandler? Ready;

    /// <summary>Raised on each shell state update (current time / player state / duration).</summary>
    public event EventHandler<InboundShellMessage>? StateReceived;

    /// <summary>Raised when the shell reports a player error (embed-disabled, unavailable, etc.).</summary>
    public event EventHandler<InboundShellMessage>? ErrorReceived;

    /// <summary>Raised for an allowlisted shell window-action request (close / pinToggle /
    /// fullscreenToggle). Non-allowlisted actions never get here — they parse to Unknown.</summary>
    public event EventHandler<InboundShellMessage>? RequestReceived;

    public PlayerShellBridge(CoreWebView2 core)
    {
        _core = core;
        _core.WebMessageReceived += OnWebMessageReceived;
    }

    /// <summary>Host -> shell: resume playback.</summary>
    public void Play() => Post(PlayerShellProtocol.Play());

    /// <summary>Host -> shell: pause playback.</summary>
    public void Pause() => Post(PlayerShellProtocol.Pause());

    /// <summary>Host -> shell: seek to <paramref name="seconds"/>.</summary>
    public void Seek(int seconds) => Post(PlayerShellProtocol.Seek(seconds));

    /// <summary>Host -> shell: request an immediate state message.</summary>
    public void RequestState() => Post(PlayerShellProtocol.RequestState());

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string? json;
        try { json = e.TryGetWebMessageAsString(); }
        catch { return; } // a non-string payload is not part of our protocol

        var message = PlayerShellProtocol.Parse(json);
        switch (message.Kind)
        {
            case ShellMessageKind.Ready:
                Ready?.Invoke(this, EventArgs.Empty);
                break;
            case ShellMessageKind.State:
                StateReceived?.Invoke(this, message);
                break;
            case ShellMessageKind.Error:
                Log.Info($"Compact shell reported a player error (code={message.ErrorCode ?? "unknown"}).");
                ErrorReceived?.Invoke(this, message);
                break;
            case ShellMessageKind.Request:
                Log.Info($"Compact shell requested a window action ({message.Action}).");
                RequestReceived?.Invoke(this, message);
                break;
        }
    }

    private void Post(string json)
    {
        if (_disposed) return;
        try { _core.PostWebMessageAsString(json); }
        catch (Exception ex) { Log.Error("Failed to post a command to the compact shell.", ex); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _core.WebMessageReceived -= OnWebMessageReceived; } catch { /* ignore */ }
    }
}

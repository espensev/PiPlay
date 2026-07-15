using Microsoft.Web.WebView2.Core;

namespace PiPlay.Services;

internal readonly record struct PlayerFirstSurfaceAppearance(
    string AccentColor,
    bool FadeEnabled,
    int FadeDelayMs,
    bool Topmost);

/// <summary>
/// Serializes Focused-surface configuration IPC. While one WebView call is in flight, subsequent
/// previews replace one pending value; a navigation suspends delivery and replays only the latest
/// value into the replacement document.
/// </summary>
internal sealed class PlayerFirstSurfaceAppearancePump : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<PlayerFirstSurfaceAppearance, Task> _applyAsync;
    private readonly Action<Exception> _reportFailure;
    private PlayerFirstSurfaceAppearance _latest;
    private long _latestVersion;
    private long _processedVersion;
    private int _navigationGeneration;
    private bool _hasLatest;
    private bool _navigationReady;
    private bool _running;
    private bool _disposed;
    private bool _failureReported;

    internal PlayerFirstSurfaceAppearancePump(
        Func<PlayerFirstSurfaceAppearance, Task> applyAsync,
        Action<Exception> reportFailure)
    {
        _applyAsync = applyAsync;
        _reportFailure = reportFailure;
    }

    internal bool IsRunningForTests
    {
        get { lock (_gate) return _running; }
    }

    internal void Enqueue(PlayerFirstSurfaceAppearance appearance)
    {
        var start = false;
        lock (_gate)
        {
            if (_disposed) return;
            if (_hasLatest && _latest == appearance) return;

            _latest = appearance;
            _hasLatest = true;
            _latestVersion++;
            start = TryStartLocked();
        }

        if (start) _ = DrainAsync();
    }

    internal void NavigationStarting()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _navigationGeneration++;
            _navigationReady = false;
            // The value applied to the old document says nothing about the replacement document.
            _processedVersion = 0;
        }
    }

    internal void NavigationCompleted(bool succeeded)
    {
        var start = false;
        lock (_gate)
        {
            if (_disposed) return;
            _navigationReady = succeeded;
            if (succeeded) start = TryStartLocked();
        }

        if (start) _ = DrainAsync();
    }

    private bool TryStartLocked()
    {
        if (_running || _disposed || !_navigationReady || !_hasLatest
            || _processedVersion == _latestVersion)
        {
            return false;
        }

        _running = true;
        return true;
    }

    private async Task DrainAsync()
    {
        while (true)
        {
            PlayerFirstSurfaceAppearance appearance;
            long version;
            int navigationGeneration;
            lock (_gate)
            {
                if (_disposed || !_navigationReady || !_hasLatest
                    || _processedVersion == _latestVersion)
                {
                    _running = false;
                    return;
                }

                appearance = _latest;
                version = _latestVersion;
                navigationGeneration = _navigationGeneration;
            }

            Exception? failure = null;
            try { await _applyAsync(appearance); }
            catch (Exception ex) { failure = ex; }

            var reportFailure = false;
            var keepRunning = false;
            lock (_gate)
            {
                if (_disposed)
                {
                    _running = false;
                    return;
                }

                // An old document's completion must neither consume nor report work for the new one.
                if (_navigationReady && navigationGeneration == _navigationGeneration)
                {
                    _processedVersion = Math.Max(_processedVersion, version);
                    if (failure is not null && !_failureReported)
                    {
                        _failureReported = true;
                        reportFailure = true;
                    }
                }

                keepRunning = _navigationReady && _hasLatest
                    && _processedVersion != _latestVersion;
                if (!keepRunning) _running = false;
            }

            if (reportFailure)
            {
                try { _reportFailure(failure!); }
                catch { /* diagnostics must never break the appearance pump */ }
            }
            if (!keepRunning) return;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}

/// <summary>
/// WebView2 lifecycle and security boundary for the optional Focused watch-page presentation.
/// The page owns media controls; this bridge accepts only the closed nonce-and-document-token
/// Popout action set and keeps live appearance/state synchronized best-effort.
/// </summary>
internal sealed class PlayerFirstSurfaceBridge : IDisposable
{
    private readonly CoreWebView2 _core;
    private readonly string _nonce;
    private readonly PlayerFirstSurfaceAppearancePump _appearancePump;
    private ulong? _activeNavigationId;
    private int _documentGeneration;
    private string? _pendingDocumentToken;
    private string? _activeDocumentToken;
    private string? _scriptId;
    private bool _documentConfigurationFailureLogged;
    private bool _disposed;

    internal event EventHandler<string>? ActionRequested;
    internal event Action<bool>? ActiveChanged;

    private PlayerFirstSurfaceBridge(CoreWebView2 core, PlayerFirstSurfaceAppearance appearance)
    {
        _core = core;
        _nonce = Guid.NewGuid().ToString("N");
        _appearancePump = new PlayerFirstSurfaceAppearancePump(
            ApplyAppearanceAsync,
            ex => Log.Error("Failed to refresh the Focused popout surface.", ex));
        _appearancePump.Enqueue(appearance);
        _core.WebMessageReceived += Core_WebMessageReceived;
        _core.NavigationStarting += Core_NavigationStarting;
        _core.NavigationCompleted += Core_NavigationCompleted;
    }

    internal static async Task<PlayerFirstSurfaceBridge> CreateAsync(
        CoreWebView2 core,
        string accentColor,
        bool fadeEnabled,
        int fadeDelayMs,
        bool topmost)
    {
        var appearance = new PlayerFirstSurfaceAppearance(
            accentColor, fadeEnabled, fadeDelayMs, topmost);
        var bridge = new PlayerFirstSurfaceBridge(core, appearance);
        try
        {
            var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
                bridge._nonce, accentColor, fadeEnabled, fadeDelayMs, topmost);
            // Registration must finish before navigation so Focused layout is present for the
            // first watch document, not merely later SPA transitions.
            bridge._scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
            return bridge;
        }
        catch
        {
            bridge.Dispose();
            throw;
        }
    }

    internal void ApplyAppearance(
        string accentColor,
        bool fadeEnabled,
        int fadeDelayMs,
        bool topmost)
    {
        if (_disposed) return;
        _appearancePump.Enqueue(new PlayerFirstSurfaceAppearance(
            accentColor, fadeEnabled, fadeDelayMs, topmost));
    }

    private Task ApplyAppearanceAsync(PlayerFirstSurfaceAppearance appearance)
    {
        var script = YouTubeDomBridge.BuildPlayerFirstConfigurationScript(
            appearance.AccentColor, appearance.FadeEnabled,
            appearance.FadeDelayMs, appearance.Topmost);
        return _core.ExecuteScriptAsync(script);
    }

    private void Core_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // PlayerWindow's navigation policy is registered first and marks blocked navigations.
        if (e.Cancel) return;
        _documentGeneration++;
        _activeNavigationId = e.NavigationId;
        _activeDocumentToken = null;
        _pendingDocumentToken = Guid.NewGuid().ToString("N");
        _appearancePump.NavigationStarting();
    }

    private void Core_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_disposed || _activeNavigationId != e.NavigationId) return;
        _ = AuthorizeCompletedDocumentAsync(e.NavigationId, e.IsSuccess);
    }

    private async Task AuthorizeCompletedDocumentAsync(ulong navigationId, bool succeeded)
    {
        var generation = _documentGeneration;
        var token = _pendingDocumentToken;
        if (!succeeded || string.IsNullOrEmpty(token) || !IsCurrentTrustedDocument())
        {
            if (IsDocumentCurrent(navigationId, generation, token)) _pendingDocumentToken = null;
            _appearancePump.NavigationCompleted(succeeded: false);
            return;
        }

        try
        {
            var authorizationResult = await _core.ExecuteScriptAsync(
                YouTubeDomBridge.BuildPlayerFirstDocumentTokenScript(token));
            if (!IsDocumentCurrent(navigationId, generation, token)) return;
            if (!IsCurrentTrustedDocument() || !ScriptReturnedTrue(authorizationResult))
            {
                RevokeCurrentDocument();
                LogDocumentConfigurationFailureOnce(null);
                return;
            }

            // The authorization script itself never posts. Publish the host-side token only after
            // ExecuteScript confirms installation, then explicitly request the recovery handshake.
            _activeDocumentToken = token;
            var stateResult = await _core.ExecuteScriptAsync(
                YouTubeDomBridge.BuildPlayerFirstStateRequestScript());
            if (!IsDocumentCurrent(navigationId, generation, token)) return;
            if (!IsCurrentTrustedDocument() || !ScriptReturnedTrue(stateResult))
            {
                RevokeCurrentDocument();
                LogDocumentConfigurationFailureOnce(null);
                return;
            }

            _pendingDocumentToken = null;
            _appearancePump.NavigationCompleted(succeeded: true);
        }
        catch (Exception ex)
        {
            if (!IsDocumentCurrent(navigationId, generation, token)) return;
            // If authorization itself failed, revoke the capability. If only the state request
            // failed, revocation is still the conservative choice and recovery chrome stays live.
            RevokeCurrentDocument();
            LogDocumentConfigurationFailureOnce(ex);
        }
    }

    private bool IsDocumentCurrent(ulong navigationId, int generation, string? token) =>
        !_disposed
        && _activeNavigationId == navigationId
        && _documentGeneration == generation
        && !string.IsNullOrEmpty(token)
        && string.Equals(_pendingDocumentToken, token, StringComparison.Ordinal);

    private bool IsCurrentTrustedDocument()
    {
        try { return PlayerSurfaceDragProtocol.IsTrustedYouTubeSource(_core.Source); }
        catch { return false; }
    }

    private static bool ScriptReturnedTrue(string? result) =>
        string.Equals(result, "true", StringComparison.OrdinalIgnoreCase);

    private void RevokeCurrentDocument()
    {
        _activeDocumentToken = null;
        _pendingDocumentToken = null;
        _appearancePump.NavigationCompleted(succeeded: false);
    }

    private void LogDocumentConfigurationFailureOnce(Exception? exception)
    {
        if (_documentConfigurationFailureLogged || _disposed) return;
        _documentConfigurationFailureLogged = true;
        if (exception is null)
            Log.Error("Focused popout controls could not authorize the current document.");
        else
            Log.Error("Focused popout controls could not authorize the current document.", exception);
    }

    private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_disposed) return;

        string? json;
        string? currentSource;
        try
        {
            json = e.TryGetWebMessageAsString();
            currentSource = _core.Source;
        }
        catch
        {
            return;
        }

        if (PlayerFirstSurfaceProtocol.TryParse(
                json, e.Source, currentSource, _nonce, _activeDocumentToken,
                out var action) && action is not null)
        {
            ActionRequested?.Invoke(this, action);
            return;
        }

        if (PlayerFirstSurfaceProtocol.TryParseState(
                json, e.Source, currentSource, _nonce, _activeDocumentToken, out var active))
            ActiveChanged?.Invoke(active);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _documentGeneration++;
        _activeDocumentToken = null;
        _pendingDocumentToken = null;
        _disposed = true;
        _appearancePump.Dispose();
        try { _core.WebMessageReceived -= Core_WebMessageReceived; } catch { /* core teardown */ }
        try { _core.NavigationStarting -= Core_NavigationStarting; } catch { /* core teardown */ }
        try { _core.NavigationCompleted -= Core_NavigationCompleted; } catch { /* core teardown */ }
        if (_scriptId is not null)
        {
            try { _core.RemoveScriptToExecuteOnDocumentCreated(_scriptId); } catch { /* core teardown */ }
            _scriptId = null;
        }
    }
}

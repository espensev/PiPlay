using Microsoft.Web.WebView2.Core;

namespace PiPlay.Services;

/// <summary>
/// WebView2 lifecycle/security side of threshold dragging from passive YouTube player pixels.
/// The injected script observes gestures; this bridge validates top-document messages and exposes a
/// capability-free event. Native movement remains owned by <see cref="PlayerWindow"/>.
/// </summary>
internal sealed class PlayerSurfaceDragBridge : IDisposable
{
    private readonly CoreWebView2 _core;
    private readonly string _nonce;
    private ulong? _activeNavigationId;
    private int _documentGeneration;
    private string? _pendingDocumentToken;
    private string? _activeDocumentToken;
    private string? _scriptId;
    private bool _available;
    private bool _configurationFailureLogged;
    private bool _disposed;

    internal event EventHandler? DragRequested;
    internal event Action<bool>? AvailabilityChanged;

    private PlayerSurfaceDragBridge(CoreWebView2 core)
    {
        _core = core;
        _nonce = Guid.NewGuid().ToString("N");
        _core.WebMessageReceived += Core_WebMessageReceived;
        _core.NavigationStarting += Core_NavigationStarting;
        _core.NavigationCompleted += Core_NavigationCompleted;
    }

    internal static async Task<PlayerSurfaceDragBridge> CreateAsync(
        CoreWebView2 core,
        double horizontalThresholdDip,
        double verticalThresholdDip)
    {
        var bridge = new PlayerSurfaceDragBridge(core);
        try
        {
            var script = YouTubeDomBridge.BuildPassiveSurfaceDragScript(
                bridge._nonce, horizontalThresholdDip, verticalThresholdDip);
            // Await registration before navigating so the script is present when that document is created.
            bridge._scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
            return bridge;
        }
        catch
        {
            bridge.Dispose();
            throw;
        }
    }

    private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e) =>
        HandleMessage(e);

    private void Core_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // PlayerWindow's allowlist handler is registered first and marks rejected navigations.
        if (_disposed || e.Cancel) return;
        _documentGeneration++;
        _activeNavigationId = e.NavigationId;
        _activeDocumentToken = null;
        _pendingDocumentToken = Guid.NewGuid().ToString("N");
        SetAvailability(false);
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
            return;
        }

        try
        {
            var result = await _core.ExecuteScriptAsync(
                YouTubeDomBridge.BuildPassiveSurfaceDocumentTokenScript(token));
            if (!IsDocumentCurrent(navigationId, generation, token)) return;

            if (!IsCurrentTrustedDocument() || !ScriptReturnedTrue(result))
            {
                RevokeCurrentDocument();
                LogConfigurationFailureOnce(null);
                return;
            }

            _activeDocumentToken = token;
            _pendingDocumentToken = null;
            SetAvailability(true);
        }
        catch (Exception ex)
        {
            if (!IsDocumentCurrent(navigationId, generation, token)) return;
            RevokeCurrentDocument();
            LogConfigurationFailureOnce(ex);
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
        SetAvailability(false);
    }

    private void SetAvailability(bool available)
    {
        if (_available == available) return;
        _available = available;
        AvailabilityChanged?.Invoke(available);
    }

    private void LogConfigurationFailureOnce(Exception? exception)
    {
        if (_configurationFailureLogged || _disposed) return;
        _configurationFailureLogged = true;
        if (exception is null)
            Log.Error("Passive player-surface dragging could not authorize the current document.");
        else
            Log.Error("Passive player-surface dragging could not authorize the current document.", exception);
    }

    private void HandleMessage(CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_disposed) return;

        string? json;
        try { json = e.TryGetWebMessageAsString(); }
        catch { return; }

        string? currentSource;
        try { currentSource = _core.Source; }
        catch { return; }

        if (PlayerSurfaceDragProtocol.TryParse(
                json, e.Source, currentSource, _nonce, _activeDocumentToken))
            DragRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _documentGeneration++;
        _activeDocumentToken = null;
        _pendingDocumentToken = null;
        SetAvailability(false);
        _disposed = true;

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

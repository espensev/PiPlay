using System.IO;
using Microsoft.Web.WebView2.Core;

namespace PiPlay.Services;

/// <summary>
/// Owns the single shared <see cref="CoreWebView2Environment"/> and PiPlay's WebView2
/// user-data folder (spec 12.3, 15.1). Both the Source Window and the Popout Player
/// initialize against this one environment so YouTube login/session/cookies stay shared.
/// </summary>
public sealed class WebViewEnvironmentService
{
    private CoreWebView2Environment? _environment;

    public CoreWebView2Environment? Environment => _environment;

    /// <summary>
    /// Create (once) the shared environment. Throws
    /// <see cref="WebView2RuntimeNotFoundException"/> if the Evergreen runtime is missing;
    /// the caller surfaces a friendly install message and exits (spec 15.4, Q-6).
    /// </summary>
    public async Task<CoreWebView2Environment> EnsureCreatedAsync()
    {
        if (_environment is not null) return _environment;

        Directory.CreateDirectory(AppPaths.WebView2UserDataDir);

        // Allow the popped-out video to start playing at the handed-off timestamp without a
        // fresh user gesture (spec 4.2 timestamp sync). Applies to both shared WebViews.
        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required",
        };

        _environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,            // use the installed Evergreen runtime
            userDataFolder: AppPaths.WebView2UserDataDir,
            options: options);

        Log.Info("WebView2 environment created.");
        return _environment;
    }
}

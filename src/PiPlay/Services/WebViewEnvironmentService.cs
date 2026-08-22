using System.IO;
using Microsoft.Web.WebView2.Core;

namespace PiPlay.Services;

/// <summary>
/// Owns the single shared <see cref="CoreWebView2Environment"/> and PiPlay's WebView2
/// user-data folder. Both the Source Window and the Popout Player
/// initialize against this one environment so YouTube login/session/cookies stay shared.
/// </summary>
public sealed class WebViewEnvironmentService
{
    private CoreWebView2Environment? _environment;

    public CoreWebView2Environment? Environment => _environment;

    // --- Compact-player shell hosting ---

    /// <summary>Virtual-host name for the local compact shell. SSOT is <see cref="NavigationPolicy.ShellHost"/>
    /// so the navigated URL, the folder mapping, and the navigation allowlist can't drift.</summary>
    public static string ShellHost => NavigationPolicy.ShellHost;

    /// <summary>The shell's stable HTTPS origin (used for the IFrame API <c>origin</c> param).</summary>
    public static string ShellOrigin => "https://" + ShellHost;

    /// <summary>The compact shell page URL the Popout Player navigates to in compact mode.</summary>
    public static string ShellPlayerUrl => ShellOrigin + "/player.html";

    /// <summary>On-disk folder holding the shell assets (player.html, player-shell.js), copied beside
    /// the app at build (a dedicated subfolder — never the app dir itself, which would expose binaries).</summary>
    public static string ShellFolder => Path.Combine(AppContext.BaseDirectory, "PlayerShell");

    /// <summary>
    /// Map the compact shell folder to <see cref="ShellHost"/> on the given WebView so the Popout
    /// Player can navigate to <see cref="ShellPlayerUrl"/> with a stable HTTPS origin. Called once
    /// per compact Popout Player, before navigating. Uses <c>DenyCors</c>; the shell
    /// loads player-shell.js same-origin and frames YouTube via frame-src — neither needs
    /// cross-origin CORS access to the piplay.local resources, so that reach stays denied.
    /// </summary>
    public static void MapShellVirtualHost(CoreWebView2 core) =>
        core.SetVirtualHostNameToFolderMapping(
            ShellHost, ShellFolder, CoreWebView2HostResourceAccessKind.DenyCors);

    /// <summary>
    /// Create (once) the shared environment. Throws
    /// <see cref="WebView2RuntimeNotFoundException"/> if the Evergreen runtime is missing;
    /// the caller surfaces a friendly install message and exits (Q-6).
    /// </summary>
    public async Task<CoreWebView2Environment> EnsureCreatedAsync()
    {
        if (_environment is not null) return _environment;

        Directory.CreateDirectory(AppPaths.WebView2UserDataDir);

        // Allow the popped-out video to start playing at the handed-off timestamp without a
        // fresh user gesture. Applies to both shared WebViews.
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

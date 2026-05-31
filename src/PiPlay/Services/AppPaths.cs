using System.IO;

namespace PiPlay.Services;

/// <summary>Central definition of every on-disk location PiPlay uses (spec 11, 18, Data &amp; Privacy Map).</summary>
public static class AppPaths
{
    /// <summary>
    /// Data root. Honors the <c>PIPLAY_DATA_ROOT</c> environment variable when set (used by
    /// tests to stay out of the real user profile); otherwise %LOCALAPPDATA%\PiPlay. Computed
    /// per access so an override set at process start is always picked up.
    /// </summary>
    public static string Root =>
        Environment.GetEnvironmentVariable("PIPLAY_DATA_ROOT") is { Length: > 0 } overrideRoot
            ? overrideRoot
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiPlay");

    public static string LogsDir => Path.Combine(Root, "logs");
    public static string LogFile => Path.Combine(LogsDir, "piplay.log");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string WebView2UserDataDir => Path.Combine(Root, "WebView2UserData");
}

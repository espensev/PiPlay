using System.IO;

namespace PiPlay.Services;

/// <summary>Central definition of every on-disk location PiPlay uses (spec 11, 18, Data &amp; Privacy Map).</summary>
public static class AppPaths
{
    /// <summary>
    /// Data root, resolved per access (so an override set at process start is always picked up):
    /// <list type="number">
    /// <item><c>PIPLAY_DATA_ROOT</c> environment variable when set (tests/CI/manual override).</item>
    /// <item>Beside the exe (<c>&lt;baseDir&gt;\PiPlayData</c>) for a portable channel such as Stable,
    /// so a deployed copy is self-contained and isolated from the dev profile.</item>
    /// <item>Otherwise %LOCALAPPDATA%\PiPlay (the normal installed app — unchanged).</item>
    /// </list>
    /// </summary>
    public static string Root => ResolveRoot(
        Environment.GetEnvironmentVariable("PIPLAY_DATA_ROOT"),
        AppChannel.Current,
        AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    /// <summary>Pure data-root resolution (see <see cref="Root"/> for the precedence rules).</summary>
    internal static string ResolveRoot(string? envOverride, PiPlayChannel channel, string baseDirectory, string localAppData)
    {
        if (!string.IsNullOrEmpty(envOverride)) return envOverride;
        if (AppChannel.IsPortableChannel(channel)) return Path.Combine(baseDirectory, "PiPlayData");
        return Path.Combine(localAppData, "PiPlay");
    }

    public static string LogsDir => Path.Combine(Root, "logs");
    public static string LogFile => Path.Combine(LogsDir, "piplay.log");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string WebView2UserDataDir => Path.Combine(Root, "WebView2UserData");
}

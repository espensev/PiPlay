using System.IO;

namespace PiPlay.Services;

/// <summary>Central definition of every on-disk location PiPlay uses (spec 11, 18, Data &amp; Privacy Map).</summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiPlay");

    public static string LogsDir => Path.Combine(Root, "logs");
    public static string LogFile => Path.Combine(LogsDir, "piplay.log");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string WebView2UserDataDir => Path.Combine(Root, "WebView2UserData");
}

namespace PiPlay.Models;

/// <summary>
/// Root application settings (spec 12.6). Persisted atomically to
/// %LOCALAPPDATA%/PiPlay/settings.json. Browser session/cookies are NOT stored here -
/// the WebView2 user-data folder owns that.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string LastUrl { get; set; } = "https://www.youtube.com/";

    /// <summary>
    /// Auto (spec §6.1): automatically start a Video Popout when a /watch video is playing. Off by
    /// default; a missing value deserializes to false, so every existing settings.json loads as Auto off.
    /// </summary>
    public bool AutoPopout { get; set; }

    public WindowSettings MainWindow { get; set; } = new();
    public PlayerSettings Player { get; set; } = new();
    public List<Profile> Profiles { get; set; } = new();
}

public sealed class WindowSettings
{
    public bool Topmost { get; set; }
    public PlacementData? Placement { get; set; }
}

public sealed class PlayerSettings
{
    public PlacementData? Placement { get; set; }
    public bool Topmost { get; set; } = true;
    public bool CompactMode { get; set; }

    /// <summary>
    /// Fade the Popout Player controls when idle (spec 11, Phase 2). On by default;
    /// when off the chrome strip stays visible exactly as in the MVP.
    /// </summary>
    public bool FadeEnabled { get; set; } = true;
    public string PinAccent { get; set; } = "cyan";
    public string FadeAccent { get; set; } = "cyan";
    public int FadeIdleDelayMs { get; set; } = 2500;
    public double IdleWindowOpacity { get; set; } = 1.0;
    public int LastWidth { get; set; } = 960;
    public int LastHeight { get; set; } = 540;
}

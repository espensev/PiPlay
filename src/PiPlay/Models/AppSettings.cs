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

    /// <summary>
    /// Global default playback mode for new popouts (spec 10.2, Phase 3). <c>false</c> = Normal page
    /// mode (the default and fallback); <c>true</c> = Compact embedded mode. A per-profile
    /// <see cref="Profile.Mode"/> overrides this per launch (REQ-PROFILE-01). Off by default; a
    /// missing value deserializes to false, so every existing settings.json keeps Normal mode.
    /// </summary>
    public bool CompactMode { get; set; }

    /// <summary>
    /// Fade the Popout Player controls when idle (spec 11, Phase 2). On by default;
    /// when off the chrome strip stays visible exactly as in the MVP.
    /// </summary>
    public bool FadeEnabled { get; set; } = true;
    public string PinAccent { get; set; } = "cyan";
    public string FadeAccent { get; set; } = "cyan";
    public int FadeIdleDelayMs { get; set; } = 2500;

    /// <summary>
    /// Whole-window opacity the Popout Player eases to when idle (spec 7.3, Phase 4). 1.0 = no
    /// idle fade (the default); a missing value deserializes to 1.0, so existing settings.json
    /// files keep today's look. Sanitized by <see cref="Services.WindowOpacityPolicy.Normalize"/>:
    /// values outside 0.1–1.0 reset to 1.0; 0.1–0.45 is the hand-edit unlock range (the UI slider
    /// floors at 0.45).
    /// </summary>
    public double IdleWindowOpacity { get; set; } = 1.0;

    /// <summary>
    /// Whole-window opacity of the Popout Player while active (spec 7.3, Phase 4 — the "Active"
    /// slider; the user's opaque reference look is this at 1.0). Same default, back-compat, and
    /// sanitization rules as <see cref="IdleWindowOpacity"/>.
    /// </summary>
    public double ConstantWindowOpacity { get; set; } = 1.0;
    public int LastWidth { get; set; } = 960;
    public int LastHeight { get; set; } = 540;
}

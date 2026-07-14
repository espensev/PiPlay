using System.Text.Json;
using System.Text.Json.Serialization;
using PiPlay.Theme;

namespace PiPlay.Models;

/// <summary>
/// Root application settings (spec 12.6). Persisted atomically to
/// %LOCALAPPDATA%/PiPlay/settings.json. Browser session/cookies are NOT stored here -
/// the WebView2 user-data folder owns that.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Schema 3 (end-pass review F2): null theme behavior values mean "use the preset default".
    /// Schema ≤2 wrote theme blocks whose nulls meant "fall back to the legacy Player fields",
    /// so SettingsService backfills those nulls from Player once on first load.
    /// </summary>
    public const int CurrentSchemaVersion = 4;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string LastUrl { get; set; } = "https://www.youtube.com/";

    /// <summary>
    /// Auto (spec §6.1): automatically start a Video Popout when a /watch video is playing. Off by
    /// default; a missing value deserializes to false, so every existing settings.json loads as Auto off.
    /// </summary>
    public bool AutoPopout { get; set; }

    public WindowSettings MainWindow { get; set; } = new();
    public PlayerSettings Player { get; set; } = new();
    public ThemeSettings Theme { get; set; } = new();
    public List<Profile> Profiles { get; set; } = new();
    public string? ActiveProfileName { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
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

    /// <summary>
    /// Auto-hide the Popout Player's chrome strip (spec 7.2, Phase 4): an idle-faded strip also
    /// height-collapses so the video fills the window; hovering the top edge reveals it. Shares
    /// the controls-fade idle source, so it only engages while <see cref="FadeEnabled"/> is on.
    /// Off by default; a missing value deserializes to false, so existing settings.json files
    /// keep the always-reserved strip row.
    /// </summary>
    public bool StripAutoHide { get; set; }
    public int LastWidth { get; set; } = 960;
    public int LastHeight { get; set; } = 540;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class ThemeSettings
{
    public string ThemeId { get; set; } = ThemeCatalog.DefaultThemeId;
    public string AccentColor { get; set; } = ThemeCatalog.DefaultAccentColor;

    /// <summary>
    /// How far the accent reaches into the chrome, 0–100 (Settings → Appearance). 0 paints only the
    /// primary action; 50 restores full toolbar glyph accent plus the v0.9.0 wash; 100 keeps those glyphs
    /// full and gives the title bar its strongest wash. A user preference, NOT a preset trait — a preset
    /// switch must not reset it. Missing from an older settings.json → the property initializer keeps
    /// the default, so no schema bump is needed.
    /// </summary>
    public int AccentIntensity { get; set; } = ThemeCatalog.DefaultAccentIntensity;
    public string FadeDelayPreset { get; set; } = ThemeCatalog.DefaultFadeDelayPreset;

    /// <summary>
    /// Corner profile override (review doc §8.1): "theme" (default) follows the selected preset's
    /// radii + native corner mode; "square"/"small"/"round" swap the whole profile (legacy "soft"
    /// normalizes to "round" — both were always DWMWCP_ROUND). A
    /// missing value deserializes to null and sanitizes to "theme", so existing settings.json
    /// files keep preset-owned corners.
    /// </summary>
    public string CornerStyle { get; set; } = ThemeCatalog.DefaultCornerStyle;

    /// <summary>
    /// Behavior OVERRIDES, not final values (end-pass review §3.2): null = follow the selected
    /// preset's default; the resolver applies preset default → override → normalized. The
    /// property names keep their original JSON spelling for settings back-compat.
    /// </summary>
    public bool? StripAutoHide { get; set; }

    /// <inheritdoc cref="StripAutoHide"/>
    public double? ActiveWindowOpacity { get; set; }

    /// <inheritdoc cref="StripAutoHide"/>
    public double? IdleWindowOpacity { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    public static ThemeSettings FromLegacy(PlayerSettings player) => new()
    {
        AccentColor = ThemeCatalog.AccentColorForLegacyAccent(player.PinAccent),
        FadeDelayPreset = ThemeCatalog.FadeDelayPresetForMilliseconds(player.FadeIdleDelayMs),
        // The legacy behavior values become EXPLICIT overrides at seed time, so the resolver's
        // preset-default fallback (end-pass review F2) can never change a migrated user's
        // configured look. Callers sanitize Player before seeding, so these are normalized.
        StripAutoHide = player.StripAutoHide,
        ActiveWindowOpacity = player.ConstantWindowOpacity,
        IdleWindowOpacity = player.IdleWindowOpacity,
    };
}

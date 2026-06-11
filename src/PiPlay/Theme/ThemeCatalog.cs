using PiPlay.Services;

namespace PiPlay.Theme;

/// <summary>
/// Native (DWM) corner preference for a top-level window. XAML radius tokens shape the controls
/// INSIDE a window; this enum shapes the real outer HWND corner via
/// DWMWA_WINDOW_CORNER_PREFERENCE. <see cref="Default"/> means "leave the window untouched"
/// (the pristine pre-theme look), so default-theme windows stay byte-identical to today.
/// </summary>
public enum DwmCornerMode
{
    Default,
    Square,      // DWMWCP_DONOTROUND
    SmallRound,  // DWMWCP_ROUNDSMALL
    Round,       // DWMWCP_ROUND
}

/// <summary>
/// Semantic per-theme corner radii in DIPs (review doc §8.2). Distinct tokens, not one universal
/// radius: a popout frame, a command button, a tooltip, and a scrollbar thumb do not want the
/// same shape.
/// </summary>
public sealed record ThemeRadii(
    double MainWindowFrame,
    double PopoutFrame,
    double TitleBar,
    double Button,
    double IconButton,
    double Input,
    double Panel,
    double Popup,
    double Thumbnail,
    double Swatch,
    double ScrollbarThumb,
    double ToolTip);

/// <summary>
/// Per-theme surface/border/text colors as #RRGGBB hex (review doc §7). The accent is NOT part of
/// the palette — it stays a separate, user-selected value layered on top.
/// </summary>
public sealed record ThemePalette(
    string AppBackground,
    string SurfaceBase,
    string SurfaceRaised,
    string SurfaceHover,
    string BorderSubtle,
    string BorderStrong,
    string TextPrimary,
    string TextSecondary,
    string Danger);

public sealed record ThemePreset(
    string Id,
    string DisplayName,
    string Description,
    string DefaultAccentColor,
    string DefaultFadeDelayPreset,
    bool DefaultStripAutoHide,
    double DefaultActiveWindowOpacity,
    double DefaultIdleWindowOpacity,
    ThemePalette Palette,
    ThemeRadii Radii,
    DwmCornerMode DwmCorners);

public sealed record ThemeAccentOption(string Key, string DisplayName, string HexColor);

public sealed record ThemeCornerStyleOption(string Key, string DisplayName);

public static class ThemeCatalog
{
    public const string DefaultThemeId = "sharp-dark";
    // The sharp-dark accent is the current shell cyan, so a fresh install and the migrated legacy
    // "cyan" seed land on the same value (AccentColorForLegacyAccent("cyan")). Every offered accent
    // is bright enough to read as an on-dark glyph and to carry the dark AccentButton text — the
    // XamlInvariantTests "Theme accent palette is readable" theory gates this.
    public const string DefaultAccentColor = "#00D4FF";
    public const string DefaultFadeDelayPreset = "normal";

    /// <summary>"theme" = corners follow the selected preset; the other styles override the whole
    /// corner profile (radius set + native corner mode), never individual per-control values.</summary>
    public const string DefaultCornerStyle = "theme";

    // Corner profiles (review doc §8.2). Sharp keeps tiny rounding (modern without going soft),
    // soft-glass gets the largest popout radius because it is the floating overlay theme.
    private static readonly ThemeRadii SharpRadii = new(
        MainWindowFrame: 4, PopoutFrame: 4, TitleBar: 4,
        Button: 5, IconButton: 5, Input: 5, Panel: 4,
        Popup: 6, Thumbnail: 3, Swatch: 6, ScrollbarThumb: 4, ToolTip: 6);

    private static readonly ThemeRadii MinimalRadii = new(
        MainWindowFrame: 6, PopoutFrame: 8, TitleBar: 6,
        Button: 6, IconButton: 6, Input: 6, Panel: 8,
        Popup: 8, Thumbnail: 4, Swatch: 8, ScrollbarThumb: 5, ToolTip: 6);

    private static readonly ThemeRadii SoftGlassRadii = new(
        MainWindowFrame: 10, PopoutFrame: 16, TitleBar: 10,
        Button: 10, IconButton: 10, Input: 10, Panel: 14,
        Popup: 14, Thumbnail: 8, Swatch: 10, ScrollbarThumb: 5, ToolTip: 8);

    private static readonly ThemeRadii SquareRadii = new(
        MainWindowFrame: 0, PopoutFrame: 0, TitleBar: 0,
        Button: 0, IconButton: 0, Input: 0, Panel: 0,
        Popup: 0, Thumbnail: 0, Swatch: 0, ScrollbarThumb: 0, ToolTip: 0);

    private static readonly ThemePreset[] PresetsValue =
    [
        new(
            DefaultThemeId,
            "Sharp Dark",
            "The current utility-first PiPlay dark shell.",
            DefaultAccentColor,
            DefaultFadeDelayPreset,
            DefaultStripAutoHide: false,
            DefaultActiveWindowOpacity: WindowOpacityPolicy.Default,
            DefaultIdleWindowOpacity: WindowOpacityPolicy.Default,
            // Darker than the previous shared palette (review doc §7.1): near-black base with
            // cool slate borders. The Colors.xaml seeds mirror these values.
            Palette: new(
                AppBackground: "#07090B", SurfaceBase: "#0D1014",
                SurfaceRaised: "#141920", SurfaceHover: "#202833",
                BorderSubtle: "#2A3441", BorderStrong: "#3A4655",
                TextPrimary: "#F2F5F7", TextSecondary: "#A8B0BA",
                Danger: "#E45D75"),
            Radii: SharpRadii,
            // Default, not SmallRound: the default theme must leave windows DWM-pristine.
            DwmCorners: DwmCornerMode.Default),
        new(
            "minimal",
            "Minimal",
            "A quieter preset for daily browsing and low-distraction popouts.",
            "#5AA9E6",
            DefaultFadeDelayPreset,
            DefaultStripAutoHide: false,
            DefaultActiveWindowOpacity: WindowOpacityPolicy.Default,
            DefaultIdleWindowOpacity: WindowOpacityPolicy.Default,
            // The pre-theme shared palette tones (review doc §7.2) — minimal IS today's look.
            Palette: new(
                AppBackground: "#0B0D0E", SurfaceBase: "#111316",
                SurfaceRaised: "#1A1E22", SurfaceHover: "#252B31",
                BorderSubtle: "#30363D", BorderStrong: "#414A55",
                TextPrimary: "#F3F5F7", TextSecondary: "#A7ADB4",
                Danger: "#FF4B55"),
            Radii: MinimalRadii,
            DwmCorners: DwmCornerMode.Default),
        new(
            "soft-glass",
            "Soft Glass",
            "A softer overlay-friendly preset for desktop popouts.",
            "#A78BFA",
            DefaultFadeDelayPreset,
            DefaultStripAutoHide: false,
            DefaultActiveWindowOpacity: 0.92,
            DefaultIdleWindowOpacity: 0.78,
            // Cooler, bluer translucent-overlay palette (review doc §7.3).
            Palette: new(
                AppBackground: "#090B0F", SurfaceBase: "#10141B",
                SurfaceRaised: "#171C26", SurfaceHover: "#242B38",
                BorderSubtle: "#384255", BorderStrong: "#526179",
                TextPrimary: "#F7F8FA", TextSecondary: "#C0C6CF",
                Danger: "#E45D75"),
            Radii: SoftGlassRadii,
            DwmCorners: DwmCornerMode.Round),
    ];

    private static readonly ThemeAccentOption[] AccentOptionsValue =
    [
        new("cyan", "Cyan", DefaultAccentColor),
        new("steel-blue", "Steel blue", "#5AA9E6"),
        // The muted steel accent (review doc §5): the darker, less-neon tone for the sharp look.
        new("steel", "Steel", "#4A8FAB"),
        new("violet", "Violet", "#A78BFA"),
        new("green", "Green", "#38D996"),
        new("amber", "Amber", "#FFC857"),
    ];

    private static readonly ThemeCornerStyleOption[] CornerStyleOptionsValue =
    [
        new(DefaultCornerStyle, "Theme"),
        new("square", "Square"),
        new("small", "Small"),
        new("soft", "Soft"),
        new("round", "Round"),
    ];

    public static IReadOnlyList<ThemePreset> Presets => PresetsValue;

    public static IReadOnlyList<ThemeAccentOption> AccentOptions => AccentOptionsValue;

    public static IReadOnlyList<ThemeCornerStyleOption> CornerStyleOptions => CornerStyleOptionsValue;

    public static string NormalizeThemeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return DefaultThemeId;
        var normalized = id.Trim().ToLowerInvariant();
        return PresetsValue.Any(p => p.Id == normalized) ? normalized : DefaultThemeId;
    }

    public static string NormalizeCornerStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style)) return DefaultCornerStyle;
        var normalized = style.Trim().ToLowerInvariant();
        return CornerStyleOptionsValue.Any(o => o.Key == normalized) ? normalized : DefaultCornerStyle;
    }

    /// <summary>The effective control radii for a preset plus the user's corner-style override
    /// (review doc §8.1): the override swaps the whole profile, never single values.</summary>
    public static ThemeRadii RadiiFor(ThemePreset preset, string? cornerStyle) =>
        NormalizeCornerStyle(cornerStyle) switch
        {
            "square" => SquareRadii,
            "small" => SharpRadii,
            "soft" => MinimalRadii,
            "round" => SoftGlassRadii,
            _ => preset.Radii,
        };

    /// <summary>The effective native corner preference for a preset plus the user's corner-style
    /// override. Explicit theme/user data — never derived from opacity (review doc §2.6).</summary>
    public static DwmCornerMode DwmCornersFor(ThemePreset preset, string? cornerStyle) =>
        NormalizeCornerStyle(cornerStyle) switch
        {
            "square" => DwmCornerMode.Square,
            "small" => DwmCornerMode.SmallRound,
            "soft" => DwmCornerMode.Round,
            "round" => DwmCornerMode.Round,
            _ => preset.DwmCorners,
        };

    public static string NormalizeAccentColor(string? color, string? fallback = null)
    {
        var candidate = NormalizeHex6(color);
        if (candidate is not null) return candidate;
        return NormalizeHex6(fallback) ?? DefaultAccentColor;
    }

    public static string NormalizeFadeDelayPreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset)) return DefaultFadeDelayPreset;
        var normalized = preset.Trim().ToLowerInvariant();
        return PlayerAppearancePolicy.FadeDelayOptions.Any(o => o.Key == normalized)
            ? normalized
            : DefaultFadeDelayPreset;
    }

    public static int FadeDelayMillisecondsForPreset(string? preset)
    {
        var normalized = NormalizeFadeDelayPreset(preset);
        return PlayerAppearancePolicy.FadeDelayOptions.First(o => o.Key == normalized).Milliseconds;
    }

    public static string FadeDelayPresetForMilliseconds(int milliseconds)
    {
        var normalized = PlayerAppearancePolicy.NormalizeFadeIdleDelayMs(milliseconds);
        return PlayerAppearancePolicy.FadeDelayOptionFor(normalized).Key;
    }

    public static string AccentColorForLegacyAccent(string? key)
    {
        return PlayerAppearancePolicy.NormalizeAccent(key) switch
        {
            "violet" => "#A78BFA",
            "green" => "#38D996",
            "amber" => "#FFC857",
            _ => DefaultAccentColor,
        };
    }

    public static ThemePreset PresetFor(string? id)
    {
        var normalized = NormalizeThemeId(id);
        return PresetsValue.First(p => p.Id == normalized);
    }

    private static string? NormalizeHex6(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;

        var value = color.Trim();
        if (value.StartsWith('#')) value = value[1..];
        if (value.Length != 6 || !value.All(IsHexDigit)) return null;
        return "#" + value.ToUpperInvariant();
    }

    private static bool IsHexDigit(char c) =>
        c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}

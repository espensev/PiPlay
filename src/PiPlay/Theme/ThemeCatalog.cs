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

    // Corner profiles (theme-v2 tight-scope spec §"Rounding targets"). Sharp is intentionally tight
    // (modern without going soft); minimal is visibly softer; soft-glass gets the largest popout
    // radius because it is the floating overlay theme. Ordered sharp ≤ minimal ≤ soft-glass per token.
    private static readonly ThemeRadii SharpRadii = new(
        MainWindowFrame: 2, PopoutFrame: 2, TitleBar: 2,
        Button: 3, IconButton: 3, Input: 3, Panel: 2,
        Popup: 4, Thumbnail: 2, Swatch: 4, ScrollbarThumb: 3, ToolTip: 4);

    private static readonly ThemeRadii MinimalRadii = new(
        MainWindowFrame: 8, PopoutFrame: 12, TitleBar: 8,
        Button: 8, IconButton: 8, Input: 8, Panel: 10,
        Popup: 10, Thumbnail: 6, Swatch: 8, ScrollbarThumb: 5, ToolTip: 8);

    private static readonly ThemeRadii SoftGlassRadii = new(
        MainWindowFrame: 14, PopoutFrame: 22, TitleBar: 14,
        Button: 12, IconButton: 12, Input: 12, Panel: 16,
        Popup: 16, Thumbnail: 10, Swatch: 12, ScrollbarThumb: 6, ToolTip: 10);

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
            // Near-black and cool (theme-v2 tight-scope spec §"Palette targets"): the darkest base
            // with cool slate borders. The Colors.xaml seeds mirror these values.
            Palette: new(
                AppBackground: "#050609", SurfaceBase: "#0B0E12",
                SurfaceRaised: "#131820", SurfaceHover: "#1E2630",
                BorderSubtle: "#2B3645", BorderStrong: "#3E4B5C",
                TextPrimary: "#F4F7FA", TextSecondary: "#9AA2AD",
                Danger: "#E45D75"),
            Radii: SharpRadii,
            // Default, not SmallRound: the default theme must leave windows DWM-pristine.
            DwmCorners: DwmCornerMode.Default),
        new(
            "minimal",
            "Minimal",
            "A quieter preset for daily browsing and low-distraction popouts.",
            "#5AA9E6",
            // Calmer daily shell: longer fade delay so the strip lingers (theme-v2 spec §"behavior
            // defaults"); strip stays pinned and the window stays opaque.
            DefaultFadeDelayPreset: "long",
            DefaultStripAutoHide: false,
            DefaultActiveWindowOpacity: WindowOpacityPolicy.Default,
            DefaultIdleWindowOpacity: WindowOpacityPolicy.Default,
            // Warm charcoal palette (theme-v2 tight-scope spec §"Palette targets").
            Palette: new(
                AppBackground: "#14120F", SurfaceBase: "#1C1A16",
                SurfaceRaised: "#26231E", SurfaceHover: "#312D27",
                BorderSubtle: "#3C372F", BorderStrong: "#50493E",
                TextPrimary: "#F4F1EC", TextSecondary: "#B0A99E",
                Danger: "#E8564C"),
            Radii: MinimalRadii,
            // Small native rounding: softer than Sharp's pristine HWND, calmer than Soft Glass.
            DwmCorners: DwmCornerMode.SmallRound),
        new(
            "soft-glass",
            "Soft Glass",
            "A softer overlay-friendly preset for desktop popouts.",
            "#A78BFA",
            // Floating overlay shell: a short fade and auto-hiding strip keep the surface clean, and
            // the window is translucent active/idle (theme-v2 tight-scope spec §"behavior defaults").
            DefaultFadeDelayPreset: "short",
            DefaultStripAutoHide: true,
            DefaultActiveWindowOpacity: 0.92,
            DefaultIdleWindowOpacity: 0.78,
            // Blue/cool translucent-overlay palette with brighter borders and secondary text
            // (theme-v2 tight-scope spec §"Palette targets").
            Palette: new(
                AppBackground: "#0B1018", SurfaceBase: "#121A26",
                SurfaceRaised: "#1B2738", SurfaceHover: "#26354B",
                BorderSubtle: "#44526E", BorderStrong: "#66799E",
                TextPrimary: "#F6F8FC", TextSecondary: "#C4CEDC",
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

    /// <summary>
    /// Accent rule for an explicit theme switch (end-pass review §3.3): adopt the next preset's
    /// default accent only when the current accent IS the previous preset's default — a
    /// deliberately chosen custom accent survives theme switches. Pure so the rule is testable
    /// without the Settings dialog and reusable once arbitrary (color-wheel) accents exist.
    /// Inputs are normalized before comparison.
    /// </summary>
    public static string AccentForThemeSwitch(string? currentAccent, ThemePreset previousPreset, ThemePreset nextPreset)
    {
        var current = NormalizeAccentColor(currentAccent);
        return current == NormalizeAccentColor(previousPreset.DefaultAccentColor)
            ? NormalizeAccentColor(nextPreset.DefaultAccentColor)
            : current;
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

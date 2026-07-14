using System.Windows;
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

/// <summary>
/// Per-theme control density in DIPs (theme-v2 tight-scope spec §"Density targets"): the heights,
/// paddings, and uniform default border weight that make Sharp feel compact and Soft Glass airy.
/// Heights/sizes are plain doubles; paddings and the default border are WPF <see cref="Thickness"/>
/// so the applier replaces Padding/BorderThickness DynamicResource entries with the struct type those
/// consumers expect (a double/string there hits the .NET 10 DynamicResource type-mismatch crash class).
/// </summary>
public sealed record ThemeDensity(
    double ControlHeight,
    double IconButtonSize,
    double ScrollbarThickness,
    Thickness ButtonPadding,
    Thickness InputPadding,
    Thickness MenuItemPadding,
    Thickness PresetChipPadding,
    Thickness ToolTipPadding,
    Thickness BorderThicknessDefault);

/// <summary>
/// Per-theme INNER elevation (theme-v2 tight-scope spec §"Elevation targets"): the drop-shadow on
/// popups/menus and raised internal panels. Inner-only — never an outer-window glow (the windows stay
/// AllowsTransparency=False and host WebView2 by HWND). A preset with a <c>null</c> Elevation has no
/// inner shadow at all (Sharp Dark): the applier writes a null Effect, not a no-op DropShadowEffect.
/// </summary>
public sealed record ThemeElevation(
    double PopupBlurRadius,
    double PopupShadowDepth,
    double PopupOpacity,
    double PanelBlurRadius,
    double PanelShadowDepth,
    double PanelOpacity);

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
    DwmCornerMode DwmCorners,
    ThemeDensity Density,
    ThemeElevation? Elevation);

public sealed record ThemeAccentOption(string Key, string DisplayName, string HexColor);

public sealed record ThemeCornerStyleOption(string Key, string DisplayName);

public static class ThemeCatalog
{
    public const string DefaultThemeId = "sharp-dark";
    // The sharp-dark accent is the current shell cyan, so a fresh install and the migrated legacy
    // "cyan" seed land on the same value (AccentColorForLegacyAccent("cyan")). Every offered accent
    // is bright enough to read as an on-dark glyph and to carry the dark AccentButton text — the
    // XamlInvariantTests "Theme accent palette is readable" theory gates this.
    public const string DefaultAccentColor = "#2BAED0";
    public const string DefaultFadeDelayPreset = "normal";

    /// <summary>"theme" = corners follow the selected preset; the other styles override the whole
    /// corner profile (radius set + native corner mode), never individual per-control values.</summary>
    public const string DefaultCornerStyle = "theme";

    /// <summary>
    /// How far the accent reaches into the chrome, 0–100 (user-set, Settings → Appearance).
    /// <para>
    /// 0 = the accent paints only the primary action (the pre-v0.9.0 look: no title-bar wash, neutral
    /// toolbar glyphs). At 50 the glyphs have reached full accent and the wash matches v0.9.0's 1.45
    /// target exactly. From 50–100 only the wash deepens, up to its restrained ceiling. This is a user
    /// preference, not a preset trait — switching theme presets must not reset it, the same way a custom
    /// accent survives a preset switch.
    /// </para>
    /// </summary>
    public const int DefaultAccentIntensity = 50;

    /// <summary>Clamps to 0–100; null (missing from settings.json) falls back to the default.</summary>
    public static int NormalizeAccentIntensity(int? intensity) =>
        intensity is null ? DefaultAccentIntensity : Math.Clamp(intensity.Value, 0, 100);

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

    // Per-preset control density (theme-v2 tight-scope spec §"Density targets"): Sharp compact,
    // Minimal normal, Soft Glass airy. ControlHeight/IconButtonSize climb strictly sharp<minimal<soft;
    // ScrollbarThickness thickens off Sharp then ties (8/10/10); BorderThicknessDefault is a uniform 1
    // across all three this pass (border weight is not a v2 differentiation axis yet). The applier
    // replaces the Density* / BorderThicknessDefault resources from these; control consumers migrate to
    // the tokens in the density-consumer pass.
    private static readonly ThemeDensity SharpDensity = new(
        ControlHeight: 30, IconButtonSize: 30, ScrollbarThickness: 8,
        ButtonPadding: new Thickness(10, 5, 10, 5), InputPadding: new Thickness(8, 0, 8, 0),
        MenuItemPadding: new Thickness(8, 5, 8, 5), PresetChipPadding: new Thickness(8, 0, 8, 0),
        ToolTipPadding: new Thickness(7, 4, 7, 4), BorderThicknessDefault: new Thickness(1));

    private static readonly ThemeDensity MinimalDensity = new(
        ControlHeight: 34, IconButtonSize: 32, ScrollbarThickness: 10,
        ButtonPadding: new Thickness(12, 6, 12, 6), InputPadding: new Thickness(10, 0, 10, 0),
        MenuItemPadding: new Thickness(10, 6, 10, 6), PresetChipPadding: new Thickness(10, 0, 10, 0),
        ToolTipPadding: new Thickness(8, 5, 8, 5), BorderThicknessDefault: new Thickness(1));

    private static readonly ThemeDensity SoftGlassDensity = new(
        ControlHeight: 38, IconButtonSize: 36, ScrollbarThickness: 10,
        ButtonPadding: new Thickness(16, 9, 16, 9), InputPadding: new Thickness(14, 2, 14, 2),
        MenuItemPadding: new Thickness(14, 9, 14, 9), PresetChipPadding: new Thickness(14, 0, 14, 0),
        ToolTipPadding: new Thickness(10, 7, 10, 7), BorderThicknessDefault: new Thickness(1));

    // Inner elevation (theme-v2 tight-scope spec §"Elevation targets"): Sharp Dark gets a null
    // Elevation (flat — no inner shadow); Minimal is subtle, Soft Glass soft. Every axis is at least as
    // strong on Soft Glass as on Minimal, blur strictly stronger.
    private static readonly ThemeElevation MinimalElevation = new(
        PopupBlurRadius: 8, PopupShadowDepth: 1, PopupOpacity: 0.22,
        PanelBlurRadius: 6, PanelShadowDepth: 1, PanelOpacity: 0.16);

    private static readonly ThemeElevation SoftGlassElevation = new(
        PopupBlurRadius: 16, PopupShadowDepth: 2, PopupOpacity: 0.34,
        PanelBlurRadius: 12, PanelShadowDepth: 2, PanelOpacity: 0.26);

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
                BorderSubtle: "#181F29", BorderStrong: "#262F3D",
                TextPrimary: "#F4F7FA", TextSecondary: "#9AA2AD",
                Danger: "#E45D75"),
            Radii: SharpRadii,
            // Default, not SmallRound: the default theme must leave windows DWM-pristine.
            DwmCorners: DwmCornerMode.Default,
            // Compact, flat: the tightest density and no inner elevation (the utility shell).
            Density: SharpDensity,
            Elevation: null),
        new(
            "minimal",
            "Minimal",
            "A quieter preset for daily browsing and low-distraction popouts.",
            "#3F84C0",
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
                BorderSubtle: "#2E2A23", BorderStrong: "#3C362D",
                TextPrimary: "#F4F1EC", TextSecondary: "#B0A99E",
                Danger: "#E8564C"),
            Radii: MinimalRadii,
            // Small native rounding: softer than Sharp's pristine HWND, calmer than Soft Glass.
            DwmCorners: DwmCornerMode.SmallRound,
            // Normal density and a subtle inner elevation on popups/panels.
            Density: MinimalDensity,
            Elevation: MinimalElevation),
        new(
            "soft-glass",
            "Soft Glass",
            "A softer overlay-friendly preset for desktop popouts.",
            "#9E84F0",
            // Floating overlay shell: a short fade and auto-hiding strip keep the surface clean, and
            // the window keeps only a slight, controlled translucency — near-opaque active with a
            // light idle fade, not the old heavy see-through (owner review: "low, controlled, not heavy").
            DefaultFadeDelayPreset: "short",
            DefaultStripAutoHide: true,
            DefaultActiveWindowOpacity: 0.97,
            DefaultIdleWindowOpacity: 0.90,
            // Blue/cool translucent-overlay palette with quieted borders and secondary text
            // (theme-v2 tight-scope spec §"Palette targets").
            Palette: new(
                AppBackground: "#0B1018", SurfaceBase: "#121A26",
                SurfaceRaised: "#1B2738", SurfaceHover: "#26354B",
                BorderSubtle: "#2A3A52", BorderStrong: "#3A4D6A",
                TextPrimary: "#F6F8FC", TextSecondary: "#C4CEDC",
                Danger: "#E45D75"),
            Radii: SoftGlassRadii,
            DwmCorners: DwmCornerMode.Round,
            // Airy density and the softest inner elevation (the floating overlay shell).
            Density: SoftGlassDensity,
            Elevation: SoftGlassElevation),
    ];

    // Deeper, less-neon defaults (owner request 2026-06-20): each pushed toward the readable floor
    // (accent must still read as a glyph on the dark UI AND carry dark text >=4.5:1), so they are
    // noticeably darker than the old neon set without failing the WCAG accent gates (ThemeCatalogTests).
    private static readonly ThemeAccentOption[] AccentOptionsValue =
    [
        new("cyan", "Cyan", DefaultAccentColor),
        new("steel-blue", "Steel blue", "#3F84C0"),
        // The muted steel accent (review doc §5): the darker, less-neon tone for the sharp look.
        new("steel", "Steel", "#4A8FAB"),
        new("violet", "Violet", "#9E84F0"),
        new("green", "Green", "#2DB57F"),
        new("amber", "Amber", "#D69A2E"),
    ];

    private static readonly ThemeCornerStyleOption[] CornerStyleOptionsValue =
    [
        new(DefaultCornerStyle, "Theme"),
        new("square", "Square"),
        new("small", "Small"),
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
        // Legacy alias: "soft" and "round" always produced the same DWMWCP_ROUND outer corner (DWM
        // exposes only three radii), so the duplicate "Soft" option was dropped 2026-06. A stored
        // "soft" keeps its rounded silhouette by normalizing to "round".
        if (normalized == "soft") normalized = "round";
        return CornerStyleOptionsValue.Any(o => o.Key == normalized) ? normalized : DefaultCornerStyle;
    }

    /// <summary>The effective control radii for a preset plus the user's corner-style override
    /// (review doc §8.1): the override swaps the whole profile, never single values.</summary>
    public static ThemeRadii RadiiFor(ThemePreset preset, string? cornerStyle) =>
        NormalizeCornerStyle(cornerStyle) switch
        {
            "square" => SquareRadii,
            "small" => SharpRadii,
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
            "round" => DwmCornerMode.Round,
            _ => preset.DwmCorners,
        };

    public static string NormalizeAccentColor(string? color, string? fallback = null)
    {
        var candidate = NormalizeHex6(color);
        if (candidate is not null) return candidate;
        return NormalizeHex6(fallback) ?? DefaultAccentColor;
    }

    public static bool IsValidHex(string? color) => NormalizeHex6(color) is not null;

    /// <summary>
    /// Global-accent rule for an explicit theme switch (end-pass review §3.3): adopt the next preset's
    /// default only when the current global IS the previous preset's default — a deliberately chosen
    /// custom global survives theme switches. Profile-owned accents do not use this substitution. The
    /// pure helper keeps the rule testable without the Settings dialog.
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
            "violet" => "#9E84F0",
            "green" => "#2DB57F",
            "amber" => "#D69A2E",
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

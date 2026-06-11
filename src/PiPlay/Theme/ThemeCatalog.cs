using PiPlay.Services;

namespace PiPlay.Theme;

public sealed record ThemePreset(
    string Id,
    string DisplayName,
    string Description,
    string DefaultAccentColor,
    string DefaultFadeDelayPreset,
    bool DefaultStripAutoHide,
    double DefaultActiveWindowOpacity,
    double DefaultIdleWindowOpacity);

public sealed record ThemeAccentOption(string Key, string DisplayName, string HexColor);

public static class ThemeCatalog
{
    public const string DefaultThemeId = "sharp-dark";
    public const string DefaultAccentColor = "#2D6F8F";
    public const string DefaultFadeDelayPreset = "normal";

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
            DefaultIdleWindowOpacity: WindowOpacityPolicy.Default),
        new(
            "minimal",
            "Minimal",
            "A quieter preset for daily browsing and low-distraction popouts.",
            "#4D7EA8",
            DefaultFadeDelayPreset,
            DefaultStripAutoHide: false,
            DefaultActiveWindowOpacity: WindowOpacityPolicy.Default,
            DefaultIdleWindowOpacity: WindowOpacityPolicy.Default),
        new(
            "soft-glass",
            "Soft Glass",
            "A softer overlay-friendly preset for desktop popouts.",
            "#A78BFA",
            DefaultFadeDelayPreset,
            DefaultStripAutoHide: false,
            DefaultActiveWindowOpacity: 0.92,
            DefaultIdleWindowOpacity: 0.78),
    ];

    private static readonly ThemeAccentOption[] AccentOptionsValue =
    [
        new("muted-cyan", "Muted cyan", DefaultAccentColor),
        new("steel-blue", "Steel blue", "#4D7EA8"),
        new("violet", "Violet", "#A78BFA"),
        new("green", "Green", "#38D996"),
        new("amber", "Amber", "#FFC857"),
    ];

    public static IReadOnlyList<ThemePreset> Presets => PresetsValue;

    public static IReadOnlyList<ThemeAccentOption> AccentOptions => AccentOptionsValue;

    public static string NormalizeThemeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return DefaultThemeId;
        var normalized = id.Trim().ToLowerInvariant();
        return PresetsValue.Any(p => p.Id == normalized) ? normalized : DefaultThemeId;
    }

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
            _ => "#00D4FF",
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

using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Theme;

public static class ThemePreferenceResolver
{
    public static string AccentColor(ThemeSettings? theme, PlayerSettings player)
    {
        var fallback = ThemeCatalog.AccentColorForLegacyAccent(player.PinAccent);
        return ThemeCatalog.NormalizeAccentColor(theme?.AccentColor, fallback);
    }

    public static int FadeIdleDelayMs(ThemeSettings? theme, PlayerSettings player) =>
        theme is null
            ? PlayerAppearancePolicy.NormalizeFadeIdleDelayMs(player.FadeIdleDelayMs)
            : ThemeCatalog.FadeDelayMillisecondsForPreset(theme.FadeDelayPreset);

    // Behavior values resolve preset default → explicit override → normalized. A theme block with
    // null overrides gets the preset's behavior, so a hand-edited
    // "themeId": "soft-glass" is translucent without manual slider work. Migration safety: the
    // raw-PlayerSettings fallback applies only when there is NO theme block at all —
    // ThemeSettings.FromLegacy copies the legacy behavior values into explicit overrides at seed
    // time, so a migrated user's configured look never changes underneath them.

    public static bool StripAutoHide(ThemeSettings? theme, PlayerSettings player) =>
        theme is null
            ? player.StripAutoHide
            : theme.StripAutoHide ?? ThemeCatalog.PresetFor(theme.ThemeId).DefaultStripAutoHide;

    public static double ActiveWindowOpacity(ThemeSettings? theme, PlayerSettings player) =>
        WindowOpacityPolicy.Normalize(theme is null
            ? player.ConstantWindowOpacity
            : theme.ActiveWindowOpacity ?? ThemeCatalog.PresetFor(theme.ThemeId).DefaultActiveWindowOpacity);

    public static double IdleWindowOpacity(ThemeSettings? theme, PlayerSettings player) =>
        WindowOpacityPolicy.Normalize(theme is null
            ? player.IdleWindowOpacity
            : theme.IdleWindowOpacity ?? ThemeCatalog.PresetFor(theme.ThemeId).DefaultIdleWindowOpacity);

    public static string CornerStyle(ThemeSettings? theme) =>
        ThemeCatalog.NormalizeCornerStyle(theme?.CornerStyle);

    /// <summary>The effective native (DWM) corner preference: the user's corner-style override
    /// over the selected preset's mode. Explicit data, never an opacity side effect.</summary>
    public static DwmCornerMode DwmCorners(ThemeSettings? theme) =>
        ThemeCatalog.DwmCornersFor(ThemeCatalog.PresetFor(theme?.ThemeId), theme?.CornerStyle);

    /// <summary>The effective control radii: the user's corner-style override over the preset's.</summary>
    public static ThemeRadii Radii(ThemeSettings? theme) =>
        ThemeCatalog.RadiiFor(ThemeCatalog.PresetFor(theme?.ThemeId), theme?.CornerStyle);
}

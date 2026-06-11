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

    public static bool StripAutoHide(ThemeSettings? theme, PlayerSettings player) =>
        theme?.StripAutoHide ?? player.StripAutoHide;

    public static double ActiveWindowOpacity(ThemeSettings? theme, PlayerSettings player) =>
        WindowOpacityPolicy.Normalize(theme?.ActiveWindowOpacity ?? player.ConstantWindowOpacity);

    public static double IdleWindowOpacity(ThemeSettings? theme, PlayerSettings player) =>
        WindowOpacityPolicy.Normalize(theme?.IdleWindowOpacity ?? player.IdleWindowOpacity);

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

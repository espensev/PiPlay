using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Theme;

/// <summary>
/// Writes the Settings-dialog appearance payload onto <see cref="AppSettings"/> (theme code
/// review P2): theme identity plus NULLABLE behavior overrides land on Theme — null means
/// "follow the selected preset's default", so an accent-only apply must not materialize
/// overrides — while the legacy Player mirrors carry the EFFECTIVE values for pre-theme
/// readers. Pure model mapping (no resources, no windows) so the Logic lane can pin the
/// null-preservation contract directly.
/// </summary>
public static class ThemeSettingsWriter
{
    public static void Apply(AppSettings settings, string themeId, string accentColor, int fadeIdleDelayMs,
        bool compactMode, double? activeOpacityOverride, double? idleOpacityOverride,
        bool? stripAutoHideOverride, string cornerStyle, int? accentIntensity = null)
    {
        settings.Theme.ThemeId = ThemeCatalog.NormalizeThemeId(themeId);
        settings.Theme.AccentColor = ThemeCatalog.NormalizeAccentColor(accentColor);
        // Accent reach is a USER preference, so null here means "the caller had nothing to say — keep
        // what is saved", NOT "reset to the default". Treating null as the default would let any apply
        // that does not carry the dial (a preset switch, an accent-only apply) silently wipe it.
        if (accentIntensity is int intensity)
            settings.Theme.AccentIntensity = ThemeCatalog.NormalizeAccentIntensity(intensity);
        settings.Player.FadeIdleDelayMs = PlayerAppearancePolicy.NormalizeFadeIdleDelayMs(fadeIdleDelayMs);
        settings.Theme.FadeDelayPreset = ThemeCatalog.FadeDelayPresetForMilliseconds(settings.Player.FadeIdleDelayMs);
        // Global compact-mode default takes effect on the NEXT popout; an open player keeps its mode.
        settings.Player.CompactMode = compactMode;
        settings.Theme.CornerStyle = ThemeCatalog.NormalizeCornerStyle(cornerStyle);
        // Same repair rule as settings-load (WindowOpacityPolicy.NormalizeOptional): an invalid
        // explicit override becomes null = "follow the preset", never a synthetic 1.0 override.
        settings.Theme.ActiveWindowOpacity = WindowOpacityPolicy.NormalizeOptional(activeOpacityOverride);
        settings.Theme.IdleWindowOpacity = WindowOpacityPolicy.NormalizeOptional(idleOpacityOverride);
        settings.Theme.StripAutoHide = stripAutoHideOverride;
        // Legacy mirrors (kept readable for pre-theme builds, overhaul Task 10) follow the
        // EFFECTIVE values; the schema-3 resolver never reads them while a theme block exists.
        settings.Player.ConstantWindowOpacity = ThemePreferenceResolver.ActiveWindowOpacity(settings.Theme, settings.Player);
        settings.Player.IdleWindowOpacity = ThemePreferenceResolver.IdleWindowOpacity(settings.Theme, settings.Player);
        settings.Player.StripAutoHide = ThemePreferenceResolver.StripAutoHide(settings.Theme, settings.Player);
    }
}

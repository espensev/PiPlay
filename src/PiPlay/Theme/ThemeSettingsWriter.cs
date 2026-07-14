using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Theme;

/// <summary>
/// Writes the Settings-dialog appearance payload onto <see cref="AppSettings"/> (theme code
/// review P2): theme identity plus NULLABLE behavior overrides land on Theme — null means
/// "follow the selected preset's default", so an accent-only apply must not materialize
/// overrides — while the profile-aware accent owner routes the effective color to either the active
/// profile or the global fallback, and legacy Player mirrors carry EFFECTIVE values for pre-theme
/// readers. Pure model mapping (no resources, no windows) so the Logic lane can pin the transaction.
/// </summary>
public static class ThemeSettingsWriter
{
    public static void Apply(AppSettings settings, string themeId, string accentColor, int fadeIdleDelayMs,
        bool compactMode, double? activeOpacityOverride, double? idleOpacityOverride,
        bool? stripAutoHideOverride, string cornerStyle, int? accentIntensity = null)
    {
        var previousPreset = ThemeCatalog.PresetFor(settings.Theme.ThemeId);
        var normalizedThemeId = ThemeCatalog.NormalizeThemeId(themeId);
        var nextPreset = ThemeCatalog.PresetFor(normalizedThemeId);
        // A profile target stays exact across preset switches, but the hidden GLOBAL fallback keeps its
        // normal preset-default behavior. Only an untouched old default advances; a custom global stays.
        if (ProfileAccentService.AccentOverridingProfile(settings) is not null)
        {
            settings.Theme.AccentColor = ThemeCatalog.AccentForThemeSwitch(
                settings.Theme.AccentColor, previousPreset, nextPreset);
        }

        settings.Theme.ThemeId = normalizedThemeId;
        // Settings edits whichever accent is painting the app. Route through the one owner that knows
        // whether that is an active profile or Theme.AccentColor. Never copy the effective profile value
        // into the global fallback; the preset transition above is the only profile-active global write.
        ProfileAccentService.CommitAccent(settings, accentColor);
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

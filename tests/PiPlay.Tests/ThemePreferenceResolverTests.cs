using PiPlay.Models;
using PiPlay.Theme;

namespace PiPlay.Tests;

/// <summary>
/// Effective-behavior resolution (ThemeCatalog and ThemeColors): preset default → explicit override →
/// normalized. The raw-PlayerSettings fallback exists ONLY for the no-theme-block legacy path —
/// ThemeSettings.FromLegacy copies legacy behavior values into explicit overrides at seed time.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Logic)]
public class ThemePreferenceResolverTests
{
    [Fact]
    public void Preset_defaults_apply_when_overrides_are_null()
    {
        // A hand-edited "themeId": "soft-glass" with no overrides IS the translucent preset —
        // the legacy player values must not leak through once a theme block exists.
        var theme = new ThemeSettings { ThemeId = "soft-glass" };
        var player = new PlayerSettings { ConstantWindowOpacity = 0.9, IdleWindowOpacity = 0.7, StripAutoHide = true };

        var preset = ThemeCatalog.PresetFor("soft-glass");
        Assert.Equal(preset.DefaultActiveWindowOpacity, ThemePreferenceResolver.ActiveWindowOpacity(theme, player));
        Assert.Equal(preset.DefaultIdleWindowOpacity, ThemePreferenceResolver.IdleWindowOpacity(theme, player));
        Assert.Equal(preset.DefaultStripAutoHide, ThemePreferenceResolver.StripAutoHide(theme, player));
    }

    [Fact]
    public void Explicit_overrides_win_over_preset_defaults()
    {
        var theme = new ThemeSettings
        {
            ThemeId = "soft-glass",
            ActiveWindowOpacity = 0.6,
            IdleWindowOpacity = 0.5,
            StripAutoHide = true,
        };

        Assert.Equal(0.6, ThemePreferenceResolver.ActiveWindowOpacity(theme, new PlayerSettings()));
        Assert.Equal(0.5, ThemePreferenceResolver.IdleWindowOpacity(theme, new PlayerSettings()));
        Assert.True(ThemePreferenceResolver.StripAutoHide(theme, new PlayerSettings()));
    }

    [Fact]
    public void Invalid_theme_id_resolves_to_sharp_dark_defaults()
    {
        var theme = new ThemeSettings { ThemeId = "hologram" };
        var player = new PlayerSettings { ConstantWindowOpacity = 0.9, IdleWindowOpacity = 0.7 };

        var sharpDark = ThemeCatalog.PresetFor(ThemeCatalog.DefaultThemeId);
        Assert.Equal(sharpDark.DefaultActiveWindowOpacity, ThemePreferenceResolver.ActiveWindowOpacity(theme, player));
        Assert.Equal(sharpDark.DefaultIdleWindowOpacity, ThemePreferenceResolver.IdleWindowOpacity(theme, player));
    }

    [Fact]
    public void Missing_theme_block_falls_back_to_legacy_player_values()
    {
        // Defensive null-theme path only (real flows sanitize Theme to non-null and FromLegacy
        // seeds explicit overrides): raw legacy behavior still resolves.
        var player = new PlayerSettings { ConstantWindowOpacity = 0.8, IdleWindowOpacity = 0.6, StripAutoHide = true };

        Assert.Equal(0.8, ThemePreferenceResolver.ActiveWindowOpacity(null, player));
        Assert.Equal(0.6, ThemePreferenceResolver.IdleWindowOpacity(null, player));
        Assert.True(ThemePreferenceResolver.StripAutoHide(null, player));
    }

    [Fact]
    public void From_legacy_seeds_behavior_values_as_explicit_overrides()
    {
        var player = new PlayerSettings
        {
            PinAccent = "green",
            FadeIdleDelayMs = 4000,
            ConstantWindowOpacity = 0.82,
            IdleWindowOpacity = 0.44,
            StripAutoHide = true,
        };

        var seeded = ThemeSettings.FromLegacy(player);

        Assert.Equal(0.82, seeded.ActiveWindowOpacity);
        Assert.Equal(0.44, seeded.IdleWindowOpacity);
        Assert.True(seeded.StripAutoHide);
        // Seeded overrides shield the migrated look from every preset's defaults.
        Assert.Equal(0.82, ThemePreferenceResolver.ActiveWindowOpacity(seeded, new PlayerSettings()));
    }
}

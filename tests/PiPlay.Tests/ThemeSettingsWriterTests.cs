using PiPlay.Models;
using PiPlay.Theme;

namespace PiPlay.Tests;

/// <summary>
/// The Settings-apply model mapping (theme code review P2): nullable behavior overrides land on
/// Theme unchanged — null stays null so an accent-only apply never detaches the user from preset
/// behavior defaults — while the legacy Player mirrors carry the EFFECTIVE values.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Logic)]
public class ThemeSettingsWriterTests
{
    [Fact]
    public void Accent_only_apply_preserves_null_behavior_overrides()
    {
        // A fresh schema-3 file: behavior follows the preset. Changing ONLY the accent must keep
        // it that way — the P2 regression was materializing these nulls as explicit overrides.
        var settings = new AppSettings { Theme = new ThemeSettings { ThemeId = "soft-glass" } };

        ThemeSettingsWriter.Apply(settings, "soft-glass", "#FFC857", 2500, compactMode: false,
            activeOpacityOverride: null, idleOpacityOverride: null, stripAutoHideOverride: null,
            cornerStyle: "theme");

        Assert.Equal("#FFC857", settings.Theme.AccentColor);
        Assert.Null(settings.Theme.ActiveWindowOpacity);
        Assert.Null(settings.Theme.IdleWindowOpacity);
        Assert.Null(settings.Theme.StripAutoHide);

        // The legacy mirrors carry the EFFECTIVE (preset-default) values for old readers.
        var preset = ThemeCatalog.PresetFor("soft-glass");
        Assert.Equal(preset.DefaultActiveWindowOpacity, settings.Player.ConstantWindowOpacity);
        Assert.Equal(preset.DefaultIdleWindowOpacity, settings.Player.IdleWindowOpacity);
        Assert.Equal(preset.DefaultStripAutoHide, settings.Player.StripAutoHide);
    }

    [Fact]
    public void Explicit_overrides_write_normalized_and_mirror_their_effective_values()
    {
        var settings = new AppSettings();

        ThemeSettingsWriter.Apply(settings, "sharp-dark", "#00D4FF", 1500, compactMode: true,
            activeOpacityOverride: 0.8, idleOpacityOverride: 5.0, stripAutoHideOverride: true,
            cornerStyle: "round");

        Assert.Equal(0.8, settings.Theme.ActiveWindowOpacity);
        // A junk override repairs to NULL = follow the preset (the same NormalizeOptional rule
        // as settings-load repair — PR #21 review note), never to a synthetic 1.0 override.
        Assert.Null(settings.Theme.IdleWindowOpacity);
        Assert.True(settings.Theme.StripAutoHide);
        Assert.Equal("round", settings.Theme.CornerStyle);
        Assert.Equal(1500, settings.Player.FadeIdleDelayMs);
        Assert.Equal("short", settings.Theme.FadeDelayPreset);
        Assert.True(settings.Player.CompactMode);
        Assert.Equal(0.8, settings.Player.ConstantWindowOpacity);
        // The idle mirror carries the EFFECTIVE value: sharp-dark's idle default.
        Assert.Equal(ThemeCatalog.PresetFor("sharp-dark").DefaultIdleWindowOpacity, settings.Player.IdleWindowOpacity);
        Assert.True(settings.Player.StripAutoHide);
    }

    [Fact]
    public void Identity_inputs_are_normalized()
    {
        var settings = new AppSettings();

        ThemeSettingsWriter.Apply(settings, "HOLOGRAM", "a78bfa", 2500, compactMode: false,
            activeOpacityOverride: null, idleOpacityOverride: null, stripAutoHideOverride: null,
            cornerStyle: "dodecagon");

        Assert.Equal(ThemeCatalog.DefaultThemeId, settings.Theme.ThemeId);
        Assert.Equal("#A78BFA", settings.Theme.AccentColor);
        Assert.Equal(ThemeCatalog.DefaultCornerStyle, settings.Theme.CornerStyle);
    }
}

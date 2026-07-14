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

    // --- Accent intensity: the user's "how far does the accent reach" dial ---

    [Fact]
    public void The_accent_intensity_dial_is_persisted_and_clamped()
    {
        var settings = new AppSettings();

        ThemeSettingsWriter.Apply(settings, "sharp-dark", "#00D4FF", 1500, compactMode: false,
            activeOpacityOverride: null, idleOpacityOverride: null, stripAutoHideOverride: null,
            cornerStyle: "theme", accentIntensity: 0);

        Assert.Equal(0, settings.Theme.AccentIntensity);   // 0 is a real choice, not "unset"

        ThemeSettingsWriter.Apply(settings, "sharp-dark", "#00D4FF", 1500, compactMode: false,
            activeOpacityOverride: null, idleOpacityOverride: null, stripAutoHideOverride: null,
            cornerStyle: "theme", accentIntensity: 4000);

        Assert.Equal(100, settings.Theme.AccentIntensity);
    }

    [Fact]
    public void Settings_apply_routes_the_effective_accent_without_overwriting_the_global_fallback()
    {
        var settings = new AppSettings
        {
            ActiveProfileName = "Violet",
            Theme = new ThemeSettings { AccentColor = "#2BAED0" },
            Profiles =
            {
                new Profile
                {
                    Name = "Violet",
                    Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    AccentColor = "#A78BFA",
                },
            },
        };

        // The wheel was untouched: this is the broad AppearanceChanged path caused by another control.
        ThemeSettingsWriter.Apply(settings, "sharp-dark", "#A78BFA", 2500, compactMode: false,
            activeOpacityOverride: null, idleOpacityOverride: null, stripAutoHideOverride: null,
            cornerStyle: "round", accentIntensity: 75);

        Assert.Equal("#2BAED0", settings.Theme.AccentColor);
        Assert.Equal("#A78BFA", settings.Profiles.Single().AccentColor);

        // If the wheel is changed, the same Settings -> Done contract edits only the named profile.
        ThemeSettingsWriter.Apply(settings, "sharp-dark", "#38D996", 2500, compactMode: false,
            activeOpacityOverride: null, idleOpacityOverride: null, stripAutoHideOverride: null,
            cornerStyle: "round", accentIntensity: 75);

        Assert.Equal("#2BAED0", settings.Theme.AccentColor);
        Assert.Equal("#38D996", settings.Profiles.Single().AccentColor);
    }

    [Fact]
    public void Preset_switch_under_a_profile_advances_only_an_untouched_global_preset_default()
    {
        var settings = new AppSettings
        {
            ActiveProfileName = "Cyan profile",
            Theme = new ThemeSettings
            {
                ThemeId = "sharp-dark",
                AccentColor = ThemeCatalog.PresetFor("sharp-dark").DefaultAccentColor,
            },
            Profiles =
            {
                new Profile
                {
                    Name = "Cyan profile",
                    Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    // Equal bytes do not make this preset-owned: it is an explicit profile value.
                    AccentColor = ThemeCatalog.PresetFor("sharp-dark").DefaultAccentColor,
                },
            },
        };

        ThemeSettingsWriter.Apply(settings, "soft-glass", settings.Profiles.Single().AccentColor!,
            2500, compactMode: false, activeOpacityOverride: null, idleOpacityOverride: null,
            stripAutoHideOverride: null, cornerStyle: "theme");

        Assert.Equal(ThemeCatalog.PresetFor("soft-glass").DefaultAccentColor, settings.Theme.AccentColor);
        Assert.Equal(ThemeCatalog.PresetFor("sharp-dark").DefaultAccentColor,
            settings.Profiles.Single().AccentColor);
    }

    /// <summary>
    /// The dial is a USER preference, like the accent color — an apply that does not carry it (a preset
    /// switch, or any older caller that omits the argument) must LEAVE IT ALONE. If null were treated as
    /// "reset to default", switching theme presets would silently wipe a user who had set the dial to 0.
    /// </summary>
    [Fact]
    public void An_apply_that_does_not_carry_the_dial_leaves_the_saved_value_alone()
    {
        var settings = new AppSettings { Theme = new ThemeSettings { AccentIntensity = 0 } };

        ThemeSettingsWriter.Apply(settings, "minimal", "#38D996", 1500, compactMode: false,
            activeOpacityOverride: null, idleOpacityOverride: null, stripAutoHideOverride: null,
            cornerStyle: "theme");   // a preset switch: says nothing about the dial

        Assert.Equal(0, settings.Theme.AccentIntensity);
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

    [Fact]
    public void Dark_app_accent_is_stored_exactly_while_presentation_is_derived_REQ_UI_01()
    {
        var settings = new AppSettings();

        ThemeSettingsWriter.Apply(settings, "sharp-dark", "#131820", 2500, compactMode: false,
            activeOpacityOverride: null, idleOpacityOverride: null, stripAutoHideOverride: null,
            cornerStyle: "theme");

        Assert.Equal("#131820", settings.Theme.AccentColor);
        Assert.NotEqual(ThemeColors.ParseColor(settings.Theme.AccentColor),
            ThemeColors.DeriveAccentSet(settings.Theme.AccentColor, ThemeCatalog.PresetFor("sharp-dark")).Primary);
    }
}

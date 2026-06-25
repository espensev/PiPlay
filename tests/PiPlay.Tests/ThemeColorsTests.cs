using System.Windows.Media;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ThemeColorsTests
{
    [Fact]
    public void ParseColor_reads_a_normalized_hex()
    {
        Assert.Equal(Color.FromRgb(0x00, 0xD4, 0xFF), ThemeColors.ParseColor("#00D4FF"));
        Assert.Equal(Color.FromRgb(0xA7, 0x8B, 0xFA), ThemeColors.ParseColor("a78bfa"));
    }

    [Fact]
    public void ParseColor_falls_back_to_the_catalog_default_for_junk()
    {
        // Delegates to ThemeCatalog.NormalizeAccentColor, so invalid input can never throw.
        Assert.Equal(ThemeColors.ParseColor(ThemeCatalog.DefaultAccentColor), ThemeColors.ParseColor("not-a-color"));
        Assert.Equal(ThemeColors.ParseColor(ThemeCatalog.DefaultAccentColor), ThemeColors.ParseColor(null));
    }

    [Fact]
    public void Lighten_blends_toward_white_and_clamps_the_amount()
    {
        var accent = Color.FromRgb(0x00, 0xD4, 0xFF);

        Assert.Equal(accent, ThemeColors.Lighten(accent, 0.0));
        Assert.Equal(Colors.White, ThemeColors.Lighten(accent, 1.0));
        Assert.Equal(Colors.White, ThemeColors.Lighten(accent, 5.0));   // clamped

        var lighter = ThemeColors.Lighten(accent, 0.30);
        Assert.True(lighter.R > accent.R && lighter.G >= accent.G && lighter.B >= accent.B);
    }

    [Fact]
    public void Brush_builds_a_frozen_accent_brush()
    {
        var brush = ThemeColors.Brush("#38D996");

        Assert.True(brush.IsFrozen);   // safe to share across the source window and popout
        Assert.Equal(Color.FromRgb(0x38, 0xD9, 0x96), brush.Color);
    }

    // --- theme-v2 derived accent tokens (Phase B / CON-1) ---

    [Fact]
    public void Mix_blends_channelwise_and_clamps()
    {
        var black = Colors.Black;
        var white = Colors.White;

        Assert.Equal(black, ThemeColors.Mix(black, white, 0.0));
        Assert.Equal(white, ThemeColors.Mix(black, white, 1.0));
        Assert.Equal(white, ThemeColors.Mix(black, white, 5.0));               // clamped
        Assert.Equal(Color.FromRgb(0x80, 0x80, 0x80), ThemeColors.Mix(black, white, 0.5));   // round(127.5)->128
    }

    [Fact]
    public void WithAlpha_sets_alpha_and_keeps_rgb()
    {
        var c = ThemeColors.ParseColor("#00D4FF");
        var a = ThemeColors.WithAlpha(c, 0x33);

        Assert.Equal(0x33, a.A);
        Assert.Equal(c.R, a.R);
        Assert.Equal(c.G, a.G);
        Assert.Equal(c.B, a.B);
    }

    [Fact]
    public void PickReadableForeground_picks_dark_on_a_bright_accent()
    {
        // Every shipped accent is bright enough to carry the dark button text (>= 4.5:1).
        Assert.Equal(Color.FromRgb(0x06, 0x14, 0x1A), ThemeColors.PickReadableForeground(ThemeColors.ParseColor("#00D4FF")));
    }

    [Fact]
    public void PickReadableForeground_picks_white_when_dark_text_fails()
    {
        // Pressed steel (#4A8FAB mixed 16% toward black) drops the dark text below 4.5:1 — the CON-1
        // case — so white is the readable choice.
        var pressedSteel = ThemeColors.Mix(ThemeColors.ParseColor("#4A8FAB"), Colors.Black, 0.16);

        Assert.True(ThemeColors.ContrastRatio(Color.FromRgb(0x06, 0x14, 0x1A), pressedSteel) < 4.5);
        Assert.Equal(Colors.White, ThemeColors.PickReadableForeground(pressedSteel));
    }

    [Fact]
    public void PickReadableForeground_returns_the_best_candidate_for_mid_tones()
    {
        // A mid-tone in the WCAG dead zone is below 4.5:1 against BOTH dark and white. The app now
        // accepts user colors anyway and returns the better of the two instead of rejecting it.
        var deadZone = Color.FromRgb(0x78, 0x78, 0x78);

        var dark = Color.FromRgb(0x06, 0x14, 0x1A);
        var darkContrast = ThemeColors.ContrastRatio(dark, deadZone);
        var whiteContrast = ThemeColors.ContrastRatio(Colors.White, deadZone);
        Assert.True(darkContrast < 4.5);
        Assert.True(whiteContrast < 4.5);
        Assert.Equal(whiteContrast > darkContrast ? Colors.White : dark, ThemeColors.PickReadableForeground(deadZone));
    }

    [Theory]
    [InlineData("sharp-dark", 0.18, 0.16, 0.58, 0.10, 0x22, 0x33)]
    [InlineData("minimal", 0.22, 0.14, 0.50, 0.12, 0x26, 0x40)]
    [InlineData("soft-glass", 0.30, 0.12, 0.40, 0.16, 0x33, 0x66)]
    public void AccentProfile_matches_the_spec_literals(
        string themeId, double hover, double pressed, double muted, double border, int subtleAlpha, int glowAlpha)
    {
        var p = ThemeColors.AccentProfileFor(themeId);

        Assert.Equal(hover, p.HoverWhiteMix);
        Assert.Equal(pressed, p.PressedBlackMix);
        Assert.Equal(muted, p.MutedSurfaceMix);
        Assert.Equal(border, p.BorderWhiteMix);
        Assert.Equal((byte)subtleAlpha, p.SubtleAlpha);
        Assert.Equal((byte)glowAlpha, p.GlowAlpha);
    }

    [Fact]
    public void DeriveAccentSet_follows_the_profile_for_the_theme()
    {
        var sharp = ThemeCatalog.PresetFor("sharp-dark");
        var set = ThemeColors.DeriveAccentSet("#4A8FAB", sharp);
        var profile = ThemeColors.AccentProfileFor("sharp-dark");
        var primary = ThemeColors.ParseColor("#4A8FAB");

        Assert.Equal(primary, set.Primary);
        Assert.Equal(ThemeColors.Mix(primary, Colors.White, profile.HoverWhiteMix), set.Hover);
        Assert.Equal(ThemeColors.Mix(primary, Colors.Black, profile.PressedBlackMix), set.Pressed);
        Assert.Equal(profile.SubtleAlpha, set.Subtle.A);
        Assert.Equal(profile.GlowAlpha, set.Glow.A);
    }
}

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

    /// <summary>
    /// The title-bar wash must be VISIBLE (it used to sit at 1.20:1, which is close to imperceptible —
    /// that is why the accent effectively painted one button) but must remain a TINT. The ceiling is the
    /// real guard: a saturated title bar re-adds the heavy framed look P1 exists to remove, so a future
    /// nudge upward has to fail this test rather than quietly ship a banner. Owner-tunable via UI-CHK-9.
    /// </summary>
    [Theory]
    [InlineData("sharp-dark")]
    [InlineData("minimal")]
    [InlineData("soft-glass")]
    public void Shell_tint_wash_is_visible_but_never_a_banner(string presetId)
    {
        var preset = ThemeCatalog.PresetFor(presetId);
        var surfaceBase = ThemeColors.ParseColor(preset.Palette.SurfaceBase);

        foreach (var accent in new[] { "#00D4FF", "#A78BFA", "#38D996", "#FFC857", "#0B0E11" })
        {
            var shell = ThemeColors.DeriveAccentSet(accent, preset).ShellTint;
            var ratio = ThemeColors.ContrastRatio(shell, surfaceBase);

            Assert.True(ratio > 1.20,
                $"{presetId}/{accent}: wash is {ratio:F2}:1 — no more visible than the old imperceptible 1.20.");
            Assert.True(ratio <= 1.90,
                $"{presetId}/{accent}: wash is {ratio:F2}:1 — that is a banner, not a tint.");
        }
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

    [Theory]
    [InlineData("sharp-dark")]
    [InlineData("minimal")]
    [InlineData("soft-glass")]
    public void Dark_custom_app_accent_stays_visible_across_window_chrome_REQ_UI_01(string themeId)
    {
        var preset = ThemeCatalog.PresetFor(themeId);
        var set = ThemeColors.DeriveAccentSet(preset.Palette.SurfaceRaised, preset);
        var surfaces = new[]
        {
            preset.Palette.AppBackground,
            preset.Palette.SurfaceBase,
            preset.Palette.SurfaceRaised,
            preset.Palette.SurfaceHover,
        };

        foreach (var surface in surfaces)
        {
            var ratio = Wcag.ContrastRatio(Hex(set.Primary), surface);
            Assert.True(ratio >= 3.0,
                $"{themeId}: derived AccentPrimary on {surface} is only {ratio:F2}:1.");
        }

        var shellRatio = Wcag.ContrastRatio(Hex(set.ShellTint), preset.Palette.SurfaceBase);
        Assert.True(shellRatio >= 1.20,
            $"{themeId}: AccentShellTint is only {shellRatio:F2}:1 against SurfaceBase.");
    }

    [Fact]
    public void EnsureContrast_preserves_a_passing_color_and_only_changes_presentation_REQ_UI_01()
    {
        var violet = ThemeColors.ParseColor("#A78BFA");
        var surface = ThemeColors.ParseColor(ThemeCatalog.PresetFor("sharp-dark").Palette.SurfaceHover);

        Assert.Equal(violet, ThemeColors.EnsureContrast(violet, surface));

        var dark = ThemeColors.ParseColor("#131820");
        var visible = ThemeColors.EnsureContrast(dark, surface);
        Assert.NotEqual(dark, visible);
        Assert.True(ThemeColors.ContrastRatio(visible, surface) >= 3.0);
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#050609")]
    [InlineData("#131820")]
    public void EnsureContrast_lifts_representative_dark_colors_in_every_theme_REQ_UI_01(string hex)
    {
        var raw = ThemeColors.ParseColor(hex);
        foreach (var preset in ThemeCatalog.Presets)
        {
            var surface = ThemeColors.ParseColor(preset.Palette.SurfaceHover);
            var visible = ThemeColors.EnsureContrast(raw, surface);
            Assert.True(ThemeColors.ContrastRatio(visible, surface) >= 3.0,
                $"{preset.Id}/{hex} did not reach 3:1.");
        }
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#050609")]
    [InlineData("#101010")]
    [InlineData("#131820")]
    public void Dark_accent_pressed_state_remains_visible_and_readable_REQ_UI_01(string hex)
    {
        foreach (var preset in ThemeCatalog.Presets)
        {
            var set = ThemeColors.DeriveAccentSet(hex, preset);
            var surface = preset.Palette.SurfaceHover;
            var stateDelta = Wcag.ContrastRatio(Hex(set.Primary), Hex(set.Pressed));
            var pressedOnSurface = Wcag.ContrastRatio(Hex(set.Pressed), surface);
            var foregroundOnPrimary = Wcag.ContrastRatio(Hex(set.OnAccent), Hex(set.Primary));
            var foregroundOnPressed = Wcag.ContrastRatio(Hex(set.OnAccentPressed), Hex(set.Pressed));

            Assert.True(stateDelta >= 1.10,
                $"{preset.Id}/{hex}: Primary-to-Pressed is only {stateDelta:F2}:1.");
            Assert.True(pressedOnSurface >= 3.0,
                $"{preset.Id}/{hex}: Pressed on SurfaceHover is only {pressedOnSurface:F2}:1.");
            Assert.True(foregroundOnPrimary >= 4.5,
                $"{preset.Id}/{hex}: OnAccent is only {foregroundOnPrimary:F2}:1.");
            Assert.True(foregroundOnPressed >= 4.5,
                $"{preset.Id}/{hex}: OnAccentPressed is only {foregroundOnPressed:F2}:1.");
        }
    }

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}

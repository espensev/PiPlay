using System.Windows.Media;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ThemeColorsTests
{
    [Theory]
    [InlineData("#00D4FF", 0x00, 0xD4, 0xFF)]
    [InlineData("a78bfa", 0xA7, 0x8B, 0xFA)]
    [InlineData("not-a-color", 0x2B, 0xAE, 0xD0)]
    [InlineData(null, 0x2B, 0xAE, 0xD0)]
    public void ParseColor_reads_normalized_hex_or_falls_back_to_catalog_default(
        string? input, byte r, byte g, byte b)
    {
        Assert.Equal(Color.FromRgb(r, g, b), ThemeColors.ParseColor(input));
    }

    /// <summary>
    /// The title-bar wash must be VISIBLE at the default intensity (above 1.20:1) while both default
    /// and full reach (intensity 100) remain a TINT rather than a saturated banner (<= 1.90:1 plus
    /// one 8-bit quantization step at full reach).
    /// </summary>
    [Theory]
    [InlineData("sharp-dark")]
    [InlineData("minimal")]
    [InlineData("soft-glass")]
    public void Shell_tint_wash_is_visible_by_default_and_never_a_banner_up_to_full_reach(string presetId)
    {
        const double BannerLine = 1.90;
        const double QuantizationSlack = 0.05;   // one 8-bit mix step, not design headroom

        var preset = ThemeCatalog.PresetFor(presetId);
        var surfaceBase = ThemeColors.ParseColor(preset.Palette.SurfaceBase);

        foreach (var accent in new[] { "#00D4FF", "#A78BFA", "#38D996", "#FFC857", "#0B0E11" })
        {
            var defaultShell = ThemeColors.DeriveAccentSet(accent, preset).ShellTint;
            var defaultRatio = ThemeColors.ContrastRatio(defaultShell, surfaceBase);

            Assert.True(defaultRatio > 1.20,
                $"{presetId}/{accent}: wash is {defaultRatio:F2}:1 — no more visible than the old imperceptible 1.20.");
            Assert.True(defaultRatio <= BannerLine,
                $"{presetId}/{accent}: wash is {defaultRatio:F2}:1 — that is a banner, not a tint.");
        }

        foreach (var accent in new[] { "#00D4FF", "#A78BFA", "#38D996", "#FFC857", "#0B0E11", "#FFFFFF" })
        {
            var fullRatio = ThemeColors.ContrastRatio(
                ThemeColors.DeriveAccentSet(accent, preset, 100).ShellTint, surfaceBase);

            Assert.True(fullRatio <= BannerLine + QuantizationSlack,
                $"{presetId}/{accent} @ full reach washes at {fullRatio:F2}:1 — that is a banner, not a tint.");
        }
    }

    // --- Accent intensity: the user's dial for how far the accent reaches (v0.10.0) ---

    /// <summary>
    /// The default is a compatibility point, not merely the middle of two arbitrary lerps. PiPlay
    /// v0.9.0 shipped full-accent toolbar glyphs and this exact 1.45 wash; reach 50 must reproduce both
    /// bytes so enabling the preference does not quietly weaken P2 for every existing user.
    /// </summary>
    [Fact]
    public void Default_intensity_reproduces_the_deployed_v0_9_0_accent_reach()
    {
        var set = ThemeColors.DeriveAccentSet(
            ThemeCatalog.DefaultAccentColor,
            ThemeCatalog.PresetFor(ThemeCatalog.DefaultThemeId),
            ThemeCatalog.DefaultAccentIntensity);

        Assert.Equal(Color.FromRgb(0x2B, 0xAE, 0xD0), set.ChromeGlyph);
        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x3F), set.ShellTint);
    }

    /// <summary>
    /// Intensity 0 must mean OFF across every accent-reach token: no title-bar wash, neutral chrome
    /// glyphs, pure-black letterbox, flat palette window background, and transparent popout edge.
    /// </summary>
    [Theory]
    [InlineData("sharp-dark")]
    [InlineData("minimal")]
    [InlineData("soft-glass")]
    public void Intensity_zero_turns_the_accent_reach_completely_off(string presetId)
    {
        var preset = ThemeCatalog.PresetFor(presetId);
        var surfaceBase = ThemeColors.ParseColor(preset.Palette.SurfaceBase);
        var textPrimary = ThemeColors.ParseColor(preset.Palette.TextPrimary);
        var appBackground = ThemeColors.ParseColor(preset.Palette.AppBackground);

        var set = ThemeColors.DeriveAccentSet("#A78BFA", preset, 0);

        Assert.Equal(surfaceBase, set.ShellTint);
        Assert.Equal(textPrimary, set.ChromeGlyph);
        Assert.Equal(Colors.Black, set.Letterbox);
        Assert.Equal(appBackground, set.BackgroundWash);
        Assert.Equal((byte)0, set.PopoutEdge.A);
    }

    /// <summary>
    /// Glyph reach finishes at the midpoint. Above 50 the glyph stays fully accented while the
    /// independently linear wash continues to deepen.
    /// </summary>
    [Theory]
    [InlineData("sharp-dark")]
    [InlineData("minimal")]
    [InlineData("soft-glass")]
    public void Intensity_fifty_and_above_gives_the_glyphs_the_full_accent(string presetId)
    {
        var preset = ThemeCatalog.PresetFor(presetId);
        foreach (var intensity in new[] { 50, 75, 100 })
        {
            var set = ThemeColors.DeriveAccentSet("#A78BFA", preset, intensity);
            Assert.Equal(set.Primary, set.ChromeGlyph);
        }
    }

    // --- Background room tones (docs/Theme_Preset_Differences.md) ---

    [Theory]
    [InlineData("sharp-dark")]
    [InlineData("minimal")]
    [InlineData("soft-glass")]
    public void Backgrounds_follow_the_dial_as_faint_room_tints(string presetId)
    {
        var preset = ThemeCatalog.PresetFor(presetId);
        var appBackground = ThemeColors.ParseColor(preset.Palette.AppBackground);

        var at25 = ThemeColors.DeriveAccentSet("#A78BFA", preset, 25);
        var at50 = ThemeColors.DeriveAccentSet("#A78BFA", preset, 50);
        var at100 = ThemeColors.DeriveAccentSet("#A78BFA", preset, 100);

        Assert.Equal(Color.FromRgb(3, 2, 4), at25.Letterbox);
        Assert.Equal(Color.FromRgb(5, 4, 8), at50.Letterbox);
        Assert.Equal(Color.FromRgb(10, 8, 15), at100.Letterbox);

        var ratio25 = ThemeColors.ContrastRatio(at25.BackgroundWash, appBackground);
        var ratio50 = ThemeColors.ContrastRatio(at50.BackgroundWash, appBackground);
        var ratio100 = ThemeColors.ContrastRatio(at100.BackgroundWash, appBackground);
        Assert.True(1.0 < ratio25 && ratio25 < ratio50 && ratio50 < ratio100 && ratio100 < 1.15,
            $"{presetId}: BackgroundWash contrast progression {ratio25:F3} -> {ratio50:F3} -> {ratio100:F3} out of bounds.");
    }

    [Fact]
    public void Letterbox_stays_near_black_even_for_the_brightest_accent()
    {
        // The letterbox frames the video: a room tint, never a color. Even a white accent at full
        // intensity must stay within a near-black ceiling.
        var set = ThemeColors.DeriveAccentSet("#FFFFFF", ThemeCatalog.PresetFor("sharp-dark"), 100);
        Assert.True(set.Letterbox is { R: <= 16, G: <= 16, B: <= 16 },
            $"letterbox {set.Letterbox} left the near-black ceiling.");
    }

    [Theory]
    [InlineData("sharp-dark")]
    [InlineData("minimal")]
    [InlineData("soft-glass")]
    public void Popout_edge_fades_with_the_dial(string presetId)
    {
        // The active-profile popout border is the derived Border tone alpha-scaled by the dial:
        // fully transparent at 0 (the dial contract), fully present at 100.
        var preset = ThemeCatalog.PresetFor(presetId);
        Assert.Equal((byte)0, ThemeColors.DeriveAccentSet("#A78BFA", preset, 0).PopoutEdge.A);

        var mid = ThemeColors.DeriveAccentSet("#A78BFA", preset, 50);
        Assert.Equal(ThemeColors.WithAlpha(mid.Border, 128), mid.PopoutEdge);

        var full = ThemeColors.DeriveAccentSet("#A78BFA", preset, 100);
        Assert.Equal(ThemeColors.WithAlpha(full.Border, 255), full.PopoutEdge);
    }

    [Theory]
    [InlineData("sharp-dark")]
    [InlineData("minimal")]
    [InlineData("soft-glass")]
    public void Background_wash_keeps_primary_text_readable_at_full_intensity(string presetId)
    {
        var preset = ThemeCatalog.PresetFor(presetId);
        var text = ThemeColors.ParseColor(preset.Palette.TextPrimary);
        foreach (var accent in new[] { "#FFFFFF", "#2BAED0", "#A78BFA" })
        {
            var wash = ThemeColors.DeriveAccentSet(accent, preset, 100).BackgroundWash;
            var ratio = ThemeColors.ContrastRatio(text, wash);
            Assert.True(ratio >= 4.5,
                $"{presetId}: TextPrimary on the {accent} wash = {ratio:F2}:1, below WCAG AA 4.5:1.");
        }
    }

    [Theory]
    [InlineData("sharp-dark", 0xCE, 0xC1, 0xFA)]
    [InlineData("minimal", 0xCE, 0xBD, 0xF0)]
    [InlineData("soft-glass", 0xCC, 0xC0, 0xFB)]
    public void Intensity_twenty_five_is_halfway_through_the_glyph_curve(
        string presetId, byte r, byte g, byte b)
    {
        var preset = ThemeCatalog.PresetFor(presetId);
        var set = ThemeColors.DeriveAccentSet("#A78BFA", preset, 25);

        Assert.Equal(Color.FromRgb(r, g, b), set.ChromeGlyph);
    }

    /// <summary>
    /// The dial must actually be a dial: turning it up strictly increases the wash. A non-monotonic
    /// mapping would make the slider feel broken.
    /// </summary>
    [Fact]
    public void Raising_the_intensity_strictly_strengthens_the_wash()
    {
        var preset = ThemeCatalog.PresetFor("sharp-dark");
        var surfaceBase = ThemeColors.ParseColor(preset.Palette.SurfaceBase);

        var previous = 0.0;
        foreach (var intensity in new[] { 0, 20, 40, 50, 60, 75, 80, 100 })
        {
            var ratio = ThemeColors.ContrastRatio(
                ThemeColors.DeriveAccentSet("#00D4FF", preset, intensity).ShellTint, surfaceBase);
            Assert.True(ratio > previous,
                $"intensity {intensity} washes at {ratio:F2}:1, not stronger than the step below ({previous:F2}:1).");
            previous = ratio;
        }
    }

    /// <summary>
    /// The safety property that makes this dial shippable: at EVERY intensity, for every preset, and even
    /// for a near-black accent, the toolbar glyph must stay legible. Without this, dragging the slider
    /// could silently render the toolbar invisible.
    /// </summary>
    [Theory]
    [InlineData("sharp-dark")]
    [InlineData("minimal")]
    [InlineData("soft-glass")]
    public void Chrome_glyphs_stay_legible_at_every_intensity(string presetId)
    {
        var preset = ThemeCatalog.PresetFor(presetId);
        var surfaceHover = ThemeColors.ParseColor(preset.Palette.SurfaceHover);

        foreach (var accent in new[] { "#00D4FF", "#A78BFA", "#0B0E11", "#050609", "#FFFFFF" })
        {
            for (var intensity = 0; intensity <= 100; intensity += 10)
            {
                var glyph = ThemeColors.DeriveAccentSet(accent, preset, intensity).ChromeGlyph;
                var ratio = ThemeColors.ContrastRatio(glyph, surfaceHover);
                Assert.True(ratio >= 3.0,
                    $"{presetId}/{accent} @ intensity {intensity}: glyph contrast is only {ratio:F2}:1.");
            }
        }
    }

    [Fact]
    public void Intensity_is_clamped_so_a_hand_edited_settings_file_cannot_break_the_chrome()
    {
        Assert.Equal(0, ThemeCatalog.NormalizeAccentIntensity(-40));
        Assert.Equal(100, ThemeCatalog.NormalizeAccentIntensity(9999));
        Assert.Equal(50, ThemeCatalog.NormalizeAccentIntensity(50));
        Assert.Equal(ThemeCatalog.DefaultAccentIntensity, ThemeCatalog.NormalizeAccentIntensity(null));
    }

    [Fact]
    public void Brush_builds_a_frozen_accent_brush()
    {
        var brush = ThemeColors.Brush("#38D996");

        Assert.True(brush.IsFrozen);   // safe to share across the source window and popout
        Assert.Equal(Color.FromRgb(0x38, 0xD9, 0x96), brush.Color);
    }

    [Fact]
    public void ContrastRatio_matches_wcag_endpoints_and_is_symmetric()
    {
        Assert.Equal(21.0, ThemeColors.ContrastRatio(Colors.Black, Colors.White), 3);
        Assert.Equal(21.0, ThemeColors.ContrastRatio(Colors.White, Colors.Black), 3);
        Assert.Equal(1.0, ThemeColors.ContrastRatio(Colors.Black, Colors.Black), 5);
        Assert.Equal(1.0, ThemeColors.ContrastRatio(Colors.White, Colors.White), 5);

        var cyan = Color.FromRgb(0x2B, 0xAE, 0xD0);
        var surface = Color.FromRgb(0x12, 0x16, 0x1D);
        Assert.Equal(
            ThemeColors.ContrastRatio(cyan, surface),
            ThemeColors.ContrastRatio(surface, cyan),
            5);
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
        Assert.True(ThemeColors.ContrastRatio(dark, deadZone) < 4.5);
        Assert.True(ThemeColors.ContrastRatio(Colors.White, deadZone) < 4.5);
        Assert.Equal(Colors.White, ThemeColors.PickReadableForeground(deadZone));
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

        Assert.Equal(Color.FromRgb(0x4A, 0x8F, 0xAB), set.Primary);
        Assert.Equal(Color.FromRgb(0x6B, 0xA3, 0xBA), set.Hover);
        Assert.Equal(Color.FromRgb(0x3E, 0x78, 0x90), set.Pressed);
        Assert.Equal(Color.FromRgb(0x5C, 0x9A, 0xB3), set.Border);
        Assert.Equal((byte)0x22, set.Subtle.A);
        Assert.Equal((byte)0x33, set.Glow.A);
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

    [Theory]
    [InlineData("#A78BFA", true)]
    [InlineData("#000000", false)]
    [InlineData("#050609", false)]
    [InlineData("#131820", false)]
    public void EnsureContrast_preserves_passing_colors_and_lifts_dark_colors_in_every_theme_REQ_UI_01(
        string hex, bool alreadyReadable)
    {
        var raw = ThemeColors.ParseColor(hex);
        foreach (var preset in ThemeCatalog.Presets)
        {
            var surface = ThemeColors.ParseColor(preset.Palette.SurfaceHover);
            var visible = ThemeColors.EnsureContrast(raw, surface);

            if (alreadyReadable)
                Assert.Equal(raw, visible);
            else
                Assert.NotEqual(raw, visible);

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

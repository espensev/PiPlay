using System.Windows;
using System.Windows.Media;
using PiPlay;
using PiPlay.Models;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Wpf)]
public class MainWindowProfileAccentTests : IDisposable
{
    // P2 (2026-07-14): selecting a profile RE-TINTS the app. These tests previously pinned the opposite
    // (the v0.6.0 split); the reversal is the point of the change.

    [Fact]
    public void Selecting_a_profile_recolors_the_app_accent_P2() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        w.ReplaceSettingsForTests(SettingsWithProfiles());

        w.SelectProfileForTests("Violet");
        Assert.Equal("#A78BFA", w.ResolvedAccentColorForTests);

        // A profile with no color of its own inherits the global accent.
        w.SelectProfileForTests("Plain");
        Assert.Equal("#00D4FF", w.ResolvedAccentColorForTests);
    });

    [Fact]
    public void Profile_selection_repaints_accent_and_shell_resources_P2() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        w.ReplaceSettingsForTests(SettingsWithProfiles());
        var preset = ThemeCatalog.PresetFor("sharp-dark");

        w.SelectProfileForTests("Violet");
        var violet = ThemeColors.DeriveAccentSet("#A78BFA", preset);
        Assert.Equal(violet.Primary, ((SolidColorBrush)Application.Current.Resources["AccentPrimary"]).Color);
        Assert.Equal(violet.ShellTint, ((SolidColorBrush)Application.Current.Resources["AccentShellTint"]).Color);
        Assert.Null(w.PendingUrlForTests);

        // Switching to a colorless profile must repaint back to the GLOBAL accent, not leave violet stuck.
        w.SelectProfileForTests("Plain");
        var global = ThemeColors.DeriveAccentSet("#00D4FF", preset);
        Assert.Equal(global.Primary, ((SolidColorBrush)Application.Current.Resources["AccentPrimary"]).Color);
        Assert.Equal(global.ShellTint, ((SolidColorBrush)Application.Current.Resources["AccentShellTint"]).Color);
        Assert.Null(w.PendingUrlForTests);

        // The two presentations must actually be DIFFERENT, or "re-tints the app" is a lie.
        Assert.NotEqual(violet.Primary, global.Primary);
    });

    [Fact]
    public void A_very_dark_profile_color_still_paints_a_visible_accent_but_stores_exactly() =>
        StaTestThread.Invoke(() =>
        {
            var settings = SettingsWithProfiles();
            settings.Profiles.Single(p => p.Name == "Violet").AccentColor = "#0B0E11";
            var w = new MainWindow();
            w.ReplaceSettingsForTests(settings);
            var preset = ThemeCatalog.PresetFor("sharp-dark");

            w.SelectProfileForTests("Violet");

            // Stored value stays EXACT...
            Assert.Equal("#0B0E11", settings.Profiles.Single(p => p.Name == "Violet").AccentColor);
            Assert.Equal("#0B0E11", w.ResolvedAccentColorForTests);

            // ...while the PAINTED accent is lifted enough to remain visible on the chrome surface,
            // otherwise a dark profile would render the toolbar glyphs invisible.
            var painted = ((SolidColorBrush)Application.Current.Resources["AccentPrimary"]).Color;
            var surfaceHover = ThemeColors.ParseColor(preset.Palette.SurfaceHover);
            Assert.True(ThemeColors.ContrastRatio(painted, surfaceHover) >= 3.0,
                $"Dark profile accent paints at only {ThemeColors.ContrastRatio(painted, surfaceHover):F2}:1.");
        });

    private static AppSettings SettingsWithProfiles() => new()
    {
        Theme = new ThemeSettings { AccentColor = "#00D4FF" },
        Profiles =
        {
            new Profile { Name = "Violet", Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", AccentColor = "#A78BFA" },
            new Profile { Name = "Plain", Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" },
        },
    };

    public void Dispose() => StaTestThread.Invoke(() =>
    {
        foreach (var w in Application.Current.Windows.Cast<Window>().ToArray())
        {
            try { w.Close(); } catch { /* a never-shown window may resist closing; ignore */ }
        }
    });
}

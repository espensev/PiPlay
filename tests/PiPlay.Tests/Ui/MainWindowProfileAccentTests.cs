using System.Windows;
using System.Windows.Media;
using PiPlay;
using PiPlay.Models;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Wpf)]
public class MainWindowProfileAccentTests : IDisposable
{
    [Fact]
    public void MainWindow_keeps_app_accent_global_when_profiles_change() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        w.ReplaceSettingsForTests(SettingsWithProfiles());

        w.SelectProfileForTests("Violet");
        Assert.Equal("#00D4FF", w.ResolvedAccentColorForTests);

        w.SelectProfileForTests("Plain");
        Assert.Equal("#00D4FF", w.ResolvedAccentColorForTests);
    });

    [Fact]
    public void MainWindow_profile_selection_does_not_recolor_app_accent_or_navigate_in_test_seam() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        w.ReplaceSettingsForTests(SettingsWithProfiles());

        w.SelectProfileForTests("Violet");
        var global = ThemeColors.DeriveAccentSet("#00D4FF", ThemeCatalog.PresetFor("sharp-dark")).Primary;
        Assert.Equal(global, ((SolidColorBrush)Application.Current.Resources["AccentPrimary"]).Color);
        Assert.Null(w.PendingUrlForTests);

        w.SelectProfileForTests("Plain");
        Assert.Equal(global, ((SolidColorBrush)Application.Current.Resources["AccentPrimary"]).Color);
        Assert.Null(w.PendingUrlForTests);
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

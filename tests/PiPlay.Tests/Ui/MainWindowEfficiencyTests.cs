using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PiPlay;
using PiPlay.Models;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Wpf)]
public class MainWindowEfficiencyTests
{
    [Fact]
    public void Injected_boot_settings_are_used_by_the_first_MainWindow()
    {
        StaTestThread.Invoke(() =>
        {
            var settings = new AppSettings
            {
                AutoPopout = true,
                Theme = new ThemeSettings { ThemeId = "minimal", AccentColor = "#38D996" },
            };

            var window = new MainWindow(settings);

            Assert.True(((ToggleButton)window.FindName("AutoToggle")!).IsChecked);
            Assert.Equal("#38D996", window.EffectivePlayerPreferencesForTests.AccentColor);
        });
    }

    [Fact]
    public void Accent_preview_coalesces_to_the_latest_value_and_skips_duplicates()
    {
        StaTestThread.Invoke(() =>
        {
            var window = new MainWindow(new AppSettings());
            var before = ((SolidColorBrush)Application.Current.Resources["AccentPrimary"]).Color;
            try
            {
                window.QueueAccentPreviewForTests("#38D996");
                window.QueueAccentPreviewForTests("#A78BFA");
                window.QueueAccentPreviewForTests("#A78BFA");

                Assert.True(window.HasPendingAccentPreviewForTests);
                Assert.Equal(before, ((SolidColorBrush)Application.Current.Resources["AccentPrimary"]).Color);

                window.FlushAccentPreviewForTests();

                var expected = ThemeColors.DeriveAccentSet(
                    "#A78BFA", ThemeCatalog.PresetFor("sharp-dark")).Primary;
                Assert.False(window.HasPendingAccentPreviewForTests);
                Assert.Equal(expected, ((SolidColorBrush)Application.Current.Resources["AccentPrimary"]).Color);

                window.QueueAccentPreviewForTests("#A78BFA");
                Assert.False(window.HasPendingAccentPreviewForTests);

                // This is the modal Apply edge: returning to the original accent immediately before
                // clicking must flush the queued final value rather than leave the last preview live.
                window.QueueAccentPreviewForTests(ThemeCatalog.DefaultAccentColor);
                Assert.True(window.HasPendingAccentPreviewForTests);
                window.FlushAccentPreviewForTests();
                var original = ThemeColors.DeriveAccentSet(
                    ThemeCatalog.DefaultAccentColor, ThemeCatalog.PresetFor("sharp-dark")).Primary;
                Assert.Equal(original, ((SolidColorBrush)Application.Current.Resources["AccentPrimary"]).Color);
            }
            finally
            {
                if (window.HasPendingAccentPreviewForTests) window.FlushAccentPreviewForTests();
                window.RevertPreviewedAccentForTests();
            }
        });
    }
}

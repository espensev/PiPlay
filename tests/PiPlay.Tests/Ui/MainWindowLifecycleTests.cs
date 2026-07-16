using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using PiPlay;
using PiPlay.Models;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Wpf)]
public class MainWindowLifecycleTests : IDisposable
{
    [Fact]
    public void Source_restore_raises_a_stale_short_placement_to_the_declared_minimum()
    {
        var saved = new PlacementData
        {
            X = 240,
            Y = 135,
            Width = 760,
            Height = 321,
            Maximized = true,
            MonitorDeviceName = @"\\.\DISPLAY2",
            MonitorWorkArea = new RectData { X = 1920, Y = 0, Width = 2560, Height = 1440 },
            DpiScale = 1.0,
        };

        var normalized = MainWindow.NormalizeSourcePlacementForRestoreForTests(saved);

        Assert.NotNull(normalized);
        Assert.Equal(240, normalized!.X);
        Assert.Equal(135, normalized.Y);
        Assert.Equal(760, normalized.Width);
        Assert.Equal(480, normalized.Height);
        Assert.True(normalized.Maximized);
        Assert.Equal(@"\\.\DISPLAY2", normalized.MonitorDeviceName);
        Assert.Equal(1.0, normalized.DpiScale);
        Assert.NotNull(normalized.MonitorWorkArea);
        Assert.Equal(1920, normalized.MonitorWorkArea!.X);
        Assert.Equal(2560, normalized.MonitorWorkArea.Width);
        Assert.Equal(1440, normalized.MonitorWorkArea.Height);
    }

    [Fact]
    public void Return_in_progress_blocks_a_new_popout_and_keeps_the_primary_action_truthful()
    {
        StaTestThread.Invoke(() =>
        {
            var window = new MainWindow();
            var button = (Button)window.FindName("PopOutButton")!;
            var label = (TextBlock)window.FindName("PopOutButtonText")!;
            var settings = (Button)window.FindName("SettingsButton")!;

            window.SetBrowserReadyForTests(true);
            Assert.True(window.CanStartVideoPopoutForTests);
            Assert.True(button.IsEnabled);
            Assert.True(settings.IsEnabled);

            window.BeginReturnForTests();

            Assert.True(window.ReturnInProgressForTests);
            Assert.False(window.CanStartVideoPopoutForTests);
            Assert.False(button.IsEnabled);
            Assert.False(settings.IsEnabled);
            Assert.Contains("Returning", label.Text, StringComparison.OrdinalIgnoreCase);

            window.CompleteReturnForTests();

            Assert.False(window.ReturnInProgressForTests);
            Assert.True(window.CanStartVideoPopoutForTests);
            Assert.True(button.IsEnabled);
            Assert.True(settings.IsEnabled);
            Assert.DoesNotContain("Returning", label.Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Returning_from_popout_restores_a_minimized_Source_without_changing_Pin(bool pinned)
    {
        StaTestThread.Invoke(() =>
        {
            var window = new MainWindow(new AppSettings
            {
                MainWindow = new WindowSettings { Topmost = pinned },
            })
            {
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            _ = new WindowInteropHelper(window).EnsureHandle();
            window.WindowState = WindowState.Minimized;
            Assert.Equal(WindowState.Minimized, window.WindowState);

            window.RestoreSourceAfterReturnForTests();

            Assert.True(window.IsVisible);
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Equal(pinned, window.Topmost);
        });
    }

    [Fact]
    public void Source_pin_is_suspended_for_the_popout_without_losing_the_saved_preference()
    {
        StaTestThread.Invoke(() =>
        {
            var window = new MainWindow(new AppSettings
            {
                MainWindow = new WindowSettings { Topmost = true },
            });
            var pin = (ToggleButton)window.FindName("PinToggle")!;

            Assert.True(window.Topmost);
            Assert.True(pin.IsChecked);
            Assert.True(pin.IsEnabled);
            Assert.Contains("Unpin", (string)pin.ToolTip);

            window.SuspendSourcePinForPopoutForTests();

            Assert.False(window.Topmost);
            Assert.False(pin.IsChecked);
            Assert.False(pin.IsEnabled);
            Assert.True(window.SavedSourceTopmostForTests);
            Assert.Contains("Pin Source", (string)pin.ToolTip);

            window.CaptureSourceTopmostPreferenceForCloseForTests();
            Assert.True(window.SavedSourceTopmostForTests);

            window.RestoreSourcePinAfterPopoutForTests();

            Assert.True(window.Topmost);
            Assert.True(pin.IsChecked);
            Assert.True(pin.IsEnabled);
            Assert.Contains("Unpin", (string)pin.ToolTip);
        });
    }

    [Fact]
    public void Profile_derived_Source_pin_survives_popout_suspend_restore_and_mid_popout_close_capture()
    {
        StaTestThread.Invoke(() =>
        {
            var window = new MainWindow(new AppSettings
            {
                MainWindow = new WindowSettings { Topmost = false },
                Profiles =
                {
                    new Profile
                    {
                        Name = "Pinned profile",
                        Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                        Topmost = true,
                    },
                },
            });
            var profiles = (ComboBox)window.FindName("ProfilesCombo")!;

            profiles.SelectedIndex = 0;
            Assert.True(window.Topmost);
            Assert.False(window.SavedSourceTopmostForTests);

            window.SuspendSourcePinForPopoutForTests();
            Assert.False(window.Topmost);

            window.CaptureSourceTopmostPreferenceForCloseForTests();
            Assert.True(window.SavedSourceTopmostForTests);

            window.RestoreSourcePinAfterPopoutForTests();
            Assert.True(window.Topmost);
        });
    }

    [Fact]
    public void Source_navigation_commands_are_disabled_while_the_placeholder_hides_the_browser()
    {
        StaTestThread.Invoke(() =>
        {
            var window = new MainWindow(new AppSettings
            {
                ActiveProfileName = "Saved",
                Profiles =
                {
                    new Profile
                    {
                        Name = "Saved",
                        Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    },
                },
            });

            var back = (Button)window.FindName("BackButton")!;
            var reload = (Button)window.FindName("ReloadButton")!;
            var home = (Button)window.FindName("HomeButton")!;
            var url = (TextBox)window.FindName("UrlBox")!;
            var profiles = (ComboBox)window.FindName("ProfilesCombo")!;
            var profileActions = (Button)window.FindName("ProfileActionsButton")!;
            var saveProfile = (MenuItem)window.FindName("SaveProfileMenuItem")!;
            var editProfile = (MenuItem)window.FindName("EditProfileMenuItem")!;
            var deleteProfile = (MenuItem)window.FindName("DeleteProfileMenuItem")!;

            Assert.True(back.IsEnabled);
            Assert.True(reload.IsEnabled);
            Assert.True(home.IsEnabled);
            Assert.True(url.IsEnabled);
            Assert.True(profiles.IsEnabled);
            Assert.True(profileActions.IsEnabled);
            Assert.True(saveProfile.IsEnabled);
            Assert.True(editProfile.IsEnabled);
            Assert.True(deleteProfile.IsEnabled);

            window.ShowSourcePlaceholder(true);

            Assert.False(back.IsEnabled);
            Assert.False(reload.IsEnabled);
            Assert.False(home.IsEnabled);
            Assert.False(url.IsEnabled);
            Assert.False(profiles.IsEnabled);
            Assert.False(profileActions.IsEnabled);
            Assert.False(saveProfile.IsEnabled);
            Assert.False(editProfile.IsEnabled);
            Assert.False(deleteProfile.IsEnabled);

            window.ShowSourcePlaceholder(false);

            Assert.True(back.IsEnabled);
            Assert.True(reload.IsEnabled);
            Assert.True(home.IsEnabled);
            Assert.True(url.IsEnabled);
            Assert.True(profiles.IsEnabled);
            Assert.True(profileActions.IsEnabled);
            Assert.True(saveProfile.IsEnabled);
            Assert.True(editProfile.IsEnabled);
            Assert.True(deleteProfile.IsEnabled);
        });
    }

    public void Dispose() => StaTestThread.Invoke(() =>
    {
        foreach (var window in Application.Current.Windows.Cast<Window>().ToArray())
        {
            try { window.Close(); }
            catch { /* Never-shown test windows can already be tearing down. */ }
        }
    });
}

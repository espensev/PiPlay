using System.Windows;
using System.Windows.Controls;
using PiPlay;
using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Tests;

/// <summary>
/// Clear browser data against the Video Popout lifecycle (spec 13.4 / 19). The WPF lane has no
/// WebView2 core, so a launch is held "mid-await" through the MainWindow seams while a clear or
/// shutdown lands on it, and the same checkpoint and rollback StartVideoPopoutAsync uses decide.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Wpf)]
public class MainWindowClearDataTests : IDisposable
{
    [Fact]
    public void Clear_is_refused_while_a_popout_launch_or_return_is_in_flight()
    {
        StaTestThread.Invoke(() =>
        {
            var window = new MainWindow(new AppSettings());
            window.SetBrowserReadyForTests(true);

            // No core in this lane, so an idle window reports the readiness reason.
            Assert.Equal(PrivacyService.ClearBrowserNotReady, window.ClearBrowserDataRefusalForTests);
            Assert.Null(window.ClearBrowserDataUnavailableHintForTests);

            // Settings stays reachable while a launch awaits page reads, so the clear itself refuses.
            window.BeginPopoutLaunchForTests();

            Assert.False(window.CanClearBrowserData);
            Assert.Equal(PrivacyService.ClearPopoutBusy, window.ClearBrowserDataRefusalForTests);
            Assert.Equal(PrivacyService.ClearPopoutBusyHint, window.ClearBrowserDataUnavailableHintForTests);

            window.CompletePopoutLaunchForTests();

            Assert.Equal(PrivacyService.ClearBrowserNotReady, window.ClearBrowserDataRefusalForTests);
            Assert.Null(window.ClearBrowserDataUnavailableHintForTests);
        });
    }

    [Fact]
    public void An_open_Settings_dialog_follows_a_launch_that_starts_and_ends_behind_it()
    {
        StaTestThread.Invoke(() =>
        {
            var window = new MainWindow(new AppSettings());
            var dialog = new SettingsWindow(isBrowserReady: true);
            var clear = (Button)dialog.FindName("ClearBrowserDataButton")!;
            window.SetBrowserReadyForTests(true);
            window.AttachSettingsDialogForTests(dialog);
            Assert.True(clear.IsEnabled);

            // Auto can start a launch while the modal Settings dialog is open.
            window.BeginPopoutLaunchForTests();

            Assert.False(clear.IsEnabled);
            Assert.Equal(PrivacyService.ClearPopoutBusyHint, clear.ToolTip);

            window.CompletePopoutLaunchForTests();

            // Refresh recomputes readiness from the live core; this lane has none.
            Assert.False(clear.IsEnabled);
            Assert.Equal(PrivacyService.ClearNotReadyHint, clear.ToolTip);
            window.AttachSettingsDialogForTests(null);
        });
    }

    [Fact]
    public void A_launch_failure_that_still_owns_the_Source_is_reported()
    {
        StaTestThread.Invoke(() =>
        {
            var window = NewUnactivatedWindow();
            window.SetBrowserReadyForTests(true);
            var core = window.BeginPopoutLaunchForTests();

            Assert.True(window.IsPopoutLaunchCurrentForTests(core));
            var rollback = window.RollBackPopoutLaunchForTests(core);

            Assert.True(rollback.IsCompletedSuccessfully);
            Assert.True(rollback.Result);
            window.CompletePopoutLaunchForTests();
        });
    }

    [Fact]
    public void A_clear_landing_mid_launch_abandons_it_without_a_Popout_and_reopens_the_gates()
    {
        StaTestThread.Invoke(() =>
        {
            var window = NewUnactivatedWindow();
            var browser = (FrameworkElement)window.FindName("Browser")!;
            var placeholder = (FrameworkElement)window.FindName("SourcePlaceholder")!;
            var popOut = (Button)window.FindName("PopOutButton")!;
            var url = (TextBox)window.FindName("UrlBox")!;
            var settings = (Button)window.FindName("SettingsButton")!;
            window.SetBrowserReadyForTests(true);

            var core = window.BeginPopoutLaunchForTests();
            window.ShowSourcePlaceholder(true);   // the launch already suppressed the Source

            // A clear that got past the refusal is still caught at the launch's next checkpoint.
            window.BeginBrowserDataClearGatesForTests();

            Assert.False(window.IsPopoutLaunchCurrentForTests(core));
            var rollback = window.RollBackPopoutLaunchForTests(core);

            Assert.True(rollback.IsCompletedSuccessfully);
            Assert.False(rollback.Result);   // abandoned, not failed: no prompt, wiped page left alone
            Assert.False(window.HasPlayerForTests);
            Assert.Equal(Visibility.Visible, browser.Visibility);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
            Assert.False(popOut.IsEnabled);
            Assert.False(url.IsEnabled);
            Assert.False(settings.IsEnabled);

            window.CompletePopoutLaunchForTests();
            window.EndBrowserDataClearGatesForTests();

            Assert.True(window.CanStartVideoPopoutForTests);
            Assert.True(popOut.IsEnabled);
            Assert.True(url.IsEnabled);
            Assert.True(settings.IsEnabled);
        });
    }

    [Fact]
    public void Closing_the_Source_mid_launch_abandons_it_without_a_Popout_or_a_reshow()
    {
        StaTestThread.Invoke(() =>
        {
            var window = new MainWindow(new AppSettings());
            window.SetBrowserReadyForTests(true);
            var core = window.BeginPopoutLaunchForTests();
            window.ShowSourcePlaceholder(true);

            window.Close();   // shutdown lands while the launch awaits a page read

            Assert.False(window.IsPopoutLaunchCurrentForTests(core));
            var rollback = window.RollBackPopoutLaunchForTests(core);

            Assert.True(rollback.IsCompletedSuccessfully);
            Assert.False(rollback.Result);
            Assert.False(window.HasPlayerForTests);
            Assert.False(window.IsVisible);
            window.CompletePopoutLaunchForTests();
        });
    }

    [Fact]
    public void A_clear_task_that_never_completes_keeps_only_Clear_itself_unavailable()
    {
        StaTestThread.Invoke(() =>
        {
            var window = new MainWindow(new AppSettings());
            var popOut = (Button)window.FindName("PopOutButton")!;
            var label = (TextBlock)window.FindName("PopOutButtonText")!;
            var url = (TextBox)window.FindName("UrlBox")!;
            var settings = (Button)window.FindName("SettingsButton")!;
            window.SetBrowserReadyForTests(true);

            // A WebView2 completion callback can be released without ever being invoked.
            var neverCompletes = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.True(window.TryStartBrowserDataClearForTests(() => neverCompletes.Task));
            window.BeginBrowserDataClearGatesForTests();

            Assert.False(window.CanStartVideoPopoutForTests);
            Assert.False(popOut.IsEnabled);
            Assert.False(url.IsEnabled);
            Assert.False(settings.IsEnabled);

            // The bounded foreground wait ends (PrivacyService.ClearTimeout) with the task pending.
            window.EndBrowserDataClearGatesForTests();

            Assert.True(window.BrowserDataClearInProgressForTests);
            Assert.Equal(PrivacyService.ClearAlreadyRunning, window.ClearBrowserDataRefusalForTests);
            Assert.Equal(PrivacyService.ClearAlreadyRunningHint, window.ClearBrowserDataUnavailableHintForTests);
            Assert.True(window.CanStartVideoPopoutForTests);
            Assert.True(popOut.IsEnabled);
            Assert.Equal("Pop out video", label.Text);
            Assert.True(url.IsEnabled);
            Assert.True(settings.IsEnabled);
        });
    }

    // Rollback restores the Source window, which shows a never-shown test window.
    private static MainWindow NewUnactivatedWindow() =>
        new(new AppSettings())
        {
            ShowActivated = false,
            ShowInTaskbar = false,
        };

    public void Dispose() => StaTestThread.Invoke(() =>
    {
        foreach (var window in Application.Current.Windows.Cast<Window>().ToArray())
        {
            try { window.Close(); }
            catch { /* Never-shown test windows can already be tearing down. */ }
        }
    });
}

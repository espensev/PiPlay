using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay;

/// <summary>
/// MainWindow / Source Window (spec 12.1). Hosts the YouTube browsing WebView, applies the
/// navigation allowlist, starts the Video Popout lifecycle, shows/hides the Source
/// Placeholder, and coordinates return.
/// </summary>
public partial class MainWindow : Window
{
    // Segoe MDL2 caption glyphs (kept as escapes so the source stays plain ASCII).
    private const string GlyphMaximize = "";
    private const string GlyphRestore = "";

    private readonly SettingsService _settingsService = new();
    private readonly AppSettings _settings;

    private bool _browserReady;
    private bool _placementRestored;
    private bool _loadingProfiles;
    private string? _pendingUrl;

    // Video Popout lifecycle state (spec 13).
    private bool _popoutInProgress;
    private PlayerWindow? _player;
    private bool _sourceWasPlayingAtPopout;

    public MainWindow()
    {
        InitializeComponent();

        _settings = _settingsService.Load();
        Icon = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Assets/piplay.ico"));

        BorderlessWindowHelper.EnableProperMaximize(this);

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += (_, _) =>
            MaximizeButton.Content = WindowState == WindowState.Maximized ? GlyphRestore : GlyphMaximize;
        SourceInitialized += (_, _) =>
        {
            if (_placementRestored) return;
            WindowPlacementService.Restore(this, _settings.MainWindow.Placement);
            _placementRestored = true;
        };

        ApplyTopmost(_settings.MainWindow.Topmost);
        LoadProfilesIntoCombo();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e) => await InitializeBrowserAsync();

    // --- WebView2 initialization + recovery (spec 15, Q-6) ---

    private async Task InitializeBrowserAsync()
    {
        try
        {
            RuntimeErrorPanel.Visibility = Visibility.Collapsed;

            var env = await App.Current.WebViewEnvironment.EnsureCreatedAsync();
            await Browser.EnsureCoreWebView2Async(env);

            var core = Browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsBuiltInErrorPageEnabled = true;
            core.Settings.IsStatusBarEnabled = false;

            core.NavigationStarting += Core_NavigationStarting;
            core.NewWindowRequested += Core_NewWindowRequested;
            core.SourceChanged += Core_SourceChanged;

            _browserReady = true;
            PopOutButton.IsEnabled = true;

            var startUrl = _pendingUrl ?? _settings.LastUrl;
            _pendingUrl = null;
            NavigateInternal(startUrl);
            Log.Info("Source browser initialized.");
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            Log.Error("WebView2 runtime not found.", ex);
            ShowRuntimeError(
                "PiPlay needs the Microsoft Edge WebView2 Evergreen Runtime to display YouTube. " +
                "Install it, then click Retry.");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to initialize the Source browser.", ex);
            ShowRuntimeError("PiPlay couldn't start the browser component.\n\n" + ex.Message);
        }
    }

    private void ShowRuntimeError(string message)
    {
        RuntimeErrorText.Text = message;
        RuntimeErrorPanel.Visibility = Visibility.Visible;
        PopOutButton.IsEnabled = false;
    }

    private void DownloadRuntime_Click(object sender, RoutedEventArgs e) =>
        OpenExternal("https://developer.microsoft.com/microsoft-edge/webview2/");

    private async void RetryRuntime_Click(object sender, RoutedEventArgs e) => await InitializeBrowserAsync();

    // --- Navigation policy (spec 15.2) - shared helper, two distinct handlers ---

    private void Core_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) &&
            NavigationPolicy.IsAllowed(uri, NavigationSurface.Source))
        {
            return;
        }

        e.Cancel = true;
        Log.Info($"Source navigation blocked, opening externally: {Log.RedactUrl(e.Uri)}");
        OpenExternal(e.Uri);
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) &&
            NavigationPolicy.IsAllowed(uri, NavigationSurface.Source))
        {
            // Keep allowed targets (YouTube / Google sign-in) inside the Source Window.
            Browser.CoreWebView2.Navigate(e.Uri);
        }
        else
        {
            Log.Info($"Source new-window blocked, opening externally: {Log.RedactUrl(e.Uri)}");
            OpenExternal(e.Uri);
        }
    }

    private void Core_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        // Display the real URL for usability; we never *log* the query (see Log.RedactUrl).
        UrlBox.Text = Browser.CoreWebView2.Source;
    }

    // --- Navigation entry points ---

    /// <summary>Navigate the Source Window, queuing until the browser is ready (used by single-instance hand-off).</summary>
    public void NavigateTo(string url)
    {
        if (!_browserReady)
        {
            _pendingUrl = url;
            return;
        }
        NavigateInternal(url);
    }

    private void NavigateInternal(string? input)
    {
        if (!_browserReady || Browser.CoreWebView2 is null)
        {
            _pendingUrl = input;
            return;
        }

        var target = ResolveNavigationUrl(input);
        try { Browser.CoreWebView2.Navigate(target); }
        catch (Exception ex) { Log.Error("Navigate failed.", ex); }
    }

    private static string ResolveNavigationUrl(string? input)
    {
        input = (input ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(input)) return "https://www.youtube.com/";

        if (YouTubeUrlHelper.TryParse(input, out var target))
            return YouTubeUrlHelper.BuildWatchUrl(target);

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return input;
        }

        // Anything else: treat as a YouTube search.
        return "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(input);
    }

    private void UrlBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        NavigateInternal(UrlBox.Text);
        e.Handled = true;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoBack == true) Browser.CoreWebView2.GoBack();
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e) => Browser.CoreWebView2?.Reload();

    private void HomeButton_Click(object sender, RoutedEventArgs e) => NavigateInternal("https://www.youtube.com/");

    private static void OpenExternal(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("Failed to open an external link.", ex);
        }
    }

    // --- Pin / topmost (spec 6.3) ---

    private void PinToggle_Click(object sender, RoutedEventArgs e)
    {
        ApplyTopmost(PinToggle.IsChecked == true);
        _settings.MainWindow.Topmost = Topmost;
        _settingsService.Save(_settings);
    }

    private void ApplyTopmost(bool on)
    {
        Topmost = on;
        PinToggle.IsChecked = on;
        PinnedHint.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- Profiles (spec 17, MVP basic save/load) ---

    private void LoadProfilesIntoCombo()
    {
        _loadingProfiles = true;
        ProfilesCombo.ItemsSource = _settings.Profiles.ToList();
        ProfilesCombo.SelectedIndex = -1;
        _loadingProfiles = false;
    }

    private void ProfilesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingProfiles || ProfilesCombo.SelectedItem is not Profile profile) return;
        if (profile.Topmost is bool tm) ApplyTopmost(tm);
        NavigateInternal(profile.Url);
    }

    private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var currentUrl = Browser.CoreWebView2?.Source ?? _settings.LastUrl;
        var (ok, error) = ProfileService.ValidateUrl(currentUrl);
        if (!ok)
        {
            MessageBox.Show(error, "PiPlay", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var name = Prompt.AskText(this, "Save profile", "Name this profile:");
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();

        if (ProfileService.Exists(_settings, name))
        {
            var overwrite = MessageBox.Show(
                $"A profile named \"{name}\" already exists. Overwrite it?",
                "PiPlay", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (overwrite != MessageBoxResult.Yes) return;
        }

        ProfileService.Save(_settings, new Profile { Name = name, Url = currentUrl, Topmost = Topmost });
        _settingsService.Save(_settings);
        LoadProfilesIntoCombo();
        Log.Info("Profile saved.");
    }

    // --- Video Popout lifecycle (spec 13) ---

    private async void PopOutButton_Click(object sender, RoutedEventArgs e) => await StartVideoPopoutAsync();

    private async Task StartVideoPopoutAsync()
    {
        // Guards (spec 13.4): browser ready, no popout in flight, single player (ADR-0005).
        if (!_browserReady || _popoutInProgress) return;
        if (_player is not null) { _player.Activate(); return; }

        _popoutInProgress = true;
        PopOutButton.IsEnabled = false;
        var core = Browser.CoreWebView2;

        try
        {
            // 1) Read source state; capture timestamp + was-playing BEFORE pausing (REQ-RETURN-01).
            var state = await YouTubeDomBridge.ReadPlayerStateAsync(core);
            _sourceWasPlayingAtPopout = state is { Paused: false };
            var seconds = state?.CurrentTime;

            // 2) Resolve the currently playing video (canonical URL first, then the address bar).
            var target = await ResolvePopoutTargetAsync(core);
            if (target is null || string.IsNullOrEmpty(target.VideoId))
            {
                MessageBox.Show("Open a YouTube video first, then press Pop out video.", "PiPlay",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var popoutUrl = YouTubeUrlHelper.BuildWatchUrl(target, seconds);

            // 3) Pause the source and show the placeholder (Q-1: no duplicate audio).
            await YouTubeDomBridge.PauseAsync(core);
            ShowSourcePlaceholder(true);

            // 4) Create the single Popout Player on the shared environment.
            var env = App.Current.WebViewEnvironment.Environment
                      ?? await App.Current.WebViewEnvironment.EnsureCreatedAsync();

            _player = new PlayerWindow(env, popoutUrl, _settings.Player.Topmost,
                _settings.Player.Placement, _settings.Player.LastWidth, _settings.Player.LastHeight,
                _settings.Player.FadeEnabled);
            _player.PlayerClosed += Player_OnClosed;
            _player.Show();

            if (target.FallbackReason is not null) Log.Info($"Popout fallback: {target.FallbackReason}");
            Log.Info($"Video Popout started at t={seconds?.ToString() ?? "0"}s, wasPlaying={_sourceWasPlayingAtPopout}.");
        }
        catch (Exception ex)
        {
            // Failure after pause (spec 13.5): restore the source and resume if it had been playing.
            Log.Error("Video Popout failed; restoring source.", ex);
            ShowSourcePlaceholder(false);
            if (_player is not null) { try { _player.Close(); } catch { /* ignore */ } _player = null; }
            if (_sourceWasPlayingAtPopout && core is not null) await YouTubeDomBridge.PlayAsync(core);
            MessageBox.Show("PiPlay couldn't pop out this video. It stayed in the main window.", "PiPlay",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _popoutInProgress = false;
            PopOutButton.IsEnabled = true;
        }
    }

    private static async Task<YouTubeTarget?> ResolvePopoutTargetAsync(CoreWebView2 core)
    {
        var canonical = await YouTubeDomBridge.ReadCanonicalUrlAsync(core);
        if (!string.IsNullOrEmpty(canonical) &&
            YouTubeUrlHelper.TryParse(canonical, out var fromCanonical) && fromCanonical.VideoId is not null)
        {
            return fromCanonical;
        }
        return YouTubeUrlHelper.TryParse(core.Source, out var fromSource) ? fromSource : null;
    }

    private void ShowSourcePlaceholder(bool visible)
    {
        // Tier-1 placeholder (spec 13.3): hide the source WebView, show the WPF black panel.
        Browser.Visibility = visible ? Visibility.Hidden : Visibility.Visible;
        SourcePlaceholder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Player_OnClosed(object? sender, PlayerReturnState state)
    {
        try
        {
            _player = null;

            // Persist Popout Player window state.
            _settings.Player.Topmost = state.Topmost;
            _settings.Player.FadeEnabled = state.FadeEnabled;
            if (state.Placement is not null)
            {
                _settings.Player.Placement = state.Placement;
                if (state.Placement.Width >= 320) _settings.Player.LastWidth = state.Placement.Width;
                if (state.Placement.Height >= 180) _settings.Player.LastHeight = state.Placement.Height;
            }

            // Return to the source (spec 14). LastKnownSeconds is nullable; 0 is a valid timestamp.
            ShowSourcePlaceholder(false);
            var core = Browser.CoreWebView2;
            if (core is not null)
            {
                // REQ-RETURN-01: resume only if the source was playing when popout started;
                // 0 is a valid timestamp distinct from unknown. Decision lives in ReturnPolicy.
                switch (ReturnPolicy.Decide(state.LastKnownSeconds, _sourceWasPlayingAtPopout))
                {
                    case ReturnAction.SeekAndPlay:
                        await YouTubeDomBridge.SeekAndPlayAsync(core, state.LastKnownSeconds!.Value);
                        break;
                    case ReturnAction.Seek:
                        await YouTubeDomBridge.SeekAsync(core, state.LastKnownSeconds!.Value);
                        break;
                    case ReturnAction.Play:
                        await YouTubeDomBridge.PlayAsync(core);
                        break;
                    case ReturnAction.None:
                        break;
                }
            }

            _settingsService.Save(_settings);
            Log.Info("Returned from Video Popout.");
        }
        catch (Exception ex)
        {
            Log.Error("Error returning from Video Popout.", ex);
        }
    }

    // --- Single-instance activation (REQ-APP-01) ---

    public void ActivateFromSecondInstance(string? url)
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();

        // Briefly assert topmost to pull to the foreground, then restore the real Pin state.
        var pinned = Topmost;
        Topmost = true;
        Topmost = pinned;

        if (!string.IsNullOrEmpty(url)) NavigateTo(url);
    }

    // --- Window chrome buttons ---

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        try
        {
            // Close the Popout Player too (its handler captures/persists player state).
            if (_player is not null) { try { _player.Close(); } catch { /* ignore */ } }

            var placement = WindowPlacementService.TryCapture(this);
            if (placement is not null) _settings.MainWindow.Placement = placement;
            if (Browser.CoreWebView2 is not null) _settings.LastUrl = Browser.CoreWebView2.Source;
            _settings.MainWindow.Topmost = Topmost;
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            Log.Error("Error saving settings on close.", ex);
        }
    }
}

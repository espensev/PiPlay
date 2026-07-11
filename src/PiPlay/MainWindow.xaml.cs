using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using PiPlay.Models;
using PiPlay.Services;
using PiPlay.Theme;

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
    private AppSettings _settings;

    private bool _browserReady;
    private bool _placementRestored;
    private bool _loadingProfiles;
    private string? _pendingUrl;

    // Video Popout lifecycle state (spec 13).
    private bool _popoutInProgress;
    private PlayerWindow? _player;
    private bool _sourceWasPlayingAtPopout;
    // The video the source was on when the popout launched (overhaul Task 3): compared against the
    // popout's returned video id to decide navigate-vs-seek on close (REQ-RETURN-01).
    private string? _popoutSourceVideoId;

    // Auto (spec §6.1): source-side playback detector + the de-dup key that blocks the return-resume
    // re-pop loop. The timer only runs while Auto is on and the browser is ready.
    private System.Windows.Threading.DispatcherTimer? _autoTimer;
    private bool _autoTickInProgress;
    private string? _autoLastHandledVideoId;

    // Guards both privacy actions against re-entrancy (double-click, reopen mid-clear).
    private bool _privacyActionInProgress;
    // True only while Clear browser data is running, so the popout's return handler does not
    // drive source playback against a session that is being wiped.
    private bool _clearingBrowserData;

    public MainWindow()
    {
        InitializeComponent();
        BorderlessWindowHelper.EnableExpandedResizeZones(this);

        _settings = _settingsService.Load();
        // Assembly-qualified pack URI (not the short form): resolves against the PiPlay assembly
        // regardless of Application.ResourceAssembly, so it loads in production and under tests.
        Icon = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/PiPlay;component/Assets/piplay.ico"));

        BorderlessWindowHelper.EnableProperMaximize(this);

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += (_, _) =>
            MaximizeButton.Content = WindowState == WindowState.Maximized ? GlyphRestore : GlyphMaximize;
        SourceInitialized += (_, _) =>
        {
            // Theme-driven native corner shape (review doc §8.7): explicit theme/override data,
            // never an opacity side effect. Default mode leaves the window DWM-pristine.
            ApplyOwnCornerMode();
            if (_placementRestored) return;
            WindowPlacementService.Restore(this, _settings.MainWindow.Placement);
            _placementRestored = true;
        };

        ApplyTopmost(_settings.MainWindow.Topmost);
        ApplyAuto(_settings.AutoPopout);
        ApplySourceAppearance();
        LoadProfilesIntoCombo();
        ApplyChannelTitle();
    }

    /// <summary>
    /// Surface a non-default channel (e.g. Stable) in the title bar and taskbar so a deployed copy is
    /// differentiable from the dev app at a glance. The Default channel keeps the plain "PiPlay" title.
    /// </summary>
    private void ApplyChannelTitle()
    {
        if (AppInfo.Channel == PiPlayChannel.Default) return;
        Title = AppInfo.WindowTitle;
        TitleText.Text = AppInfo.WindowTitle;
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
            UpdateAutoDetector();   // start the Auto detector if Auto was left on
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

    private void ApplySourceAppearance()
    {
        ApplySourceAppearance(ResolvedAccentColor);
    }

    private void ApplySourceAppearance(string accentColor)
    {
        // One contrast-safe presentation accent drives the Source Pin and pinned hint. The persisted
        // Theme.AccentColor remains exact; SurfaceHover is the live floor used by the shared resources.
        var surface = Application.Current.TryFindResource("SurfaceHover") as SolidColorBrush;
        var pinBrush = surface is null
            ? ThemeColors.Brush(accentColor)
            : ThemeColors.ContrastBrush(accentColor, surface.Color);
        ToggleAccent.SetCheckedBrush(PinToggle, pinBrush);
        PinnedHint.Foreground = pinBrush;
    }

    // --- Auto: auto-popout on playback (spec §6.1) ---

    private void AutoToggle_Click(object sender, RoutedEventArgs e)
    {
        _settings.AutoPopout = AutoToggle.IsChecked == true;
        _settingsService.Save(_settings);
        UpdateAutoDetector();
    }

    /// <summary>Reflect the Auto setting on the toolbar toggle (no side effects on the detector).</summary>
    private void ApplyAuto(bool on) => AutoToggle.IsChecked = on;

    /// <summary>Start/stop the playback detector to match the Auto setting and browser readiness.</summary>
    private void UpdateAutoDetector()
    {
        if (_browserReady && _settings.AutoPopout)
        {
            if (_autoTimer is null)
            {
                _autoTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(250),   // matches PlayerWindow._syncTimer
                };
                _autoTimer.Tick += AutoTimer_Tick;
            }
            // Clear the de-dup on (re)enable so Auto immediately pops the video already playing.
            _autoLastHandledVideoId = null;
            _autoTimer.Start();
        }
        else
        {
            _autoTimer?.Stop();
        }
    }

    private async void AutoTimer_Tick(object? sender, EventArgs e)
    {
        // One tick at a time (a slow DOM read must not stack); skip while a popout owns the source.
        if (_autoTickInProgress) return;
        if (!_browserReady || !_settings.AutoPopout) return;
        if (_popoutInProgress || _player is not null || _clearingBrowserData) return;
        var core = Browser.CoreWebView2;
        if (core is null) return;

        _autoTickInProgress = true;
        try
        {
            var src = core.Source;
            YouTubeUrlHelper.TryParse(src, out var target);

            var state = await YouTubeDomBridge.ReadPlayerStateAsync(core);
            var decision = AutoPopoutPolicy.Decide(
                autoEnabled: true,
                isPlaying: state is { Paused: false },
                isWatchVideo: YouTubeUrlHelper.IsWatchUrl(src),
                currentVideoId: target.VideoId,
                lastHandledVideoId: _autoLastHandledVideoId,
                popoutActive: _popoutInProgress || _player is not null);

            if (decision == AutoPopDecision.Pop)
            {
                Log.Info("Auto: playback detected on a new video; starting Video Popout.");
                await StartVideoPopoutAsync();
            }
        }
        catch (Exception ex)
        {
            // Best-effort, per the DOM-bridge contract (Q-6): a hiccup means no auto-pop, never a crash.
            Log.Error("Auto-popout detector tick failed.", ex);
        }
        finally
        {
            _autoTickInProgress = false;
        }
    }

    // --- Profiles (spec 17, MVP basic save/load) ---

    private void LoadProfilesIntoCombo()
    {
        _loadingProfiles = true;
        var profiles = _settings.Profiles.ToList();
        ProfilesCombo.ItemsSource = profiles;
        ProfilesCombo.SelectedItem = _settings.ActiveProfileName is null
            ? null
            : profiles.FirstOrDefault(p => string.Equals(p.Name, _settings.ActiveProfileName, StringComparison.OrdinalIgnoreCase));
        _loadingProfiles = false;
        UpdateProfileCommandState();
        ApplyResolvedAccent();
    }

    /// <summary>Edit/Delete act on the selected profile, so they are enabled only when one is picked.</summary>
    private void UpdateProfileCommandState() =>
        EditProfileButton.IsEnabled = DeleteProfileButton.IsEnabled = ProfilesCombo.SelectedItem is Profile;

    private void ProfilesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateProfileCommandState();
        if (_loadingProfiles) return;
        if (ProfilesCombo.SelectedItem is not Profile profile)
        {
            ProfileAccentService.ClearActiveProfile(_settings);
            ApplyResolvedAccent();
            _settingsService.Save(_settings);
            return;
        }

        ProfileAccentService.SetActiveProfile(_settings, profile);
        ApplyResolvedAccent();
        _settingsService.Save(_settings);
        if (profile.Topmost is bool tm) ApplyTopmost(tm);
        NavigateInternal(profile.Url);
    }

    private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var currentUrl = Browser.CoreWebView2?.Source ?? _settings.LastUrl;
        var (ok, error) = ProfileService.ValidateUrl(currentUrl);
        if (!ok)
        {
            Prompt.ShowInfo(this, "Save profile", error!);
            return;
        }

        var name = Prompt.AskText(this, "Save profile", "Name this profile:");
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();

        var existing = ProfileService.Find(_settings, name);
        if (existing is not null)
        {
            if (!Prompt.AskConfirm(this, "Overwrite profile?",
                    $"A profile named \"{name}\" already exists. Overwrite it?", "Overwrite"))
            {
                return;
            }
        }

        ProfileService.Save(_settings, ProfileService.CreateQuickSaveProfile(existing, name, currentUrl, Topmost));
        _settingsService.Save(_settings);
        LoadProfilesIntoCombo();
        Log.Info("Profile saved.");
    }

    private void EditProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesCombo.SelectedItem is not Profile original) return;

        var edited = Prompt.EditProfile(this, original.Name, original.Url, original.Mode, original.AccentColor,
            EffectiveAccentColor);
        if (edited is null)
        {
            ApplyResolvedAccent();
            return;   // cancelled (and an invalid edit never gets here)
        }

        // Name, Url, and the Phase 3 playback Mode are editable; carry the other Phase 2 fields through.
        var updated = new Profile
        {
            Name = edited.Value.Name,
            Url = edited.Value.Url,
            Mode = PlaybackModePolicy.NormalizeProfileMode(edited.Value.Mode),
            AccentColor = ProfileService.NormalizeAccentForStorage(edited.Value.AccentColor),
            Topmost = original.Topmost,
            FadeEnabled = original.FadeEnabled,
            Bounds = original.Bounds,
        };

        var outcome = ProfileService.Update(_settings, original.Name, updated);
        if (outcome == ProfileUpdateOutcome.NameConflict)
        {
            // Same overwrite/rename prompt the Save path uses (spec 17: no silent clutter).
            if (!Prompt.AskConfirm(this, "Overwrite profile?",
                    $"A profile named \"{updated.Name}\" already exists. Overwrite it?", "Overwrite"))
            {
                return;
            }
            outcome = ProfileService.Update(_settings, original.Name, updated, overwrite: true);
        }

        if (outcome != ProfileUpdateOutcome.Updated) return;   // NotFound is not reachable from a live selection

        ProfileAccentService.RenameActiveProfileIfMatches(_settings, original.Name, updated.Name);

        _settingsService.Save(_settings);
        LoadProfilesIntoCombo();
        Log.Info("Profile edited.");
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesCombo.SelectedItem is not Profile profile) return;

        if (!Prompt.AskConfirm(this, "Delete profile?",
                $"Delete the profile \"{profile.Name}\"? This can't be undone.", "Delete", danger: true))
        {
            return;
        }

        ProfileService.Remove(_settings, profile.Name);
        ProfileAccentService.ClearActiveProfileIfMatches(_settings, profile.Name);
        _settingsService.Save(_settings);
        LoadProfilesIntoCombo();
        Log.Info("Profile deleted.");
    }

    // --- Privacy actions: Settings window (spec 19, Phase 2, REQ-PRIVACY-01/02) ---

    /// <summary>Whether Clear browser data can run right now (browser initialized + live core).</summary>
    internal bool CanClearBrowserData => _browserReady && Browser.CoreWebView2 is not null;

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_privacyActionInProgress) return;

        var dialog = new SettingsWindow(
            isBrowserReady: CanClearBrowserData,
            themeId: _settings.Theme.ThemeId,
            accentColor: ResolvedAccentColor,
            fadeIdleDelayMs: EffectiveFadeIdleDelayMs,
            compactMode: _settings.Player.CompactMode,
            activeOpacityOverride: _settings.Theme.ActiveWindowOpacity,
            idleOpacityOverride: _settings.Theme.IdleWindowOpacity,
            stripAutoHideOverride: _settings.Theme.StripAutoHide,
            cornerStyle: EffectiveCornerStyle,
            accentEditContext: "Editing the app accent.")
        {
            Owner = this,
            Topmost = Topmost,
        };
        // Live preview (spec 7.3 / plan Task 3): slider moves apply to the open popout immediately.
        // animate:false — the event fires per drag tick; restarting the 150ms fade each tick would
        // stair-step and churn animation timers. The drag itself is the animation.
        dialog.OpacityPreviewChanged += (constant, idle) => _player?.ApplyWindowOpacity(constant, idle, animate: false);
        dialog.AccentPreviewChanged += LivePreviewAccent;

        if (dialog.ShowDialog() != true)
        {
            // Dismissed without applying: undo live previews back to the persisted levels/accent.
            _player?.ApplyWindowOpacity(EffectiveActiveWindowOpacity, EffectiveIdleWindowOpacity);
            ApplyResolvedAccent();
            return;
        }

        if (dialog.RequestedAction == PrivacyAction.ResetAppState)
        {
            PerformResetAppState();
            return;
        }

        if (dialog.AppearanceChanged)
        {
            ApplyPlayerPreferences(dialog.ThemeId, dialog.AccentColor, dialog.FadeIdleDelayMs, dialog.CompactMode,
                dialog.ActiveOpacityOverride, dialog.IdleOpacityOverride, dialog.StripAutoHideOverride,
                dialog.CornerStyle);
        }

        switch (dialog.RequestedAction)
        {
            case PrivacyAction.ClearBrowserData:
                _ = PerformClearBrowserDataAsync();
                break;
        }
    }

    private void PerformResetAppState()
    {
        if (_privacyActionInProgress) return;
        _privacyActionInProgress = true;
        try
        {
            ApplyResetState();
            Prompt.ShowInfo(this, PrivacyService.ResetDoneTitle, PrivacyService.ResetDoneBody);
        }
        finally
        {
            _privacyActionInProgress = false;
        }
    }

    /// <summary>
    /// Apply a reset to the live UI: defaults to settings.json, empty profiles combo, pin off.
    /// References no Browser/WebView2 member and queues no navigation, so the user stays signed in.
    /// Internal so a headless WPF test can prove it runs with a null CoreWebView2.
    /// </summary>
    internal void ApplyResetState()
    {
        _settings = _settingsService.Reset();
        ApplyTopmost(false);
        ApplyAuto(false);
        ApplyThemeResources();
        ApplySourceAppearance();
        ApplyOwnCornerMode();
        ApplyOpenPlayerAppearance();
        UpdateAutoDetector();   // Auto is off after reset → stop the detector
        LoadProfilesIntoCombo();
    }

    private void ApplyPlayerPreferences(string themeId, string accentColor, int fadeIdleDelayMs, bool compactMode,
        double? activeOpacityOverride, double? idleOpacityOverride, bool? stripAutoHideOverride,
        string cornerStyle = ThemeCatalog.DefaultCornerStyle)
    {
        // Theme accent is the single source of truth for Pin/Fade color now (overhaul Task 10);
        // behavior values are NULLABLE overrides (theme code review P2) — the pure writer keeps
        // nulls null so an accent-only apply leaves the user following preset defaults, and
        // mirrors the EFFECTIVE values onto the legacy Player fields.
        var globalAccent = ThemeCatalog.NormalizeAccentColor(accentColor);
        ThemeSettingsWriter.Apply(_settings, themeId, globalAccent, fadeIdleDelayMs, compactMode,
            activeOpacityOverride, idleOpacityOverride, stripAutoHideOverride, cornerStyle);
        CommitAccent(accentColor);

        // Restyle the theme-driven shell resources live (DynamicResource consumers re-resolve), then
        // the runtime-applied Pin/Fade glyphs and the native window corners, so the whole theme
        // moves together on apply.
        ApplyThemeResources();
        ApplySourceAppearance();
        ApplyOwnCornerMode();
        ApplyOpenPlayerAppearance();
        _settingsService.Save(_settings);
    }

    private string EffectiveAccentColor =>
        ThemePreferenceResolver.AccentColor(_settings.Theme, _settings.Player);

    private Profile? ActiveProfile =>
        ProfileAccentService.ActiveProfile(_settings);

    internal string ResolvedAccentColor =>
        ProfileAccentService.ResolvedAccentColor(_settings, EffectiveAccentColor);

    internal void LivePreviewAccent(string hex)
    {
        if (!ThemeCatalog.IsValidHex(hex)) return;
        ApplyAccentEverywhere(ThemeCatalog.NormalizeAccentColor(hex));
    }

    private string CommitAccent(string hex)
    {
        return ProfileAccentService.CommitAccent(_settings, hex);
    }

    private void ApplyResolvedAccent() => ApplyAccentEverywhere(ResolvedAccentColor);

    private void ApplyAccentEverywhere(string accentColor)
    {
        ThemeResourceApplier.ApplyAccentOnly(Application.Current.Resources, accentColor,
            ThemeCatalog.PresetFor(_settings.Theme.ThemeId));
        ApplySourceAppearance(accentColor);
        ApplyOpenPlayerAppearance(accentColor);
    }

    private void ApplyThemeResources()
    {
        ThemeResourceApplier.Apply(Application.Current.Resources, _settings.Theme, _settings.Player);
    }

    private int EffectiveFadeIdleDelayMs =>
        ThemePreferenceResolver.FadeIdleDelayMs(_settings.Theme, _settings.Player);

    private bool EffectiveStripAutoHide =>
        ThemePreferenceResolver.StripAutoHide(_settings.Theme, _settings.Player);

    private double EffectiveActiveWindowOpacity =>
        ThemePreferenceResolver.ActiveWindowOpacity(_settings.Theme, _settings.Player);

    private double EffectiveIdleWindowOpacity =>
        ThemePreferenceResolver.IdleWindowOpacity(_settings.Theme, _settings.Player);

    private string EffectiveCornerStyle =>
        ThemePreferenceResolver.CornerStyle(_settings.Theme);

    private DwmCornerMode EffectiveDwmCornerMode =>
        ThemePreferenceResolver.DwmCorners(_settings.Theme);

    /// <summary>Apply the theme/override native corner preference to this window's own HWND.
    /// No-op before SourceInitialized (which applies the initial mode).</summary>
    private void ApplyOwnCornerMode()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        WindowOpacityApplier.SetCornerMode(hwnd, EffectiveDwmCornerMode);
        WindowOpacityApplier.SetBorderColor(hwnd, suppress: true);   // P1 borderless: no Win11 DWM frame hairline
    }

    private void ApplyOpenPlayerAppearance()
    {
        ApplyOpenPlayerAppearance(ResolvedAccentColor);
    }

    private void ApplyOpenPlayerAppearance(string accentColor)
    {
        _player?.ApplyAppearance(accentColor, EffectiveFadeIdleDelayMs, EffectiveStripAutoHide);
        _player?.ApplyWindowOpacity(EffectiveActiveWindowOpacity, EffectiveIdleWindowOpacity);
        _player?.ApplyCornerMode(EffectiveDwmCornerMode);
    }

    internal void ReplaceSettingsForTests(AppSettings settings)
    {
        _settings = settings;
        ApplyThemeResources();
        ApplySourceAppearance();   // refresh the imperative pin/hint accent from the injected settings
        LoadProfilesIntoCombo();
    }

    internal string ResolvedAccentColorForTests => ResolvedAccentColor;

    // The dismiss-without-apply / edit-cancel revert path (ShowDialog() != true), exposed so the
    // revert can be asserted without driving a modal dialog.
    internal void RevertPreviewedAccentForTests() => ApplyResolvedAccent();

    internal void SelectProfileForTests(string? name)
    {
        ProfileAccentService.SetActiveProfile(_settings, name is null ? null : ProfileService.Find(_settings, name));
        LoadProfilesIntoCombo();
    }

    internal (string AccentColor, int FadeIdleDelayMs, double ActiveWindowOpacity, double IdleWindowOpacity,
        bool StripAutoHide, string CornerStyle) EffectivePlayerPreferencesForTests =>
        (ResolvedAccentColor, EffectiveFadeIdleDelayMs, EffectiveActiveWindowOpacity, EffectiveIdleWindowOpacity,
            EffectiveStripAutoHide, EffectiveCornerStyle);

    private async Task PerformClearBrowserDataAsync()
    {
        if (_privacyActionInProgress) return;

        try
        {
            // Re-check readiness at execution time (the cached enabled state can be stale). This
            // lives INSIDE the try so a throw here (e.g. CoreWebView2 access during a WebView2
            // teardown) can never escape this fire-and-forget task unobserved.
            var core = Browser.CoreWebView2;
            if (!CanClearBrowserData || core is null)
            {
                Prompt.ShowInfo(this, PrivacyService.ClearResultTitle, PrivacyService.ClearBrowserNotReady);
                return;
            }

            _privacyActionInProgress = true;
            _clearingBrowserData = true;
            SettingsButton.IsEnabled = false;   // no second Settings window mid-await

            // Single shared profile: closing the popout avoids it showing a logged-out surface.
            // _clearingBrowserData makes the popout's return handler skip driving source playback.
            if (_player is not null) { try { _player.Close(); } catch { /* ignore */ } }

            // Bound the wait (PrivacyService.ClearTimeout) so a hung clear can never wedge the
            // gear/privacy actions for the rest of the session. The clear runs on the SOURCE core,
            // which we keep alive, so its completion handler always fires (a closed WebView would
            // release it un-invoked). Time it so the bound can be retuned from real durations.
            var sw = Stopwatch.StartNew();
            await PrivacyService.ClearBrowserDataAsync(core).WaitAsync(PrivacyService.ClearTimeout);
            sw.Stop();

            // Reflect the signed-out state; a nav hiccup must not mask a successful clear.
            try { NavigateInternal("https://www.youtube.com/"); }
            catch (Exception navEx) { Log.Error("Post-clear navigation failed.", navEx); }

            Log.Info($"Browser data cleared in {sw.ElapsedMilliseconds} ms (user signed out).");
            Prompt.ShowInfo(this, PrivacyService.ClearDoneTitle, PrivacyService.ClearDoneBody);
        }
        catch (TimeoutException)
        {
            // The wait elapsed, not the clear: ClearBrowsingDataAsync may still finish in the
            // background. Tell the truth instead of reporting a failure that may not be one.
            Log.Warn("Clear browser data exceeded the timeout; it may still complete in the background.");
            Prompt.ShowInfo(this, PrivacyService.ClearResultTitle, PrivacyService.ClearTimedOut);
        }
        catch (Exception ex)
        {
            Log.Error("Clear browser data failed.", ex);
            Prompt.ShowInfo(this, PrivacyService.ClearResultTitle, PrivacyService.ClearFailed);
        }
        finally
        {
            _privacyActionInProgress = false;
            _clearingBrowserData = false;
            SettingsButton.IsEnabled = true;
        }
    }

    /// <summary>Test-only: the navigation queued while the browser was not ready (null = none).</summary>
    internal string? PendingUrlForTests => _pendingUrl;

    // --- Video Popout lifecycle (spec 13) ---

    private async void PopOutButton_Click(object sender, RoutedEventArgs e) => await StartVideoPopoutAsync();

    private void PlaceholderShowPopoutButton_Click(object sender, RoutedEventArgs e) =>
        ActivateExistingPlayer();

    private async Task StartVideoPopoutAsync()
    {
        // Guards (spec 13.4): browser ready, no popout in flight, single player (ADR-0005).
        if (!_browserReady || _popoutInProgress) return;
        if (_player is not null) { ActivateExistingPlayer(); return; }

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
                Prompt.ShowInfo(this, "Pop out video", "Open a YouTube video first, then press Pop out video.");
                return;
            }

            // Auto de-dup: remember this video so the return-resume play edge (and an in-source
            // pause/resume) isn't read as a fresh play that should re-pop it (spec §6.1).
            _autoLastHandledVideoId = target.VideoId;
            _popoutSourceVideoId = target.VideoId;

            // 2b) Resolve the effective playback mode (spec 10): a matching profile override wins,
            // otherwise the global compact default. Compact mode uses the embedded YouTube player;
            // normal mode keeps the full watch page. Normal remains the default and the fallback.
            var mode = PlaybackModePolicy.ResolveEffectivePopoutMode(
                ResolveActiveProfileMode(target.VideoId), _settings.Player.CompactMode);
            var popoutUrl = PlaybackModePolicy.BuildPopoutUrl(
                mode, target, seconds, WebViewEnvironmentService.ShellPlayerUrl);

            // 3) Pause the source and show the placeholder (Q-1: no duplicate audio). A non-null
            // FallbackReason (mix/radio drop) rides along as the placeholder note (Q-6) — it was
            // previously log-only, invisible to the user.
            await YouTubeDomBridge.PauseAsync(core);
            ShowSourcePlaceholder(true, target.FallbackReason);

            // 4) Create the single Popout Player on the shared environment, in the resolved mode.
            var env = App.Current.WebViewEnvironment.Environment
                      ?? await App.Current.WebViewEnvironment.EnsureCreatedAsync();

            // The target doubles as the compact error bar's fallback handle (spec 10.3 / Q-6,
            // Stage 4): a compact player that can't play rebuilds the normal watch URL from it,
            // so carry the live timestamp onto it — a shell that errors before ever playing has
            // no shell-reported seconds, and the fallback must not restart the video at 0:00.
            target.StartSeconds = seconds ?? target.StartSeconds;
            _player = new PlayerWindow(env, popoutUrl, _settings.Player.Topmost,
                _settings.Player.Placement, _settings.Player.LastWidth, _settings.Player.LastHeight,
                _settings.Player.FadeEnabled, ResolvedAccentColor,
                EffectiveFadeIdleDelayMs, mode, target,
                EffectiveActiveWindowOpacity, EffectiveIdleWindowOpacity,
                EffectiveStripAutoHide, EffectiveDwmCornerMode);
            _player.PlayerClosed += Player_OnClosed;
            _player.Show();

            if (target.FallbackReason is not null) Log.Info($"Popout fallback: {target.FallbackReason}");
            Log.Info($"Video Popout started at t={seconds?.ToString() ?? "0"}s, " +
                     $"wasPlaying={_sourceWasPlayingAtPopout}, mode={mode}.");
        }
        catch (Exception ex)
        {
            // Failure after pause (spec 13.5): restore the source and resume if it had been playing.
            Log.Error("Video Popout failed; restoring source.", ex);
            ShowSourcePlaceholder(false);
            if (_player is not null) { try { _player.Close(); } catch { /* ignore */ } _player = null; }
            if (_sourceWasPlayingAtPopout && core is not null) await YouTubeDomBridge.PlayAsync(core);
            Prompt.ShowInfo(this, "Pop out video", "PiPlay couldn't pop out this video. It stayed in the main window.");
        }
        finally
        {
            _popoutInProgress = false;
            PopOutButton.IsEnabled = true;
            UpdatePopoutActionState();   // covers both outcomes: player created or rolled back
        }
    }

    /// <summary>
    /// Show/focus the existing popout (ADR-0005 activate-existing rule). RestoreWindow first:
    /// Activate() alone does not un-minimize, and restoring (not forcing Normal) keeps a
    /// maximized popout maximized.
    /// </summary>
    private void ActivateExistingPlayer()
    {
        if (_player is null) return;
        if (_player.WindowState == WindowState.Minimized)
            System.Windows.SystemCommands.RestoreWindow(_player);
        _player.Activate();
    }

    /// <summary>
    /// Reflect the single-player lifecycle on the primary action (Q-6): while a popout is open the
    /// button shows/focuses it instead of implying a second popout will open. Label, tooltip, and
    /// UIA name flip together so the accessible name never lies (REQ-UI-02).
    /// </summary>
    private void UpdatePopoutActionState() => ApplyPopoutActionState(_player is not null);

    internal void ApplyPopoutActionState(bool hasPlayer)
    {
        var label = hasPlayer ? "Show popout" : "Pop out video";
        PopOutButtonText.Text = label;
        System.Windows.Automation.AutomationProperties.SetName(PopOutButton, label);
        PopOutButton.ToolTip = hasPlayer
            ? "Bring the open Video Popout to the front"
            : "Pop out the current video";
    }

    /// <summary>
    /// The selected profile's playback-mode override for this popout. REQ-PROFILE-01 governs the
    /// per-field precedence (a profile field overrides the global default); the pure
    /// <see cref="PlaybackModePolicy.ResolveProfileOverride"/> additionally scopes the override to
    /// the profile's own video, so a stale combo selection plus manual navigation can't apply a
    /// profile's compact preference to an unrelated video. Null when no profile is selected, the
    /// profile carries no mode, or the popout target is not that profile's video.
    /// </summary>
    private string? ResolveActiveProfileMode(string? targetVideoId)
    {
        if (ProfilesCombo.SelectedItem is not Profile profile) return null;
        var profileVideoId = YouTubeUrlHelper.TryParse(profile.Url, out var profileTarget)
            ? profileTarget.VideoId
            : null;
        return PlaybackModePolicy.ResolveProfileOverride(profile.Mode, profileVideoId, targetVideoId);
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

    internal void ShowSourcePlaceholder(bool visible, string? note = null)
    {
        // Tier-1 placeholder (spec 13.3): hide the source WebView, show the WPF black panel.
        Browser.Visibility = visible ? Visibility.Hidden : Visibility.Visible;
        SourcePlaceholder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        // Optional non-blocking note (Q-6), e.g. the mix/radio fallback reason. Cleared with the
        // placeholder so a stale note can't survive into the next popout.
        PlaceholderNoteText.Text = note ?? string.Empty;
        PlaceholderNoteText.Visibility =
            visible && !string.IsNullOrEmpty(note) ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Player_OnClosed(object? sender, PlayerReturnState state)
    {
        try
        {
            _player = null;
            UpdatePopoutActionState();

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
            await ApplyReturnActionAsync(state);

            _settingsService.Save(_settings);
            Log.Info("Returned from Video Popout.");
        }
        catch (Exception ex)
        {
            Log.Error("Error returning from Video Popout.", ex);
        }
    }

    /// <summary>
    /// The return decision of <see cref="Player_OnClosed"/> (REQ-RETURN-01), separate from the
    /// persistence half so the WPF lane can drive it without writing real settings. Skipped
    /// entirely while Clear browser data is wiping the session — the page is about to be
    /// cleared/navigated, so driving it would be wasted or race. Navigate needs no live core
    /// (<see cref="NavigateInternal"/> queues until the browser is ready); the script-driven
    /// cases do, and fall through silently without one.
    /// </summary>
    internal async Task ApplyReturnActionAsync(PlayerReturnState state)
    {
        if (_clearingBrowserData) return;
        var core = Browser.CoreWebView2;

        // REQ-RETURN-01: resume only if the source was playing when popout started;
        // 0 is a valid timestamp distinct from unknown. Decision lives in ReturnPolicy.
        switch (ReturnPolicy.Decide(state.LastKnownSeconds, _sourceWasPlayingAtPopout,
                    state.VideoId, _popoutSourceVideoId))
        {
            case ReturnAction.Navigate:
                // The popout ended on a DIFFERENT video (recommendation click, playlist
                // auto-advance, SPA navigation): bring the source to where the user
                // actually is. The timestamp rides the watch URL; Auto's de-dup key
                // updates FIRST so the returned video is not instantly re-popped.
                _autoLastHandledVideoId = state.VideoId;
                NavigateInternal(YouTubeUrlHelper.BuildWatchUrl(
                    new YouTubeTarget { VideoId = state.VideoId }, state.LastKnownSeconds));
                break;
            case ReturnAction.SeekAndPlay when core is not null:
                await YouTubeDomBridge.SeekAndPlayAsync(core, state.LastKnownSeconds!.Value);
                break;
            case ReturnAction.Seek when core is not null:
                await YouTubeDomBridge.SeekAsync(core, state.LastKnownSeconds!.Value);
                break;
            case ReturnAction.Play when core is not null:
                await YouTubeDomBridge.PlayAsync(core);
                break;
        }
    }

    // Return seams (overhaul Task 3, WPF lane): drive the navigate-vs-seek return decision
    // headlessly — the queued pending URL is the observable for the Navigate case.
    internal void SeedPopoutReturnForTests(string sourceVideoId) => _popoutSourceVideoId = sourceVideoId;
    internal string? AutoLastHandledVideoIdForTests => _autoLastHandledVideoId;

    // --- Single-instance activation (REQ-APP-01) ---

    public void ActivateFromSecondInstance(string? url)
    {
        // Un-minimize to the window's prior state (Normal or Maximized) rather than forcing Normal,
        // which would silently drop a maximized layout the user left (REQ-WINDOW-01 / spec 16.4).
        // RestoreWindow remembers the pre-minimize state; the guard keeps it from un-maximizing.
        if (WindowState == WindowState.Minimized) System.Windows.SystemCommands.RestoreWindow(this);
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
            _autoTimer?.Stop();
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

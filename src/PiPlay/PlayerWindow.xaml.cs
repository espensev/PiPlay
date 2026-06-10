using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using PiPlay.Models;
using PiPlay.Services;
using PiPlay.Theme;

namespace PiPlay;

/// <summary>
/// PlayerWindow / Popout Player (spec 12.2): a borderless media window hosting the
/// popped-out YouTube playback on the shared WebView2 environment. Provides native-quality
/// move/resize, a Pin toggle, polls the last-known timestamp, and reports return state to
/// the Source Window when it closes.
/// </summary>
public partial class PlayerWindow : Window
{
    private readonly CoreWebView2Environment _environment;
    private readonly string _url;
    private readonly PlacementData? _placement;
    private readonly DispatcherTimer _syncTimer;
    private readonly PlayerReturnState _returnState = new();

    // Mutable: the compact fallback (spec 10.3 / Q-6) flips the window to normal-mode behavior in
    // place, so the mode-dependent seams (timestamp source, minimum size) follow the live surface.
    private PlaybackMode _mode;

    // Controls fade (spec 11, Phase 2): idle/hover state machine over the chrome strip only.
    private readonly DispatcherTimer _idleTimer;
    private bool _fadeEnabled;
    private bool _isDragging;
    private bool _controlsVisible = true;

    // Whole-window opacity (spec 7.3, Phase 4): two persisted levels applied as layered alpha by
    // WindowOpacityApplier. Idle ONSET shares _idleTimer with the controls fade (one idleness
    // definition); the hover-restore poll exists because WPF receives no mouse events over the
    // WebView2 child HWND (Stage 0 spike finding), so restoring on movement over the video needs
    // a cursor probe. The poll runs only while the window is opacity-idle-faded.
    private double _constantWindowOpacity;
    private double _idleWindowOpacity;
    private bool _windowOpacityIdle;
    private int _probeCursorX, _probeCursorY;        // last cursor position the activity probe saw
    private long _lastInWindowCursorMoveMs = -1;     // Environment.TickCount64 of the last in-window move
    private readonly DispatcherTimer _opacityHoverPoll;

    private bool _navCompleted;
    private bool _capturedReturn;
    private bool _nudgedPlay;

    // Compact (shell) mode only: the host side of the IFrame-API bridge (spec 10.3), the target
    // the error bar's fallback reopens in normal page mode (Stage 4 / Q-6), the one-shot "the
    // IFrame API never came up" watchdog, and the one-way fallback latch.
    private PlayerShellBridge? _shellBridge;
    private readonly YouTubeTarget? _fallbackTarget;
    private readonly DispatcherTimer _shellReadyTimer;
    private bool _fellBack;

    /// <summary>Raised once when the player has closed, carrying the state needed to return (spec 14).</summary>
    public event EventHandler<PlayerReturnState>? PlayerClosed;

    internal TimeSpan FadeIdleDelayForTests => _idleTimer.Interval;

    public PlayerWindow(
        CoreWebView2Environment environment,
        string url,
        bool topmost,
        PlacementData? placement,
        int defaultWidth,
        int defaultHeight,
        bool fadeEnabled,
        string pinAccent = PlayerAppearancePolicy.DefaultAccent,
        string fadeAccent = PlayerAppearancePolicy.DefaultAccent,
        int fadeIdleDelayMs = PlayerAppearancePolicy.DefaultFadeIdleDelayMs,
        PlaybackMode mode = PlaybackMode.Normal,
        YouTubeTarget? fallbackTarget = null,
        double constantWindowOpacity = WindowOpacityPolicy.Default,
        double idleWindowOpacity = WindowOpacityPolicy.Default)
    {
        InitializeComponent();
        BorderlessWindowHelper.EnableExpandedResizeZones(this);

        _environment = environment;
        _url = url;
        _mode = mode;
        _fallbackTarget = fallbackTarget;

        // Mode-specific minimum (spec 10.2 / 16.1): compact embed mode needs a larger floor than the
        // 320x180 normal minimum so the embedded player controls stay usable. MinWidth/MinHeight set
        // the floor and clamp the launch size up (the Math.Max below). A saved sub-minimum placement
        // is raised to the same floor via PlacementMath.EnsureMinSize (the saved bounds are physical
        // pixels; EnsureMinSize converts the DIP minimum with the saved DPI scale), so a window
        // restored across modes never opens below the mode minimum.
        var minWidth = PlaybackModePolicy.MinWidthFor(mode);
        var minHeight = PlaybackModePolicy.MinHeightFor(mode);
        MinWidth = minWidth;
        MinHeight = minHeight;
        _placement = placement is null ? null : PlacementMath.EnsureMinSize(placement, minWidth, minHeight);

        Width = Math.Max(MinWidth, defaultWidth);
        Height = Math.Max(MinHeight, defaultHeight);
        if (placement is null) WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Topmost = topmost;
        PinToggle.IsChecked = topmost;

        _fadeEnabled = fadeEnabled;
        FadeToggle.IsChecked = fadeEnabled;
        _returnState.FadeEnabled = fadeEnabled;

        _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _syncTimer.Tick += SyncTimer_Tick;

        // Compact only: if the shell never sends ANY bridge message (ready/state/error) within the
        // policy window, the IFrame API is dead (blocked script, offline) — surface the error bar
        // (spec 10.3 / Q-6). Any inbound message stops it.
        _shellReadyTimer = new DispatcherTimer { Interval = PlayerShellErrorPolicy.ReadyTimeout };
        _shellReadyTimer.Tick += ShellReadyTimer_Tick;

        // Idle timer drives the fade-out; any mouse move restarts it (spec 22.1 fade row).
        _idleTimer = new DispatcherTimer();
        _idleTimer.Tick += IdleTimer_Tick;
        ApplyAppearance(pinAccent, fadeAccent, fadeIdleDelayMs);
        MouseMove += (_, _) => OnUserActivity();
        MouseEnter += (_, _) => OnUserActivity();

        _constantWindowOpacity = WindowOpacityPolicy.Normalize(constantWindowOpacity);
        _idleWindowOpacity = WindowOpacityPolicy.Normalize(idleWindowOpacity);
        _opacityHoverPoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _opacityHoverPoll.Tick += OpacityHoverPoll_Tick;

        Loaded += (_, _) => ApplyFadeState();
        Loaded += async (_, _) => await InitializePlayerAsync();
        SourceInitialized += (_, _) =>
        {
            if (_placement is not null) WindowPlacementService.Restore(this, _placement);
            ApplyWindowOpacityToHwnd(animate: false);   // appear at the configured level, no flash
        };
        Closing += PlayerWindow_Closing;
        Closed += PlayerWindow_Closed;
    }

    private async Task InitializePlayerAsync()
    {
        try
        {
            await Player.EnsureCoreWebView2Async(_environment);

            var core = Player.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsBuiltInErrorPageEnabled = true;
            core.Settings.IsStatusBarEnabled = false;

            core.NavigationStarting += Core_NavigationStarting;
            core.NewWindowRequested += Core_NewWindowRequested;
            core.NavigationCompleted += Core_NavigationCompleted;

            if (_mode == PlaybackMode.Compact)
            {
                // Compact mode hosts the local shell (spec 10.3): map its virtual host before
                // navigating, then let the shell bridge (IFrame API state) drive the return timestamp.
                WebViewEnvironmentService.MapShellVirtualHost(core);
                _shellBridge = new PlayerShellBridge(core);
                _shellBridge.Ready += ShellBridge_Ready;
                _shellBridge.StateReceived += ShellBridge_StateReceived;
                _shellBridge.ErrorReceived += ShellBridge_ErrorReceived;
            }

            core.Navigate(_url);
            if (_mode == PlaybackMode.Compact) _shellReadyTimer.Start();
            Log.Info($"Popout Player initialized (mode={_mode}).");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to initialize the Popout Player.", ex);
            Prompt.ShowInfo(this, "Video Popout", "PiPlay couldn't start the popout player.\n\n" + ex.Message);
            Close();
        }
    }

    // --- Navigation policy: YouTube only (REQ-NAV-02) ---

    private void Core_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) &&
            NavigationPolicy.IsAllowed(uri, NavigationSurface.Player))
        {
            return;
        }

        e.Cancel = true;
        Log.Info($"Player navigation blocked, opening externally: {Log.RedactUrl(e.Uri)}");
        OpenExternal(e.Uri);
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // Never replace the player with an external page; open it in the system browser.
        e.Handled = true;
        OpenExternal(e.Uri);
    }

    private void Core_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _navCompleted = true;
        // A compact shell that failed to load outright can never message the host — surface the
        // error bar now instead of waiting out the watchdog (spec 10.3 / Q-6, Stage 4).
        if (_mode == PlaybackMode.Compact && !e.IsSuccess)
        {
            _shellReadyTimer.Stop();
            ShowShellError(PlayerShellErrorPolicy.ShellLoadFailedMessage);
        }
        // Normal mode polls the YouTube page DOM for the timestamp; compact mode reads it from the
        // shell bridge (IFrame API). The mode-specific choice lives in PlaybackModePolicy so the
        // "one source of truth for the timestamp" invariant is unit-testable (spec 10.3).
        if (PlaybackModePolicy.UsesDomSyncTimer(_mode) && !_syncTimer.IsEnabled) _syncTimer.Start();
    }

    // --- Compact shell bridge + error/fallback path (spec 10.3 / Q-6, Stage 4) ---

    private void ShellBridge_Ready(object? sender, EventArgs e) => _shellReadyTimer.Stop();

    private void ShellBridge_StateReceived(object? sender, InboundShellMessage state)
    {
        if (_mode != PlaybackMode.Compact) return;
        _shellReadyTimer.Stop();
        // Compact mode's source of truth for the return timestamp is the IFrame API, not the DOM.
        _returnState.LastKnownSeconds = state.CurrentTime;
        // A playing state proves recovery (e.g. a playlist auto-advanced past a dead entry) —
        // clear a showing error so the bar can't outlive the problem it reported.
        if (PlayerShellErrorPolicy.ShouldAutoDismiss(state.PlayerState)) HideShellError();
    }

    private void ShellBridge_ErrorReceived(object? sender, InboundShellMessage message)
    {
        if (_mode != PlaybackMode.Compact) return;
        _shellReadyTimer.Stop();
        ShowShellError(PlayerShellErrorPolicy.Describe(message.ErrorCode));
    }

    private void ShellReadyTimer_Tick(object? sender, EventArgs e)
    {
        _shellReadyTimer.Stop();
        if (_mode != PlaybackMode.Compact) return;
        ShowShellError(PlayerShellErrorPolicy.ReadyTimeoutMessage);
    }

    private void ShowShellError(string message)
    {
        ErrorText.Text = message;
        ErrorBar.Visibility = Visibility.Visible;
        // Redacted target only (spec 17): never the full query string.
        Log.Info($"Compact player error shown (\"{message}\") for {Log.RedactUrl(_url)}; " +
                 "normal-page fallback offered.");
    }

    private void HideShellError()
    {
        if (ErrorBar.Visibility == Visibility.Collapsed) return;
        ErrorBar.Visibility = Visibility.Collapsed;
    }

    private void FallbackButton_Click(object sender, RoutedEventArgs e) => FallBackToNormalPage();

    private void ErrorDismissButton_Click(object sender, RoutedEventArgs e) => HideShellError();

    /// <summary>
    /// The error bar's fallback action (Q-6): re-navigate this same window to the normal watch
    /// page for the same target at the best-known timestamp. In-place — closing instead would fire
    /// the return lifecycle (source resume / auto re-popout). From here the window IS a
    /// normal-mode player: the DOM sync timer becomes the one timestamp source, the shell bridge
    /// is disposed, and the minimum relaxes to the normal floor. One-way by design; saved mode
    /// preferences are untouched.
    /// </summary>
    private void FallBackToNormalPage()
    {
        if (_fellBack || _fallbackTarget is null || Player.CoreWebView2 is null) return;
        _fellBack = true;

        _shellReadyTimer.Stop();
        try { _shellBridge?.Dispose(); } catch { /* ignore */ }
        _shellBridge = null;

        _mode = PlaybackMode.Normal;
        MinWidth = PlaybackModePolicy.MinWidthFor(PlaybackMode.Normal);
        MinHeight = PlaybackModePolicy.MinHeightFor(PlaybackMode.Normal);
        HideShellError();

        var url = YouTubeUrlHelper.BuildWatchUrl(_fallbackTarget, _returnState.LastKnownSeconds);
        Log.Info($"Compact fallback: reopening in normal page mode: {Log.RedactUrl(url)}");
        Player.CoreWebView2.Navigate(url);
    }

    // Test seams (WPF lane): drive the error/fallback path without a live WebView2 or shell.
    internal void HandleShellStateForTests(InboundShellMessage state) => ShellBridge_StateReceived(this, state);
    internal void HandleShellErrorForTests(InboundShellMessage message) => ShellBridge_ErrorReceived(this, message);
    internal void HandleShellLoadFailureForTests() => ShowShellError(PlayerShellErrorPolicy.ShellLoadFailedMessage);
    internal void RequestFallbackForTests() => FallBackToNormalPage();
    internal bool IsErrorBarVisibleForTests => ErrorBar.Visibility == Visibility.Visible;
    internal string ErrorTextForTests => ErrorText.Text;

    private async void SyncTimer_Tick(object? sender, EventArgs e)
    {
        if (!_navCompleted || Player.CoreWebView2 is null) return;
        var state = await YouTubeDomBridge.ReadPlayerStateAsync(Player.CoreWebView2);
        if (state is null) return;

        // The popout is the active surface now: if it came up paused, nudge play once
        // (play() is an allowed control per spec 19). Best-effort; never forced again.
        if (!_nudgedPlay && state.Paused)
        {
            _nudgedPlay = true;
            await YouTubeDomBridge.PlayAsync(Player.CoreWebView2);
        }

        _returnState.LastKnownSeconds = state.CurrentTime;
    }

    // --- Chrome ---

    private void PinToggle_Click(object sender, RoutedEventArgs e) => Topmost = PinToggle.IsChecked == true;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ChromeStrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        _isDragging = true;
        try { DragMove(); }
        catch { /* DragMove throws if the button was already released */ }
        finally
        {
            _isDragging = false;
            OnUserActivity(); // keep controls up briefly after a drag, then resume idle countdown
        }
    }

    // --- Controls fade (spec 11, Phase 2) ---

    private void FadeToggle_Click(object sender, RoutedEventArgs e)
    {
        _fadeEnabled = FadeToggle.IsChecked == true;
        _returnState.FadeEnabled = _fadeEnabled;
        ApplyFadeState();
    }

    internal void ApplyAppearance(string? pinAccent, string? fadeAccent, int fadeIdleDelayMs)
    {
        ToggleAccent.SetCheckedBrush(PinToggle, ResolveAccentBrush(pinAccent));
        ToggleAccent.SetCheckedBrush(FadeToggle, ResolveAccentBrush(fadeAccent));
        _idleTimer.Interval = TimeSpan.FromMilliseconds(
            PlayerAppearancePolicy.NormalizeFadeIdleDelayMs(fadeIdleDelayMs));

        if (_fadeEnabled && _idleTimer.IsEnabled) RestartIdleTimer();
    }

    private Brush ResolveAccentBrush(string? accentKey) =>
        (Brush)FindResource(PlayerAppearancePolicy.BrushResourceKeyFor(accentKey));

    /// <summary>Reset the fade lifecycle to match <see cref="_fadeEnabled"/>: either show-and-arm or pin visible.</summary>
    private void ApplyFadeState()
    {
        if (_fadeEnabled)
        {
            ShowControls();
            RestartIdleTimer();
        }
        else
        {
            _idleTimer.Stop();
            ShowControls(); // disabling fade restores the MVP "always visible" behavior immediately
            ExitWindowOpacityIdle(); // no idle source while fade is off, so no idle opacity either
        }
    }

    private void OnUserActivity()
    {
        ExitWindowOpacityIdle();
        if (!_fadeEnabled) return;
        ShowControls();
        RestartIdleTimer();
    }

    private void RestartIdleTimer()
    {
        _idleTimer.Stop();
        _idleTimer.Start();
    }

    private void IdleTimer_Tick(object? sender, EventArgs e)
    {
        _idleTimer.Stop();
        // Activity WPF couldn't see: the probe watches the WebView2 area (no WPF mouse events
        // there — Stage 0 spike finding). Recent in-window movement means not idle — re-arm.
        // This feeds the ONE idleness definition, holding up both the strip and the window
        // opacity. The probe only runs while idle opacity is configured, so default-look fade
        // behavior is untouched.
        if (_opacityHoverPoll.IsEnabled && _lastInWindowCursorMoveMs >= 0 &&
            Environment.TickCount64 - _lastInWindowCursorMoveMs < _idleTimer.Interval.TotalMilliseconds)
        {
            RestartIdleTimer();
            return;
        }
        if (FadePolicy.ShouldHide(_fadeEnabled, ChromeStrip.IsMouseOver, _isDragging, idleElapsed: true))
        {
            HideControls();
            // Window opacity idles on the SAME tick with the SAME inputs (one idleness definition,
            // design 2026-06-10 §5).
            EnterWindowOpacityIdle();
        }
        else if (_fadeEnabled)
        {
            // Something is still holding controls up (pointer over the strip / mid-drag); re-arm.
            RestartIdleTimer();
        }
    }

    // --- Whole-window opacity (spec 7.3, Phase 4) ---

    internal (double Constant, double Idle) WindowOpacityLevelsForTests => (_constantWindowOpacity, _idleWindowOpacity);
    internal bool IsWindowOpacityIdleForTests => _windowOpacityIdle;
    internal bool IsOpacityHoverPollRunningForTests => _opacityHoverPoll.IsEnabled;
    internal void EnterWindowOpacityIdleForTests() => EnterWindowOpacityIdle();
    internal void OnUserActivityForTests() => OnUserActivity();

    /// <summary>Live re-apply seam, called by MainWindow for settings changes and the dialog's
    /// live preview (mirrors <see cref="ApplyAppearance"/>).</summary>
    internal void ApplyWindowOpacity(double constantOpacity, double idleOpacity)
    {
        _constantWindowOpacity = WindowOpacityPolicy.Normalize(constantOpacity);
        _idleWindowOpacity = WindowOpacityPolicy.Normalize(idleOpacity);
        ApplyWindowOpacityToHwnd(animate: true);
    }

    private void ApplyWindowOpacityToHwnd(bool animate)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;   // SourceInitialized applies the initial state

        var active = WindowOpacityPolicy.Effective(isIdle: false, _constantWindowOpacity, _idleWindowOpacity);
        var idle = WindowOpacityPolicy.Effective(isIdle: true, _constantWindowOpacity, _idleWindowOpacity);
        // Rounded corners belong to the floating translucent look (spike S-3). They track the
        // CONFIGURED feature, not the momentary alpha, so hover-restores don't square the corners;
        // with both levels at 1.0 the window stays byte-identical to the pre-Phase-4 popout.
        WindowOpacityApplier.SetRoundedCorners(hwnd, rounded: active < WindowOpacityPolicy.Max || idle < WindowOpacityPolicy.Max);
        WindowOpacityApplier.Apply(hwnd, _windowOpacityIdle ? idle : active, animate);

        // The activity probe runs for as long as an idle dip is configured (it both prevents
        // idle onset during movement over the video and restores after onset). Off at defaults.
        var pollWanted = idle < active;
        if (pollWanted && !_opacityHoverPoll.IsEnabled) _opacityHoverPoll.Start();
        else if (!pollWanted) _opacityHoverPoll.Stop();
    }

    private void EnterWindowOpacityIdle()
    {
        if (_windowOpacityIdle) return;
        _windowOpacityIdle = true;
        ApplyWindowOpacityToHwnd(animate: true);
    }

    private void ExitWindowOpacityIdle()
    {
        if (!_windowOpacityIdle) return;
        _windowOpacityIdle = false;
        ApplyWindowOpacityToHwnd(animate: true);
    }

    /// <summary>
    /// Activity sensor for the area WPF can't see (the WebView2 child swallows all mouse input):
    /// records the last cursor MOVE over this window so idle onset is prevented during sustained
    /// movement over the video, and restores immediately when movement happens while opacity-idle
    /// — spec 7.3 "hover restores full opacity". A cursor parked on the video still goes (and
    /// stays) idle.
    /// </summary>
    private void OpacityHoverPoll_Tick(object? sender, EventArgs e)
    {
        if (!WindowOpacityApplier.TryGetCursorPos(out var x, out var y)) return;
        var moved = x != _probeCursorX || y != _probeCursorY;
        _probeCursorX = x;
        _probeCursorY = y;
        if (!moved || PresentationSource.FromVisual(this) is null) return;

        var p = PointFromScreen(new Point(x, y));
        if (p.X < 0 || p.Y < 0 || p.X > ActualWidth || p.Y > ActualHeight) return;
        _lastInWindowCursorMoveMs = Environment.TickCount64;
        if (_windowOpacityIdle) OnUserActivity();
    }

    private void ShowControls()
    {
        if (_controlsVisible && ChromeStrip.IsHitTestVisible) return;
        _controlsVisible = true;
        ChromeStrip.IsHitTestVisible = true;
        AnimateStripOpacity(1.0);
    }

    private void HideControls()
    {
        if (!_controlsVisible) return;
        _controlsVisible = false;
        // Drop hit-testing only once fully faded so a hidden strip can't swallow clicks (Q-8).
        AnimateStripOpacity(0.0, onCompleted: () => { if (!_controlsVisible) ChromeStrip.IsHitTestVisible = false; });
    }

    private void AnimateStripOpacity(double to, Action? onCompleted = null)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(FadePolicy.FadeDurationMs));
        if (onCompleted is not null) animation.Completed += (_, _) => onCompleted();
        ChromeStrip.BeginAnimation(OpacityProperty, animation);
    }

    // --- Close / return (spec 14) ---

    private void PlayerWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_capturedReturn) return;
        _capturedReturn = true;

        _syncTimer.Stop();
        _idleTimer.Stop();
        _shellReadyTimer.Stop();
        _opacityHoverPoll.Stop();
        _returnState.Topmost = Topmost;
        _returnState.FadeEnabled = _fadeEnabled;
        _returnState.Placement = WindowPlacementService.TryCapture(this);
    }

    private void PlayerWindow_Closed(object? sender, EventArgs e)
    {
        try { _shellBridge?.Dispose(); } catch { /* ignore */ }
        try { Player.Dispose(); } catch { /* ignore */ }

        PlayerClosed?.Invoke(this, _returnState);
        Log.Info($"Popout Player closed; lastKnownSeconds=" +
                 $"{(_returnState.LastKnownSeconds?.ToString() ?? "unknown")}.");
    }

    private static void OpenExternal(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error("Failed to open an external link.", ex); }
    }
}

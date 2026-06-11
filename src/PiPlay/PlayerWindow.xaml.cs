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
    // Segoe MDL2 caption glyphs (kept as escapes so the source stays plain ASCII).
    private const string GlyphMaximize = "";
    private const string GlyphRestore = "";

    private readonly CoreWebView2Environment _environment;
    private readonly PlacementData? _placement;
    private readonly DispatcherTimer _syncTimer;
    private readonly PlayerReturnState _returnState = new();

    // Expand/restore (overhaul Task 4): true only while a compact fullscreen ELEMENT (the shell's
    // YouTube fullscreen button) caused the expansion, so exiting element fullscreen restores the
    // window without un-expanding one the user expanded deliberately.
    private bool _maximizedForFullScreenElement;

    // Mutable: the compact fallback (spec 10.3 / Q-6) flips the window to normal-mode behavior in
    // place, so the mode-dependent seams (timestamp source, minimum size) follow the live surface.
    private PlaybackMode _mode;

    // Controls fade (spec 11, Phase 2): idle/hover state machine over the chrome strip only.
    private readonly DispatcherTimer _idleTimer;
    private bool _fadeEnabled;
    private bool _isDragging;
    private bool _controlsVisible = true;

    // Strip auto-hide (spec 7.2 chrome fade, Phase 4): when on, an idle-hidden strip also
    // height-collapses so the video fills the window; the top-edge hover band (via the activity
    // probe — WPF sees no mouse over the WebView2 child) reveals it. Same idle source as the
    // fade, so fade-off means no auto-hide either.
    private bool _stripAutoHide;

    // Whole-window opacity (spec 7.3, Phase 4): two persisted levels applied as layered alpha by
    // WindowOpacityApplier. Idle ONSET shares _idleTimer with the controls fade (one idleness
    // definition); the hover-restore poll exists because WPF receives no mouse events over the
    // WebView2 child HWND (Stage 0 spike finding), so restoring on movement over the video needs
    // a cursor probe. The poll runs while anything needs it (idle dip configured, or strip
    // auto-hide armed) — see UpdateActivityProbe; off at defaults.
    private double _constantWindowOpacity;
    private double _idleWindowOpacity;
    private bool _windowOpacityIdle;
    private int _probeCursorX, _probeCursorY;        // last cursor position the activity probe saw
    private long _lastInWindowCursorMoveMs = -1;     // Environment.TickCount64 of the last in-window move
    private readonly DispatcherTimer _opacityHoverPoll;

    private bool _navCompleted;
    private bool _capturedReturn;
    private bool _nudgedPlay;

    // Compact (shell) mode only: the host side of the IFrame-API bridge (spec 10.3), the one-shot
    // "the IFrame API never came up" watchdog, and the one-way fallback latch.
    private PlayerShellBridge? _shellBridge;
    private readonly DispatcherTimer _shellReadyTimer;
    private bool _fellBack;

    // Mutable navigation state (overhaul Task 3): an in-place retarget (recommendation click via
    // NewWindowRequested) moves the player off its launch video, so the current URL and the target
    // the error bar's fallback reopens (Stage 4 / Q-6) must follow the LIVE video, not the launch one.
    private string _currentUrl;
    private YouTubeTarget? _currentTarget;

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
        string? accentColor = ThemeCatalog.DefaultAccentColor,
        int fadeIdleDelayMs = PlayerAppearancePolicy.DefaultFadeIdleDelayMs,
        PlaybackMode mode = PlaybackMode.Normal,
        YouTubeTarget? fallbackTarget = null,
        double constantWindowOpacity = WindowOpacityPolicy.Default,
        double idleWindowOpacity = WindowOpacityPolicy.Default,
        bool stripAutoHide = false)
    {
        InitializeComponent();
        BorderlessWindowHelper.EnableExpandedResizeZones(this);

        _environment = environment;
        _currentUrl = url;
        _mode = mode;
        _currentTarget = fallbackTarget;
        _returnState.VideoId = fallbackTarget?.VideoId;   // the launch video until navigation says otherwise

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
        // ForNextLaunch on the way IN as well as on capture (Task 4): a popout never LAUNCHES
        // expanded, including from pre-fix settings files that saved Maximized.
        _placement = placement is null
            ? null
            : PlacementMath.EnsureMinSize(PlacementMath.ForNextLaunch(placement)!, minWidth, minHeight);

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

        _constantWindowOpacity = WindowOpacityPolicy.Normalize(constantWindowOpacity);
        _idleWindowOpacity = WindowOpacityPolicy.Normalize(idleWindowOpacity);
        _opacityHoverPoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _opacityHoverPoll.Tick += OpacityHoverPoll_Tick;

        // Idle timer drives the fade-out; any mouse move restarts it (spec 22.1 fade row).
        // Both timers exist before ApplyAppearance, which touches the interval AND the probe.
        _idleTimer = new DispatcherTimer();
        _idleTimer.Tick += IdleTimer_Tick;
        ApplyAppearance(accentColor, fadeIdleDelayMs, stripAutoHide);
        MouseMove += (_, _) => OnUserActivity();
        MouseEnter += (_, _) => OnUserActivity();

        Loaded += (_, _) => ApplyFadeState();
        Loaded += async (_, _) => await InitializePlayerAsync();
        // OS-driven state changes (Win+Up/Down, aero snap) must keep the affordance honest too —
        // the direct calls in ToggleExpandedState cover the test lane, where unshown windows
        // never receive StateChanged.
        StateChanged += (_, _) => HandleWindowStateChanged();
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
            core.SourceChanged += Core_SourceChanged;
            // Secondary expand route (overhaul Task 4): the compact shell's YouTube fullscreen
            // button raises a fullscreen ELEMENT that today fills only the WebView bounds the
            // player already fills — honor it as window expansion. The handler gates on the LIVE
            // mode, so the normal watch page gains no new fullscreen invariant.
            core.ContainsFullScreenElementChanged += (_, _) =>
                ApplyFullScreenElementState(core.ContainsFullScreenElement);

            if (_mode == PlaybackMode.Compact)
            {
                // Compact mode hosts the local shell (spec 10.3): map its virtual host before
                // navigating, then let the shell bridge (IFrame API state) drive the return timestamp.
                WebViewEnvironmentService.MapShellVirtualHost(core);
                _shellBridge = new PlayerShellBridge(core);
                _shellBridge.Ready += ShellBridge_Ready;
                _shellBridge.StateReceived += ShellBridge_StateReceived;
                _shellBridge.ErrorReceived += ShellBridge_ErrorReceived;
                _shellBridge.RequestReceived += ShellBridge_RequestReceived;
            }

            core.Navigate(_currentUrl);
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
        // URL-shape proxy (overhaul Task 3): WebView2 exposes no window-open disposition, so a
        // left-click on a recommendation and an explicit "open in new window" look identical. A
        // playable YouTube target stays in THIS player (ADR-0005: never a second window); anything
        // else opens in the system browser.
        e.Handled = true;
        if (!TryRetargetForNewWindow(e.Uri)) OpenExternal(e.Uri);
    }

    /// <summary>Internal for the WPF test lane (CoreWebView2 event args cannot be constructed).</summary>
    internal bool TryRetargetForNewWindow(string uri)
    {
        if (PopoutNavigationPolicy.DecideNewWindow(uri, out var target) != PopoutNewWindowAction.RetargetInPlace)
            return false;
        RetargetTo(target!);
        return true;
    }

    /// <summary>
    /// Move this player to a new target in place, in its CURRENT mode (compact shell rebuild or
    /// normal watch URL). All launch-time state that the navigation invalidates follows: the
    /// fallback target (the error bar must reopen the NEW video), the return video id, the
    /// timestamp (unknown until the new page/shell reports), the one-shot play nudge, and the
    /// compact ready-watchdog (a dead NEW shell must still surface the error bar).
    /// </summary>
    private void RetargetTo(YouTubeTarget target)
    {
        _currentTarget = target;
        _returnState.VideoId = target.VideoId;
        _returnState.LastKnownSeconds = null;
        _nudgedPlay = false;
        // The navigation tears down any fullscreen element with no exit event reaching a handler
        // that still believes the OLD page's element is up — drop the latch, keep the window state
        // (yanking the window around mid-retarget would be worse than staying expanded).
        _maximizedForFullScreenElement = false;
        _currentUrl = PlaybackModePolicy.BuildPopoutUrl(
            _mode, target, target.StartSeconds, WebViewEnvironmentService.ShellPlayerUrl);

        if (_mode == PlaybackMode.Compact)
        {
            HideShellError();
            // Re-arm the watchdog. A final state message from the dying old shell can stop it in
            // the brief window before the navigation commits — accepted residual: the failed-load
            // path in Core_NavigationCompleted still covers a shell that never comes up at all.
            _shellReadyTimer.Stop();
            _shellReadyTimer.Start();
        }

        Log.Info($"Popout retarget ({_mode}): {Log.RedactUrl(_currentUrl)}");
        Player.CoreWebView2?.Navigate(_currentUrl);
    }

    /// <summary>
    /// Track the CURRENT video for return (REQ-RETURN-01): YouTube SPA navigations and
    /// autoplay-advance never raise NavigationStarting, but Source follows them. The compact
    /// shell's piplay.local URL fails the YouTube host check, so this is naturally
    /// normal-page-only — compact tracking comes from shell state messages instead.
    /// </summary>
    private void Core_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        if (YouTubeUrlHelper.TryParse(Player.CoreWebView2?.Source, out var t) && t.VideoId is not null)
            _returnState.VideoId = t.VideoId;
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
        // Protocol v3: the shell reports the CURRENT video (playlist auto-advance and in-iframe
        // clicks are invisible to the host). PlayerShellProtocol.Parse already rejected malformed
        // ids at the wire (the parse IS the trust boundary), so a non-empty value here is a
        // well-formed id; absent/invalid keeps the last-known id.
        if (!string.IsNullOrEmpty(state.VideoId)) _returnState.VideoId = state.VideoId;
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

    /// <summary>
    /// Map an allowlisted shell window-action request (design 2026-06-10 §2) onto the EXISTING
    /// native handlers — the shell can ask for exactly what the chrome strip already does, never
    /// more. Off-allowlist actions were already dropped at the parse layer (they arrive as
    /// Unknown, which the bridge never surfaces), so this switch is the second of two gates.
    /// </summary>
    private void ShellBridge_RequestReceived(object? sender, InboundShellMessage message)
    {
        if (_mode != PlaybackMode.Compact) return;
        switch (message.Action)
        {
            case PlayerShellProtocol.ActionClose:
                // Deferred: Close() disposes the WebView2 in PlayerWindow_Closed, and this handler
                // runs inside CoreWebView2.WebMessageReceived — disposing the control inside its
                // own event callback is a documented WebView2 reentrancy hazard.
                Dispatcher.BeginInvoke(Close);
                break;
            case PlayerShellProtocol.ActionPinToggle:
                PinToggle.IsChecked = PinToggle.IsChecked != true;
                Topmost = PinToggle.IsChecked == true;
                break;
            case PlayerShellProtocol.ActionFullscreenToggle:
                ToggleExpandedState();
                break;
        }
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
        Log.Info($"Compact player error shown (\"{message}\") for {Log.RedactUrl(_currentUrl)}; " +
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
        if (_fellBack || _currentTarget is null || Player.CoreWebView2 is null) return;
        _fellBack = true;

        _shellReadyTimer.Stop();
        try { _shellBridge?.Dispose(); } catch { /* ignore */ }
        _shellBridge = null;

        _mode = PlaybackMode.Normal;
        MinWidth = PlaybackModePolicy.MinWidthFor(PlaybackMode.Normal);
        MinHeight = PlaybackModePolicy.MinHeightFor(PlaybackMode.Normal);
        // The mode gate makes further fullscreen-element events no-ops, so a latch set by the dying
        // shell could never clear — drop it (window state stays; the expand button is the exit).
        _maximizedForFullScreenElement = false;
        HideShellError();

        // A zero timestamp here means the shell never actually played (the usual fallback cause,
        // e.g. embedding disabled), so treat it as unknown and let BuildWatchUrl fall through to
        // the target's launch StartSeconds rather than restarting the video at 0:00.
        var seconds = _returnState.LastKnownSeconds is > 0 ? _returnState.LastKnownSeconds : null;
        _currentUrl = YouTubeUrlHelper.BuildWatchUrl(_currentTarget, seconds);
        Log.Info($"Compact fallback: reopening in normal page mode: {Log.RedactUrl(_currentUrl)}");
        Player.CoreWebView2.Navigate(_currentUrl);
    }

    // Test seams (WPF lane): drive the error/fallback path without a live WebView2 or shell.
    internal void HandleShellStateForTests(InboundShellMessage state) => ShellBridge_StateReceived(this, state);
    internal void HandleShellErrorForTests(InboundShellMessage message) => ShellBridge_ErrorReceived(this, message);
    internal void HandleShellLoadFailureForTests() => ShowShellError(PlayerShellErrorPolicy.ShellLoadFailedMessage);
    internal void RequestFallbackForTests() => FallBackToNormalPage();
    internal void HandleShellRequestForTests(InboundShellMessage message) => ShellBridge_RequestReceived(this, message);
    internal bool IsErrorBarVisibleForTests => ErrorBar.Visibility == Visibility.Visible;
    internal string ErrorTextForTests => ErrorText.Text;

    // Retarget / return-state seams (overhaul Task 3, WPF lane).
    internal string CurrentUrlForTests => _currentUrl;
    internal string? CurrentFallbackVideoIdForTests => _currentTarget?.VideoId;
    internal string? ReturnVideoIdForTests => _returnState.VideoId;
    internal int? ReturnSecondsForTests => _returnState.LastKnownSeconds;
    internal PlacementData? LaunchPlacementForTests => _placement;

    // Strip auto-hide seams (Wpf lane): drive the collapse/reveal state machine headlessly.
    internal bool StripAutoHideForTests => _stripAutoHide;
    internal bool IsChromeStripCollapsedForTests => ChromeStrip.Visibility == Visibility.Collapsed;
    internal bool IsChromeStripHitTestVisibleForTests => ChromeStrip.IsHitTestVisible;
    internal void HideControlsForTests() => HideControls();
    internal void CompleteHideFadeForTests() => OnHideFadeCompleted();

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
        // Expanded covers the monitor: there is nowhere to drag to, and DragMove on a maximized
        // borderless window misbehaves (the frame moves without un-maximizing). Restore is a
        // deliberate act (expand button / Esc), never a drag side effect.
        if (WindowState == WindowState.Maximized) return;
        _isDragging = true;
        try { DragMove(); }
        catch { /* DragMove throws if the button was already released */ }
        finally
        {
            _isDragging = false;
            OnUserActivity(); // keep controls up briefly after a drag, then resume idle countdown
        }
    }

    // --- Expand / restore (overhaul Task 4) ---

    private void ExpandButton_Click(object sender, RoutedEventArgs e) => ToggleExpandedState();

    /// <summary>
    /// The ONE expand path (Q-2): the native strip button and the shell's fullscreenToggle request
    /// land here. Maximize semantics are the decided full-monitor cover (owner decision 2026-06-10,
    /// no work-area hook). A user toggle clears the fullscreen-element latch: from here on the
    /// expansion is theirs, and an element exit must not undo it.
    /// </summary>
    private void ToggleExpandedState()
    {
        _maximizedForFullScreenElement = false;
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateExpandAffordance();
        // Any expand path counts as activity (adopted from the parallel b35c0dd landing): an
        // auto-hidden strip un-collapses, so the restore affordance is immediately reachable in
        // the new state instead of waiting for the top-edge reveal.
        OnUserActivity();
    }

    /// <summary>
    /// Runs on every StateChanged: any exit from Maximized — ours, or an OS path our toggles never
    /// see (Win+Down, aero snap) — invalidates the element-caused latch, because the expansion it
    /// described no longer exists. Without this, a stale latch survives an OS restore and the
    /// state machine lies until the next element event (review finding 2026-06-10).
    /// </summary>
    private void HandleWindowStateChanged()
    {
        if (WindowState != WindowState.Maximized) _maximizedForFullScreenElement = false;
        UpdateExpandAffordance();
    }

    /// <summary>Keep glyph and tooltip truthful in both states; the UIA name stays state-neutral
    /// ("Expand or restore popout", XamlInvariantTests pins it).</summary>
    private void UpdateExpandAffordance()
    {
        var maximized = WindowState == WindowState.Maximized;
        ExpandButton.Content = maximized ? GlyphRestore : GlyphMaximize;
        ExpandButton.ToolTip = maximized ? "Restore popout" : "Expand popout";
    }

    /// <summary>
    /// Secondary expand route (spec settled decision 14): a compact fullscreen ELEMENT (the
    /// shell's YouTube fullscreen button) expands the window; exiting restores it ONLY if the
    /// element caused the expansion. Gated on the LIVE mode — the compact→normal fallback flips
    /// <see cref="_mode"/> in place, and the normal watch page must not gain a fullscreen
    /// invariant (Popout Standard / Fullview Faded stay one path).
    /// </summary>
    private void ApplyFullScreenElementState(bool containsFullScreenElement)
    {
        if (_mode != PlaybackMode.Compact) return;
        if (containsFullScreenElement)
        {
            if (WindowState == WindowState.Maximized) return;   // already the user's posture
            _maximizedForFullScreenElement = true;
            WindowState = WindowState.Maximized;
            UpdateExpandAffordance();
            OnUserActivity();   // reveal the strip in the new state (see ToggleExpandedState)
        }
        else if (_maximizedForFullScreenElement)
        {
            _maximizedForFullScreenElement = false;
            if (WindowState != WindowState.Maximized) return;   // user already restored it
            WindowState = WindowState.Normal;
            UpdateExpandAffordance();
            OnUserActivity();
        }
    }

    /// <summary>Esc restores an expanded popout (Task 4 reversibility). Only reachable while WPF
    /// owns focus — keys pressed inside the WebView2 child stay in the browser.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && TryRestoreFromEscape()) e.Handled = true;
        base.OnPreviewKeyDown(e);
    }

    private bool TryRestoreFromEscape()
    {
        if (WindowState != WindowState.Maximized) return false;
        _maximizedForFullScreenElement = false;
        WindowState = WindowState.Normal;
        UpdateExpandAffordance();
        OnUserActivity();
        return true;
    }

    // Expand/restore seams (WPF lane): KeyEventArgs needs a PresentationSource and fullscreen
    // element events need a live CoreWebView2, neither of which exists for an unshown window —
    // and unshown windows receive no StateChanged, so the OS path is driven directly.
    internal void ApplyFullScreenElementStateForTests(bool contains) => ApplyFullScreenElementState(contains);
    internal bool HandleEscapeForTests() => TryRestoreFromEscape();
    internal void HandleWindowStateChangedForTests() => HandleWindowStateChanged();
    internal bool IsMaximizedForFullScreenElementForTests => _maximizedForFullScreenElement;
    internal string ExpandGlyphForTests => (string)ExpandButton.Content;
    internal string ExpandToolTipForTests => (string)ExpandButton.ToolTip;

    // --- Controls fade (spec 11, Phase 2) ---

    private void FadeToggle_Click(object sender, RoutedEventArgs e)
    {
        _fadeEnabled = FadeToggle.IsChecked == true;
        _returnState.FadeEnabled = _fadeEnabled;
        ApplyFadeState();
    }

    internal void ApplyAppearance(string? accentColor, int fadeIdleDelayMs, bool stripAutoHide = false)
    {
        // One theme accent drives both Popout Pin and Popout Fade (overhaul Task 10); the brush is
        // shared (frozen) across the two toggles.
        var accentBrush = ResolveAccentBrush(accentColor);
        ToggleAccent.SetCheckedBrush(PinToggle, accentBrush);
        ToggleAccent.SetCheckedBrush(FadeToggle, accentBrush);
        _idleTimer.Interval = TimeSpan.FromMilliseconds(
            PlayerAppearancePolicy.NormalizeFadeIdleDelayMs(fadeIdleDelayMs));

        var autoHideTurnedOff = _stripAutoHide && !stripAutoHide;
        _stripAutoHide = stripAutoHide;
        if (autoHideTurnedOff && ChromeStrip.Visibility != Visibility.Visible) ShowControls();
        UpdateActivityProbe();

        if (_fadeEnabled && _idleTimer.IsEnabled) RestartIdleTimer();
    }

    private static Brush ResolveAccentBrush(string? accentColor) => ThemeColors.Brush(accentColor);

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
        UpdateActivityProbe();   // the fade state is a probe input (strip auto-hide gates on it)
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
    /// live preview (mirrors <see cref="ApplyAppearance"/>). The preview passes
    /// <paramref name="animate"/> = false: a slider drag fires per tick, and restarting the fade
    /// animation on every tick would stair-step — the drag itself is the animation.</summary>
    internal void ApplyWindowOpacity(double constantOpacity, double idleOpacity, bool animate = true)
    {
        _constantWindowOpacity = WindowOpacityPolicy.Normalize(constantOpacity);
        _idleWindowOpacity = WindowOpacityPolicy.Normalize(idleOpacity);
        ApplyWindowOpacityToHwnd(animate);
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
        UpdateActivityProbe();
    }

    /// <summary>
    /// The activity probe covers the WebView2 area WPF can't see. It runs for as long as
    /// something needs it: an idle opacity dip (prevent onset during movement, restore after) or
    /// the auto-hiding strip (top-edge reveal). Off at defaults, so default-look behavior never
    /// pays for it.
    /// </summary>
    private void UpdateActivityProbe()
    {
        var active = WindowOpacityPolicy.Effective(isIdle: false, _constantWindowOpacity, _idleWindowOpacity);
        var idle = WindowOpacityPolicy.Effective(isIdle: true, _constantWindowOpacity, _idleWindowOpacity);
        var wanted = (idle < active || (_stripAutoHide && _fadeEnabled)) &&
                     new System.Windows.Interop.WindowInteropHelper(this).Handle != IntPtr.Zero;
        if (wanted && !_opacityHoverPoll.IsEnabled) _opacityHoverPoll.Start();
        else if (!wanted) _opacityHoverPoll.Stop();
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
        // Inside our rectangle is not enough: an unpinned popout can sit BEHIND another app, and
        // movement over that app must not count as activity (the idle fade would never engage).
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !WindowOpacityApplier.IsPointOverWindow(hwnd, x, y)) return;
        _lastInWindowCursorMoveMs = Environment.TickCount64;

        // Top-edge band reveals a collapsed strip (spec 7.2: hover restores full chrome).
        if (_stripAutoHide && p.Y <= FadePolicy.TopEdgeRevealBandDip && ChromeStrip.Visibility != Visibility.Visible)
        {
            OnUserActivity();
            return;
        }

        // Movement elsewhere over the video restores window opacity (spec 7.3) and re-arms
        // idleness WITHOUT popping the strip up — the strip reveals only via its own hover paths.
        if (_windowOpacityIdle)
        {
            ExitWindowOpacityIdle();
            RestartIdleTimer();
        }
    }

    private void ShowControls()
    {
        // Un-collapse first (strip auto-hide): the reveal must restore the layout row before the
        // opacity fade-in has anything to fade in.
        if (ChromeStrip.Visibility != Visibility.Visible) ChromeStrip.Visibility = Visibility.Visible;
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
        AnimateStripOpacity(0.0, onCompleted: OnHideFadeCompleted);
    }

    /// <summary>Runs when the hide fade finishes (and from the test seam: animation clocks only
    /// tick once a window has rendered, so the Wpf lane drives this directly).</summary>
    private void OnHideFadeCompleted()
    {
        if (_controlsVisible) return;   // re-shown mid-fade: leave it interactive
        ChromeStrip.IsHitTestVisible = false;
        // Strip auto-hide (Phase 4): once fully faded, also height-collapse so the video fills
        // the window; the top-edge hover band or any reveal path restores the row.
        if (_stripAutoHide) ChromeStrip.Visibility = Visibility.Collapsed;
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
        // ForNextLaunch (overhaul Task 4): closing expanded must not make the NEXT popout launch
        // expanded — the capture keeps the prior normal bounds and drops only the maximized flag.
        _returnState.Placement = PlacementMath.ForNextLaunch(WindowPlacementService.TryCapture(this));
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

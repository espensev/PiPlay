using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using PiPlay.Models;
using PiPlay.Services;

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

    // Controls fade (spec 11, Phase 2): idle/hover state machine over the chrome strip only.
    private readonly DispatcherTimer _idleTimer;
    private bool _fadeEnabled;
    private bool _isDragging;
    private bool _controlsVisible = true;

    private bool _navCompleted;
    private bool _capturedReturn;
    private bool _nudgedPlay;

    /// <summary>Raised once when the player has closed, carrying the state needed to return (spec 14).</summary>
    public event EventHandler<PlayerReturnState>? PlayerClosed;

    public PlayerWindow(
        CoreWebView2Environment environment,
        string url,
        bool topmost,
        PlacementData? placement,
        int defaultWidth,
        int defaultHeight,
        bool fadeEnabled)
    {
        InitializeComponent();

        _environment = environment;
        _url = url;
        _placement = placement;

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

        // Idle timer drives the fade-out; any mouse move restarts it (spec 22.1 fade row).
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FadePolicy.IdleDelayMs) };
        _idleTimer.Tick += IdleTimer_Tick;
        MouseMove += (_, _) => OnUserActivity();
        MouseEnter += (_, _) => OnUserActivity();

        Loaded += (_, _) => ApplyFadeState();
        Loaded += async (_, _) => await InitializePlayerAsync();
        SourceInitialized += (_, _) =>
        {
            if (_placement is not null) WindowPlacementService.Restore(this, _placement);
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

            core.Navigate(_url);
            Log.Info("Popout Player initialized.");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to initialize the Popout Player.", ex);
            MessageBox.Show("PiPlay couldn't start the popout player.\n\n" + ex.Message,
                "PiPlay", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        if (!_syncTimer.IsEnabled) _syncTimer.Start();
    }

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
        }
    }

    private void OnUserActivity()
    {
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
        if (FadePolicy.ShouldHide(_fadeEnabled, ChromeStrip.IsMouseOver, _isDragging, idleElapsed: true))
        {
            HideControls();
        }
        else if (_fadeEnabled)
        {
            // Something is still holding controls up (pointer over the strip / mid-drag); re-arm.
            RestartIdleTimer();
        }
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
        _returnState.Topmost = Topmost;
        _returnState.FadeEnabled = _fadeEnabled;
        _returnState.Placement = WindowPlacementService.TryCapture(this);
    }

    private void PlayerWindow_Closed(object? sender, EventArgs e)
    {
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

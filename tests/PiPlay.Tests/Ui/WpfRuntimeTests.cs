using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Microsoft.Web.WebView2.Wpf;
using PiPlay;
using PiPlay.Models;
using PiPlay.Services;
using PiPlay.Theme;

namespace PiPlay.Tests;

/// <summary>
/// Layer 3 — live WPF on a shared STA thread. Constructs the real windows (never shown, so
/// WebView2/network are untouched) to prove every {StaticResource} resolves at runtime and the
/// burned-in DependencyProperty values hold, plus a RenderTargetBitmap check that the URL text
/// is not clipped to a band at 150% DPI (the affirmative guard for the "rounding = 0" bug).
/// </summary>
[Trait(TestCategories.Key, TestCategories.Wpf)]
public class WpfRuntimeTests : IDisposable
{
    private static PlayerWindow NewPlayer() =>
        // environment is only used in InitializePlayerAsync (Loaded), never in the ctor.
        new(environment: null!, url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            topmost: false, placement: null, defaultWidth: 960, defaultHeight: 540, fadeEnabled: true);

    // --- Construction proves runtime resource resolution + template compilation ---

    [Fact]
    public void MainWindow_constructs_without_throwing() => StaTestThread.Invoke(() =>
    {
        var ex = Record.Exception(() => new MainWindow());
        Assert.Null(ex);
    });

    [Fact]
    public void PlayerWindow_constructs_without_throwing() => StaTestThread.Invoke(() =>
    {
        var ex = Record.Exception(() => NewPlayer());
        Assert.Null(ex);
    });

    [Fact]
    public void SettingsWindow_constructs_without_throwing() => StaTestThread.Invoke(() =>
    {
        var ex = Record.Exception(() => new SettingsWindow(isBrowserReady: true));
        Assert.Null(ex);
    });

    [Fact]
    public void SettingsWindow_shows_the_tested_privacy_wording() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true);
        Assert.Equal(PiPlay.Services.PrivacyService.ResetDescription,
            ((TextBlock)w.FindName("ResetDescriptionText")!).Text);
        Assert.Equal(PiPlay.Services.PrivacyService.ClearDescription,
            ((TextBlock)w.FindName("ClearDescriptionText")!).Text);
        Assert.Equal(PiPlay.Services.PrivacyService.ResetActionLabel,
            (string)((Button)w.FindName("ResetAppStateButton")!).Content);
        Assert.Equal(PiPlay.Services.PrivacyService.ClearActionLabel,
            (string)((Button)w.FindName("ClearBrowserDataButton")!).Content);
    });

    [Fact]
    public void SettingsWindow_disables_only_clear_when_browser_not_ready() => StaTestThread.Invoke(() =>
    {
        var notReady = new SettingsWindow(isBrowserReady: false);
        Assert.True(((Button)notReady.FindName("ResetAppStateButton")!).IsEnabled);
        Assert.False(((Button)notReady.FindName("ClearBrowserDataButton")!).IsEnabled);

        var ready = new SettingsWindow(isBrowserReady: true);
        Assert.True(((Button)ready.FindName("ResetAppStateButton")!).IsEnabled);
        Assert.True(((Button)ready.FindName("ClearBrowserDataButton")!).IsEnabled);
    });

    [Fact]
    public void SettingsWindow_explains_why_clear_is_disabled() => StaTestThread.Invoke(() =>
    {
        var notReady = new SettingsWindow(isBrowserReady: false);
        var clear = (Button)notReady.FindName("ClearBrowserDataButton")!;
        Assert.Equal(PiPlay.Services.PrivacyService.ClearNotReadyHint, (string)clear.ToolTip);
        Assert.True(ToolTipService.GetShowOnDisabled(clear));   // tip shows on the disabled button

        // When the browser is ready the button is enabled and carries no explanatory tooltip.
        var ready = new SettingsWindow(isBrowserReady: true);
        Assert.Null(((Button)ready.FindName("ClearBrowserDataButton")!).ToolTip);
    });

    [Fact]
    public void SettingsWindow_reflects_and_updates_player_appearance_input() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true, pinAccent: "green", fadeAccent: "amber", fadeIdleDelayMs: 4000);

        Assert.Equal("green", w.PinAccent);
        Assert.Equal("amber", w.FadeAccent);
        Assert.Equal(4000, w.FadeIdleDelayMs);
        Assert.True(((ToggleButton)w.FindName("PinAccentGreenSwatch")!).IsChecked);
        Assert.True(((ToggleButton)w.FindName("FadeAccentAmberSwatch")!).IsChecked);
        Assert.True(((ToggleButton)w.FindName("FadeDelayLongPreset")!).IsChecked);

        ((ToggleButton)w.FindName("PinAccentVioletSwatch")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        ((ToggleButton)w.FindName("FadeAccentCyanSwatch")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        ((ToggleButton)w.FindName("FadeDelayShortPreset")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        Assert.True(w.AppearanceChanged);
        Assert.Equal("violet", w.PinAccent);
        Assert.Equal("cyan", w.FadeAccent);
        Assert.Equal(1500, w.FadeIdleDelayMs);
        Assert.True(((ToggleButton)w.FindName("PinAccentVioletSwatch")!).IsChecked);
        Assert.True(((ToggleButton)w.FindName("FadeAccentCyanSwatch")!).IsChecked);
        Assert.True(((ToggleButton)w.FindName("FadeDelayShortPreset")!).IsChecked);
    });

    // --- Resolved DependencyProperty invariants (runtime counterpart to Layer 1) ---

    [Fact]
    public void MainWindow_holds_layout_and_airspace_invariants() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        Assert.False(w.UseLayoutRounding);
        Assert.False(w.AllowsTransparency);
        Assert.Equal(WindowStyle.None, w.WindowStyle);
        var chrome = WindowChrome.GetWindowChrome(w);
        Assert.NotNull(chrome);
        Assert.Equal(new CornerRadius(0), chrome!.CornerRadius);
    });

    [Fact]
    public void PlayerWindow_holds_layout_and_airspace_invariants() => StaTestThread.Invoke(() =>
    {
        var w = NewPlayer();
        Assert.False(w.UseLayoutRounding);
        Assert.False(w.AllowsTransparency);
        Assert.Equal(WindowStyle.None, w.WindowStyle);
        Assert.Equal(new CornerRadius(0), WindowChrome.GetWindowChrome(w)!.CornerRadius);
    });

    [Fact]
    public void Expanded_resize_hook_returns_diagonal_corner_for_real_nchittest_message() => StaTestThread.Invoke(() =>
    {
        var w = new Window
        {
            Width = 240,
            Height = 160,
            Left = 100,
            Top = 100,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.CanResize,
            AllowsTransparency = false,
            Opacity = 0,
            ShowActivated = false,
            ShowInTaskbar = false,
        };

        WindowChrome.SetWindowChrome(w, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(BorderlessResizeHitTestPolicy.ResizeBorderDip),
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false,
        });
        BorderlessWindowHelper.EnableExpandedResizeZones(w);

        var hwnd = new WindowInteropHelper(w).EnsureHandle();
        w.Show();
        w.UpdateLayout();
        Assert.True(BorderlessWindowHelper.HasExpandedResizeSubclassForTests(hwnd));
        var p = w.PointToScreen(new Point(20, 5)); // top band, inside the 32 DIP left corner length
        var roundTrip = w.PointFromScreen(p);
        Assert.InRange(roundTrip.X, 19.5, 20.5);
        Assert.InRange(roundTrip.Y, 4.5, 5.5);
        var result = SendMessage(hwnd, WM_NCHITTEST, IntPtr.Zero, MakeLParam((int)p.X, (int)p.Y));

        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOPLEFT, result.ToInt32());
    });

    [Fact]
    public void MainWindow_exposes_settings_button() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        Assert.IsType<Button>(w.FindName("SettingsButton"));
    });

    [Fact]
    public void MainWindow_source_pin_uses_configurable_accent_behavior() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        var pin = (ToggleButton)w.FindName("PinToggle")!;
        Assert.Same(Application.Current.FindResource("AccentCyan"), ToggleAccent.GetCheckedBrush(pin));
    });

    [Fact]
    public void PlayerWindow_applies_configurable_pin_fade_accents_and_delay() => StaTestThread.Invoke(() =>
    {
        var w = new PlayerWindow(
            environment: null!,
            url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            topmost: false,
            placement: null,
            defaultWidth: 960,
            defaultHeight: 540,
            fadeEnabled: true,
            pinAccent: "green",
            fadeAccent: "amber",
            fadeIdleDelayMs: 4000);

        Assert.Same(Application.Current.FindResource("AccentGreen"),
            ToggleAccent.GetCheckedBrush((ToggleButton)w.FindName("PinToggle")!));
        Assert.Same(Application.Current.FindResource("AccentAmber"),
            ToggleAccent.GetCheckedBrush((ToggleButton)w.FindName("FadeToggle")!));

        w.ApplyAppearance("violet", "cyan", 1500);
        Assert.Same(Application.Current.FindResource("AccentViolet"),
            ToggleAccent.GetCheckedBrush((ToggleButton)w.FindName("PinToggle")!));
        Assert.Same(Application.Current.FindResource("AccentCyan"),
            ToggleAccent.GetCheckedBrush((ToggleButton)w.FindName("FadeToggle")!));
        Assert.Equal(TimeSpan.FromMilliseconds(1500), w.FadeIdleDelayForTests);
    });

    [Fact]
    public void Reset_clears_dirty_ui_without_a_live_browser() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        var pin = (ToggleButton)w.FindName("PinToggle")!;
        var profiles = (ComboBox)w.FindName("ProfilesCombo")!;

        // Drive a dirty pre-state so the assertions prove the reset TRANSITION, not the
        // already-true fresh-construction defaults. (Setting IsChecked programmatically does not
        // fire the Click handler; setting ItemsSource leaves SelectedIndex at -1, so no navigation.)
        pin.IsChecked = true;
        profiles.ItemsSource = new[]
        {
            new Profile { Name = "Lo-fi", Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" },
        };
        Assert.NotEmpty(profiles.Items);
        Assert.True(pin.IsChecked);

        // CoreWebView2 is null because the window is never shown (Loaded never runs).
        var ex = Record.Exception(() => w.ApplyResetState());

        Assert.Null(ex);
        Assert.Empty(profiles.Items);                               // profiles cleared by reset
        Assert.False(pin.IsChecked);                                // pin turned off by reset
        Assert.Null(w.PendingUrlForTests);                          // reset queued no navigation
        Assert.Null(((WebView2)w.FindName("Browser")!).Source);     // browser source untouched
    });

    [Fact]
    public void Clear_is_not_ready_on_a_window_without_a_browser() => StaTestThread.Invoke(() =>
    {
        // The window is never shown, so the WebView2 core is never created: Clear must gate off.
        // Guards the execution-time readiness re-check that prevents clearing with a stale state.
        var w = new MainWindow();
        Assert.False(w.CanClearBrowserData);
    });

    [Fact]
    public void SettingsWindow_has_no_requested_action_until_confirmed() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true);
        Assert.Equal(PrivacyAction.None, w.RequestedAction);
    });

    [Fact]
    public void DangerButton_style_resolves_at_runtime() => StaTestThread.Invoke(() =>
    {
        // The destructive confirm resolves DangerButton from code (Prompt.AskConfirm), not XAML,
        // so the markup StaticResource sweep misses it — prove it resolves to a Style at runtime.
        Assert.IsType<Style>(Application.Current.TryFindResource("DangerButton"));
    });

    [Fact]
    public void Prompt_dialogs_are_borderless_dark() => StaTestThread.Invoke(() =>
    {
        // The themed dialogs are built in code (no XAML), so the markup suite can't guard them.
        // Lock the borderless-dark invariants so they can't silently regress to a light chrome.
        var win = Prompt.BuildShell(owner: null, title: "Test title", out var body);
        Assert.Equal(WindowStyle.None, win.WindowStyle);
        Assert.False(win.AllowsTransparency);
        Assert.False(win.UseLayoutRounding);   // the "rounding = 0" guard also covers code-built dialogs
        Assert.Equal(ResizeMode.NoResize, win.ResizeMode);
        Assert.False(win.ShowInTaskbar);
        Assert.Same(Application.Current.Resources["AppBackground"], win.Background);
        Assert.NotNull(body);

        // 1px border > dock panel > [draggable title bar with a CloseIconButton, body].
        var border = Assert.IsType<Border>(win.Content);
        var dock = Assert.IsType<DockPanel>(border.Child);
        var bar = Assert.IsType<Grid>(dock.Children[0]);
        Assert.Contains(bar.Children.OfType<Button>(),
            b => ReferenceEquals(b.Style, Application.Current.Resources["CloseIconButton"]));
    });

    [Fact]
    public void Named_controls_resolve_to_expected_types() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        Assert.IsType<TextBox>(w.FindName("UrlBox"));
        Assert.IsType<Button>(w.FindName("PopOutButton"));
        Assert.IsType<ComboBox>(w.FindName("ProfilesCombo"));
        Assert.IsType<Border>(w.FindName("SourcePlaceholder"));
        Assert.IsType<Border>(w.FindName("RuntimeErrorPanel"));
    });

    [Fact]
    public void Auto_toggle_reflects_loaded_setting_and_is_off_by_default() => StaTestThread.Invoke(() =>
    {
        // No settings file in the temp data root => AutoPopout defaults off => the toggle is unchecked.
        var auto = (ToggleButton)new MainWindow().FindName("AutoToggle")!;
        Assert.NotEqual(true, auto.IsChecked);
    });

    [Fact]
    public void DarkTextBox_template_applies_and_resolves_part_content_host() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        var url = (TextBox)w.FindName("UrlBox")!;
        url.Measure(new Size(400, 32));
        url.Arrange(new Rect(0, 0, 400, 32));
        url.ApplyTemplate();
        Assert.NotNull(url.Template);
        Assert.NotNull(url.Template.FindName("PART_ContentHost", url));
    });

    // --- Compact player mode (Phase 3, Stage 1): mode-specific minimums + settings/profile UI ---

    [Fact]
    public void PlayerWindow_uses_normal_minimum_size_by_default() => StaTestThread.Invoke(() =>
    {
        var w = NewPlayer();   // no mode argument -> PlaybackMode.Normal
        Assert.Equal(PlaybackModePolicy.NormalMinWidth, w.MinWidth);
        Assert.Equal(PlaybackModePolicy.NormalMinHeight, w.MinHeight);
    });

    [Fact]
    public void PlayerWindow_uses_compact_minimum_and_clamps_launch_size_up() => StaTestThread.Invoke(() =>
    {
        // Launch below the compact floor: MinWidth/MinHeight must be the compact minimum and the
        // resolved launch size must clamp up to it (spec 10.2: no 320x180 for embed mode).
        var w = new PlayerWindow(
            environment: null!,
            url: "https://www.youtube.com/embed/dQw4w9WgXcQ?autoplay=1",
            topmost: false,
            placement: null,
            defaultWidth: 320,
            defaultHeight: 180,
            fadeEnabled: true,
            mode: PlaybackMode.Compact);

        Assert.Equal(PlaybackModePolicy.CompactMinWidth, w.MinWidth);
        Assert.Equal(PlaybackModePolicy.CompactMinHeight, w.MinHeight);
        Assert.True(w.Width >= PlaybackModePolicy.CompactMinWidth, $"Width {w.Width} below compact floor.");
        Assert.True(w.Height >= PlaybackModePolicy.CompactMinHeight, $"Height {w.Height} below compact floor.");
    });

    [Fact]
    public void SettingsWindow_reflects_and_toggles_compact_mode() => StaTestThread.Invoke(() =>
    {
        var on = new SettingsWindow(isBrowserReady: true, compactMode: true);
        Assert.True(on.CompactMode);
        Assert.True(((ToggleButton)on.FindName("CompactModeToggle")!).IsChecked);

        var w = new SettingsWindow(isBrowserReady: true, compactMode: false);
        Assert.False(w.CompactMode);
        var toggle = (ToggleButton)w.FindName("CompactModeToggle")!;
        Assert.False(toggle.IsChecked);   // strictly off, not merely "not true" (rejects null too)

        // Simulate a user toggle: the checked state flips, then the Click handler reads it.
        toggle.IsChecked = true;
        toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.True(w.CompactMode);
        Assert.True(w.AppearanceChanged);   // any persisted player preference change is flagged
    });

    [Fact]
    public void Profile_mode_picker_round_trips_the_durable_token() => StaTestThread.Invoke(() =>
    {
        Assert.Equal("compact", Prompt.BuildModePicker("compact").SelectedMode());
        Assert.Equal("normal", Prompt.BuildModePicker("normal").SelectedMode());
        Assert.Null(Prompt.BuildModePicker(null).SelectedMode());
        Assert.Equal("compact", Prompt.BuildModePicker("embed").SelectedMode());   // legacy alias
        Assert.Null(Prompt.BuildModePicker("bogus").SelectedMode());               // unknown -> global

        // Changing the selection is reflected by the getter (covers the editor round-trip).
        var (element, selectedMode) = Prompt.BuildModePicker(null);
        var combo = (ComboBox)element;
        combo.SelectedIndex = 2;   // "Compact player"
        Assert.Equal("compact", selectedMode());
    });

    // --- Dark theme is actually wired at runtime (rebuts the stale "renders light" reports) ---

    [Fact]
    public void Dark_theme_styles_are_applied_at_runtime() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();

        // The chrome controls use the dark styles (not platform-default light ones).
        Assert.Same(Application.Current.TryFindResource("DarkComboBox"),
            ((ComboBox)w.FindName("ProfilesCombo")!).Style);
        Assert.Same(Application.Current.TryFindResource("DarkTextBox"),
            ((TextBox)w.FindName("UrlBox")!).Style);

        // The app-wide implicit ToolTip style exists and is dark (Background = SurfaceRaised).
        var toolTipStyle = Application.Current.TryFindResource(typeof(ToolTip)) as Style;
        Assert.NotNull(toolTipStyle);
        var bg = toolTipStyle!.Setters.OfType<Setter>()
            .First(s => s.Property == Control.BackgroundProperty).Value as SolidColorBrush;
        var surfaceRaised = (SolidColorBrush)Application.Current.FindResource("SurfaceRaised");
        Assert.Equal(surfaceRaised.Color, bg!.Color);
    });

    // --- DPI characterization: URL text is not clipped to a band at 150% DPI ---

    [Fact]
    public void UrlText_is_not_clipped_to_a_band_at_150pct_dpi() => StaTestThread.Invoke(() =>
    {
        const double dpi = 144; // 150%
        var host = new Border
        {
            Width = 320,
            Height = 32,
            UseLayoutRounding = false, // production setting
            Background = Brushes.Black,
            Child = new TextBox
            {
                Text = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                Style = (Style)Application.Current.FindResource("DarkTextBox"),
            },
        };

        host.Measure(new Size(320, 32));
        host.Arrange(new Rect(0, 0, 320, 32));
        host.UpdateLayout();

        var rtb = new RenderTargetBitmap(
            (int)Math.Ceiling(320 * dpi / 96), (int)Math.Ceiling(32 * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(host);

        var inkedRows = CountInkedRows(rtb);
        Assert.True(inkedRows >= 8, $"Only {inkedRows} inked rows — text appears clipped to a band.");
    });

    // Count horizontal scanlines containing a bright (text-ink) pixel. Text is #F3F5F7 on dark.
    private static int CountInkedRows(RenderTargetBitmap rtb)
    {
        var w = rtb.PixelWidth;
        var h = rtb.PixelHeight;
        var stride = w * 4;
        var px = new byte[h * stride];
        rtb.CopyPixels(px, stride, 0);

        var rows = 0;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = y * stride + x * 4; // Pbgra32: B,G,R,A
                if (px[i] > 80 || px[i + 1] > 80 || px[i + 2] > 80) { rows++; break; }
            }
        }
        return rows;
    }

    private const int WM_NCHITTEST = 0x0084;

    private static IntPtr MakeLParam(int x, int y) =>
        new(unchecked((x & 0xffff) | ((y & 0xffff) << 16)));

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // Layer 3 constructs real windows (never shown). Close any that remain on the shared STA
    // thread after each test so they don't accumulate on Application.Windows for the whole run.
    public void Dispose() => StaTestThread.Invoke(() =>
    {
        foreach (var w in Application.Current.Windows.Cast<Window>().ToArray())
        {
            try { w.Close(); } catch { /* a never-shown window may resist closing; ignore */ }
        }
    });
}

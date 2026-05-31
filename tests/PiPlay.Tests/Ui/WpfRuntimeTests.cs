using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using PiPlay;

namespace PiPlay.Tests;

/// <summary>
/// Layer 3 — live WPF on a shared STA thread. Constructs the real windows (never shown, so
/// WebView2/network are untouched) to prove every {StaticResource} resolves at runtime and the
/// burned-in DependencyProperty values hold, plus a RenderTargetBitmap check that the URL text
/// is not clipped to a band at 150% DPI (the affirmative guard for the "rounding = 0" bug).
/// </summary>
[Trait(TestCategories.Key, TestCategories.Wpf)]
public class WpfRuntimeTests
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
}

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
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

    // --- Theme resource application (overhaul Task 9): replace the accent entries; DynamicResource
    // consumers re-resolve, including controls already realized in open windows. ---

    [Fact]
    public void ThemeResourceApplier_replaces_the_accent_entries_from_the_theme() => StaTestThread.Invoke(() =>
    {
        var res = new ResourceDictionary();   // empty (startup, before windows): the entries are added
        ThemeResourceApplier.Apply(res, new ThemeSettings { AccentColor = "#38D996" }, new PlayerSettings());

        var primary = (SolidColorBrush)res["AccentPrimary"];
        var light = (SolidColorBrush)res["AccentPrimaryLight"];
        Assert.Equal(Color.FromRgb(0x38, 0xD9, 0x96), primary.Color);   // base takes the accent
        Assert.True(primary.IsFrozen);                                  // shareable across windows
        Assert.True(light.Color.G >= primary.Color.G && light.Color != primary.Color);   // lighter derivation
    });

    [Fact]
    public void ThemeResourceApplier_applies_palette_and_radii_from_the_preset() => StaTestThread.Invoke(() =>
    {
        var res = new ResourceDictionary();
        ThemeResourceApplier.Apply(res, new ThemeSettings { ThemeId = "soft-glass" }, new PlayerSettings());

        // Every palette brush key lands as a frozen brush matching the preset palette, with its
        // companion Color token in step.
        var palette = ThemeCatalog.PresetFor("soft-glass").Palette;
        string[] hexes =
        [
            palette.AppBackground, palette.SurfaceBase, palette.SurfaceRaised, palette.SurfaceHover,
            palette.BorderSubtle, palette.BorderStrong, palette.TextPrimary, palette.TextSecondary,
            palette.Danger,
        ];
        for (var i = 0; i < ThemeResourceApplier.PaletteBrushKeys.Length; i++)
        {
            var key = ThemeResourceApplier.PaletteBrushKeys[i];
            var brush = Assert.IsType<SolidColorBrush>(res[key]);
            Assert.True(brush.IsFrozen, $"{key} brush not frozen.");
            Assert.Equal(ThemeColors.ParseColor(hexes[i]), brush.Color);
            Assert.Equal(brush.Color, (Color)res[key + "Color"]);
        }

        // Radius tokens follow the preset's ThemeRadii; the title bar rounds only its top corners;
        // the compatibility aliases ride Input/Button.
        Assert.Equal(new CornerRadius(16), res["RadiusPopoutFrame"]);
        Assert.Equal(new CornerRadius(10, 10, 0, 0), res["RadiusTitleBar"]);
        Assert.Equal(new CornerRadius(10), res["RadiusButton"]);
        Assert.Equal(res["RadiusInput"], res["ControlCornerRadius"]);
        Assert.Equal(res["RadiusButton"], res["ButtonCornerRadius"]);

        // The corner-style override swaps radii but never the palette.
        var overridden = new ResourceDictionary();
        ThemeResourceApplier.Apply(overridden,
            new ThemeSettings { ThemeId = "soft-glass", CornerStyle = "square" }, new PlayerSettings());
        Assert.Equal(new CornerRadius(0), overridden["RadiusButton"]);
        Assert.Equal(ThemeColors.ParseColor(palette.AppBackground),
            ((SolidColorBrush)overridden["AppBackground"]).Color);

        // An unknown theme id falls back to sharp-dark values end to end.
        var fallback = new ResourceDictionary();
        ThemeResourceApplier.Apply(fallback, new ThemeSettings { ThemeId = "no-such-theme" }, new PlayerSettings());
        Assert.Equal(ThemeColors.ParseColor(ThemeCatalog.PresetFor("sharp-dark").Palette.AppBackground),
            ((SolidColorBrush)fallback["AppBackground"]).Color);
        Assert.Equal(new CornerRadius(ThemeCatalog.PresetFor("sharp-dark").Radii.Button), fallback["RadiusButton"]);
    });

    [Fact]
    public void Theme_restyle_reaches_dynamic_surface_and_radius_consumers() => StaTestThread.Invoke(() =>
    {
        // The PR #18 replace-not-mutate mechanism, applied verbatim to the new tokens: a DarkButton
        // resolves {DynamicResource SurfaceRaised} (fill) and {DynamicResource RadiusButton}
        // (template corner), so REPLACING those app entries restyles consumers at realize time.
        var originalSurface = Application.Current.Resources["SurfaceRaised"];
        var originalRadius = Application.Current.Resources["RadiusButton"];
        try
        {
            var sentinel = Color.FromRgb(0x12, 0x34, 0x56);
            var brush = new SolidColorBrush(sentinel);
            brush.Freeze();
            Application.Current.Resources["SurfaceRaised"] = brush;
            Application.Current.Resources["RadiusButton"] = new CornerRadius(13);

            var btn = new Button { Style = (Style)Application.Current.FindResource("DarkButton") };
            btn.Measure(new Size(200, 40));   // realize: style + template resolve the replaced entries
            Assert.Equal(sentinel, ((SolidColorBrush)btn.Background).Color);
            var bd = (Border)btn.Template.FindName("bd", btn);
            Assert.Equal(new CornerRadius(13), bd.CornerRadius);
        }
        finally
        {
            Application.Current.Resources["SurfaceRaised"] = originalSurface;   // never pollute the shared app
            Application.Current.Resources["RadiusButton"] = originalRadius;
        }
    });

    [Fact]
    public void Accent_recolor_reaches_a_dynamic_resource_consumer() => StaTestThread.Invoke(() =>
    {
        // The AccentButton fill resolves {DynamicResource AccentPrimary}. REPLACING the App resource
        // (what the applier does) changes what the consumer resolves — the recolor mechanism the
        // compiled-BAML frozen seed brushes cannot satisfy by mutation. (For an element IN a window
        // the update is live; WPF only withholds change notifications from this untethered button, so
        // we assert resolution at realize-time rather than a post-realize live swap.)
        var original = Application.Current.Resources["AccentPrimary"];
        try
        {
            var sentinel = Color.FromRgb(0x12, 0x34, 0x56);
            var brush = new SolidColorBrush(sentinel);
            brush.Freeze();
            Application.Current.Resources["AccentPrimary"] = brush;

            var btn = new Button { Style = (Style)Application.Current.FindResource("AccentButton") };
            btn.Measure(new Size(200, 40));   // realize: Background resolves the replaced resource
            Assert.Equal(sentinel, ((SolidColorBrush)btn.Background).Color);
        }
        finally
        {
            Application.Current.Resources["AccentPrimary"] = original;   // never pollute the shared app
        }
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
    public void SettingsWindow_reflects_and_updates_theme_and_accent_input() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true, themeId: "soft-glass", accentColor: "#38D996",
            fadeIdleDelayMs: 4000);

        Assert.Equal("soft-glass", w.ThemeId);
        Assert.Equal("#38D996", w.AccentColor);
        Assert.Equal(4000, w.FadeIdleDelayMs);
        Assert.True(((ToggleButton)w.FindName("ThemeSoftGlassPreset")!).IsChecked);
        Assert.True(((ToggleButton)w.FindName("AccentChipGreen")!).IsChecked);
        Assert.True(((ToggleButton)w.FindName("FadeDelayLongPreset")!).IsChecked);

        // Picking a single accent chip retargets the one accent and checks only that chip.
        ((ToggleButton)w.FindName("AccentChipViolet")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        ((ToggleButton)w.FindName("FadeDelayShortPreset")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        Assert.True(w.AppearanceChanged);
        Assert.Equal("#A78BFA", w.AccentColor);
        Assert.Equal(1500, w.FadeIdleDelayMs);
        Assert.True(((ToggleButton)w.FindName("AccentChipViolet")!).IsChecked);
        Assert.False(((ToggleButton)w.FindName("AccentChipGreen")!).IsChecked);
        Assert.True(((ToggleButton)w.FindName("FadeDelayShortPreset")!).IsChecked);

        // Picking a preset adopts that preset's default accent (Sharp Dark -> cyan) and re-checks it.
        ((ToggleButton)w.FindName("ThemeSharpDarkPreset")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal("sharp-dark", w.ThemeId);
        Assert.Equal(ThemeCatalog.DefaultAccentColor, w.AccentColor);
        Assert.True(((ToggleButton)w.FindName("AccentChipCyan")!).IsChecked);
        Assert.False(((ToggleButton)w.FindName("AccentChipViolet")!).IsChecked);
    });

    [Fact]
    public void SettingsWindow_reflects_and_updates_corner_style() => StaTestThread.Invoke(() =>
    {
        // The ctor seeds the persisted override and checks its chip; an unknown value lands on
        // "theme" (the preset-owned default).
        var w = new SettingsWindow(isBrowserReady: true, cornerStyle: "round");
        Assert.Equal("round", w.CornerStyle);
        Assert.True(((ToggleButton)w.FindName("CornerStyleRoundChip")!).IsChecked);
        Assert.Equal("theme", new SettingsWindow(isBrowserReady: true, cornerStyle: "bogus").CornerStyle);

        // Clicking a chip retargets the override, flags the change, and re-checks only that chip.
        ((ToggleButton)w.FindName("CornerStyleSquareChip")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.True(w.AppearanceChanged);
        Assert.Equal("square", w.CornerStyle);
        Assert.True(((ToggleButton)w.FindName("CornerStyleSquareChip")!).IsChecked);
        Assert.False(((ToggleButton)w.FindName("CornerStyleRoundChip")!).IsChecked);
    });

    [Fact]
    public void SettingsWindow_preset_click_adopts_the_preset_defaults() => StaTestThread.Invoke(() =>
    {
        // Review doc §2.1: an EXPLICIT preset selection applies the preset's behavior defaults —
        // accent, fade delay, strip auto-hide, opacity levels — and returns corners to the theme.
        // Controls touched afterwards become overrides (they fire their own handlers).
        var w = new SettingsWindow(isBrowserReady: true, themeId: "sharp-dark", accentColor: "#FFC857",
            fadeIdleDelayMs: 1500, constantWindowOpacity: 1.0, idleWindowOpacity: 1.0,
            stripAutoHide: true, cornerStyle: "square");

        ((ToggleButton)w.FindName("ThemeSoftGlassPreset")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        var preset = ThemeCatalog.PresetFor("soft-glass");
        Assert.Equal("soft-glass", w.ThemeId);
        Assert.Equal(preset.DefaultAccentColor, w.AccentColor);
        Assert.Equal(ThemeCatalog.DefaultCornerStyle, w.CornerStyle);
        Assert.True(((ToggleButton)w.FindName("CornerStyleThemeChip")!).IsChecked);
        Assert.Equal(ThemeCatalog.FadeDelayMillisecondsForPreset(preset.DefaultFadeDelayPreset), w.FadeIdleDelayMs);
        Assert.Equal(preset.DefaultStripAutoHide, w.StripAutoHide);
        Assert.Equal(preset.DefaultStripAutoHide, ((ToggleButton)w.FindName("StripAutoHideToggle")!).IsChecked);
        Assert.Equal(preset.DefaultActiveWindowOpacity, w.ConstantWindowOpacity);
        Assert.Equal(preset.DefaultIdleWindowOpacity, w.IdleWindowOpacity);
        Assert.True(w.AppearanceChanged);
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
    public void MainWindow_source_pin_uses_the_theme_accent() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        var pin = (ToggleButton)w.FindName("PinToggle")!;
        var hint = (TextBlock)w.FindName("PinnedHint")!;

        // The source Pin glyph and the pinned hint share one accent brush built from Theme.AccentColor
        // (overhaul Task 10); default settings resolve to the sharp-dark cyan.
        var accent = (SolidColorBrush)ToggleAccent.GetCheckedBrush(pin)!;
        Assert.Equal(ThemeColors.ParseColor(ThemeCatalog.DefaultAccentColor), accent.Color);
        Assert.Equal(accent.Color, ((SolidColorBrush)hint.Foreground).Color);
    });

    [Fact]
    public void MainWindow_resolves_popout_preferences_from_theme_overrides() => StaTestThread.Invoke(() =>
    {
        // Regression for PR #18 review: the ThemePreferenceResolver tests were correct, but
        // MainWindow still fed new/reapplied popouts from the stale legacy PlayerSettings fields.
        var w = new MainWindow();
        w.ReplaceSettingsForTests(new AppSettings
        {
            Player = new PlayerSettings
            {
                PinAccent = "amber",
                FadeIdleDelayMs = 4000,
                ConstantWindowOpacity = 0.9,
                IdleWindowOpacity = 0.7,
                StripAutoHide = true,
            },
            Theme = new ThemeSettings
            {
                AccentColor = "#A78BFA",
                FadeDelayPreset = "short",
                ActiveWindowOpacity = 0.6,
                IdleWindowOpacity = 0.4,
                StripAutoHide = false,
                CornerStyle = "round",
            },
        });

        var prefs = w.EffectivePlayerPreferencesForTests;

        Assert.Equal("#A78BFA", prefs.AccentColor);
        Assert.Equal(1500, prefs.FadeIdleDelayMs);
        Assert.Equal(0.6, prefs.ActiveWindowOpacity);
        Assert.Equal(0.4, prefs.IdleWindowOpacity);
        Assert.False(prefs.StripAutoHide);
        Assert.Equal("round", prefs.CornerStyle);
    });

    [Fact]
    public void PlayerWindow_applies_one_accent_to_pin_and_fade_and_the_delay() => StaTestThread.Invoke(() =>
    {
        var w = new PlayerWindow(
            environment: null!,
            url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            topmost: false,
            placement: null,
            defaultWidth: 960,
            defaultHeight: 540,
            fadeEnabled: true,
            accentColor: "#38D996",
            fadeIdleDelayMs: 4000);

        var pin = (ToggleButton)w.FindName("PinToggle")!;
        var fade = (ToggleButton)w.FindName("FadeToggle")!;

        // One accent brush drives BOTH toggles (overhaul Task 10): the same frozen instance, colored
        // from Theme.AccentColor.
        Assert.Same(ToggleAccent.GetCheckedBrush(pin), ToggleAccent.GetCheckedBrush(fade));
        Assert.Equal(Color.FromRgb(0x38, 0xD9, 0x96), ((SolidColorBrush)ToggleAccent.GetCheckedBrush(pin)!).Color);

        w.ApplyAppearance("#A78BFA", 1500);
        Assert.Equal(Color.FromRgb(0xA7, 0x8B, 0xFA), ((SolidColorBrush)ToggleAccent.GetCheckedBrush(pin)!).Color);
        Assert.Equal(Color.FromRgb(0xA7, 0x8B, 0xFA), ((SolidColorBrush)ToggleAccent.GetCheckedBrush(fade)!).Color);
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
        var close = Assert.Single(bar.Children.OfType<Button>(),
            b => ReferenceEquals(b.Style, Application.Current.Resources["CloseIconButton"]));

        // Code-built and icon-only: the markup a11y sweep can't see it, so pin the UIA name here
        // (REQ-UI-02, overhaul Task 7).
        Assert.False(string.IsNullOrWhiteSpace(
            System.Windows.Automation.AutomationProperties.GetName(close)),
            "Prompt close button is missing an automation name.");
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
    public void Popout_action_state_flips_label_tooltip_and_uia_name_together() => StaTestThread.Invoke(() =>
    {
        // Q-6 / REQ-UI-02 (overhaul Task 6): while a popout is open the primary action must say
        // show/focus, and the accessible name must flip in the same code path as the label.
        var w = new MainWindow();
        var btn = (Button)w.FindName("PopOutButton")!;
        var label = (TextBlock)w.FindName("PopOutButtonText")!;

        w.ApplyPopoutActionState(hasPlayer: true);
        Assert.Equal("Show popout", label.Text);
        Assert.Equal("Show popout", System.Windows.Automation.AutomationProperties.GetName(btn));
        Assert.Contains("front", (string)btn.ToolTip);

        w.ApplyPopoutActionState(hasPlayer: false);
        Assert.Equal("Pop out video", label.Text);
        Assert.Equal("Pop out video", System.Windows.Automation.AutomationProperties.GetName(btn));
        Assert.Contains("Pop out", (string)btn.ToolTip);
    });

    [Fact]
    public void Source_placeholder_surfaces_and_clears_the_fallback_note() => StaTestThread.Invoke(() =>
    {
        // The mix/radio FallbackReason was log-only; it now rides the placeholder (Q-6) and must
        // be cleared with it so a stale note can't survive into the next popout.
        var w = new MainWindow();
        var note = (TextBlock)w.FindName("PlaceholderNoteText")!;

        w.ShowSourcePlaceholder(true, "Mix/radio playlists aren't supported in Video Popout - popped out the current video.");
        Assert.Equal(Visibility.Visible, note.Visibility);
        Assert.Contains("Mix/radio", note.Text);

        w.ShowSourcePlaceholder(true);   // no reason this time
        Assert.Equal(Visibility.Collapsed, note.Visibility);
        Assert.Equal(string.Empty, note.Text);

        w.ShowSourcePlaceholder(false);
        Assert.Equal(Visibility.Collapsed, note.Visibility);
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

    // --- Compact error bar + normal-page fallback (Phase 3, Stage 4: spec 10.3 / Q-6) ---

    private static PlayerWindow NewCompactPlayer() =>
        new(environment: null!, url: "https://piplay.local/player.html?v=dQw4w9WgXcQ",
            topmost: false, placement: null, defaultWidth: 960, defaultHeight: 540,
            fadeEnabled: true, mode: PlaybackMode.Compact,
            fallbackTarget: new YouTubeTarget { VideoId = "dQw4w9WgXcQ" });

    [Fact]
    public void Compact_error_bar_is_collapsed_on_construction() => StaTestThread.Invoke(() =>
    {
        Assert.False(NewCompactPlayer().IsErrorBarVisibleForTests);
    });

    [Fact]
    public void Shell_error_shows_the_bar_with_the_policy_message() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        w.HandleShellErrorForTests(new InboundShellMessage(ShellMessageKind.Error, ErrorCode: "101"));

        Assert.True(w.IsErrorBarVisibleForTests);
        Assert.Equal(PlayerShellErrorPolicy.Describe("101"), w.ErrorTextForTests);
    });

    [Fact]
    public void Shell_load_failure_shows_the_bar_with_the_load_message() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        w.HandleShellLoadFailureForTests();

        Assert.True(w.IsErrorBarVisibleForTests);
        Assert.Equal(PlayerShellErrorPolicy.ShellLoadFailedMessage, w.ErrorTextForTests);
    });

    [Fact]
    public void Playing_state_auto_dismisses_the_error_bar_but_others_do_not() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        w.HandleShellErrorForTests(new InboundShellMessage(ShellMessageKind.Error, ErrorCode: "100"));

        // Buffering does not prove recovery; the bar stays.
        w.HandleShellStateForTests(new InboundShellMessage(ShellMessageKind.State, CurrentTime: 0, PlayerState: 3));
        Assert.True(w.IsErrorBarVisibleForTests);

        // A playing state does (e.g. a playlist auto-advanced past the dead entry).
        w.HandleShellStateForTests(new InboundShellMessage(
            ShellMessageKind.State, CurrentTime: 7, PlayerState: PlayerShellErrorPolicy.StatePlaying));
        Assert.False(w.IsErrorBarVisibleForTests);
    });

    [Fact]
    public void Shell_errors_are_ignored_in_normal_mode() => StaTestThread.Invoke(() =>
    {
        var w = NewPlayer();   // normal mode: no shell, so an error can't surface the compact bar
        w.HandleShellErrorForTests(new InboundShellMessage(ShellMessageKind.Error, ErrorCode: "101"));
        Assert.False(w.IsErrorBarVisibleForTests);
    });

    [Fact]
    public void Fallback_without_a_live_webview_is_a_guarded_no_op() => StaTestThread.Invoke(() =>
    {
        // The window is never shown, so CoreWebView2 was never created: the fallback must refuse
        // safely (no navigation target) and leave the error state and compact floor untouched.
        var w = NewCompactPlayer();
        w.HandleShellErrorForTests(new InboundShellMessage(ShellMessageKind.Error, ErrorCode: "150"));

        var ex = Record.Exception(w.RequestFallbackForTests);
        Assert.Null(ex);
        Assert.True(w.IsErrorBarVisibleForTests);
        Assert.Equal(PlaybackModePolicy.CompactMinWidth, w.MinWidth);
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

    // --- Settings is bounded + scrollable (overhaul Task 5) ---

    [Fact]
    public void SettingsWindow_height_is_bounded_by_the_work_area() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true);

        // Pin the exact clamp derivation: work area less the margin, with the usability floor —
        // the floor WINS on a sub-468px work area, so asserting "<= work area" outright would
        // self-contradict the floor there (review finding 2026-06-11).
        Assert.True(double.IsFinite(w.MaxHeight), "Settings MaxHeight must be bounded.");
        Assert.Equal(Math.Max(420, SystemParameters.WorkArea.Height - 48), w.MaxHeight);
        // Frame model reconciled with the b35c0dd landing: fixed launch Height under the clamp.
        Assert.True(w.Height <= w.MaxHeight, $"Launch Height {w.Height} exceeds the clamp {w.MaxHeight}.");
        Assert.True(w.MinHeight > 0, "The dialog must declare a usable MinHeight.");
        Assert.IsType<ScrollViewer>(w.FindName("SettingsScroll"));
    });

    [Fact]
    public void SettingsWindow_states_that_compact_applies_to_new_popouts() => StaTestThread.Invoke(() =>
    {
        // Spec acceptance: "Settings copy states that Compact player applies to new popouts only."
        var hint = (TextBlock)new SettingsWindow(isBrowserReady: true).FindName("CompactModeHintText")!;
        Assert.Contains("new Popout Players", hint.Text);
    });

    // --- Whole-window opacity (spec 7.3, Phase 4) ---

    [Fact]
    public void SettingsWindow_reflects_and_updates_window_opacity() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true, constantWindowOpacity: 0.8, idleWindowOpacity: 0.6);

        Assert.Equal(0.8, w.ConstantWindowOpacity);
        Assert.Equal(0.6, w.IdleWindowOpacity);
        Assert.Equal(80, ((Slider)w.FindName("ActiveOpacitySlider")!).Value);
        Assert.Equal(60, ((Slider)w.FindName("IdleOpacitySlider")!).Value);
        Assert.False(w.AppearanceChanged);   // seeding the sliders must not count as a user change

        (double Constant, double Idle)? preview = null;
        w.OpacityPreviewChanged += (c, i) => preview = (c, i);
        ((Slider)w.FindName("IdleOpacitySlider")!).Value = 45;

        Assert.True(w.AppearanceChanged);
        Assert.Equal(0.45, w.IdleWindowOpacity);
        Assert.Equal((0.8, 0.45), preview);
    });

    [Fact]
    public void SettingsWindow_preserves_hand_edited_sub_floor_opacity_until_the_slider_moves() => StaTestThread.Invoke(() =>
    {
        // Spec 7.3 explicit unlock: a hand-edited 0.25 displays clamped at the 45% slider floor
        // but must survive an unrelated settings change untouched.
        var w = new SettingsWindow(isBrowserReady: true, constantWindowOpacity: 0.25, idleWindowOpacity: 1.0);

        Assert.Equal(45, ((Slider)w.FindName("ActiveOpacitySlider")!).Value);
        Assert.Equal(0.25, w.ConstantWindowOpacity);

        ((Slider)w.FindName("IdleOpacitySlider")!).Value = 70;   // user touches only the idle slider
        Assert.Equal(0.25, w.ConstantWindowOpacity);             // the unlock survives
        Assert.Equal(0.7, w.IdleWindowOpacity);

        ((Slider)w.FindName("ActiveOpacitySlider")!).Value = 50; // moving THE slider replaces it
        Assert.Equal(0.5, w.ConstantWindowOpacity);
    });

    [Fact]
    public void PlayerWindow_records_normalized_window_opacity_levels() => StaTestThread.Invoke(() =>
    {
        var w = new PlayerWindow(environment: null!, url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            topmost: false, placement: null, defaultWidth: 960, defaultHeight: 540, fadeEnabled: true,
            constantWindowOpacity: 0.8, idleWindowOpacity: 5.0);

        Assert.Equal((0.8, 1.0), w.WindowOpacityLevelsForTests);   // junk idle reset by the policy

        w.ApplyWindowOpacity(0.6, 0.45);
        Assert.Equal((0.6, 0.45), w.WindowOpacityLevelsForTests);
    });

    [Fact]
    public void PlayerWindow_opacity_idle_state_is_safe_without_an_hwnd() => StaTestThread.Invoke(() =>
    {
        // Never shown: entering/leaving the opacity idle state must not start the hover poll or
        // touch native state (the SourceInitialized hook owns the first real application).
        var w = NewPlayer();
        var ex = Record.Exception(() =>
        {
            w.EnterWindowOpacityIdleForTests();
            Assert.True(w.IsWindowOpacityIdleForTests);
            Assert.False(w.IsOpacityHoverPollRunningForTests);
        });
        Assert.Null(ex);
    });

    [Fact]
    public void Window_opacity_applier_engages_a_real_hwnd_and_disengages_cleanly() => StaTestThread.Invoke(() =>
    {
        // Real HWND (the BorderlessWindowHelper test recipe): the guard subclass must hold the
        // layered bit against WPF's HwndTarget strip, and 1.0 must restore the pristine exstyle.
        var w = new Window
        {
            Width = 240, Height = 160, Left = 100, Top = 100,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanResize,
            AllowsTransparency = false, Opacity = 0, ShowActivated = false, ShowInTaskbar = false,
        };
        var hwnd = new WindowInteropHelper(w).EnsureHandle();

        // Acceptance criterion 1: feature-off calls on a pristine window are a strict no-op —
        // no tracking state, no subclass-driven exstyle change (Null target proves never tracked).
        var pristine = GetWindowLongPtrW(hwnd, -20).ToInt64();
        WindowOpacityApplier.SetCornerMode(hwnd, DwmCornerMode.Default);
        WindowOpacityApplier.Apply(hwnd, 1.0, animate: false);
        Assert.Null(WindowOpacityApplier.TargetAlphaForTests(hwnd));
        Assert.Equal(DwmCornerMode.Default, WindowOpacityApplier.CornerModeForTests(hwnd));
        Assert.Equal(pristine, GetWindowLongPtrW(hwnd, -20).ToInt64());

        // Engage BEFORE Show: production order (the popout's SourceInitialized runs inside Show),
        // so the bit + alpha must survive WPF's show-time style application.
        WindowOpacityApplier.Apply(hwnd, 0.6, animate: false);
        w.Show();
        Assert.True(WindowOpacityApplier.IsEngagedForTests(hwnd));
        Assert.Equal((byte)153, WindowOpacityApplier.TargetAlphaForTests(hwnd));
        Assert.Equal((byte)153, WindowOpacityApplier.CurrentAlphaForTests(hwnd));
        var ex = GetWindowLongPtrW(hwnd, -20).ToInt64();
        Assert.True((ex & 0x00080000) != 0, $"WS_EX_LAYERED missing from live exstyle 0x{ex:X} after Show.");
        Assert.True((ex & 0x00000020) == 0, $"WS_EX_TRANSPARENT set on live exstyle 0x{ex:X} (ADR-0006).");
        Assert.False(WindowOpacityApplier.LastExStyleWriteCarriedTransparentBitForTests(hwnd));

        // Spike finding 2: a wholesale exstyle rewrite that drops the bit (what WPF does during
        // move/size/topmost) must be defeated by the WM_STYLECHANGING forcing, not healed later.
        SetWindowLongPtrW(hwnd, -20, new IntPtr(ex & ~0x00080000));
        var afterHostileWrite = GetWindowLongPtrW(hwnd, -20).ToInt64();
        Assert.True((afterHostileWrite & 0x00080000) != 0,
            $"Guard failed to force WS_EX_LAYERED through a hostile rewrite: 0x{afterHostileWrite:X}.");
        w.Topmost = true;   // production-shaped stressor on the same path
        var afterTopmost = GetWindowLongPtrW(hwnd, -20).ToInt64();
        Assert.True((afterTopmost & 0x00080000) != 0, $"Topmost toggle dropped the bit: 0x{afterTopmost:X}.");

        WindowOpacityApplier.SetCornerMode(hwnd, DwmCornerMode.Round);
        Assert.Equal(DwmCornerMode.Round, WindowOpacityApplier.CornerModeForTests(hwnd));
        // Mode is theme/user data: every non-default mode (incl. an explicit Square override on a
        // previously-rounded window) lands and is tracked.
        WindowOpacityApplier.SetCornerMode(hwnd, DwmCornerMode.Square);
        Assert.Equal(DwmCornerMode.Square, WindowOpacityApplier.CornerModeForTests(hwnd));
        WindowOpacityApplier.SetCornerMode(hwnd, DwmCornerMode.SmallRound);
        Assert.Equal(DwmCornerMode.SmallRound, WindowOpacityApplier.CornerModeForTests(hwnd));
        // The reset transition is a real user path (corner style back to Theme on sharp-dark /
        // theme switched off soft-glass): Default on a MODIFIED window must land DWMWCP_DEFAULT,
        // not early-return — only the never-touched pristine case skips the write.
        WindowOpacityApplier.SetCornerMode(hwnd, DwmCornerMode.Default);
        Assert.Equal(DwmCornerMode.Default, WindowOpacityApplier.CornerModeForTests(hwnd));

        WindowOpacityApplier.Apply(hwnd, 1.0, animate: false);
        Assert.False(WindowOpacityApplier.IsEngagedForTests(hwnd));
        var restored = GetWindowLongPtrW(hwnd, -20).ToInt64();
        Assert.True((restored & 0x00080000) == 0, $"WS_EX_LAYERED still set after disengage: 0x{restored:X}.");

        // Re-engage after a disengage (the constant=1.0 + idle<1.0 cycle hits this every round trip).
        WindowOpacityApplier.Apply(hwnd, 0.45, animate: false);
        Assert.True(WindowOpacityApplier.IsEngagedForTests(hwnd));
        Assert.Equal((byte)115, WindowOpacityApplier.CurrentAlphaForTests(hwnd));
        var reengaged = GetWindowLongPtrW(hwnd, -20).ToInt64();
        Assert.True((reengaged & 0x00080000) != 0, $"Re-engage failed to land the bit: 0x{reengaged:X}.");
    });

    [Fact]
    public void PlayerWindow_applies_active_and_idle_levels_through_the_applier() => StaTestThread.Invoke(() =>
    {
        // EnsureHandle fires SourceInitialized without Show: Loaded never runs, so WebView2 and
        // the network stay untouched, but the initial opacity application is real.
        var w = new PlayerWindow(environment: null!, url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            topmost: false, placement: null, defaultWidth: 960, defaultHeight: 540, fadeEnabled: true,
            constantWindowOpacity: 0.8, idleWindowOpacity: 0.6);
        var hwnd = new WindowInteropHelper(w).EnsureHandle();

        // SourceInitialized applied the ACTIVE level (the "appear at the configured level" hook).
        Assert.True(WindowOpacityApplier.IsEngagedForTests(hwnd));
        Assert.Equal(WindowOpacityPolicy.ToAlphaByte(0.8), WindowOpacityApplier.TargetAlphaForTests(hwnd));

        // Idle onset applies the idle level and arms the hover-restore poll (idle < active).
        w.EnterWindowOpacityIdleForTests();
        Assert.Equal(WindowOpacityPolicy.ToAlphaByte(0.6), WindowOpacityApplier.TargetAlphaForTests(hwnd));
        Assert.True(w.IsOpacityHoverPollRunningForTests);

        // Settings change while idle keeps applying the IDLE level (the live-preview path).
        w.ApplyWindowOpacity(0.7, 0.5);
        Assert.Equal(WindowOpacityPolicy.ToAlphaByte(0.5), WindowOpacityApplier.TargetAlphaForTests(hwnd));

        // Activity restores the active level; the probe keeps running while an idle dip is
        // configured (it must PREVENT the next idle onset during movement over the video).
        w.OnUserActivityForTests();
        Assert.False(w.IsWindowOpacityIdleForTests);
        Assert.Equal(WindowOpacityPolicy.ToAlphaByte(0.7), WindowOpacityApplier.TargetAlphaForTests(hwnd));
        Assert.True(w.IsOpacityHoverPollRunningForTests);

        // Turning the feature off stops the probe (defaults never run it).
        w.ApplyWindowOpacity(1.0, 1.0);
        Assert.False(w.IsOpacityHoverPollRunningForTests);

        w.Close();
    });

    [Fact]
    public void PlayerWindow_applies_the_theme_corner_mode_to_its_hwnd() => StaTestThread.Invoke(() =>
    {
        // End-to-end corner wiring (the opacity test's recipe): EnsureHandle fires
        // SourceInitialized without Show, so the initial DWM corner application is real.
        var w = new PlayerWindow(environment: null!, url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            topmost: false, placement: null, defaultWidth: 960, defaultHeight: 540, fadeEnabled: true,
            dwmCornerMode: DwmCornerMode.Round);
        var hwnd = new WindowInteropHelper(w).EnsureHandle();
        Assert.Equal(DwmCornerMode.Round, WindowOpacityApplier.CornerModeForTests(hwnd));

        // The live re-apply seam (MainWindow's ApplyOpenPlayerAppearance path on settings change).
        w.ApplyCornerMode(DwmCornerMode.Square);
        Assert.Equal(DwmCornerMode.Square, WindowOpacityApplier.CornerModeForTests(hwnd));

        w.Close();
    });

    [Fact]
    public void MainWindow_applies_the_theme_corner_mode_at_source_initialized() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        w.ReplaceSettingsForTests(new AppSettings { Theme = new ThemeSettings { CornerStyle = "round" } });
        var hwnd = new WindowInteropHelper(w).EnsureHandle();
        Assert.Equal(DwmCornerMode.Round, WindowOpacityApplier.CornerModeForTests(hwnd));
        w.Close();
    });

    [Fact]
    public void SettingsWindow_wears_the_pending_corner_style_on_its_own_hwnd() => StaTestThread.Invoke(() =>
    {
        // The dialog itself is the instant feedback surface for the corner-style row.
        var w = new SettingsWindow(isBrowserReady: true, cornerStyle: "round");
        var hwnd = new WindowInteropHelper(w).EnsureHandle();
        Assert.Equal(DwmCornerMode.Round, WindowOpacityApplier.CornerModeForTests(hwnd));

        ((ToggleButton)w.FindName("CornerStyleSquareChip")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(DwmCornerMode.Square, WindowOpacityApplier.CornerModeForTests(hwnd));

        w.Close();
    });

    [Fact]
    public void Prompt_dialogs_wear_the_current_theme_corner_mode() => StaTestThread.Invoke(() =>
    {
        // Prompts are built outside the settings flow, so they read the applier's last-applied
        // mode — without this they would be the only differently-shaped dialog under a
        // round/square theme (adversarial review finding).
        var res = new ResourceDictionary();
        ThemeResourceApplier.Apply(res, new ThemeSettings { ThemeId = "soft-glass" }, new PlayerSettings());
        try
        {
            Assert.Equal(DwmCornerMode.Round, ThemeResourceApplier.CurrentDwmCorners);
            var shell = Prompt.BuildShell(owner: null, "Test", out _);
            var hwnd = new WindowInteropHelper(shell).EnsureHandle();
            Assert.Equal(DwmCornerMode.Round, WindowOpacityApplier.CornerModeForTests(hwnd));
            shell.Close();
        }
        finally
        {
            // Restore the static for the rest of the suite (sharp-dark default = Default).
            ThemeResourceApplier.Apply(new ResourceDictionary(), new ThemeSettings(), new PlayerSettings());
        }
    });

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr newValue);

    // --- Shell request channel + strip auto-hide (spec 7.2 / 10.3, Phase 4 Task 4) ---

    private static PlayerWindow NewCompactAutoHidePlayer() =>
        new(environment: null!, url: "https://piplay.local/player.html?v=dQw4w9WgXcQ",
            topmost: false, placement: null, defaultWidth: 960, defaultHeight: 540,
            fadeEnabled: true, mode: PlaybackMode.Compact,
            fallbackTarget: new YouTubeTarget { VideoId = "dQw4w9WgXcQ" }, stripAutoHide: true);

    private static InboundShellMessage Request(string action) =>
        new(ShellMessageKind.Request, Action: action);

    [Fact]
    public void Shell_close_request_closes_the_player() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        var closed = false;
        w.PlayerClosed += (_, _) => closed = true;

        w.HandleShellRequestForTests(Request(PlayerShellProtocol.ActionClose));
        // Close is deferred out of the WebMessageReceived callback (WebView2 reentrancy guard);
        // a Background-priority nested invoke drains the queued Normal-priority Close first.
        Assert.False(closed);
        w.Dispatcher.Invoke(static () => { }, DispatcherPriority.Background);

        Assert.True(closed);
    });

    [Fact]
    public void Shell_pin_request_toggles_topmost_and_the_strip_toggle() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        Assert.False(w.Topmost);

        w.HandleShellRequestForTests(Request(PlayerShellProtocol.ActionPinToggle));
        Assert.True(w.Topmost);
        Assert.True(((ToggleButton)w.FindName("PinToggle")!).IsChecked);   // native toggle stays in sync

        w.HandleShellRequestForTests(Request(PlayerShellProtocol.ActionPinToggle));
        Assert.False(w.Topmost);
        Assert.False(((ToggleButton)w.FindName("PinToggle")!).IsChecked);
    });

    [Fact]
    public void Shell_fullscreen_request_toggles_maximized() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        Assert.Equal(WindowState.Normal, w.WindowState);

        w.HandleShellRequestForTests(Request(PlayerShellProtocol.ActionFullscreenToggle));
        Assert.Equal(WindowState.Maximized, w.WindowState);

        w.HandleShellRequestForTests(Request(PlayerShellProtocol.ActionFullscreenToggle));
        Assert.Equal(WindowState.Normal, w.WindowState);
    });

    // --- Expand / restore affordance (overhaul Task 4) ---

    private const string GlyphMaximize = "";
    private const string GlyphRestore = "";

    private static void ClickExpand(PlayerWindow w) =>
        ((Button)w.FindName("ExpandButton")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    [Fact]
    public void Expand_button_toggles_maximized_in_normal_mode_and_keeps_the_affordance_honest() =>
        StaTestThread.Invoke(() =>
        {
            // Normal mode deliberately: the native button must serve BOTH playback modes.
            var w = NewPlayer();
            Assert.Equal(GlyphMaximize, w.ExpandGlyphForTests);
            Assert.Equal("Expand popout", w.ExpandToolTipForTests);

            ClickExpand(w);
            Assert.Equal(WindowState.Maximized, w.WindowState);
            Assert.Equal(GlyphRestore, w.ExpandGlyphForTests);
            Assert.Equal("Restore popout", w.ExpandToolTipForTests);

            ClickExpand(w);
            Assert.Equal(WindowState.Normal, w.WindowState);
            Assert.Equal(GlyphMaximize, w.ExpandGlyphForTests);
            Assert.Equal("Expand popout", w.ExpandToolTipForTests);
        });

    [Fact]
    public void Shell_fullscreen_request_updates_the_expand_affordance() => StaTestThread.Invoke(() =>
    {
        // The shell request and the native button are ONE path; the glyph follows either caller.
        var w = NewCompactPlayer();
        w.HandleShellRequestForTests(Request(PlayerShellProtocol.ActionFullscreenToggle));
        Assert.Equal(GlyphRestore, w.ExpandGlyphForTests);
    });

    [Fact]
    public void Fullscreen_element_expands_compact_and_exit_restores() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();

        w.ApplyFullScreenElementStateForTests(contains: true);
        Assert.Equal(WindowState.Maximized, w.WindowState);
        Assert.Equal(GlyphRestore, w.ExpandGlyphForTests);

        w.ApplyFullScreenElementStateForTests(contains: false);
        Assert.Equal(WindowState.Normal, w.WindowState);
        Assert.Equal(GlyphMaximize, w.ExpandGlyphForTests);
    });

    [Fact]
    public void Fullscreen_element_is_ignored_in_normal_mode() => StaTestThread.Invoke(() =>
    {
        // The gate is the LIVE mode (Popout Standard / Fullview Faded must not gain a new
        // fullscreen invariant) — same gate the compact→normal fallback relies on.
        var w = NewPlayer();
        w.ApplyFullScreenElementStateForTests(contains: true);
        Assert.Equal(WindowState.Normal, w.WindowState);
    });

    [Fact]
    public void Fullscreen_element_exit_keeps_a_user_expanded_window() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        ClickExpand(w);   // the user's own posture, not the element's
        Assert.Equal(WindowState.Maximized, w.WindowState);

        w.ApplyFullScreenElementStateForTests(contains: true);
        w.ApplyFullScreenElementStateForTests(contains: false);
        Assert.Equal(WindowState.Maximized, w.WindowState);
    });

    [Fact]
    public void Expand_toggle_reveals_a_collapsed_strip_so_restore_stays_reachable() => StaTestThread.Invoke(() =>
    {
        // Adopted from the parallel b35c0dd landing: any expand path counts as activity, so an
        // auto-hidden strip un-collapses and the restore affordance is reachable in the new state
        // without waiting for the top-edge reveal (Task 4 reversibility).
        var w = NewCompactAutoHidePlayer();
        w.HideControlsForTests();
        w.CompleteHideFadeForTests();
        Assert.True(w.IsChromeStripCollapsedForTests);

        ClickExpand(w);
        Assert.Equal(WindowState.Maximized, w.WindowState);
        Assert.False(w.IsChromeStripCollapsedForTests);
        Assert.True(w.IsChromeStripHitTestVisibleForTests);
    });

    [Fact]
    public void Os_restore_clears_the_fullscreen_element_latch() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        w.ApplyFullScreenElementStateForTests(contains: true);
        Assert.True(w.IsMaximizedForFullScreenElementForTests);

        // The OS path our toggles never see (Win+Down, aero snap): the state changes, then
        // StateChanged runs the sync — the expansion the latch described no longer exists.
        w.WindowState = WindowState.Normal;
        w.HandleWindowStateChangedForTests();
        Assert.False(w.IsMaximizedForFullScreenElementForTests);

        // The element's later exit must not disturb the state the user chose via the OS.
        w.ApplyFullScreenElementStateForTests(contains: false);
        Assert.Equal(WindowState.Normal, w.WindowState);
    });

    [Fact]
    public void Escape_restores_an_expanded_popout_and_is_inert_otherwise() => StaTestThread.Invoke(() =>
    {
        var w = NewPlayer();
        Assert.False(w.HandleEscapeForTests());

        ClickExpand(w);
        Assert.True(w.HandleEscapeForTests());
        Assert.Equal(WindowState.Normal, w.WindowState);
        Assert.Equal(GlyphMaximize, w.ExpandGlyphForTests);
    });

    [Fact]
    public void Popout_never_launches_expanded_even_from_a_maximized_capture() => StaTestThread.Invoke(() =>
    {
        // Pre-fix settings files can carry Maximized=true; the ctor normalizes it away.
        var w = new PlayerWindow(environment: null!, url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            topmost: false,
            placement: new PlacementData { X = 20, Y = 20, Width = 900, Height = 560, Maximized = true },
            defaultWidth: 960, defaultHeight: 540, fadeEnabled: true);

        Assert.NotNull(w.LaunchPlacementForTests);
        Assert.False(w.LaunchPlacementForTests!.Maximized);
        Assert.Equal(900, w.LaunchPlacementForTests.Width);   // bounds survive the normalization
    });

    // --- In-place retarget + video-aware return state (overhaul Task 3) ---

    [Fact]
    public void Compact_player_carries_its_launch_video_in_the_return_state() => StaTestThread.Invoke(() =>
    {
        Assert.Equal("dQw4w9WgXcQ", NewCompactPlayer().ReturnVideoIdForTests);
    });

    [Fact]
    public void Shell_state_updates_the_return_video_and_timestamp() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        w.HandleShellStateForTests(new InboundShellMessage(
            ShellMessageKind.State, CurrentTime: 42, PlayerState: 1, VideoId: "autoAdvVid1"));

        Assert.Equal("autoAdvVid1", w.ReturnVideoIdForTests);   // playlist auto-advance tracked
        Assert.Equal(42, w.ReturnSecondsForTests);

        // A state message WITHOUT a videoId (pre-v3 shape) keeps the last-known id.
        w.HandleShellStateForTests(new InboundShellMessage(ShellMessageKind.State, CurrentTime: 50));
        Assert.Equal("autoAdvVid1", w.ReturnVideoIdForTests);
        Assert.Equal(50, w.ReturnSecondsForTests);
    });

    [Fact]
    public void Hostile_shell_video_ids_never_become_the_return_target() => StaTestThread.Invoke(() =>
    {
        // End-to-end through the REAL wire path: the shell string later becomes a SOURCE
        // navigation target on close, and PlayerShellProtocol.Parse (the trust boundary) must
        // reject the malformed id before the host ever sees it.
        var w = NewCompactPlayer();
        w.HandleShellStateForTests(PlayerShellProtocol.Parse(
            "{\"v\":3,\"type\":\"state\",\"currentTime\":5,\"playerState\":1,\"videoId\":\"abc&evil=1//\"}"));

        Assert.Equal("dQw4w9WgXcQ", w.ReturnVideoIdForTests);   // launch id kept
        Assert.Equal(5, w.ReturnSecondsForTests);               // the timestamp itself still lands
    });

    [Fact]
    public void Compact_retarget_rebuilds_the_shell_url_and_resets_launch_state() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        w.HandleShellStateForTests(new InboundShellMessage(ShellMessageKind.State, CurrentTime: 42, PlayerState: 1));

        Assert.True(w.TryRetargetForNewWindow("https://www.youtube.com/watch?v=recVideo001&t=30s"));

        Assert.Contains("piplay.local/player.html", w.CurrentUrlForTests);   // same mode: shell rebuild
        Assert.Contains("v=recVideo001", w.CurrentUrlForTests);
        Assert.Contains("start=30", w.CurrentUrlForTests);
        Assert.Equal("recVideo001", w.ReturnVideoIdForTests);
        Assert.Equal("recVideo001", w.CurrentFallbackVideoIdForTests);   // error-bar fallback follows
        Assert.Null(w.ReturnSecondsForTests);   // unknown until the NEW shell reports
    });

    [Fact]
    public void Normal_retarget_navigates_to_the_watch_url() => StaTestThread.Invoke(() =>
    {
        var w = NewPlayer();
        Assert.True(w.TryRetargetForNewWindow("https://www.youtube.com/watch?v=recVideo001"));

        Assert.StartsWith("https://www.youtube.com/watch?v=recVideo001", w.CurrentUrlForTests);
        Assert.Equal("recVideo001", w.ReturnVideoIdForTests);
    });

    [Fact]
    public void Non_playable_new_window_targets_are_not_retargeted() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();
        var urlBefore = w.CurrentUrlForTests;

        Assert.False(w.TryRetargetForNewWindow("https://www.youtube.com/@SomeChannel"));
        Assert.False(w.TryRetargetForNewWindow("https://example.com/watch?v=dQw4w9WgXcQ"));

        Assert.Equal(urlBefore, w.CurrentUrlForTests);
        Assert.Equal("dQw4w9WgXcQ", w.ReturnVideoIdForTests);   // launch state untouched
    });

    // --- Video-aware return on the SOURCE side (overhaul Task 3) ---

    [Fact]
    public void Return_to_a_different_video_navigates_and_arms_the_auto_dedup_key() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        w.SeedPopoutReturnForTests(sourceVideoId: "AAAAAAAAAAA");

        w.ApplyReturnActionAsync(new PlayerReturnState { VideoId = "BBBBBBBBBBB", LastKnownSeconds = 42 })
            .GetAwaiter().GetResult();   // Navigate completes synchronously (no core to script)

        // The browser never initializes in the test lane, so the navigation queues — the queued
        // URL IS the assertion: the source heads to the RETURNED video at its timestamp.
        Assert.Equal("https://www.youtube.com/watch?v=BBBBBBBBBBB&t=42s", w.PendingUrlForTests);
        // De-dup key armed before navigating: Auto must not instantly re-pop the returned video.
        Assert.Equal("BBBBBBBBBBB", w.AutoLastHandledVideoIdForTests);
    });

    [Fact]
    public void Return_on_the_same_video_seeks_rather_than_navigates() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        w.SeedPopoutReturnForTests(sourceVideoId: "AAAAAAAAAAA");

        w.ApplyReturnActionAsync(new PlayerReturnState { VideoId = "AAAAAAAAAAA", LastKnownSeconds = 42 })
            .GetAwaiter().GetResult();

        Assert.Null(w.PendingUrlForTests);               // the seek path scripts a live core instead
        Assert.Null(w.AutoLastHandledVideoIdForTests);   // de-dup key untouched on a plain return
    });

    [Fact]
    public void Shell_requests_are_ignored_in_normal_mode() => StaTestThread.Invoke(() =>
    {
        // The shell only exists in compact mode; after the fallback flips the window to normal
        // mode, a late request must be inert (the bridge is disposed, but the guard is belt-and-braces).
        var w = NewPlayer();
        w.HandleShellRequestForTests(Request(PlayerShellProtocol.ActionPinToggle));
        Assert.False(w.Topmost);
    });

    [Fact]
    public void Strip_auto_hide_collapses_after_the_fade_and_activity_restores_it() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactAutoHidePlayer();
        Assert.True(w.StripAutoHideForTests);
        Assert.False(w.IsChromeStripCollapsedForTests);   // visible on construction

        w.HideControlsForTests();
        w.CompleteHideFadeForTests();   // the fade's Completed callback (clocks don't tick headless)
        Assert.True(w.IsChromeStripCollapsedForTests);
        Assert.False(w.IsChromeStripHitTestVisibleForTests);   // Q-8: hidden strip swallows no clicks

        w.OnUserActivityForTests();   // any reveal path must restore the layout row immediately
        Assert.False(w.IsChromeStripCollapsedForTests);
        Assert.True(w.IsChromeStripHitTestVisibleForTests);
    });

    [Fact]
    public void Strip_does_not_collapse_when_auto_hide_is_off() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactPlayer();   // fade on, auto-hide off: Stage 4 behavior byte-for-byte
        w.HideControlsForTests();
        w.CompleteHideFadeForTests();

        Assert.False(w.IsChromeStripCollapsedForTests);        // faded, but the row is still reserved
        Assert.False(w.IsChromeStripHitTestVisibleForTests);
    });

    [Fact]
    public void Reveal_mid_fade_keeps_the_strip_interactive_and_uncollapsed() => StaTestThread.Invoke(() =>
    {
        // Activity lands between the hide decision and the fade's completion: the late completion
        // callback must not knock down a strip the user just got back.
        var w = NewCompactAutoHidePlayer();
        w.HideControlsForTests();
        w.OnUserActivityForTests();
        w.CompleteHideFadeForTests();   // the stale callback fires after the reveal

        Assert.False(w.IsChromeStripCollapsedForTests);
        Assert.True(w.IsChromeStripHitTestVisibleForTests);
    });

    [Fact]
    public void Turning_auto_hide_off_restores_a_collapsed_strip() => StaTestThread.Invoke(() =>
    {
        var w = NewCompactAutoHidePlayer();
        w.HideControlsForTests();
        w.CompleteHideFadeForTests();
        Assert.True(w.IsChromeStripCollapsedForTests);   // precondition: collapsed

        // The settings path (MainWindow live re-apply) turns the behavior off mid-collapse.
        w.ApplyAppearance("#00D4FF", 2500, stripAutoHide: false);

        Assert.False(w.StripAutoHideForTests);
        Assert.False(w.IsChromeStripCollapsedForTests);
    });

    [Fact]
    public void Auto_hide_arms_the_activity_probe_and_fade_off_disarms_it() => StaTestThread.Invoke(() =>
    {
        // At 1.0/1.0 opacity defaults the probe used to never run; the auto-hiding strip needs it
        // for the top-edge reveal (WPF sees no mouse over the WebView2 child). Fade off removes
        // the only idleness source, so the probe must stop with it.
        var w = NewCompactAutoHidePlayer();
        _ = new WindowInteropHelper(w).EnsureHandle();   // SourceInitialized applies opacity + probe
        Assert.True(w.IsOpacityHoverPollRunningForTests);

        var fade = (ToggleButton)w.FindName("FadeToggle")!;
        fade.IsChecked = false;
        fade.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.False(w.IsOpacityHoverPollRunningForTests);
        Assert.False(w.IsChromeStripCollapsedForTests);   // fade off pins the strip visible

        fade.IsChecked = true;
        fade.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.True(w.IsOpacityHoverPollRunningForTests);

        w.Close();
    });

    [Fact]
    public void SettingsWindow_reflects_and_toggles_strip_auto_hide() => StaTestThread.Invoke(() =>
    {
        var on = new SettingsWindow(isBrowserReady: true, stripAutoHide: true);
        Assert.True(on.StripAutoHide);
        Assert.True(((ToggleButton)on.FindName("StripAutoHideToggle")!).IsChecked);

        var w = new SettingsWindow(isBrowserReady: true, stripAutoHide: false);
        Assert.False(w.StripAutoHide);
        var toggle = (ToggleButton)w.FindName("StripAutoHideToggle")!;
        Assert.False(toggle.IsChecked);
        Assert.False(w.AppearanceChanged);   // seeding must not count as a user change

        toggle.IsChecked = true;
        toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.True(w.StripAutoHide);
        Assert.True(w.AppearanceChanged);
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
        // The style references the THEMED brush via DynamicResource (so a theme change restyles
        // open tooltips), so resolve it through a realized ToolTip rather than casting the
        // Setter.Value (which is now a DynamicResourceExtension, not a brush).
        var toolTipStyle = Application.Current.TryFindResource(typeof(ToolTip)) as Style;
        Assert.NotNull(toolTipStyle);
        var tip = new ToolTip { Style = toolTipStyle };
        var surfaceRaised = (SolidColorBrush)Application.Current.FindResource("SurfaceRaised");
        var bg = Assert.IsType<SolidColorBrush>(tip.Background);
        Assert.Equal(surfaceRaised.Color, bg.Color);
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PiPlay;
using PiPlay.Controls;
using PiPlay.Services;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Wpf)]
public class SettingsWindowAppearanceTests : IDisposable
{
    [Fact]
    public void SettingsWindow_reflects_and_updates_theme_and_accent_input() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true, themeId: "soft-glass", accentColor: "#38D996",
            fadeIdleDelayMs: 4000);

        Assert.Equal("soft-glass", w.ThemeId);
        Assert.Equal("#38D996", w.AccentColor);
        Assert.Equal(4000, w.FadeIdleDelayMs);
        Assert.True(((ToggleButton)w.FindName("ThemeSoftGlassPreset")!).IsChecked);
        Assert.Equal("#38D996", ((AccentColorPicker)w.FindName("AccentPicker")!).SelectedColor);
        Assert.True(((ToggleButton)w.FindName("FadeDelayLongPreset")!).IsChecked);

        // Use the soft-glass default so the Sharp Dark switch below exercises the preset-default path.
        ((AccentColorPicker)w.FindName("AccentPicker")!).SelectedColor = "#9E84F0";
        ((ToggleButton)w.FindName("FadeDelayShortPreset")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        Assert.True(w.AppearanceChanged);
        Assert.Equal("#9E84F0", w.AccentColor);
        Assert.Equal(1500, w.FadeIdleDelayMs);
        Assert.Equal("#9E84F0", ((AccentColorPicker)w.FindName("AccentPicker")!).SelectedColor);
        Assert.True(((ToggleButton)w.FindName("FadeDelayShortPreset")!).IsChecked);

        ((ToggleButton)w.FindName("ThemeSharpDarkPreset")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal("sharp-dark", w.ThemeId);
        Assert.Equal(ThemeCatalog.DefaultAccentColor, w.AccentColor);
        Assert.Equal(ThemeCatalog.DefaultAccentColor, ((AccentColorPicker)w.FindName("AccentPicker")!).SelectedColor);
    });

    [Fact]
    public void SettingsWindow_reflects_and_updates_corner_style() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true, cornerStyle: "round");
        Assert.Equal("round", w.CornerStyle);
        Assert.True(((ToggleButton)w.FindName("CornerStyleRoundChip")!).IsChecked);
        Assert.Equal("theme", new SettingsWindow(isBrowserReady: true, cornerStyle: "bogus").CornerStyle);

        ((ToggleButton)w.FindName("CornerStyleSquareChip")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.True(w.AppearanceChanged);
        Assert.Equal("square", w.CornerStyle);
        Assert.True(((ToggleButton)w.FindName("CornerStyleSquareChip")!).IsChecked);
        Assert.False(((ToggleButton)w.FindName("CornerStyleRoundChip")!).IsChecked);
    });

    [Fact]
    public void SettingsWindow_preset_click_adopts_the_preset_defaults() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true, themeId: "sharp-dark",
            accentColor: ThemeCatalog.DefaultAccentColor,
            fadeIdleDelayMs: 1500, activeOpacityOverride: 1.0, idleOpacityOverride: 1.0,
            stripAutoHideOverride: true, cornerStyle: "square");

        ((ToggleButton)w.FindName("ThemeSoftGlassPreset")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        var preset = ThemeCatalog.PresetFor("soft-glass");
        Assert.Equal("soft-glass", w.ThemeId);
        Assert.Equal(preset.DefaultAccentColor, w.AccentColor);
        Assert.Equal(ThemeCatalog.DefaultCornerStyle, w.CornerStyle);
        Assert.True(((ToggleButton)w.FindName("CornerStyleThemeChip")!).IsChecked);
        Assert.Equal(ThemeCatalog.FadeDelayMillisecondsForPreset(preset.DefaultFadeDelayPreset), w.FadeIdleDelayMs);
        Assert.Null(w.StripAutoHideOverride);
        Assert.Null(w.ActiveOpacityOverride);
        Assert.Null(w.IdleOpacityOverride);
        Assert.Equal(preset.DefaultStripAutoHide, w.StripAutoHide);
        Assert.Equal(preset.DefaultStripAutoHide, ((ToggleButton)w.FindName("StripAutoHideToggle")!).IsChecked);
        Assert.Equal(preset.DefaultActiveWindowOpacity, w.ConstantWindowOpacity);
        Assert.Equal(preset.DefaultIdleWindowOpacity, w.IdleWindowOpacity);
        Assert.Equal(Math.Round(Math.Max(preset.DefaultActiveWindowOpacity, WindowOpacityPolicy.UiFloor) * 100.0),
            ((Slider)w.FindName("ActiveOpacitySlider")!).Value);
        Assert.True(w.AppearanceChanged);
    });

    [Fact]
    public void SettingsWindow_accent_only_change_keeps_behavior_on_preset_defaults() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true);

        ((AccentColorPicker)w.FindName("AccentPicker")!).SelectedColor = "#FFC857";

        Assert.True(w.AppearanceChanged);
        Assert.Null(w.ActiveOpacityOverride);
        Assert.Null(w.IdleOpacityOverride);
        Assert.Null(w.StripAutoHideOverride);

        ((Slider)w.FindName("IdleOpacitySlider")!).Value = 70;
        Assert.Equal(0.7, w.IdleOpacityOverride);
        Assert.Null(w.ActiveOpacityOverride);
        Assert.Null(w.StripAutoHideOverride);
    });

    [Fact]
    public void SettingsWindow_preset_click_preserves_a_custom_accent() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true, themeId: "sharp-dark", accentColor: "#FFC857");

        ((ToggleButton)w.FindName("ThemeSoftGlassPreset")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        Assert.Equal("soft-glass", w.ThemeId);
        Assert.Equal("#FFC857", w.AccentColor);
        Assert.Equal("#FFC857", ((AccentColorPicker)w.FindName("AccentPicker")!).SelectedColor);

        ((AccentColorPicker)w.FindName("AccentPicker")!).SelectedColor = "#9E84F0";
        ((ToggleButton)w.FindName("ThemeSharpDarkPreset")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(ThemeCatalog.DefaultAccentColor, w.AccentColor);
        Assert.Equal(ThemeCatalog.DefaultAccentColor, ((AccentColorPicker)w.FindName("AccentPicker")!).SelectedColor);
    });

    [Fact]
    public void SettingsWindow_previews_accent_live_and_gates_done() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true);
        Assert.False(w.AppearanceChanged);
        string? preview = null;
        w.AccentPreviewChanged += hex => preview = hex;

        var picker = (AccentColorPicker)w.FindName("AccentPicker")!;
        picker.SelectedColor = "#A78BFA";

        Assert.Equal("#A78BFA", preview);
        Assert.True(w.AppearanceChanged);
        Assert.Equal("#A78BFA", w.AccentColor);
        Assert.True(((Button)w.FindName("DoneButton")!).IsEnabled);

        picker.SelectedColor = "#787878";
        Assert.False(((Button)w.FindName("DoneButton")!).IsEnabled);
        Assert.Equal("#A78BFA", preview);
    });

    [Fact]
    public void SettingsWindow_done_button_is_the_primary_confirm() => StaTestThread.Invoke(() =>
    {
        var done = (Button)new SettingsWindow(isBrowserReady: true).FindName("DoneButton")!;
        Assert.Equal("Done", (string)done.Content);
        Assert.Same(Application.Current.FindResource("AccentButton"), done.Style);
        Assert.Equal("Done", System.Windows.Automation.AutomationProperties.GetName(done));
        Assert.True(done.IsDefault);
    });

    [Fact]
    public void SettingsWindow_done_button_dismisses_the_dialog() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true) { Opacity = 0, ShowActivated = false };
        var closed = false;
        w.Closed += (_, _) => closed = true;
        try
        {
            w.Show();
            ((AccentColorPicker)w.FindName("AccentPicker")!).SelectedColor = "#FFC857";
            Assert.True(w.AppearanceChanged);

            ((Button)w.FindName("DoneButton")!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.True(closed, "Clicking Done must dismiss the Settings dialog.");
        }
        finally
        {
            if (w.IsVisible) w.Close();
        }
    });

    public void Dispose() => StaTestThread.Invoke(() =>
    {
        foreach (var w in Application.Current.Windows.Cast<Window>().ToArray())
        {
            try { w.Close(); } catch { /* a never-shown window may resist closing; ignore */ }
        }
    });
}

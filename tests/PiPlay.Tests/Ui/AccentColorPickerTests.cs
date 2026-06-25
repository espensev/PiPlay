using System.Windows.Controls;
using PiPlay;
using PiPlay.Controls;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Wpf)]
public class AccentColorPickerTests
{
    [Fact]
    public void AccentColorPicker_accepts_valid_rgb_colors_and_defaults_invalid() => StaTestThread.Invoke(() =>
    {
        var picker = new AccentColorPicker { SelectedColor = "#38D996" };
        Assert.Equal("#38D996", picker.SelectedColor);
        Assert.True(picker.IsSelectedReadable);

        picker.SelectedColor = "#787878";
        Assert.True(picker.IsSelectedReadable);
        picker.UseNearestReadable();

        Assert.True(picker.IsSelectedReadable);
        Assert.Equal("#787878", picker.SelectedColor);

        picker.SelectedColor = "not-a-color";
        Assert.False(picker.IsSelectedReadable);
        picker.UseNearestReadable();
        Assert.Equal(ThemeCatalog.DefaultAccentColor, picker.SelectedColor);
    });

    [Fact]
    public void AccentColorPicker_raises_preview_for_valid_values_only() => StaTestThread.Invoke(() =>
    {
        var picker = new AccentColorPicker { SelectedColor = "#00D4FF" };
        string? last = null;
        picker.PreviewColorChanged += hex => last = hex;

        picker.SelectedColor = "#A78BFA";
        Assert.Equal("#A78BFA", last);

        picker.SelectedColor = "#787878";
        Assert.Equal("#787878", last);

        picker.SelectedColor = "not-a-color";
        Assert.Equal("#787878", last);
    });

    [Fact]
    public void AccentColorPicker_preserves_hue_and_saturation_through_a_brightness_round_trip() => StaTestThread.Invoke(() =>
    {
        var picker = new AccentColorPicker { SelectedColor = "#A78BFA" };
        var slider = (Slider)picker.FindName("ValueSlider")!;
        var (h0, _, _) = ColorMath.RgbToHsv(ThemeColors.ParseColor(picker.SelectedColor));

        slider.Value = 0.0;
        slider.Value = 1.0;

        var (h1, s1, _) = ColorMath.RgbToHsv(ThemeColors.ParseColor(picker.SelectedColor));
        Assert.True(s1 > 0.3, $"saturation collapsed to {s1:F2} after a brightness round trip");
        Assert.Equal(h0, h1, 0);
    });

    [Fact]
    public void AccentColorPicker_does_not_rewrite_the_channel_box_being_edited() => StaTestThread.Invoke(() =>
    {
        var picker = new AccentColorPicker { SelectedColor = "#00D4FF" };
        var r = (TextBox)picker.FindName("RInput")!;
        r.Text = "05";
        Assert.Equal("05", r.Text);
    });

    [Fact]
    public void AccentColorPicker_invalid_rgb_makes_profile_accent_unsaveable() => StaTestThread.Invoke(() =>
    {
        var picker = new AccentColorPicker { SelectedColor = "#A78BFA" };
        bool? readable = null;
        picker.ReadabilityChanged += value => readable = value;

        ((TextBox)picker.FindName("RInput")!).Text = "999";

        Assert.False(picker.IsSelectedReadable);
        Assert.False(readable);
        Assert.Equal("#A78BFA", picker.SelectedColor);
        Assert.False(Prompt.CanSaveProfileAccent(useProfileAccent: true, picker));
        Assert.True(Prompt.CanSaveProfileAccent(useProfileAccent: false, picker));
    });

    [Fact]
    public void AccentColorPicker_presets_match_the_catalog() => StaTestThread.Invoke(() =>
    {
        var picker = new AccentColorPicker();
        var presets = (System.Collections.IEnumerable)((ItemsControl)picker.FindName("PresetRow")!).ItemsSource;

        Assert.Equal(ThemeCatalog.AccentOptions, presets);
    });
}

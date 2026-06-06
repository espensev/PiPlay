using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PiPlay.Theme;

/// <summary>Applies a configurable checked-state brush to icon toggle buttons.</summary>
public static class ToggleAccent
{
    public static readonly DependencyProperty CheckedBrushProperty =
        DependencyProperty.RegisterAttached(
            "CheckedBrush",
            typeof(Brush),
            typeof(ToggleAccent),
            new FrameworkPropertyMetadata(null, OnCheckedBrushChanged));

    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached(
            "IsHooked",
            typeof(bool),
            typeof(ToggleAccent),
            new PropertyMetadata(false));

    public static Brush? GetCheckedBrush(DependencyObject obj) =>
        (Brush?)obj.GetValue(CheckedBrushProperty);

    public static void SetCheckedBrush(DependencyObject obj, Brush? value) =>
        obj.SetValue(CheckedBrushProperty, value);

    private static bool GetIsHooked(DependencyObject obj) =>
        (bool)obj.GetValue(IsHookedProperty);

    private static void SetIsHooked(DependencyObject obj, bool value) =>
        obj.SetValue(IsHookedProperty, value);

    private static void OnCheckedBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToggleButton toggle) return;
        EnsureHooked(toggle);
        Apply(toggle);
    }

    private static void EnsureHooked(ToggleButton toggle)
    {
        if (GetIsHooked(toggle)) return;
        toggle.Checked += ToggleStateChanged;
        toggle.Unchecked += ToggleStateChanged;
        toggle.Loaded += ToggleLoaded;
        SetIsHooked(toggle, true);
    }

    private static void ToggleLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggle) Apply(toggle);
    }

    private static void ToggleStateChanged(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggle) Apply(toggle);
    }

    private static void Apply(ToggleButton toggle)
    {
        if (toggle.IsChecked == true && GetCheckedBrush(toggle) is { } brush)
        {
            toggle.Foreground = brush;
            toggle.BorderBrush = brush;
        }
        else
        {
            toggle.ClearValue(Control.ForegroundProperty);
            toggle.ClearValue(Control.BorderBrushProperty);
        }
    }
}

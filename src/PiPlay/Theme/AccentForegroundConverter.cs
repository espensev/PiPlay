using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PiPlay.Theme;

/// <summary>Converts an arbitrary accent hex to the best dark/white foreground brush.</summary>
public sealed class AccentForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && ThemeCatalog.IsValidHex(hex))
        {
            var foreground = ThemeColors.PickReadableForeground(ThemeColors.ParseColor(hex));
            var brush = new SolidColorBrush(foreground);
            brush.Freeze();
            return brush;
        }

        return Application.Current.TryFindResource("TextPrimary") ?? Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

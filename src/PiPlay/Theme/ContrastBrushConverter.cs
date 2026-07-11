using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PiPlay.Theme;

/// <summary>
/// Converts a persisted RGB string plus a live adjacent-surface brush into a frozen, contrast-safe
/// presentation brush. Null/invalid profile colors intentionally render no identity rail.
/// </summary>
public sealed class ContrastBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string hex || !ThemeCatalog.IsValidHex(hex))
            return Brushes.Transparent;

        var adjacent = values[1] switch
        {
            SolidColorBrush brush => brush.Color,
            Color color => color,
            _ => (Color?)null,
        };
        return adjacent is Color surface
            ? ThemeColors.ContrastBrush(hex, surface)
            : Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        [Binding.DoNothing, Binding.DoNothing];
}

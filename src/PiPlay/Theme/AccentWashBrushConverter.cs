using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PiPlay.Theme;

/// <summary>
/// Converts a persisted RGB string plus the theme-published wash alpha into a frozen low-alpha
/// identity wash (2026-08-09 profile-backgrounds design: dropdown rows wear their OWN accent
/// behind the identity rail). Null/invalid accents render no wash — the plain row surface is the
/// fallback, matching <see cref="ContrastBrushConverter"/>'s contract for the rail.
/// </summary>
public sealed class AccentWashBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string hex || !ThemeCatalog.IsValidHex(hex) ||
            values[1] is not byte alpha)
        {
            return Brushes.Transparent;
        }

        var brush = new SolidColorBrush(
            ThemeColors.WithAlpha(ThemeColors.ParseColor(hex), alpha));
        brush.Freeze();
        return brush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        [Binding.DoNothing, Binding.DoNothing];
}

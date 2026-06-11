using System.Globalization;
using System.Windows.Media;

namespace PiPlay.Theme;

/// <summary>
/// Pure color helpers for the theme resource layer (overhaul Task 9). Turns a normalized accent hex
/// (<see cref="ThemeCatalog.NormalizeAccentColor"/>) into WPF brushes/colors and derives the lighter
/// hover variant. No WPF dispatcher affinity: <see cref="Color"/> is a struct and the math is
/// thread-agnostic; only <see cref="Brush"/> allocates a Freezable (frozen before return so it is
/// safe to share across the source window and popout).
/// </summary>
public static class ThemeColors
{
    /// <summary>Parse a <c>#RRGGBB</c> hex (the normalized accent form) into an opaque color.</summary>
    public static Color ParseColor(string? hex)
    {
        var value = ThemeCatalog.NormalizeAccentColor(hex)[1..];   // normalize guarantees 6 upper-hex
        var r = byte.Parse(value.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = byte.Parse(value.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = byte.Parse(value.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>Blend a color toward white by <paramref name="amount"/> (0..1) for a hover/light variant.</summary>
    public static Color Lighten(Color color, double amount)
    {
        var t = Math.Clamp(amount, 0.0, 1.0);
        return Color.FromRgb(
            Mix(color.R, 255, t),
            Mix(color.G, 255, t),
            Mix(color.B, 255, t));
    }

    /// <summary>A frozen accent brush built from a hex, for runtime application to icon toggles.</summary>
    public static SolidColorBrush Brush(string? hex)
    {
        var brush = new SolidColorBrush(ParseColor(hex));
        brush.Freeze();
        return brush;
    }

    private static byte Mix(byte from, byte to, double t) =>
        (byte)Math.Round(from + (to - from) * t);
}

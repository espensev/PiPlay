using System.Windows.Media;

namespace PiPlay.Theme;

/// <summary>RGB/HSV conversion for the accent wheel. Hue is 0..360, saturation/value 0..1.</summary>
public static class ColorMath
{
    public static (double H, double S, double V) RgbToHsv(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var h = 0.0;
        if (delta > 1e-9)
        {
            if (Math.Abs(max - r) < 1e-9)
                h = 60.0 * (((g - b) / delta) % 6.0);
            else if (Math.Abs(max - g) < 1e-9)
                h = 60.0 * (((b - r) / delta) + 2.0);
            else
                h = 60.0 * (((r - g) / delta) + 4.0);
        }

        if (h < 0.0) h += 360.0;
        var s = max <= 1e-9 ? 0.0 : delta / max;
        return (h, s, max);
    }

    public static Color HsvToRgb(double h, double s, double v)
    {
        h = ((h % 360.0) + 360.0) % 360.0;
        s = Math.Clamp(s, 0.0, 1.0);
        v = Math.Clamp(v, 0.0, 1.0);

        var c = v * s;
        var x = c * (1.0 - Math.Abs((h / 60.0 % 2.0) - 1.0));
        var m = v - c;

        var (r, g, b) = h switch
        {
            < 60.0 => (c, x, 0.0),
            < 120.0 => (x, c, 0.0),
            < 180.0 => (0.0, c, x),
            < 240.0 => (0.0, x, c),
            < 300.0 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return Color.FromRgb(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    private static byte ToByte(double channel) => (byte)Math.Round(Math.Clamp(channel, 0.0, 1.0) * 255.0);
}

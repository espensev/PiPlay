using System.Globalization;

namespace PiPlay.Tests;

/// <summary>WCAG 2.x relative-luminance contrast ratio from #AARRGGBB / #RRGGBB hex strings.</summary>
internal static class Wcag
{
    public static double ContrastRatio(string hexA, string hexB)
    {
        var (la, lb) = (Luminance(hexA), Luminance(hexB));
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 8) hex = hex.Substring(2); // drop alpha
        var r = Channel(hex.Substring(0, 2));
        var g = Channel(hex.Substring(2, 2));
        var b = Channel(hex.Substring(4, 2));
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double Channel(string twoHex)
    {
        var v = int.Parse(twoHex, NumberStyles.HexNumber) / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}

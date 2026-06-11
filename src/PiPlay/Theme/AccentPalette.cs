using System.Windows.Media;

namespace PiPlay.Theme;

/// <summary>
/// Derives the themed accent family (hover/pressed/dim/border/foreground) from one normalized
/// accent hex (UI overhaul Task 9). Pure and deterministic so the XAML defaults in
/// Theme/Colors.xaml can be pinned to <c>Derive(ThemeCatalog.DefaultAccentColor)</c> by tests.
/// Foreground picks white or black by WCAG contrast: the two ratios against any color multiply
/// to 21, so the better one is always at least sqrt(21) ~ 4.58:1 — safe for any future accent.
/// </summary>
public sealed record AccentPalette(
    Color Accent,
    Color Hover,
    Color Pressed,
    Color Dim,
    Color Border,
    Color Foreground)
{
    // Dim/Border mute the accent toward the dark surface; this mirrors the
    // Theme.SurfaceBaseColor default in Theme/Colors.xaml.
    private static readonly Color DimBase = Color.FromRgb(0x11, 0x13, 0x16);

    public static AccentPalette Derive(string accentHex)
    {
        var accent = ParseHex(ThemeCatalog.NormalizeAccentColor(accentHex));
        return new AccentPalette(
            Accent: accent,
            Hover: Mix(accent, Colors.White, 0.12),
            Pressed: Mix(accent, Colors.Black, 0.16),
            Dim: Mix(accent, DimBase, 0.55),
            Border: Mix(accent, DimBase, 0.30),
            Foreground: ContrastRatio(Colors.White, accent) >= ContrastRatio(Colors.Black, accent)
                ? Colors.White
                : Colors.Black);
    }

    /// <summary>WCAG 2.x contrast ratio (alpha ignored; both colors treated as opaque).</summary>
    public static double ContrastRatio(Color a, Color b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double RelativeLuminance(Color c) =>
        0.2126 * Linearize(c.R) + 0.7152 * Linearize(c.G) + 0.0722 * Linearize(c.B);

    private static double Linearize(byte channel)
    {
        var v = channel / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static Color Mix(Color from, Color to, double t) => Color.FromRgb(
        MixChannel(from.R, to.R, t),
        MixChannel(from.G, to.G, t),
        MixChannel(from.B, to.B, t));

    private static byte MixChannel(byte from, byte to, double t) =>
        (byte)Math.Round(from + (to - from) * t, MidpointRounding.AwayFromZero);

    private static Color ParseHex(string normalizedHex) => Color.FromRgb(
        Convert.ToByte(normalizedHex.Substring(1, 2), 16),
        Convert.ToByte(normalizedHex.Substring(3, 2), 16),
        Convert.ToByte(normalizedHex.Substring(5, 2), 16));
}

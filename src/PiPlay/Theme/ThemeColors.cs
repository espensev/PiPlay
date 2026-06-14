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

    /// <summary>Blend two colors channel-wise by <paramref name="amount"/> (0..1, clamped).</summary>
    public static Color Mix(Color from, Color to, double amount)
    {
        var t = Math.Clamp(amount, 0.0, 1.0);
        return Color.FromRgb(Mix(from.R, to.R, t), Mix(from.G, to.G, t), Mix(from.B, to.B, t));
    }

    /// <summary>The same RGB at a new alpha — for the subtle/glow accent overlay tokens.</summary>
    public static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    /// <summary>
    /// WCAG 2.x relative-luminance contrast ratio between two opaque colors. Deliberately the SAME
    /// formula as the test oracle (<c>tests/PiPlay.Tests/Infrastructure/Wcag.cs</c>) so the runtime
    /// foreground choice agrees with the contrast gates that police it.
    /// </summary>
    public static double ContrastRatio(Color a, Color b)
    {
        var (la, lb) = (RelativeLuminance(a), RelativeLuminance(b));
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double RelativeLuminance(Color c) =>
        0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

    private static double Channel(byte v)
    {
        var s = v / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    /// <summary>The dark button-text foreground used on light accents (theme-v2 spec).</summary>
    private static readonly Color DarkForeground = Color.FromRgb(0x06, 0x14, 0x1A);

    /// <summary>
    /// Pick the foreground that reads on <paramref name="accent"/> at WCAG AA (>= 4.5:1): the dark
    /// button text if it clears the bar, else white. FAIL-CLOSED — if neither candidate reaches
    /// 4.5:1 it throws rather than returning a sub-threshold fallback (theme-v2 spec; review TG-4).
    /// </summary>
    public static Color PickReadableForeground(Color accent)
    {
        if (ContrastRatio(DarkForeground, accent) >= 4.5) return DarkForeground;
        if (ContrastRatio(Colors.White, accent) >= 4.5) return Colors.White;
        throw new InvalidOperationException(
            $"Accent #{accent.R:X2}{accent.G:X2}{accent.B:X2} has no WCAG-AA foreground: neither dark nor white reaches 4.5:1.");
    }

    /// <summary>The theme-v2 accent derivation profile for a theme id (spec "Suggested profiles").</summary>
    public static ThemeAccentProfile AccentProfileFor(string? themeId) =>
        ThemeCatalog.NormalizeThemeId(themeId) switch
        {
            "minimal"    => new ThemeAccentProfile(0.22, 0.14, 0.50, 0.12, 0x26, 0x40),
            "soft-glass" => new ThemeAccentProfile(0.30, 0.12, 0.40, 0.16, 0x33, 0x66),
            _            => new ThemeAccentProfile(0.18, 0.16, 0.58, 0.10, 0x22, 0x33),   // sharp-dark / default
        };

    /// <summary>
    /// Derive the accent state tokens for one base accent under a theme (theme-v2 "Derivation
    /// algorithm"). Each theme reads the same base accent differently via its
    /// <see cref="ThemeAccentProfile"/> and its own raised surface.
    /// </summary>
    public static DerivedAccentSet DeriveAccentSet(string? baseAccent, ThemePreset preset)
    {
        var profile = AccentProfileFor(preset.Id);
        var primary = ParseColor(baseAccent);
        var surfaceRaised = ParseColor(preset.Palette.SurfaceRaised);

        var hover = Mix(primary, Colors.White, profile.HoverWhiteMix);
        var pressed = Mix(primary, Colors.Black, profile.PressedBlackMix);
        var muted = Mix(primary, surfaceRaised, profile.MutedSurfaceMix);
        var border = Mix(primary, Colors.White, profile.BorderWhiteMix);
        var subtle = WithAlpha(primary, profile.SubtleAlpha);
        var glow = WithAlpha(primary, profile.GlowAlpha);
        var onAccent = PickReadableForeground(primary);
        // CON-1: re-pick the foreground against the DARKER pressed fill, not reuse OnAccent — a dim
        // accent (steel) drops below 4.5:1 when pressed, so this flips it to white. Fail-closed.
        var onAccentPressed = PickReadableForeground(pressed);

        return new DerivedAccentSet(primary, hover, pressed, muted, border, subtle, glow, onAccent, onAccentPressed);
    }
}

/// <summary>
/// Per-theme recipe for turning one base accent into the derived accent state tokens (theme-v2
/// spec "Derivation algorithm"). The same base accent reads differently per theme because each
/// preset mixes it toward white/black/its own raised surface by different amounts.
/// </summary>
public sealed record ThemeAccentProfile(
    double HoverWhiteMix,
    double PressedBlackMix,
    double MutedSurfaceMix,
    double BorderWhiteMix,
    byte SubtleAlpha,
    byte GlowAlpha);

/// <summary>
/// The derived accent state colors for one (base accent x theme) pairing. A pure value set — the
/// spec forbids persisting derived colors; the applier turns these into frozen brushes at apply time.
/// </summary>
public sealed record DerivedAccentSet(
    Color Primary,
    Color Hover,
    Color Pressed,
    Color Muted,
    Color Border,
    Color Subtle,
    Color Glow,
    Color OnAccent,
    Color OnAccentPressed);

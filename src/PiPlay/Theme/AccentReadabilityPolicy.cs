using System.Windows.Media;

namespace PiPlay.Theme;

public enum AccentGate
{
    None,
    OnAccent,
    OnAccentPressed,
    Border,
    GlyphOnHover,
    Invalid,
}

public sealed record AccentReadability(bool IsReadable, AccentGate FailingGate);

/// <summary>Single policy for arbitrary accent hex values before they reach the fail-closed pipeline.</summary>
public static class AccentReadabilityPolicy
{
    private const double TextMinimum = 4.5;
    private const double UiMinimum = 3.0;

    public static AccentReadability Evaluate(string? hex)
    {
        if (!ThemeCatalog.IsValidHex(hex)) return new AccentReadability(false, AccentGate.Invalid);
        var normalized = ThemeCatalog.NormalizeAccentColor(hex);

        foreach (var preset in ThemeCatalog.Presets)
        {
            var gate = FirstFailingGate(normalized, preset);
            if (gate != AccentGate.None) return new AccentReadability(false, gate);
        }

        return new AccentReadability(true, AccentGate.None);
    }

    public static string NearestReadable(string? hex)
    {
        if (!ThemeCatalog.IsValidHex(hex)) return ThemeCatalog.DefaultAccentColor;
        var normalized = ThemeCatalog.NormalizeAccentColor(hex);
        if (Evaluate(normalized).IsReadable) return normalized;

        var (h, s, v) = ColorMath.RgbToHsv(ThemeColors.ParseColor(normalized));
        for (var nextV = v; nextV <= 1.0 + 1e-9; nextV += 0.04)
        {
            var candidate = Hex(ColorMath.HsvToRgb(h, s, Math.Min(nextV, 1.0)));
            if (Evaluate(candidate).IsReadable) return candidate;
        }

        for (var nextS = s; nextS >= -1e-9; nextS -= 0.04)
        {
            var candidate = Hex(ColorMath.HsvToRgb(h, Math.Max(nextS, 0.0), 1.0));
            if (Evaluate(candidate).IsReadable) return candidate;
        }

        return NearestPresetByHue(h);
    }

    private static AccentGate FirstFailingGate(string hex, ThemePreset preset)
    {
        DerivedAccentSet set;
        try
        {
            set = ThemeColors.DeriveAccentSet(hex, preset);
        }
        catch (InvalidOperationException)
        {
            return AccentGate.OnAccent;
        }

        if (ThemeColors.ContrastRatio(set.OnAccent, set.Primary) < TextMinimum) return AccentGate.OnAccent;
        if (ThemeColors.ContrastRatio(set.OnAccent, set.Hover) < TextMinimum) return AccentGate.OnAccent;
        if (ThemeColors.ContrastRatio(set.OnAccentPressed, set.Pressed) < TextMinimum) return AccentGate.OnAccentPressed;

        var surfaceBase = ThemeColors.ParseColor(preset.Palette.SurfaceBase);
        var surfaceRaised = ThemeColors.ParseColor(preset.Palette.SurfaceRaised);
        var surfaceHover = ThemeColors.ParseColor(preset.Palette.SurfaceHover);
        if (ThemeColors.ContrastRatio(set.Border, surfaceBase) < UiMinimum) return AccentGate.Border;
        if (ThemeColors.ContrastRatio(set.Border, surfaceRaised) < UiMinimum) return AccentGate.Border;
        if (ThemeColors.ContrastRatio(set.Primary, surfaceHover) < UiMinimum) return AccentGate.GlyphOnHover;

        return AccentGate.None;
    }

    private static string NearestPresetByHue(double hue)
    {
        var best = ThemeCatalog.DefaultAccentColor;
        var bestDelta = double.MaxValue;
        foreach (var option in ThemeCatalog.AccentOptions)
        {
            var (optionHue, _, _) = ColorMath.RgbToHsv(ThemeColors.ParseColor(option.HexColor));
            var delta = Math.Abs(((optionHue - hue + 540.0) % 360.0) - 180.0);
            if (delta >= bestDelta) continue;
            bestDelta = delta;
            best = option.HexColor;
        }

        return best;
    }

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}

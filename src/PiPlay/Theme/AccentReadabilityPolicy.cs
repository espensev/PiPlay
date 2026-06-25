namespace PiPlay.Theme;

public enum AccentGate
{
    None,
    Invalid,
}

public sealed record AccentReadability(bool IsReadable, AccentGate FailingGate);

/// <summary>Single policy for arbitrary accent hex values before they reach the theme pipeline.</summary>
public static class AccentReadabilityPolicy
{
    public static AccentReadability Evaluate(string? hex)
    {
        if (!ThemeCatalog.IsValidHex(hex)) return new AccentReadability(false, AccentGate.Invalid);
        return new AccentReadability(true, AccentGate.None);
    }

    public static string NearestReadable(string? hex)
    {
        if (!ThemeCatalog.IsValidHex(hex)) return ThemeCatalog.DefaultAccentColor;
        return ThemeCatalog.NormalizeAccentColor(hex);
    }
}

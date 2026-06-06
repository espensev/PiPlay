namespace PiPlay.Services;

public sealed record AccentOption(string Key, string DisplayName, string BrushResourceKey);

public sealed record FadeDelayOption(string Key, string DisplayName, int Milliseconds);

/// <summary>Allowed Popout Player appearance settings and safe defaults.</summary>
public static class PlayerAppearancePolicy
{
    public const string DefaultAccent = "cyan";
    public const int DefaultFadeIdleDelayMs = FadePolicy.IdleDelayMs;

    private static readonly AccentOption[] AccentOptionsValue =
    [
        new("cyan", "Cyan", "AccentCyan"),
        new("violet", "Violet", "AccentViolet"),
        new("green", "Green", "AccentGreen"),
        new("amber", "Amber", "AccentAmber"),
    ];

    private static readonly FadeDelayOption[] FadeDelayOptionsValue =
    [
        new("short", "Short", 1500),
        new("normal", "Normal", DefaultFadeIdleDelayMs),
        new("long", "Long", 4000),
    ];

    public static IReadOnlyList<AccentOption> AccentOptions => AccentOptionsValue;

    public static IReadOnlyList<FadeDelayOption> FadeDelayOptions => FadeDelayOptionsValue;

    public static string NormalizeAccent(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return DefaultAccent;
        var normalized = key.Trim();
        return AccentOptionsValue.Any(o => string.Equals(o.Key, normalized, StringComparison.OrdinalIgnoreCase))
            ? normalized.ToLowerInvariant()
            : DefaultAccent;
    }

    public static string BrushResourceKeyFor(string? key)
    {
        var normalized = NormalizeAccent(key);
        return AccentOptionsValue.First(o => o.Key == normalized).BrushResourceKey;
    }

    public static string DisplayNameForAccent(string? key)
    {
        var normalized = NormalizeAccent(key);
        return AccentOptionsValue.First(o => o.Key == normalized).DisplayName;
    }

    public static int NormalizeFadeIdleDelayMs(int milliseconds) =>
        FadeDelayOptionsValue.Any(o => o.Milliseconds == milliseconds)
            ? milliseconds
            : DefaultFadeIdleDelayMs;

    public static FadeDelayOption FadeDelayOptionFor(int milliseconds)
    {
        var normalized = NormalizeFadeIdleDelayMs(milliseconds);
        return FadeDelayOptionsValue.First(o => o.Milliseconds == normalized);
    }
}

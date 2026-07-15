namespace PiPlay.Services;

/// <summary>Visual presentation used by a Popout Player.</summary>
public enum PopoutPresentation
{
    /// <summary>The ordinary YouTube watch page and its native layout.</summary>
    Standard,

    /// <summary>The watch-page player expanded into a no-crop, media-first surface with PiPlay controls.</summary>
    Focused,
}

/// <summary>
/// Pure resolution of the Popout Player presentation. This is intentionally independent from
/// <see cref="PlaybackMode"/>: both Standard and Focused presentation keep Normal watch-page
/// playback, while the dormant Compact mode remains a separate playback architecture.
/// </summary>
public static class PopoutPresentationPolicy
{
    /// <summary>Durable <see cref="Models.Profile.Presentation"/> token that forces Standard.</summary>
    public const string ProfilePresentationStandard = "standard";

    /// <summary>Durable <see cref="Models.Profile.Presentation"/> token that forces Focused.</summary>
    public const string ProfilePresentationFocused = "focused";

    /// <summary>
    /// Normalize a stored profile override to the durable vocabulary. Null/blank means use the
    /// global default; an unknown value is repaired to null so it cannot silently opt into an
    /// invasive presentation.
    /// </summary>
    public static string? NormalizeProfilePresentation(string? presentation)
    {
        if (string.IsNullOrWhiteSpace(presentation)) return null;
        return presentation.Trim().ToLowerInvariant() switch
        {
            ProfilePresentationStandard => ProfilePresentationStandard,
            ProfilePresentationFocused => ProfilePresentationFocused,
            _ => null,
        };
    }

    /// <summary>
    /// Resolve the effective presentation for a new Popout Player. A valid profile override wins
    /// per field (REQ-PROFILE-01); otherwise the global Focused preference supplies the default.
    /// </summary>
    public static PopoutPresentation ResolveEffectivePresentation(
        string? profilePresentation, bool globalFocused) =>
        NormalizeProfilePresentation(profilePresentation) switch
        {
            ProfilePresentationStandard => PopoutPresentation.Standard,
            ProfilePresentationFocused => PopoutPresentation.Focused,
            _ => globalFocused ? PopoutPresentation.Focused : PopoutPresentation.Standard,
        };

    /// <summary>
    /// Scope a profile presentation override to that profile's own concrete video. A stale profile
    /// selection, playlist-only profile, or unrelated target falls back to the global preference.
    /// </summary>
    public static string? ResolveProfileOverride(
        string? profilePresentation, string? profileVideoId, string? targetVideoId)
    {
        var normalized = NormalizeProfilePresentation(profilePresentation);
        if (normalized is null || string.IsNullOrEmpty(targetVideoId)) return null;
        return string.Equals(profileVideoId, targetVideoId, StringComparison.Ordinal)
            ? normalized
            : null;
    }
}

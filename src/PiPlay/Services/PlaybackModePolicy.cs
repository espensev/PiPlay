using PiPlay.Models;

namespace PiPlay.Services;

/// <summary>Effective playback surface for a Popout Player (spec 10).</summary>
public enum PlaybackMode
{
    /// <summary>Mode A — full YouTube page in the Popout Player (spec 10.1). Default and fallback.</summary>
    Normal,
    /// <summary>Mode B — embedded YouTube player in the Popout Player (spec 10.2). Opt-in, Phase 3.</summary>
    Compact,
}

/// <summary>
/// Pure resolution of PiPlay's playback mode (spec 10): Normal YouTube page mode (default,
/// fallback) versus Compact embedded-player mode (Phase 3, opt-in). Owns the durable profile-mode
/// vocabulary, the global/profile precedence (REQ-PROFILE-01), and the mode-specific minimum
/// window sizes. No UI, settings I/O, or WebView dependencies, so it is trivially unit-testable.
/// </summary>
public static class PlaybackModePolicy
{
    /// <summary>Durable <see cref="Models.Profile.Mode"/> value that forces Normal page mode.</summary>
    public const string ProfileModeNormal = "normal";
    /// <summary>Durable <see cref="Models.Profile.Mode"/> value that forces Compact embedded mode.</summary>
    public const string ProfileModeCompact = "compact";

    // Normal page mode stays usable at the spec 10.1 / 16.1 minimum.
    public const int NormalMinWidth = 320;
    public const int NormalMinHeight = 180;

    // Compact embed/IFrame mode needs a larger floor so the embedded player controls stay usable
    // (spec 10.2: do not reuse the 320x180 normal minimum for embed mode; prefer at least 480x270).
    public const int CompactMinWidth = 480;
    public const int CompactMinHeight = 270;

    /// <summary>
    /// Normalize a stored/edited profile mode to the durable vocabulary: <c>null</c> (use the
    /// global default), <see cref="ProfileModeNormal"/>, or <see cref="ProfileModeCompact"/>. The
    /// legacy/internal <c>"embed"</c> token normalizes to <see cref="ProfileModeCompact"/>
    /// (<see cref="Models.Profile.Mode"/> reserved <c>"embed"</c> before the durable name was
    /// settled). Any other value sanitizes to <c>null</c> so an unknown mode falls back to the
    /// global default instead of crashing or forcing an unintended surface. Case-insensitive.
    /// </summary>
    public static string? NormalizeProfileMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return null;
        return mode.Trim().ToLowerInvariant() switch
        {
            ProfileModeNormal => ProfileModeNormal,
            ProfileModeCompact => ProfileModeCompact,
            "embed" => ProfileModeCompact,   // backward-compatible alias
            _ => null,
        };
    }

    /// <summary>
    /// Resolve the effective playback mode for a popout: a profile override wins per field
    /// (REQ-PROFILE-01); an unset (or unknown) profile mode falls back to the global compact
    /// preference. Mirrors <c>profile.Mode ?? global PlayerSettings.CompactMode</c>.
    /// </summary>
    public static PlaybackMode ResolveEffectiveMode(string? profileMode, bool globalCompact) =>
        NormalizeProfileMode(profileMode) switch
        {
            ProfileModeNormal => PlaybackMode.Normal,
            ProfileModeCompact => PlaybackMode.Compact,
            _ => globalCompact ? PlaybackMode.Compact : PlaybackMode.Normal,
        };

    /// <summary>
    /// Master switch for the embed Compact player. Dropped 2026-06: the embedded IFrame breaks on
    /// embed-disabled videos for near-zero visible gain (owner review §4). False = new popouts are
    /// always Normal. The Compact path (ResolveEffectiveMode, the shell/IFrame, Profile.Mode) is kept
    /// DORMANT behind this flag rather than deleted, because the shell is the compact timestamp source.
    /// </summary>
    public const bool CompactPlayerEnabled = false;

    /// <summary>
    /// Effective popout mode honoring <see cref="CompactPlayerEnabled"/>: Normal whenever Compact is
    /// disabled, otherwise the profile/global resolution. The popout-creation path calls THIS, not
    /// <see cref="ResolveEffectiveMode"/> directly.
    /// </summary>
    public static PlaybackMode ResolveEffectivePopoutMode(string? profileMode, bool globalCompact) =>
        CompactPlayerEnabled ? ResolveEffectiveMode(profileMode, globalCompact) : PlaybackMode.Normal;

    /// <summary>Minimum window width (DIP) for the given mode (spec 10.2 / 16.1).</summary>
    public static int MinWidthFor(PlaybackMode mode) =>
        mode == PlaybackMode.Compact ? CompactMinWidth : NormalMinWidth;

    /// <summary>Minimum window height (DIP) for the given mode (spec 10.2 / 16.1).</summary>
    public static int MinHeightFor(PlaybackMode mode) =>
        mode == PlaybackMode.Compact ? CompactMinHeight : NormalMinHeight;

    /// <summary>
    /// Whether the given mode reads the return timestamp by polling the YouTube page DOM
    /// (<see cref="YouTubeDomBridge"/>). True for Normal page mode only; Compact mode reads it from
    /// the shell bridge (IFrame API) instead, so exactly one source drives the timestamp (spec 10.3).
    /// The Popout Player consults this to decide whether to run its DOM sync timer.
    /// </summary>
    public static bool UsesDomSyncTimer(PlaybackMode mode) => mode == PlaybackMode.Normal;

    /// <summary>
    /// Build the popout navigation URL for the resolved mode (spec 10): compact navigates to the
    /// local PiPlay shell (<see cref="YouTubeUrlHelper.BuildShellUrl"/>, which hosts the YouTube
    /// IFrame API player); normal uses the full watch page (<see cref="YouTubeUrlHelper.BuildWatchUrl"/>).
    /// The shell base is injected (the host owns the virtual-host name) so this stays a pure,
    /// unit-testable join of mode + target and the MainWindow popout path is a thin caller.
    /// </summary>
    public static string BuildPopoutUrl(
        PlaybackMode mode, YouTubeTarget target, int? seconds, string compactShellBaseUrl) =>
        mode == PlaybackMode.Compact
            ? YouTubeUrlHelper.BuildShellUrl(target, seconds, compactShellBaseUrl)
            : YouTubeUrlHelper.BuildWatchUrl(target, seconds);

    /// <summary>
    /// The profile's playback-mode override, applied only when the popout target IS that profile's
    /// own video. REQ-PROFILE-01 governs the per-field precedence (a profile field overrides the
    /// global default); this video-id match is an extra guard that stops a stale combo selection
    /// from applying a profile's compact preference to an unrelated video. Returns the durable mode
    /// token when the (non-null) <paramref name="profileMode"/> applies, else null so the global
    /// default takes over. <paramref name="profileVideoId"/> is the video id parsed from the
    /// profile's URL (null for a playlist-only or unparseable profile URL, which then never matches
    /// a concrete target).
    /// </summary>
    public static string? ResolveProfileOverride(string? profileMode, string? profileVideoId, string? targetVideoId)
    {
        if (profileMode is null || string.IsNullOrEmpty(targetVideoId)) return null;
        return profileVideoId == targetVideoId ? profileMode : null;
    }
}

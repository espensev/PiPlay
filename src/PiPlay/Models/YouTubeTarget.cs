namespace PiPlay.Models;

/// <summary>
/// Result of parsing a YouTube URL (produced by YouTubeUrlHelper). Carries whatever was
/// found plus an optional FallbackReason used for the playlist mix/radio degrade case
/// (spec 12.4, 22.1) so the UI can show a non-blocking note without failing the popout.
/// </summary>
public sealed class YouTubeTarget
{
    public string? VideoId { get; set; }
    public string? PlaylistId { get; set; }

    /// <summary>Start offset in seconds. Nullable; 0 is a valid, known offset.</summary>
    public int? StartSeconds { get; set; }

    /// <summary>True for a playlist?list= page with no specific video.</summary>
    public bool IsPlaylistOnly { get; set; }

    /// <summary>Non-null when the target was degraded (e.g. a mix/radio list was dropped).</summary>
    public string? FallbackReason { get; set; }

    public bool IsValid => !string.IsNullOrEmpty(VideoId) || !string.IsNullOrEmpty(PlaylistId);
}

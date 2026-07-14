using PiPlay.Models;

namespace PiPlay.Services;

/// <summary>
/// Selects the playback target for a Video Popout without letting a stale YouTube SPA canonical
/// replace the URL the Source Window is visibly on. Pure so the identity rule can be regression-tested
/// without WebView2.
/// </summary>
public static class PopoutTargetResolver
{
    public static YouTubeTarget? Resolve(string? currentSource, string? canonical)
    {
        var hasSource = YouTubeUrlHelper.TryParse(currentSource, out var fromSource);
        if (hasSource && !string.IsNullOrEmpty(fromSource.VideoId)) return fromSource;

        var hasCanonical = YouTubeUrlHelper.TryParse(canonical, out var fromCanonical);
        if (hasCanonical && !string.IsNullOrEmpty(fromCanonical.VideoId)) return fromCanonical;

        // Preserve the older playlist/fallback behavior when neither surface exposes a video id.
        if (hasSource) return fromSource;
        return hasCanonical ? fromCanonical : null;
    }
}

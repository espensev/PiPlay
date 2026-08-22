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

    /// <summary>
    /// Overlay the first playable item read off a playlist browse page onto a playlist-only target
    /// (a playlist-only launch starts a playable item). Only the item's video id is
    /// adopted — the target keeps its own playlist id, so a page-provided link pointing into another
    /// list (or anywhere else) can at worst start a different first video, never redirect the launch.
    /// The id itself is charset-validated by <see cref="YouTubeUrlHelper.TryParse"/>; the raw link is
    /// never navigated. Returns the target unchanged when it is not playlist-only, already has a
    /// video, or the link doesn't parse to a video — the popout then degrades to the playlist page.
    /// </summary>
    public static YouTubeTarget WithFirstPlaylistItem(YouTubeTarget target, string? firstItemUrl)
    {
        if (!target.IsPlaylistOnly || !string.IsNullOrEmpty(target.VideoId)) return target;
        if (!YouTubeUrlHelper.TryParse(firstItemUrl, out var item) || string.IsNullOrEmpty(item.VideoId))
            return target;

        return new YouTubeTarget
        {
            VideoId = item.VideoId,
            PlaylistId = target.PlaylistId,
            StartSeconds = target.StartSeconds,
            IsPlaylistOnly = true,
            FallbackReason = target.FallbackReason,
        };
    }

    /// <summary>
    /// A DOM read yields back to the dispatcher, so the YouTube SPA can advance the Source underneath
    /// it (autoplay-next, or a click landing in that instant). Honoring the captured identity blindly
    /// would pop video/playlist A while the Source has already moved to B. For a captured video, an
    /// address with no video id is not positive evidence of a move because hidden-address surfaces are
    /// why Auto carries a captured identity. A playlist-only page always exposes its list in the Source
    /// URL, so it stands only while the live address remains a playlist-only page with the same list.
    ///
    /// So the captured identity stands until the LIVE address positively contradicts it. An address with
    /// no video id is NOT evidence of a move — pages whose URL hides the id are the whole reason the
    /// caller captured an identity instead of re-resolving — so it keeps the captured target rather than
    /// abandoning the launch.
    /// </summary>
    public static bool CapturedTargetStillMatchesSource(YouTubeTarget? captured, string? liveSource)
    {
        if (captured is null) return false;

        if (captured.IsPlaylistOnly)
        {
            if (string.IsNullOrEmpty(captured.PlaylistId)
                || !YouTubeUrlHelper.TryParse(liveSource, out var livePlaylist)
                || !livePlaylist.IsPlaylistOnly)
                return false;

            return string.Equals(captured.PlaylistId, livePlaylist.PlaylistId, StringComparison.Ordinal);
        }

        if (string.IsNullOrEmpty(captured.VideoId)) return false;

        if (!YouTubeUrlHelper.TryParse(liveSource, out var live) || string.IsNullOrEmpty(live.VideoId))
            return true;

        return string.Equals(captured.VideoId, live.VideoId, StringComparison.Ordinal);
    }
}

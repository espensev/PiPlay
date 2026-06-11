using PiPlay.Models;

namespace PiPlay.Services;

/// <summary>What the Popout Player does with a new-window request (overhaul Task 3).</summary>
public enum PopoutNewWindowAction
{
    /// <summary>A playable YouTube target: retarget THIS player in place (ADR-0005: never a second window).</summary>
    RetargetInPlace,

    /// <summary>Everything else: hand off to the system browser.</summary>
    OpenExternal,
}

/// <summary>
/// Pure new-window decision for the Popout Player. WebView2's NewWindowRequested exposes no
/// window-open disposition, so a left-click on a target=_blank recommendation and an explicit
/// "open in new window" are indistinguishable — the only workable gate is the target's URL shape.
/// A parsed YouTube target WITH a video id stays in the player (compact recommendations keep
/// playing in PiPlay); anything else — channels, search, playlist-only pages, non-YouTube — goes
/// external. Deliberately TryParse-with-VideoId rather than <see cref="NavigationPolicy.IsAllowed"/>:
/// the allowlist admits whole-site YouTube URLs that the compact shell cannot host.
/// </summary>
public static class PopoutNavigationPolicy
{
    public static PopoutNewWindowAction DecideNewWindow(string? uri, out YouTubeTarget? target)
    {
        if (YouTubeUrlHelper.TryParse(uri, out var parsed) && !string.IsNullOrEmpty(parsed.VideoId))
        {
            target = parsed;
            return PopoutNewWindowAction.RetargetInPlace;
        }

        target = null;
        return PopoutNewWindowAction.OpenExternal;
    }
}

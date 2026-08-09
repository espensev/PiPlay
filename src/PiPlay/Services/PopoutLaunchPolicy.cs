using PiPlay.Models;

namespace PiPlay.Services;

/// <summary>Fail-closed preconditions that must hold before Popout construction can begin.</summary>
internal static class PopoutLaunchPolicy
{
    /// <summary>
    /// A target the popout can launch: a concrete video, or a playlist page whose first playable
    /// item the popout will start (spec 13.1 / 22.1). Anything else keeps the "open a video first"
    /// prompt.
    /// </summary>
    public static bool IsLaunchableTarget(YouTubeTarget? target) => target is { IsValid: true };

    /// <summary>
    /// Whether the acknowledged-suppression contract applies to this launch. A concrete video is
    /// audibly playing on the source, so an unacknowledged suppression must abort (Q-1, fail
    /// closed). A playlist page has no guaranteed video element — there "no video found" is a
    /// legitimate outcome, so suppression is best-effort (a miniplayer, when present, still gets
    /// suppressed by the same script).
    /// </summary>
    public static bool RequiresAcknowledgedSuppression(YouTubeTarget target) =>
        !string.IsNullOrEmpty(target.VideoId);

    /// <summary>
    /// Transfer playback ownership away from Source or throw into MainWindow's existing rollback
    /// path. Code after this await is unreachable when suppression was not acknowledged.
    /// </summary>
    public static async Task RequireAcknowledgedSourceSuppressionAsync(
        Func<Task<bool>> suppressSourceAsync)
    {
        ArgumentNullException.ThrowIfNull(suppressSourceAsync);
        if (!await suppressSourceAsync())
        {
            throw new InvalidOperationException(
                "Source playback suppression was not acknowledged; Popout launch was aborted.");
        }
    }
}

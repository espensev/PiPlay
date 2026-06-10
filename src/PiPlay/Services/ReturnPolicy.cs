namespace PiPlay.Services;

/// <summary>What the Source Window should do on return from the Popout Player (spec 14).</summary>
public enum ReturnAction
{
    /// <summary>Do nothing: timestamp unknown and the source was paused at popout.</summary>
    None,
    /// <summary>Resume playback at the current position: timestamp unknown but the source was playing.</summary>
    Play,
    /// <summary>Seek to the last-known timestamp and stay paused.</summary>
    Seek,
    /// <summary>Seek to the last-known timestamp and resume.</summary>
    SeekAndPlay,
    /// <summary>The popout ended on a DIFFERENT video: navigate the source to it (timestamp rides
    /// the watch URL; YouTube's own watch-page playback behavior applies).</summary>
    Navigate,
}

/// <summary>
/// Pure decision for REQ-RETURN-01, extracted from <see cref="MainWindow"/> so it is testable
/// without WebView2. Resume only if the source was playing when popout started; treat a null
/// <paramref name="lastKnownSeconds"/> as "unknown" (0 is a valid timestamp, not unknown).
/// </summary>
public static class ReturnPolicy
{
    public static ReturnAction Decide(int? lastKnownSeconds, bool sourceWasPlaying)
    {
        if (lastKnownSeconds is not null)
            return sourceWasPlaying ? ReturnAction.SeekAndPlay : ReturnAction.Seek;
        return sourceWasPlaying ? ReturnAction.Play : ReturnAction.None;
    }

    /// <summary>
    /// Video-aware overload (overhaul Task 3): when the player reports it ended on a DIFFERENT
    /// video than the one popped out, the source must NAVIGATE there — seeking the original video
    /// to the new video's timestamp is the corruption this fixes. Either id unknown (null/empty)
    /// falls back to the timestamp-only decision above, preserving pre-Task-3 behavior.
    /// </summary>
    public static ReturnAction Decide(
        int? lastKnownSeconds, bool sourceWasPlaying, string? returnedVideoId, string? sourceVideoIdAtPopout)
    {
        if (!string.IsNullOrEmpty(returnedVideoId) && !string.IsNullOrEmpty(sourceVideoIdAtPopout) &&
            !string.Equals(returnedVideoId, sourceVideoIdAtPopout, StringComparison.Ordinal))
        {
            return ReturnAction.Navigate;
        }
        return Decide(lastKnownSeconds, sourceWasPlaying);
    }
}

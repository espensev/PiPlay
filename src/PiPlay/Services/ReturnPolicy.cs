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
}

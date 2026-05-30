namespace PiPlay.Models;

/// <summary>
/// State reported by the Popout Player back to the Source Window when it closes (spec 14).
/// The Source Window owns <c>sourceWasPlayingAtPopout</c> (captured before pausing) and
/// uses it together with <see cref="LastKnownSeconds"/> to honor REQ-RETURN-01.
/// </summary>
public sealed class PlayerReturnState
{
    /// <summary>Last known player timestamp. Nullable; 0 is valid and distinct from unknown.</summary>
    public int? LastKnownSeconds { get; set; }

    public bool Topmost { get; set; }
    public PlacementData? Placement { get; set; }
}

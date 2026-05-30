namespace PiPlay.Models;

/// <summary>
/// A named saved launch target (spec 17). Nullable fields fall back to the global
/// default per field; non-null fields override it (REQ-PROFILE-01). MVP uses Name + Url;
/// the other fields are carried for Phase 2 profile editing.
/// </summary>
public sealed class Profile
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";

    /// <summary>null = default (normal page mode). "embed" reserved for compact mode (Phase 3).</summary>
    public string? Mode { get; set; }

    public bool? Topmost { get; set; }
    public bool? FadeEnabled { get; set; }
    public PlacementData? Bounds { get; set; }
}

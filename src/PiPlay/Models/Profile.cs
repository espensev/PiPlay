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

    /// <summary>
    /// Per-profile playback mode override (spec 10, Phase 3). <c>null</c> = use the global
    /// <see cref="PlayerSettings.CompactMode"/> default; <c>"normal"</c> forces Normal page mode;
    /// <c>"compact"</c> forces Compact embedded mode. The legacy/internal <c>"embed"</c> token is
    /// accepted as a <c>"compact"</c> alias and normalized on load (see
    /// <see cref="Services.PlaybackModePolicy"/> / <see cref="Services.SettingsService"/>).
    /// </summary>
    public string? Mode { get; set; }

    public bool? Topmost { get; set; }
    public bool? FadeEnabled { get; set; }
    public PlacementData? Bounds { get; set; }
}

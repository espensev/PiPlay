namespace PiPlay.Models;

/// <summary>
/// A named saved launch target. Nullable fields fall back to the global
/// default per field; non-null fields override it (REQ-PROFILE-01). MVP uses Name + Url;
/// the other fields are carried through profile editing.
/// </summary>
public sealed class Profile
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";

    /// <summary>
    /// Per-profile playback mode override. <c>null</c> = use the global
    /// <see cref="PlayerSettings.CompactMode"/> default; <c>"normal"</c> forces Normal page mode;
    /// <c>"compact"</c> forces Compact embedded mode. The legacy/internal <c>"embed"</c> token is
    /// accepted as a <c>"compact"</c> alias and normalized on load (see
    /// <see cref="Services.PlaybackModePolicy"/> / <see cref="Services.SettingsService"/>).
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// Per-profile Popout presentation override. <c>null</c> uses the global
    /// <see cref="PlayerSettings.FocusedPresentation"/> default; <c>"standard"</c> forces the
    /// ordinary watch page and <c>"focused"</c> opts this profile into the media-first overlay.
    /// Playback mode remains independently controlled by <see cref="Mode"/>.
    /// </summary>
    public string? Presentation { get; set; }

    /// <summary>Optional per-profile identity color. <c>null</c> leaves the aligned identity rail transparent.</summary>
    public string? AccentColor { get; set; }

    public bool? Topmost { get; set; }
    public bool? FadeEnabled { get; set; }
    public PlacementData? Bounds { get; set; }
}

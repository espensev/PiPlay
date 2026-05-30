namespace PiPlay.Models;

/// <summary>Serializable rectangle in screen pixels.</summary>
public sealed class RectData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// Persisted window placement: normal-position bounds in screen pixels plus the
/// monitor identity, so PiPlay can restore to the same monitor and clamp to a visible
/// work area when the monitor layout changes (spec 16.4, REQ-PROFILE-02).
/// </summary>
public sealed class PlacementData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Maximized { get; set; }
    public string? MonitorDeviceName { get; set; }
    public RectData? MonitorWorkArea { get; set; }
    public double DpiScale { get; set; } = 1.0;
}

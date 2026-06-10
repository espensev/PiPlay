using PiPlay.Models;

namespace PiPlay.Services;

/// <summary>Integer pixel rectangle (Left/Top inclusive bounds; Width/Height derived from Right/Bottom).</summary>
public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>
/// Pure placement geometry, extracted from <see cref="WindowPlacementService"/> so the
/// "never restore a window off-screen" clamp (spec 16.4, REQ-PROFILE-02) is unit-testable
/// without a live <see cref="System.Windows.Window"/> or monitor enumeration.
/// </summary>
public static class PlacementMath
{
    /// <summary>Clamp <paramref name="r"/> into <paramref name="work"/>: shrink to fit, then keep fully on-screen.</summary>
    public static RectI Clamp(RectI r, RectI work)
    {
        var w = Math.Min(r.Width, work.Width);
        var h = Math.Min(r.Height, work.Height);

        var x = r.Left;
        var y = r.Top;
        if (x < work.Left) x = work.Left;
        if (y < work.Top) y = work.Top;
        if (x + w > work.Right) x = work.Right - w;
        if (y + h > work.Bottom) y = work.Bottom - h;

        return new RectI(x, y, x + w, y + h);
    }

    /// <summary>
    /// Raise a saved placement's size up to a minimum expressed in device-independent pixels (DIP),
    /// using the placement's own DPI scale to convert — placement bounds are physical pixels, the
    /// minimums are DIP (spec 10.2 / 16.1). Position, monitor identity, and maximized state are
    /// preserved. Used so a saved sub-minimum placement (e.g. a normal-mode 320x180 window reopened
    /// in compact mode) restores at least at the mode minimum instead of opening unusably small.
    /// A non-positive saved DPI scale is treated as 1.0 (older/partial data must not zero the floor).
    /// </summary>
    public static PlacementData EnsureMinSize(PlacementData data, int minWidthDip, int minHeightDip)
    {
        var scale = data.DpiScale > 0 ? data.DpiScale : 1.0;
        var minWidthPx = (int)Math.Ceiling(minWidthDip * scale);
        var minHeightPx = (int)Math.Ceiling(minHeightDip * scale);

        return new PlacementData
        {
            X = data.X,
            Y = data.Y,
            Width = Math.Max(data.Width, minWidthPx),
            Height = Math.Max(data.Height, minHeightPx),
            Maximized = data.Maximized,
            MonitorDeviceName = data.MonitorDeviceName,
            MonitorWorkArea = data.MonitorWorkArea,
            DpiScale = data.DpiScale,
        };
    }
}

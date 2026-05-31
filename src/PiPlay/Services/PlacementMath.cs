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
}

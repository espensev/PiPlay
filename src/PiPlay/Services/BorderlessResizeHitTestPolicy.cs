namespace PiPlay.Services;

/// <summary>
/// Pure non-client resize hit-test policy for PiPlay's borderless windows (REQ-WINDOW-02).
/// Coordinates are WPF device-independent pixels relative to the window's top-left corner.
/// </summary>
internal static class BorderlessResizeHitTestPolicy
{
    public const double ResizeBorderDip = 10;
    public const double CornerLengthDip = 32;

    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;

    public static int? HitTest(
        double width,
        double height,
        double x,
        double y,
        bool isResizable,
        bool isNormalWindowState)
    {
        if (!isResizable || !isNormalWindowState) return null;
        if (width <= 0 || height <= 0) return null;
        if (double.IsNaN(x) || double.IsNaN(y)) return null;
        if (x < 0 || y < 0 || x > width || y > height) return null;

        var border = Math.Min(ResizeBorderDip, Math.Min(width, height) / 2);
        if (border <= 0) return null;

        var corner = Math.Min(CornerLengthDip, Math.Min(width, height) / 2);

        var onLeft = x < border;
        var onRight = x >= width - border;
        var onTop = y < border;
        var onBottom = y >= height - border;

        var nearLeftCorner = x <= corner;
        var nearRightCorner = x >= width - corner;
        var nearTopCorner = y <= corner;
        var nearBottomCorner = y >= height - corner;

        // Corners are edge-band lengths, not filled corner squares. This makes diagonal resize
        // easier to acquire without stealing clicks from caption/player controls away from edges.
        if ((onTop && nearLeftCorner) || (onLeft && nearTopCorner)) return HTTOPLEFT;
        if ((onTop && nearRightCorner) || (onRight && nearTopCorner)) return HTTOPRIGHT;
        if ((onBottom && nearLeftCorner) || (onLeft && nearBottomCorner)) return HTBOTTOMLEFT;
        if ((onBottom && nearRightCorner) || (onRight && nearBottomCorner)) return HTBOTTOMRIGHT;

        if (onLeft) return HTLEFT;
        if (onRight) return HTRIGHT;
        if (onTop) return HTTOP;
        if (onBottom) return HTBOTTOM;

        return null;
    }
}

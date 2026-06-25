using PiPlay.Services;

namespace PiPlay.Tests;

/// <summary>
/// Hit-test logic for the borderless resize band. Points are derived from the policy constants
/// (<see cref="BorderlessResizeHitTestPolicy.ResizeBorderDip"/> / <c>CornerLengthDip</c>) rather than
/// hard-coded pixels, so the band can be re-tuned (e.g. the P1 10→4 DIP shrink) without recalibrating
/// every case — the tests guard the *invariant* (correct HT codes at the configured band), not a literal.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Logic)]
public class BorderlessResizeHitTestPolicyTests
{
    private const double Width = 960;
    private const double Height = 540;
    private static double Band => BorderlessResizeHitTestPolicy.ResizeBorderDip;
    private static double Corner => BorderlessResizeHitTestPolicy.CornerLengthDip;

    // A perpendicular position safely clear of both corner zones (no diagonal influence).
    private static double MidX => Width / 2;
    private static double MidY => Height / 2;

    private static int? HitTest(double x, double y) =>
        BorderlessResizeHitTestPolicy.HitTest(Width, Height, x, y, isResizable: true, isNormalWindowState: true);

    [Fact]
    public void Edges_inside_the_band_return_cardinal_resize_results()
    {
        Assert.Equal(BorderlessResizeHitTestPolicy.HTLEFT, HitTest(Band / 2, MidY));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTRIGHT, HitTest(Width - Band / 2, MidY));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOP, HitTest(MidX, Band / 2));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTBOTTOM, HitTest(MidX, Height - Band / 2));
    }

    [Fact]
    public void Resize_zones_start_exactly_at_the_configured_border()
    {
        // Right/bottom zones begin AT width-band / height-band (x >= width-border); just inside is client.
        Assert.Equal(BorderlessResizeHitTestPolicy.HTRIGHT, HitTest(Width - Band, MidY));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTBOTTOM, HitTest(MidX, Height - Band));
        Assert.Null(HitTest(Width - Band - 0.1, MidY));
        Assert.Null(HitTest(MidX, Height - Band - 0.1));
        // Left/top bands are exclusive at exactly `band` (x < border).
        Assert.Null(HitTest(Band, MidY));
        Assert.Null(HitTest(MidX, Band));
    }

    [Fact]
    public void Corners_extend_along_the_edge_band()
    {
        // Within a corner length of a side, the edge band reports the diagonal (both orientations).
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOPLEFT, HitTest(Corner / 2, Band / 2));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOPLEFT, HitTest(Band / 2, Corner / 2));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOPRIGHT, HitTest(Width - Corner / 2, Band / 2));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOPRIGHT, HitTest(Width - Band / 2, Corner / 2));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTBOTTOMLEFT, HitTest(Corner / 2, Height - Band / 2));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTBOTTOMLEFT, HitTest(Band / 2, Height - Corner / 2));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTBOTTOMRIGHT, HitTest(Width - Corner / 2, Height - Band / 2));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTBOTTOMRIGHT, HitTest(Width - Band / 2, Height - Corner / 2));
    }

    [Fact]
    public void Corner_length_boundary_is_explicit()
    {
        // On the top band: within CornerLength of the left edge -> diagonal; just past -> cardinal.
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOPLEFT, HitTest(Corner, Band / 2));         // x <= corner
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOP, HitTest(Corner + 0.1, Band / 2));       // x > corner
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOPLEFT, HitTest(Band / 2, Corner));         // left band: y <= corner
        Assert.Equal(BorderlessResizeHitTestPolicy.HTLEFT, HitTest(Band / 2, Corner + 0.1));      // y > corner
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOPRIGHT, HitTest(Width - Corner, Band / 2));
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOP, HitTest(Width - Corner - 0.1, Band / 2));
    }

    [Fact]
    public void Interior_and_outer_border_boundary_points_are_client_area()
    {
        Assert.Null(HitTest(MidX, MidY));
        Assert.Null(HitTest(Band, MidY));   // exactly on the left border edge: x < border is false
        Assert.Null(HitTest(MidX, Band));
        Assert.Null(HitTest(Width - Band - 0.1, MidY));
        Assert.Null(HitTest(MidX, Height - Band - 0.1));
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, -1)]
    [InlineData(961, 5)]
    [InlineData(5, 541)]
    public void Out_of_window_points_are_ignored(double x, double y) => Assert.Null(HitTest(x, y));

    [Fact]
    public void Maximized_windows_do_not_report_resize_zones() =>
        Assert.Null(BorderlessResizeHitTestPolicy.HitTest(
            Width, Height, x: Band / 2, y: MidY, isResizable: true, isNormalWindowState: false));

    [Fact]
    public void Non_resizable_windows_do_not_report_resize_zones() =>
        Assert.Null(BorderlessResizeHitTestPolicy.HitTest(
            Width, Height, x: Band / 2, y: MidY, isResizable: false, isNormalWindowState: true));

    [Fact]
    public void Tiny_windows_clamp_the_resize_band_to_half_the_window()
    {
        // A window only one band wide/tall forces border = min(Band, dim/2) onto the dim/2 arm
        // (dim/2 = Band/2 < Band), so the band clamps to half the window — exercised at any DIP value.
        var tiny = Band;
        var clamped = tiny / 2;   // the effective (clamped) border
        // Inside the clamped band near the top-left corner -> that diagonal.
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOPLEFT,
            BorderlessResizeHitTestPolicy.HitTest(
                tiny, tiny, x: clamped / 2, y: clamped / 2, isResizable: true, isNormalWindowState: true));
        // The opposite point falls in the right+bottom clamped band -> the other diagonal, which
        // only holds if the split sits at dim/2 (i.e. the clamp, not the full Band, is in effect).
        Assert.Equal(BorderlessResizeHitTestPolicy.HTBOTTOMRIGHT,
            BorderlessResizeHitTestPolicy.HitTest(
                tiny, tiny, x: tiny - clamped / 2, y: tiny - clamped / 2, isResizable: true, isNormalWindowState: true));
    }
}

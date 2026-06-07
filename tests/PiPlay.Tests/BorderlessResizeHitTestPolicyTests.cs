using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class BorderlessResizeHitTestPolicyTests
{
    private const double Width = 960;
    private const double Height = 540;

    [Theory]
    [InlineData(5, 100, BorderlessResizeHitTestPolicy.HTLEFT)]
    [InlineData(955, 100, BorderlessResizeHitTestPolicy.HTRIGHT)]
    [InlineData(100, 5, BorderlessResizeHitTestPolicy.HTTOP)]
    [InlineData(100, 535, BorderlessResizeHitTestPolicy.HTBOTTOM)]
    public void Edges_return_cardinal_resize_results(double x, double y, int expected)
    {
        Assert.Equal(expected, HitTest(x, y));
    }

    [Theory]
    [InlineData(20, 5, BorderlessResizeHitTestPolicy.HTTOPLEFT)]      // top band, left corner length
    [InlineData(5, 20, BorderlessResizeHitTestPolicy.HTTOPLEFT)]      // left band, top corner length
    [InlineData(940, 5, BorderlessResizeHitTestPolicy.HTTOPRIGHT)]
    [InlineData(955, 20, BorderlessResizeHitTestPolicy.HTTOPRIGHT)]
    [InlineData(20, 535, BorderlessResizeHitTestPolicy.HTBOTTOMLEFT)]
    [InlineData(5, 520, BorderlessResizeHitTestPolicy.HTBOTTOMLEFT)]
    [InlineData(940, 535, BorderlessResizeHitTestPolicy.HTBOTTOMRIGHT)]
    [InlineData(955, 520, BorderlessResizeHitTestPolicy.HTBOTTOMRIGHT)]
    public void Corners_extend_along_the_edge_band(double x, double y, int expected)
    {
        Assert.Equal(expected, HitTest(x, y));
    }

    [Theory]
    [InlineData(32, 5, BorderlessResizeHitTestPolicy.HTTOPLEFT)]
    [InlineData(33, 5, BorderlessResizeHitTestPolicy.HTTOP)]
    [InlineData(5, 32, BorderlessResizeHitTestPolicy.HTTOPLEFT)]
    [InlineData(5, 33, BorderlessResizeHitTestPolicy.HTLEFT)]
    [InlineData(928, 5, BorderlessResizeHitTestPolicy.HTTOPRIGHT)]
    [InlineData(927, 5, BorderlessResizeHitTestPolicy.HTTOP)]
    public void Corner_length_boundary_is_explicit(double x, double y, int expected)
    {
        Assert.Equal(expected, HitTest(x, y));
    }

    [Theory]
    [InlineData(20, 20)]
    [InlineData(10, 100)]
    [InlineData(100, 10)]
    [InlineData(949.9, 100)]
    [InlineData(100, 529.9)]
    public void Interior_and_outer_border_boundary_points_are_client_area(double x, double y)
    {
        Assert.Null(HitTest(x, y));
    }

    [Theory]
    [InlineData(950, 100, BorderlessResizeHitTestPolicy.HTRIGHT)]
    [InlineData(100, 530, BorderlessResizeHitTestPolicy.HTBOTTOM)]
    public void Right_and_bottom_resize_zones_start_at_the_configured_border(double x, double y, int expected)
    {
        Assert.Equal(expected, HitTest(x, y));
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, -1)]
    [InlineData(961, 5)]
    [InlineData(5, 541)]
    public void Out_of_window_points_are_ignored(double x, double y)
    {
        Assert.Null(HitTest(x, y));
    }

    [Fact]
    public void Maximized_windows_do_not_report_resize_zones()
    {
        Assert.Null(BorderlessResizeHitTestPolicy.HitTest(
            Width,
            Height,
            x: 5,
            y: 100,
            isResizable: true,
            isNormalWindowState: false));
    }

    [Fact]
    public void Non_resizable_windows_do_not_report_resize_zones()
    {
        Assert.Null(BorderlessResizeHitTestPolicy.HitTest(
            Width,
            Height,
            x: 5,
            y: 100,
            isResizable: false,
            isNormalWindowState: true));
    }

    [Fact]
    public void Tiny_windows_clamp_border_and_corner_lengths_to_half_the_window()
    {
        Assert.Equal(BorderlessResizeHitTestPolicy.HTTOPLEFT,
            BorderlessResizeHitTestPolicy.HitTest(
                width: 18,
                height: 18,
                x: 4,
                y: 4,
                isResizable: true,
                isNormalWindowState: true));
    }

    private static int? HitTest(double x, double y) =>
        BorderlessResizeHitTestPolicy.HitTest(
            Width,
            Height,
            x,
            y,
            isResizable: true,
            isNormalWindowState: true);
}

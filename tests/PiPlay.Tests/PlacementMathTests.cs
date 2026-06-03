using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PlacementMathTests
{
    private static readonly RectI Work = new(0, 0, 1920, 1080);

    [Fact]
    public void Inside_work_area_is_unchanged()
    {
        var r = new RectI(100, 100, 1060, 640); // 960x540 fully inside
        Assert.Equal(r, PlacementMath.Clamp(r, Work));
    }

    [Fact]
    public void Offscreen_right_is_pulled_back_onto_the_monitor()
    {
        var r = new RectI(1900, 100, 2860, 640); // 960 wide, starts past the right edge
        var c = PlacementMath.Clamp(r, Work);
        Assert.True(c.Right <= Work.Right);
        Assert.Equal(960, c.Width); // size preserved
        Assert.Equal(100, c.Top);   // vertical position unchanged
    }

    [Fact]
    public void Negative_origin_is_clamped_to_work_origin()
    {
        var r = new RectI(-500, -500, 460, 40);
        var c = PlacementMath.Clamp(r, Work);
        Assert.Equal(0, c.Left);
        Assert.Equal(0, c.Top);
    }

    [Fact]
    public void Window_larger_than_work_area_is_shrunk_to_fit()
    {
        var r = new RectI(0, 0, 4000, 3000);
        var c = PlacementMath.Clamp(r, Work);
        Assert.Equal(1920, c.Width);
        Assert.Equal(1080, c.Height);
    }

    [Fact]
    public void Clamps_onto_a_secondary_monitor_work_area()
    {
        // Saved on a monitor to the right; clamp must keep it within that monitor's work rect.
        var work2 = new RectI(1920, 0, 3840, 1080);
        var r = new RectI(3800, 1000, 4760, 1540); // hanging off the bottom-right of monitor 2
        var c = PlacementMath.Clamp(r, work2);
        Assert.True(c.Left >= work2.Left && c.Right <= work2.Right);
        Assert.True(c.Top >= work2.Top && c.Bottom <= work2.Bottom);
    }
}

using PiPlay.Models;
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

    // --- EnsureMinSize: raise a saved sub-minimum placement up to the mode floor (compact 480x270) ---

    [Fact]
    public void EnsureMinSize_raises_a_sub_minimum_placement_to_the_floor()
    {
        // A normal-mode 320x180 placement reopened in compact mode (480x270) must clamp up.
        var saved = new PlacementData { X = 100, Y = 50, Width = 320, Height = 180, DpiScale = 1.0 };

        var clamped = PlacementMath.EnsureMinSize(saved, 480, 270);

        Assert.Equal(480, clamped.Width);
        Assert.Equal(270, clamped.Height);
        Assert.Equal(100, clamped.X);   // position preserved
        Assert.Equal(50, clamped.Y);
    }

    [Fact]
    public void EnsureMinSize_leaves_an_already_large_placement_unchanged()
    {
        var saved = new PlacementData { X = 0, Y = 0, Width = 960, Height = 540, DpiScale = 1.0 };
        var clamped = PlacementMath.EnsureMinSize(saved, 480, 270);
        Assert.Equal(960, clamped.Width);
        Assert.Equal(540, clamped.Height);
    }

    [Fact]
    public void EnsureMinSize_converts_the_dip_minimum_with_the_saved_dpi_scale()
    {
        // Physical px bounds at 150%: the 480x270 DIP floor is 720x405 physical px.
        var saved = new PlacementData { Width = 600, Height = 300, DpiScale = 1.5 };
        var clamped = PlacementMath.EnsureMinSize(saved, 480, 270);
        Assert.Equal(720, clamped.Width);
        Assert.Equal(405, clamped.Height);
    }

    [Fact]
    public void EnsureMinSize_treats_a_non_positive_dpi_scale_as_one()
    {
        // Older/partial saved data with DpiScale = 0 must not zero the floor.
        var saved = new PlacementData { Width = 100, Height = 100, DpiScale = 0 };
        var clamped = PlacementMath.EnsureMinSize(saved, 480, 270);
        Assert.Equal(480, clamped.Width);
        Assert.Equal(270, clamped.Height);
    }

    [Fact]
    public void EnsureMinSize_preserves_monitor_and_maximized_metadata()
    {
        var saved = new PlacementData
        {
            X = 10, Y = 20, Width = 100, Height = 100, Maximized = true,
            MonitorDeviceName = @"\\.\DISPLAY2",
            MonitorWorkArea = new RectData { X = 1920, Y = 0, Width = 1920, Height = 1080 },
            DpiScale = 1.0,
        };

        var clamped = PlacementMath.EnsureMinSize(saved, 480, 270);

        Assert.True(clamped.Maximized);
        Assert.Equal(@"\\.\DISPLAY2", clamped.MonitorDeviceName);
        Assert.NotNull(clamped.MonitorWorkArea);
        Assert.Equal(1920, clamped.MonitorWorkArea!.X);
    }

    // --- ForNextLaunch: a closed-expanded popout must not relaunch expanded (overhaul Task 4) ---

    [Fact]
    public void ForNextLaunch_drops_only_the_maximized_flag()
    {
        var captured = new PlacementData
        {
            X = 100, Y = 50, Width = 960, Height = 540, Maximized = true,
            MonitorDeviceName = @"\\.\DISPLAY2",
            MonitorWorkArea = new RectData { X = 1920, Y = 0, Width = 1920, Height = 1080 },
            DpiScale = 1.5,
        };

        var next = PlacementMath.ForNextLaunch(captured)!;

        Assert.False(next.Maximized);
        // The captured bounds are the prior NORMAL rectangle (rcNormalPosition) — they all survive.
        Assert.Equal(100, next.X);
        Assert.Equal(50, next.Y);
        Assert.Equal(960, next.Width);
        Assert.Equal(540, next.Height);
        Assert.Equal(@"\\.\DISPLAY2", next.MonitorDeviceName);
        Assert.Equal(1920, next.MonitorWorkArea!.X);
        Assert.Equal(1.5, next.DpiScale);
    }

    [Fact]
    public void ForNextLaunch_passes_null_through()
    {
        // No capture (e.g. the window never got an HWND) stays no capture.
        Assert.Null(PlacementMath.ForNextLaunch(null));
    }
}

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
        Assert.Equal(new RectI(960, 100, 1920, 640), c);
    }

    [Fact]
    public void Negative_origin_is_clamped_to_work_origin()
    {
        var r = new RectI(-500, -500, 460, 40);
        var c = PlacementMath.Clamp(r, Work);
        Assert.Equal(new RectI(0, 0, 960, 540), c);
    }

    [Fact]
    public void Window_larger_than_work_area_is_shrunk_to_fit()
    {
        var r = new RectI(0, 0, 4000, 3000);
        var c = PlacementMath.Clamp(r, Work);
        Assert.Equal(Work, c);
    }

    [Fact]
    public void Clamps_onto_a_secondary_monitor_work_area()
    {
        // Saved on a monitor to the right; clamp must keep it within that monitor's work rect.
        var work2 = new RectI(1920, 0, 3840, 1080);
        var r = new RectI(3800, 1000, 4760, 1540); // hanging off the bottom-right of monitor 2
        var c = PlacementMath.Clamp(r, work2);
        Assert.Equal(new RectI(2880, 540, 3840, 1080), c);
    }

    // --- EnsureMinSize: raise a saved sub-minimum placement up to the mode floor (compact 480x270) ---

    [Fact]
    public void EnsureMinSize_raises_a_sub_minimum_placement_to_the_floor()
    {
        // A normal-mode 320x180 placement reopened in compact mode (480x270) must clamp up
        // while preserving position, monitor, and maximized metadata.
        var saved = new PlacementData
        {
            X = 100,
            Y = 50,
            Width = 320,
            Height = 180,
            Maximized = true,
            MonitorDeviceName = @"\\.\DISPLAY2",
            MonitorWorkArea = new RectData { X = 1920, Y = 0, Width = 1920, Height = 1080 },
            DpiScale = 1.0,
        };

        var clamped = PlacementMath.EnsureMinSize(saved, 480, 270);

        Assert.Equal(480, clamped.Width);
        Assert.Equal(270, clamped.Height);
        Assert.Equal(100, clamped.X);   // position preserved
        Assert.Equal(50, clamped.Y);
        Assert.True(clamped.Maximized);
        Assert.Equal(@"\\.\DISPLAY2", clamped.MonitorDeviceName);
        Assert.NotNull(clamped.MonitorWorkArea);
        Assert.Equal(1920, clamped.MonitorWorkArea!.X);
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

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void EnsureMinSize_treats_a_non_positive_dpi_scale_as_one(double dpiScale)
    {
        // Older/partial saved data with invalid DpiScale must not zero or blow up the floor.
        var saved = new PlacementData { Width = 100, Height = 100, DpiScale = dpiScale };
        var clamped = PlacementMath.EnsureMinSize(saved, 480, 270);
        Assert.Equal(480, clamped.Width);
        Assert.Equal(270, clamped.Height);
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
        Assert.Equal(1080, next.MonitorWorkArea.Height);
        Assert.Equal(1.5, next.DpiScale);

        // Pure copy (adopted from the b35c0dd landing): the saved input is untouched and the
        // result shares no mutable parts with it.
        Assert.True(captured.Maximized);
        Assert.NotSame(captured.MonitorWorkArea, next.MonitorWorkArea);
    }

    [Fact]
    public void ForNextLaunch_passes_null_through()
    {
        // No capture (e.g. the window never got an HWND) stays no capture.
        Assert.Null(PlacementMath.ForNextLaunch(null));
    }

    // --- WorkspaceToScreen / ScreenToWorkspace: taskbar-offset coordinate translation ---

    [Fact]
    public void Workspace_and_screen_coordinates_roundtrip_with_top_docked_taskbar()
    {
        var monitor = new RectI(0, 0, 1920, 1080);
        var work = new RectI(0, 48, 1920, 1080); // 48px top-docked taskbar
        var workspace = new RectI(100, 0, 1060, 540); // flush with top of work area

        var screen = PlacementMath.WorkspaceToScreen(workspace, monitor, work);
        Assert.Equal(new RectI(100, 48, 1060, 588), screen);

        var clamped = PlacementMath.Clamp(screen, work);
        Assert.Equal(screen, clamped);

        var restoredWorkspace = PlacementMath.ScreenToWorkspace(clamped, monitor, work);
        Assert.Equal(workspace, restoredWorkspace);
    }

    [Fact]
    public void Workspace_and_screen_coordinates_roundtrip_with_left_docked_taskbar()
    {
        var monitor = new RectI(-1920, 0, 0, 1080);
        var work = new RectI(-1860, 0, 0, 1080); // 60px left-docked taskbar on left monitor
        var workspace = new RectI(-1920, 50, -960, 590); // flush with left of work area

        var screen = PlacementMath.WorkspaceToScreen(workspace, monitor, work);
        Assert.Equal(new RectI(-1860, 50, -900, 590), screen);

        var clamped = PlacementMath.Clamp(screen, work);
        Assert.Equal(screen, clamped);

        var restoredWorkspace = PlacementMath.ScreenToWorkspace(clamped, monitor, work);
        Assert.Equal(workspace, restoredWorkspace);
    }

    // --- ToDipSize: convert physical pixel placement dimensions back to DIP ---

    [Theory]
    [InlineData(960, 540, 1.0, 960, 540)]
    [InlineData(1200, 675, 1.25, 960, 540)]
    [InlineData(1440, 810, 1.5, 960, 540)]
    [InlineData(960, 540, 0.0, 960, 540)]
    [InlineData(960, 540, -1.0, 960, 540)]
    [InlineData(960, 540, double.NaN, 960, 540)]
    [InlineData(960, 540, double.PositiveInfinity, 960, 540)]
    public void ToDipSize_divides_physical_dimensions_by_valid_dpi_scale(
        int widthPx,
        int heightPx,
        double dpiScale,
        int expectedWidthDip,
        int expectedHeightDip)
    {
        var data = new PlacementData
        {
            Width = widthPx,
            Height = heightPx,
            DpiScale = dpiScale,
        };

        var (widthDip, heightDip) = PlacementMath.ToDipSize(data);

        Assert.Equal(expectedWidthDip, widthDip);
        Assert.Equal(expectedHeightDip, heightDip);
    }
}

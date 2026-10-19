using PiPlay.Services;
using PiPlay.Theme;
using Xunit;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class RoundedWindowRegionPolicyTests
{
    [Theory]
    [InlineData(DwmCornerMode.Round, 22, true, true)]
    [InlineData(DwmCornerMode.Round, 22, false, false)]
    [InlineData(DwmCornerMode.Round, 0, true, false)]
    [InlineData(DwmCornerMode.SmallRound, 22, true, false)]
    [InlineData(DwmCornerMode.Square, 22, true, false)]
    [InlineData(DwmCornerMode.Default, 22, true, false)]
    public void CanApply_identifies_when_snap_classification_is_worth_the_native_calls(
        DwmCornerMode mode,
        double radiusDip,
        bool normal,
        bool expected) =>
        Assert.Equal(expected, RoundedWindowRegionPolicy.CanApply(mode, radiusDip, normal));

    [Theory]
    [InlineData(96, 44)]
    [InlineData(120, 56)]
    [InlineData(144, 66)]
    [InlineData(192, 88)]
    public void Geometry_scales_the_22_dip_radius_to_device_pixels(uint dpi, int expectedDiameter)
    {
        var geometry = RoundedWindowRegionPolicy.CreateGeometry(960, 540, 22, dpi);

        Assert.NotNull(geometry);
        Assert.Equal(new RoundedWindowRegionPolicy.Geometry(960, 540, expectedDiameter, expectedDiameter),
            geometry.Value);
    }

    [Fact]
    public void Geometry_clamps_the_radius_to_half_the_short_edge()
    {
        var geometry = RoundedWindowRegionPolicy.CreateGeometry(40, 20, radiusDip: 100, dpi: 192);

        Assert.NotNull(geometry);
        Assert.Equal(20, geometry.Value.EllipseWidthPx);
        Assert.Equal(20, geometry.Value.EllipseHeightPx);
    }

    [Theory]
    [InlineData(0, 540, 22, 96)]
    [InlineData(960, 0, 22, 96)]
    [InlineData(960, 540, 0, 96)]
    [InlineData(960, 540, 22, 0)]
    public void Geometry_rejects_invalid_inputs(int width, int height, double radius, uint dpi) =>
        Assert.Null(RoundedWindowRegionPolicy.CreateGeometry(width, height, radius, dpi));

    [Theory]
    [InlineData(0, 0, 960, 1040, true)]      // snapped left half
    [InlineData(960, 0, 1920, 520, true)]    // snapped top-right quarter
    [InlineData(0, 0, 640, 1040, true)]      // snapped left third
    [InlineData(0, 0, 960, 540, false)]      // corner-aligned, but not a snap-layout height
    [InlineData(100, 0, 1060, 540, false)]   // top edge only: still a floating rounded window
    [InlineData(0, 100, 960, 640, false)]    // left edge only
    [InlineData(100, 100, 1060, 640, false)] // fully floating
    public void Snap_like_requires_alignment_on_both_work_area_axes(
        int left,
        int top,
        int right,
        int bottom,
        bool expected) =>
        Assert.Equal(expected, RoundedWindowRegionPolicy.IsSnapLike(
            left, top, right, bottom,
            workLeft: 0, workTop: 0, workRight: 1920, workBottom: 1040));

    [Theory]
    [InlineData(true, false, false, false, false, DwmCornerMode.Default, 0xFF, true)]
    [InlineData(true, false, false, false, false, DwmCornerMode.Square, 0x40, true)]
    [InlineData(false, false, false, false, false, DwmCornerMode.Default, 0xFF, false)]     // Maximized
    [InlineData(true, true, false, false, false, DwmCornerMode.Default, 0xFF, false)]       // Snap-like
    [InlineData(true, false, true, false, false, DwmCornerMode.Default, 0xFF, false)]       // Focused active
    [InlineData(true, false, false, true, false, DwmCornerMode.Default, 0xFF, false)]       // Strip collapsed
    [InlineData(true, false, false, false, true, DwmCornerMode.Default, 0xFF, false)]       // Custom region applied
    [InlineData(true, false, false, false, false, DwmCornerMode.SmallRound, 0xFF, false)]   // SmallRound
    [InlineData(true, false, false, false, false, DwmCornerMode.Round, 0xFF, false)]        // Round
    [InlineData(true, false, false, false, false, DwmCornerMode.Default, 0x00, false)]      // Zero alpha (intensity 0)
    public void ShouldShowPopoutEdgeBorder_suppresses_border_for_borderless_rounded_snapped_or_zero_alpha_states(
        bool isNormalWindowState,
        bool isSnapLike,
        bool focusedSurfaceActive,
        bool stripCollapsed,
        bool customWindowRegionApplied,
        DwmCornerMode cornerMode,
        byte edgeAlpha,
        bool expected) =>
        Assert.Equal(
            expected,
            RoundedWindowRegionPolicy.ShouldShowPopoutEdgeBorder(
                isNormalWindowState,
                isSnapLike,
                focusedSurfaceActive,
                stripCollapsed,
                customWindowRegionApplied,
                cornerMode,
                edgeAlpha));
}

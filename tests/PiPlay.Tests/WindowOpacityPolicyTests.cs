using PiPlay.Services;
using Xunit;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class WindowOpacityPolicyTests
{
    // --- Normalize: reset-not-clamp, file floor vs UI floor ---

    [Theory]
    [InlineData(1.0, 1.0)]     // default / opaque look passes through
    [InlineData(0.45, 0.45)]   // UI slider floor is a legal stored value
    [InlineData(0.62, 0.62)]   // ordinary in-range value passes through
    [InlineData(0.10, 0.10)]   // hand-edit unlock floor is honored exactly (spec 7.3 explicit unlock)
    [InlineData(0.25, 0.25)]   // unlock range (0.10-0.45) is honored: the manual edit IS the unlock
    [InlineData(0.0999, 1.0)]  // below the file floor RESETS to opaque (not clamped to the floor)
    [InlineData(0.0, 1.0)]
    [InlineData(-1.0, 1.0)]
    [InlineData(1.01, 1.0)]    // above max resets too
    [InlineData(5.0, 1.0)]
    [InlineData(double.NaN, 1.0)]  // NaN must reset: NaN would otherwise survive range checks and map to alpha 0
    public void Normalize_resets_out_of_range_values_to_opaque(double input, double expected)
    {
        Assert.Equal(expected, WindowOpacityPolicy.Normalize(input));
    }

    // --- Effective: idle vs constant interplay ---

    [Theory]
    [InlineData(false, 1.0, 1.0, 1.0)]    // defaults: feature off, opaque while active
    [InlineData(true, 1.0, 1.0, 1.0)]     // defaults: feature off, opaque while idle
    [InlineData(false, 0.8, 0.45, 0.8)]   // active uses the constant level
    [InlineData(true, 0.8, 0.45, 0.45)]   // idle uses the idle level
    [InlineData(true, 0.6, 1.0, 0.6)]     // idle never brightens past active (spec 7.3: hover RESTORES)
    [InlineData(true, 0.6, 0.6, 0.6)]     // equal levels: idle is a no-op
    [InlineData(false, 5.0, 0.45, 1.0)]   // raw persisted junk is normalized before use
    [InlineData(true, 0.8, -3.0, 0.8)]    // junk idle resets to 1.0, then clamps to the active level
    [InlineData(true, -1.0, 0.9, 0.9)]    // junk constant resets to 1.0 in the idle branch too
    public void Effective_applies_idle_level_only_when_idle_and_never_brightens(
        bool isIdle, double constant, double idle, double expected)
    {
        Assert.Equal(expected, WindowOpacityPolicy.Effective(isIdle, constant, idle));
    }

    // --- ToAlphaByte: the LWA_ALPHA mapping the applier feeds to the HWND ---

    [Theory]
    [InlineData(1.0, 255)]
    [InlineData(0.6, 153)]    // the spike's stage-A value
    [InlineData(0.45, 115)]   // the spike's stage-B value (UI floor)
    [InlineData(0.10, 26)]    // file floor
    [InlineData(double.NaN, 255)]  // junk normalizes to opaque, never alpha 0 (Q-8: alpha 0 stops hit-testing)
    [InlineData(-2.0, 255)]
    public void ToAlphaByte_maps_levels_to_layered_alpha(double opacity, byte expected)
    {
        Assert.Equal(expected, WindowOpacityPolicy.ToAlphaByte(opacity));
    }

    // --- Constants the rest of the feature hangs off ---

    [Fact]
    public void Ui_floor_is_45_percent_and_file_floor_is_10_percent()
    {
        Assert.Equal(0.45, WindowOpacityPolicy.UiFloor);
        Assert.Equal(0.10, WindowOpacityPolicy.FileFloor);
    }

    [Fact]
    public void Window_fade_shares_the_controls_fade_duration()
    {
        // One fade rhythm per window (design 2026-06-10 §5): the strip fade and the window fade
        // must never drift apart.
        Assert.Equal(FadePolicy.FadeDurationMs, WindowOpacityPolicy.FadeDurationMs);
    }
}

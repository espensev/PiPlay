using PiPlay.Theme;
using Xunit.Abstractions;

namespace PiPlay.Tests;

/// <summary>
/// Runs WCAG contrast checks with the independent <see cref="Wcag.ContrastRatio"/> test oracle and
/// emits each measured ratio through xUnit output.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Logic)]
public class ContrastReportTests
{
    private readonly ITestOutputHelper _out;

    public ContrastReportTests(ITestOutputHelper output) => _out = output;

    [Theory]
    // --- Base pairs already proven by the shipping gates (kept so the report mirrors them) ---
    [InlineData("#FFFFFF", "#E45D75", "white text on Danger (sharp/soft-glass rose)", 3.0)]
    [InlineData("#FFFFFF", "#E8564C", "white text on Danger (minimal warm)", 3.0)]
    [InlineData("#FF06141A", "#00D4FF", "dark button text on accent cyan (install default)", 4.5)]
    [InlineData("#FF06141A", "#4A8FAB", "dark button text on accent steel (dimmest chip)", 4.5)]
    public void Contrast_report_for_candidate_pairs(string fg, string bg, string label, double floor)
    {
        var ratio = Wcag.ContrastRatio(fg, bg);
        _out.WriteLine($"{label}: {fg} on {bg} = {ratio:F2}:1 (floor {floor:F1})");
        Assert.True(ratio >= floor, $"{label}: {fg} on {bg} = {ratio:F2}:1, below required {floor:F1}:1.");
    }

    /// <summary>
    /// PIN TEST — the report must AGREE with the real shipping gates, so it can never silently diverge
    /// from the contrast the app actually enforces. There is exactly one formula (Wcag.cs):
    /// (a) reproduce a published, third-party-checkable reference value to lock the formula, and
    /// (b) recompute — from the LIVE catalog — the worst dark-text-on-accent pair the catalog gate
    /// asserts (steel, the dimmest accent), proving this file sees the same data the gate sees. If the
    /// formula or the catalog's steel value drifts, this fails before any candidate row is trusted.
    /// </summary>
    [Fact]
    public void Report_agrees_with_the_shipping_gates()
    {
        // (a) Published reference: white #FFFFFF on the sharp/soft-glass rose #E45D75 is 3.43:1
        //     (ThemeCatalogTests white-on-Danger gate). Tolerance < 0.005 = the published 2-dp precision,
        //     and avoids a banker's-rounding dependency on the exact tie.
        var rose = Wcag.ContrastRatio("#FFFFFF", "#E45D75");
        Assert.True(System.Math.Abs(rose - 3.43) < 0.005,
            $"published reference white-on-#E45D75 should be 3.43:1, got {rose:F4}:1.");

        // (b) Steel is the dimmest offered accent. ThemeCatalogTests.Preset_palettes_meet_contrast_minimums
        //     asserts Wcag.ContrastRatio("#FF06141A", accent.HexColor) >= 4.5 for every accent. Recompute the
        //     steel pair here from the live catalog and assert the same floor — same path, same verdict.
        var steel = ThemeCatalog.AccentOptions.Single(o => o.Key == "steel").HexColor;
        var ratio = Wcag.ContrastRatio("#FF06141A", steel);
        Assert.True(ratio >= 4.5,
            $"pin: dark text on accent steel ({steel}) = {ratio:F2}:1 — this report disagrees with the live catalog gate.");
    }
}

using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class WindowPlacementServiceTests
{
    // A PerMonitorV2 HWND created at the primary monitor's DPI. A placement onto a monitor with
    // another DPI raises WM_DPICHANGED, and WPF (HwndTarget.OnDpiChanged) applies the suggested
    // rect: the placed size scaled by the DPI ratio. Later placements on that monitor stay as given.
    private sealed class FakeWindow(uint creationDpi, uint targetDpi, params bool[] results)
    {
        private int _calls;

        public uint Dpi { get; private set; } = creationDpi;
        public (int Width, int Height) Size { get; private set; }
        public int Placements => _calls;

        public bool Place(int width, int height)
        {
            var succeeded = _calls < results.Length ? results[_calls] : true;
            _calls++;
            if (!succeeded) return false;

            Size = (width, height);
            if (Dpi != targetDpi && targetDpi != 0)
            {
                var scale = (double)targetDpi / Dpi;
                Size = ((int)Math.Round(width * scale), (int)Math.Round(height * scale));
                Dpi = targetDpi;
            }
            return true;
        }
    }

    private static bool Restore(FakeWindow window) =>
        WindowPlacementService.SetPlacementAtTargetDpiForTests(() => window.Dpi, () => window.Place(1280, 720));

    [Theory]
    [InlineData(96u, 144u)]   // 100% primary, saved on a 150% monitor (one call: ~1920x1080)
    [InlineData(144u, 96u)]   // 150% primary, saved on a 100% monitor (one call: ~853x480)
    [InlineData(96u, 120u)]
    public void Restore_across_a_dpi_boundary_lands_the_saved_pixel_size(uint creationDpi, uint targetDpi)
    {
        var window = new FakeWindow(creationDpi, targetDpi);

        Assert.True(Restore(window));

        Assert.Equal((1280, 720), window.Size);
        Assert.Equal(2, window.Placements);
    }

    [Fact]
    public void Restore_on_the_creation_dpi_places_once()
    {
        var window = new FakeWindow(creationDpi: 144, targetDpi: 144);

        Assert.True(Restore(window));

        Assert.Equal((1280, 720), window.Size);
        Assert.Equal(1, window.Placements);
    }

    [Fact]
    public void A_failed_first_placement_is_not_repeated()
    {
        var window = new FakeWindow(creationDpi: 96, targetDpi: 144, results: [false]);

        Assert.False(Restore(window));

        Assert.Equal(1, window.Placements);
    }

    [Fact]
    public void The_repeat_reports_its_own_result()
    {
        var window = new FakeWindow(creationDpi: 96, targetDpi: 144, results: [true, false]);

        Assert.False(Restore(window));

        Assert.Equal(2, window.Placements);
    }

    [Fact]
    public void An_unknown_dpi_places_once()
    {
        // GetDpiForWindow returns 0 for a handle it cannot resolve; no DPI change can be inferred.
        var placements = 0;

        var placed = WindowPlacementService.SetPlacementAtTargetDpiForTests(() => 0, () => ++placements > 0);

        Assert.True(placed);
        Assert.Equal(1, placements);
    }
}

using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class FocusedPinAffordanceScriptTests
{
    [Fact]
    public void Focused_pin_names_both_actions_and_updates_pressed_and_title_state()
    {
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500);

        Assert.Contains(
            "const label = config.topmost ? \"Unpin popout from top\" : \"Pin popout on top\";",
            script);
        Assert.Contains("setAttribute(controls.pin, \"aria-pressed\", config.topmost);", script);
        Assert.Contains("setAttribute(controls.pin, \"aria-label\", label);", script);
        Assert.Contains("if (controls.pin.title !== label) controls.pin.title = label;", script);
    }
}

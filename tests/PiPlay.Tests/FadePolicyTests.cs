using PiPlay.Services;
using Xunit;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class FadePolicyTests
{
    [Theory]
    [InlineData(false, false, false, true, false)] // Fade disabled: never hides
    [InlineData(true, false, false, true, true)]   // Idle and pointer away: hides
    [InlineData(true, true, false, true, false)]   // Pointer over: stays visible
    [InlineData(true, false, true, true, false)]   // Dragging: stays visible
    [InlineData(true, false, false, false, false)] // Before idle elapses: stays visible
    public void ShouldHide_hides_only_when_enabled_idle_and_unattended(
        bool fadeEnabled,
        bool isPointerOver,
        bool isDragging,
        bool idleElapsed,
        bool expected)
    {
        Assert.Equal(expected, FadePolicy.ShouldHide(fadeEnabled, isPointerOver, isDragging, idleElapsed));
    }
}

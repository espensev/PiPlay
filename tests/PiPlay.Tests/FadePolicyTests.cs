using PiPlay.Services;
using Xunit;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class FadePolicyTests
{
    // --- ShouldHide ---

    [Fact]
    public void ShouldHide_WhenFadeDisabled_NeverHides()
    {
        // Fade off must behave exactly like the MVP: controls always visible,
        // regardless of pointer/drag/idle state.
        Assert.False(FadePolicy.ShouldHide(fadeEnabled: false, isPointerOver: false, isDragging: false, idleElapsed: true));
    }

    [Fact]
    public void ShouldHide_WhenIdleAndPointerAway_Hides()
    {
        Assert.True(FadePolicy.ShouldHide(fadeEnabled: true, isPointerOver: false, isDragging: false, idleElapsed: true));
    }

    [Fact]
    public void ShouldHide_WhenPointerOver_StaysVisible()
    {
        Assert.False(FadePolicy.ShouldHide(fadeEnabled: true, isPointerOver: true, isDragging: false, idleElapsed: true));
    }

    [Fact]
    public void ShouldHide_WhenDragging_StaysVisible()
    {
        Assert.False(FadePolicy.ShouldHide(fadeEnabled: true, isPointerOver: false, isDragging: true, idleElapsed: true));
    }

    [Fact]
    public void ShouldHide_BeforeIdleElapses_StaysVisible()
    {
        Assert.False(FadePolicy.ShouldHide(fadeEnabled: true, isPointerOver: false, isDragging: false, idleElapsed: false));
    }

    // --- ShouldShow ---

    [Fact]
    public void ShouldShow_WhenFadeDisabled_AlwaysShows()
    {
        Assert.True(FadePolicy.ShouldShow(fadeEnabled: false, isPointerOver: false, isDragging: false));
    }

    [Fact]
    public void ShouldShow_WhenPointerOver_Shows()
    {
        Assert.True(FadePolicy.ShouldShow(fadeEnabled: true, isPointerOver: true, isDragging: false));
    }

    [Fact]
    public void ShouldShow_WhenDragging_Shows()
    {
        Assert.True(FadePolicy.ShouldShow(fadeEnabled: true, isPointerOver: false, isDragging: true));
    }

    [Fact]
    public void ShouldShow_WhenIdleAndPointerAway_DoesNotForceShow()
    {
        Assert.False(FadePolicy.ShouldShow(fadeEnabled: true, isPointerOver: false, isDragging: false));
    }
}

using PiPlay.Services;
using Xunit;

namespace PiPlay.Tests;

/// <summary>
/// Pure-policy gate for the DWM frame-border suppression (P1 borderless follow-up): borderless is
/// the default, but the Windows High Contrast system border is an accessibility boundary cue that
/// must survive (the v0.7.0 pass likewise preserved keyboard focus rings).
/// </summary>
[Trait(TestCategories.Key, TestCategories.Logic)]
public class WindowBorderColorPolicyTests
{
    [Theory]
    [InlineData(true, false, true)]    // borderless default, normal mode → suppress the frame
    [InlineData(true, true, false)]    // High Contrast → keep the system border (a11y boundary)
    [InlineData(false, false, false)]  // a future "borders on" theme → don't suppress
    [InlineData(false, true, false)]   // not requested + HC → still don't suppress
    public void ShouldSuppressBorder_suppresses_only_when_requested_and_not_high_contrast(
        bool requested, bool highContrast, bool expected) =>
        Assert.Equal(expected, WindowOpacityApplier.ShouldSuppressBorder(requested, highContrast));
}

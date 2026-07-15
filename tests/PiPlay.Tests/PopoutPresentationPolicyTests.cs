using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PopoutPresentationPolicyTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("standard", "standard")]
    [InlineData(" STANDARD ", "standard")]
    [InlineData("focused", "focused")]
    [InlineData("Focused", "focused")]
    [InlineData("overlay", null)]
    [InlineData("compact", null)]
    public void NormalizeProfilePresentation_maps_to_the_closed_durable_vocabulary(
        string? input, string? expected)
    {
        Assert.Equal(expected, PopoutPresentationPolicy.NormalizeProfilePresentation(input));
    }

    [Theory]
    [InlineData(null, false, PopoutPresentation.Standard)]
    [InlineData(null, true, PopoutPresentation.Focused)]
    [InlineData("standard", true, PopoutPresentation.Standard)]
    [InlineData("focused", false, PopoutPresentation.Focused)]
    [InlineData("unknown", false, PopoutPresentation.Standard)]
    [InlineData("unknown", true, PopoutPresentation.Focused)]
    public void ResolveEffectivePresentation_applies_profile_then_global_precedence_REQ_PROFILE_01(
        string? profilePresentation, bool globalFocused, PopoutPresentation expected)
    {
        Assert.Equal(expected,
            PopoutPresentationPolicy.ResolveEffectivePresentation(profilePresentation, globalFocused));
    }

    [Theory]
    [InlineData("focused", "vid00000001", "vid00000001", "focused")]
    [InlineData("standard", "vid00000001", "vid00000001", "standard")]
    [InlineData(" FOCUSED ", "vid00000001", "vid00000001", "focused")]
    [InlineData("focused", "vid00000001", "vid00000002", null)]
    [InlineData("focused", null, "vid00000001", null)]
    [InlineData("focused", "vid00000001", null, null)]
    [InlineData("focused", "vid00000001", "", null)]
    [InlineData(null, "vid00000001", "vid00000001", null)]
    [InlineData("unknown", "vid00000001", "vid00000001", null)]
    public void ResolveProfileOverride_applies_only_to_the_profiles_own_video(
        string? presentation, string? profileVideoId, string? targetVideoId, string? expected)
    {
        Assert.Equal(expected,
            PopoutPresentationPolicy.ResolveProfileOverride(presentation, profileVideoId, targetVideoId));
    }
}

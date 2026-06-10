using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ReturnPolicyTests
{
    [Theory]
    [InlineData(120, true, ReturnAction.SeekAndPlay)]
    [InlineData(120, false, ReturnAction.Seek)]
    [InlineData(0, true, ReturnAction.SeekAndPlay)]   // 0 is a valid timestamp
    [InlineData(0, false, ReturnAction.Seek)]
    [InlineData(null, true, ReturnAction.Play)]       // unknown timestamp, was playing
    [InlineData(null, false, ReturnAction.None)]      // unknown + paused: do nothing
    public void Decide_matches_REQ_RETURN_01(int? lastKnownSeconds, bool wasPlaying, ReturnAction expected)
    {
        Assert.Equal(expected, ReturnPolicy.Decide(lastKnownSeconds, wasPlaying));
    }

    // Video-aware overload (overhaul Task 3): a popout that ended on a DIFFERENT video must make
    // the source NAVIGATE there; seeking the original video to the new video's timestamp is the
    // corruption this fixes.
    [Theory]
    [InlineData(120, true)]
    [InlineData(120, false)]
    [InlineData(null, true)]
    [InlineData(null, false)]
    public void Differing_video_ids_decide_navigate_regardless_of_timestamp_and_intent(
        int? lastKnownSeconds, bool wasPlaying)
    {
        Assert.Equal(ReturnAction.Navigate,
            ReturnPolicy.Decide(lastKnownSeconds, wasPlaying, "newVideo0001", "oldVideo0001"));
    }

    [Theory]
    [InlineData("sameVideo001", "sameVideo001", 120, true, ReturnAction.SeekAndPlay)]   // unchanged video
    [InlineData(null, "oldVideo0001", 120, false, ReturnAction.Seek)]                   // returned id unknown
    [InlineData("", "oldVideo0001", null, true, ReturnAction.Play)]                     // empty = unknown
    [InlineData("newVideo0001", null, null, false, ReturnAction.None)]                  // source id unknown
    public void Unknown_or_unchanged_ids_fall_back_to_the_timestamp_decision(
        string? returnedId, string? sourceId, int? lastKnownSeconds, bool wasPlaying, ReturnAction expected)
    {
        Assert.Equal(expected, ReturnPolicy.Decide(lastKnownSeconds, wasPlaying, returnedId, sourceId));
    }
}

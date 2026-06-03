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
}

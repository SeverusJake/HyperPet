using HyperPet.App.Pets;
using HyperPet.Core.Pets;
using Xunit;

namespace HyperPet.App.Tests.Pets;

public class PlaybackStepTests
{
    [Fact]
    public void Forward_Advances_UntilLastFrame_ThenCompletes()
    {
        var s1 = PlaybackStep.Next(PlayMode.Forward, index: 0, direction: 1, frameCount: 3);
        Assert.Equal(1, s1.Index);
        Assert.False(s1.Completed);

        var s2 = PlaybackStep.Next(PlayMode.Forward, index: 1, direction: 1, frameCount: 3);
        Assert.Equal(2, s2.Index);
        Assert.False(s2.Completed);

        var s3 = PlaybackStep.Next(PlayMode.Forward, index: 2, direction: 1, frameCount: 3);
        Assert.True(s3.Completed);
        Assert.Equal(2, s3.Index);
    }

    [Fact]
    public void Reverse_Decrements_UntilFirstFrame_ThenCompletes()
    {
        var s1 = PlaybackStep.Next(PlayMode.Reverse, index: 2, direction: -1, frameCount: 3);
        Assert.Equal(1, s1.Index);
        Assert.False(s1.Completed);

        var s2 = PlaybackStep.Next(PlayMode.Reverse, index: 0, direction: -1, frameCount: 3);
        Assert.True(s2.Completed);
        Assert.Equal(0, s2.Index);
    }

    [Fact]
    public void PingPong_Flips_AtEnd_AndCompletesOnForwardEnd()
    {
        var atEnd = PlaybackStep.Next(PlayMode.PingPong, index: 2, direction: 1, frameCount: 3);
        Assert.True(atEnd.Completed);
        Assert.Equal(-1, atEnd.Direction);

        var mid = PlaybackStep.Next(PlayMode.PingPong, index: 0, direction: 1, frameCount: 3);
        Assert.Equal(1, mid.Index);
        Assert.False(mid.Completed);
    }

    [Fact]
    public void SingleFrame_CompletesImmediately()
    {
        var s = PlaybackStep.Next(PlayMode.Forward, index: 0, direction: 1, frameCount: 1);
        Assert.True(s.Completed);
        Assert.Equal(0, s.Index);
    }
}

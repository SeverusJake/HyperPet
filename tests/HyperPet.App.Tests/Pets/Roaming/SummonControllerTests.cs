using HyperPet.App.Pets.Roaming;
using Xunit;

namespace HyperPet.App.Tests.Pets.Roaming;

public class SummonControllerTests
{
    private static SummonController Make() => new() { WalkSpeed = 10 };

    [Fact]
    public void FacesRight_WhenTargetIsToTheRight()
    {
        var c = Make();
        c.Start(0, 0, targetX: 100, targetY: 0);
        c.Tick();
        Assert.Equal("runRight", c.CurrentAnimation);
        Assert.Equal(10, c.X);
        Assert.Equal(0, c.Y);
        Assert.False(c.Arrived);
    }

    [Fact]
    public void FacesLeft_WhenTargetIsToTheLeft()
    {
        var c = Make();
        c.Start(100, 0, targetX: 0, targetY: 0);
        c.Tick();
        Assert.Equal("runLeft", c.CurrentAnimation);
        Assert.Equal(90, c.X);
    }

    [Fact]
    public void Tick_MovesAlongBothAxes()
    {
        var c = Make();
        c.Start(0, 0, targetX: 30, targetY: 40); // distance 50, speed 10 -> 1/5
        c.Tick();
        Assert.Equal(6, c.X, 3);
        Assert.Equal(8, c.Y, 3);
    }

    [Fact]
    public void SnapsToTarget_AndSetsArrived_WhenWithinOneStep()
    {
        var c = Make();
        c.Start(0, 0, targetX: 4, targetY: 3); // distance 5 < speed 10
        c.Tick();
        Assert.Equal(4, c.X, 3);
        Assert.Equal(3, c.Y, 3);
        Assert.True(c.Arrived);
    }

    [Fact]
    public void Arrived_StaysAtTarget_OnSubsequentTicks()
    {
        var c = Make();
        c.Start(0, 0, targetX: 4, targetY: 3);
        c.Tick();
        c.Tick();
        Assert.Equal(4, c.X, 3);
        Assert.Equal(3, c.Y, 3);
        Assert.True(c.Arrived);
    }
}

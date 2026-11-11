using System.Windows;
using HyperPet.App.Views;

namespace HyperPet.App.Tests.Views;

public sealed class NotificationPlacementTests
{
    [Theory]
    [InlineData(500, 400, 192, 208, 280, 180, 456, 208)]
    [InlineData(500, 0, 192, 208, 280, 500, 704, 0)]
    [InlineData(1000, 0, 192, 208, 280, 500, 708, 0)]
    [InlineData(300, 192, 100, 100, 280, 180, 210, 0)]
    public void Compute_PrefersAboveThenRightThenLeft(
        double x, double y, double width, double height, double bubbleWidth, double bubbleHeight,
        double expectedX, double expectedY)
    {
        Rect actual = NotificationPlacement.Compute(new Rect(x, y, width, height),
            new Size(bubbleWidth, bubbleHeight), new WorkArea(0, 0, 1200, 900));
        Assert.Equal(new Rect(expectedX, expectedY, bubbleWidth, bubbleHeight), actual);
    }

    [Fact]
    public void Compute_UsesBelow_WhenOtherSidesCannotFit()
    {
        Rect actual = NotificationPlacement.Compute(new Rect(100, 30, 192, 208),
            new Size(280, 300), new WorkArea(0, 0, 400, 900));
        Assert.Equal(new Rect(56, 250, 280, 300), actual);
    }

    [Fact]
    public void Compute_RespectsNegativeMonitorOrigin()
    {
        Rect actual = NotificationPlacement.Compute(new Rect(-1000, 300, 192, 208),
            new Size(280, 180), new WorkArea(-1920, -200, 0, 880));
        Assert.Equal(new Rect(-1044, 108, 280, 180), actual);
    }

    [Fact]
    public void Compute_ShrinksToLargestFreeViewport_WhenNoFullSizeFits()
    {
        Rect actual = NotificationPlacement.Compute(new Rect(104, 46, 192, 208),
            new Size(280, 300), new WorkArea(0, 0, 400, 300));
        Assert.Equal(new Rect(308, 0, 92, 300), actual);
    }

    [Fact]
    public void Compute_ReturnsEmpty_WhenWorkAreaHasNoSpaceOutsidePet()
    {
        Rect actual = NotificationPlacement.Compute(new Rect(0, 0, 100, 100),
            new Size(280, 300), new WorkArea(0, 0, 100, 100));
        Assert.True(actual.IsEmpty);
    }

    [Fact]
    public void Compute_AroundEveryEdge_StaysInsideWorkAreaWithoutCoveringPet()
    {
        var workArea = new WorkArea(-1200, -100, 0, 800);
        var bounds = new Rect(-1200, -100, 1200, 900);
        foreach (double x in new[] { -1200d, -700d, -192d })
        foreach (double y in new[] { -100d, 300d, 592d })
        foreach (var size in new[] { new Size(280, 160), new Size(280, 420), new Size(1600, 1400) })
        {
            var pet = new Rect(x, y, 192, 208);
            Rect actual = NotificationPlacement.Compute(pet, size, workArea);
            Assert.True(bounds.Contains(actual), $"Outside monitor: {actual}");
            pet.Inflate(11.9, 11.9);
            Assert.False(pet.IntersectsWith(actual), $"Covers pet: {actual}");
        }
    }
}

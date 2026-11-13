using HyperPet.App.Notifications;
using HyperPet.Core.Notifications;

namespace HyperPet.App.Tests.Notifications;

public sealed class DebugNotificationTestTests
{
    [Theory]
    [InlineData(DebugNotificationScenario.Short, 1)]
    [InlineData(DebugNotificationScenario.Long, 1)]
    [InlineData(DebugNotificationScenario.Unicode, 1)]
    [InlineData(DebugNotificationScenario.Burst, 3)]
    [InlineData(DebugNotificationScenario.Repeated, 2)]
    public void Samples_AreCorrelatedAndCountDistinctDeliveries(DebugNotificationScenario scenario, int count)
    {
        var run = new DebugNotificationTest(scenario);
        Assert.Equal(count, run.Messages.Count);
        foreach (var message in run.Messages.Select((value, index) => new HyperNotification(index.ToString(), "Sim", value.Title, value.Body, DateTimeOffset.UtcNow, source: NotificationSource.InApp)))
        { run.Observe(message, true); run.Observe(message, true); }
        Assert.Equal(count, run.Received); Assert.Equal(count, run.Shown); Assert.True(run.Completion.IsCompleted);
        if (scenario == DebugNotificationScenario.Repeated) Assert.Equal(run.Messages[0], run.Messages[1]);
    }

    [Fact]
    public void UnrelatedNotification_DoesNotCompleteTest_AndSuppressionIsVisible()
    {
        var run = new DebugNotificationTest(DebugNotificationScenario.Short);
        run.Observe(new("other", "Zalo", "private", "content", DateTimeOffset.UtcNow), true);
        Assert.Equal(0, run.Received);
        run.Observe(new("test", "Sim", run.Messages[0].Title, run.Messages[0].Body, DateTimeOffset.UtcNow), false);
        Assert.Equal(1, run.Received); Assert.Equal(0, run.Shown); Assert.True(run.Completion.IsCompleted);
    }

    [Fact]
    public void WrongDecodedBody_IsNotReportedAsPassing()
    {
        var run = new DebugNotificationTest(DebugNotificationScenario.Short);
        run.Observe(new("wrong", "Sim", run.Messages[0].Title, "Wrong UIA node", DateTimeOffset.UtcNow), true);
        Assert.DoesNotContain("Test passed.", run.Summary);
        Assert.Contains("decoded text", run.Summary);
    }
}

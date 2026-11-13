using HyperPet.App.Notifications;

namespace HyperPet.App.Tests.Notifications;
public sealed class PopupOccurrenceTests
{
    [Fact]
    public void SameHandle_NewContentAndReopen_AreNewOccurrences()
    {
        var tracker = new PopupOccurrenceTracker();
        var first = tracker.Observe(new IntPtr(1), "a", "bc");
        Assert.NotNull(first);
        Assert.Null(tracker.Observe(new IntPtr(1), "a", "bc"));
        Assert.NotEqual(first, tracker.Observe(new IntPtr(1), "ab", "c"));
        tracker.Prune(Array.Empty<IntPtr>());
        Assert.NotEqual(first, tracker.Observe(new IntPtr(1), "a", "bc"));
    }
    [Fact]
    public void EmptyAccessibilityText_IsNotCachedAsAnAlert()
    {
        var tracker = new PopupOccurrenceTracker();
        Assert.Null(tracker.Observe(new IntPtr(1), "", ""));
        Assert.NotNull(tracker.Observe(new IntPtr(1), "ready", "message"));
    }
    [Fact]
    public void UnreadablePopup_IsReportedOnce_AfterConsecutiveEmptyScans()
    {
        var tracker = new PopupOccurrenceTracker();
        for (int scan = 1; scan < PopupOccurrenceTracker.EmptyScansBeforeFallback; scan++)
            Assert.Null(tracker.Observe(new IntPtr(1), "", ""));
        Assert.NotNull(tracker.Observe(new IntPtr(1), "", ""));
        Assert.Null(tracker.Observe(new IntPtr(1), "", ""));
        // Text that turns readable later is the same popup, not a second alert.
        Assert.Null(tracker.Observe(new IntPtr(1), "sender", "late text"));
        Assert.NotNull(tracker.Observe(new IntPtr(1), "sender", "next message"));
    }
    [Fact]
    public void ReadableTextBeforeFallback_AlertsWithContentOnly()
    {
        var tracker = new PopupOccurrenceTracker();
        Assert.Null(tracker.Observe(new IntPtr(1), "", ""));
        Assert.NotNull(tracker.Observe(new IntPtr(1), "sender", "text"));
        Assert.Null(tracker.Observe(new IntPtr(1), "", ""));
        Assert.Null(tracker.Observe(new IntPtr(1), "", ""));
    }
    [Fact]
    public async Task AutomationWork_RunsOnMtaAndSerializes()
    {
        using var worker = new AutomationWorker(); int current = 0, maximum = 0;
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => worker.RunAsync(() =>
        {
            Assert.Equal(ApartmentState.MTA, Thread.CurrentThread.GetApartmentState());
            maximum = Math.Max(maximum, ++current); --current;
        })));
        Assert.Equal(1, maximum);
    }
}

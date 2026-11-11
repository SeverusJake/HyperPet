using HyperPet.Core.Notifications;
using HyperPet.Core.Pet;

namespace HyperPet.Core.Tests.Notifications;

public sealed class NotificationInboxTests
{
    [Fact]
    public void Enqueue_PreservesCurrentAndPromotesPendingInArrivalOrder()
    {
        var inbox = new NotificationInbox();
        var first = Alert("first");
        var second = Alert("second");
        var third = Alert("third");

        Assert.True(inbox.Enqueue(first));
        Assert.False(inbox.Enqueue(second));
        Assert.False(inbox.Enqueue(third));

        Assert.Equal(first, inbox.Current);
        Assert.Equal(2, inbox.PendingCount);
        Assert.Equal(second, inbox.Advance());
        Assert.Equal(second, inbox.Current);
        Assert.Equal(1, inbox.PendingCount);
        Assert.Equal(third, inbox.Advance());
        Assert.Null(inbox.Advance());
        Assert.Null(inbox.Current);
        Assert.Equal(0, inbox.PendingCount);
    }

    [Fact]
    public void Enqueue_WhenPendingLimitReached_DiscardsOldestPending()
    {
        var inbox = new NotificationInbox(pendingLimit: 2);
        var current = Alert("current");
        inbox.Enqueue(current);
        inbox.Enqueue(Alert("discarded"));
        inbox.Enqueue(Alert("second"));
        inbox.Enqueue(Alert("third"));

        Assert.Equal(current, inbox.Current);
        Assert.Equal(2, inbox.PendingCount);
        Assert.Equal("second", inbox.Advance()!.Title);
        Assert.Equal("third", inbox.Advance()!.Title);
        Assert.Null(inbox.Advance());
    }

    [Fact]
    public void Enqueue_DefaultPendingLimit_KeepsNewestHundred()
    {
        var inbox = new NotificationInbox();
        inbox.Enqueue(Alert("current"));
        for (var i = 0; i <= 100; i++)
        {
            inbox.Enqueue(Alert(i.ToString()));
        }

        Assert.Equal(100, inbox.PendingCount);
        Assert.Equal("1", inbox.Advance()!.Title);
        for (var i = 2; i <= 100; i++)
        {
            Assert.Equal(i.ToString(), inbox.Advance()!.Title);
        }
        Assert.Null(inbox.Advance());
    }

    [Fact]
    public void Enqueue_RecordsHistoryImmediatelyNewestFirstWithStableUniqueIds()
    {
        var inbox = new NotificationInbox(historyLimit: 2);
        inbox.Enqueue(Alert("first"));
        inbox.Enqueue(Alert("second"));
        var secondId = inbox.History[0].Id;
        inbox.Enqueue(Alert("third"));

        Assert.Equal(new[] { "third", "second" }, inbox.History.Select(entry => entry.Alert.Title));
        Assert.Equal(secondId, inbox.History[1].Id);
        Assert.All(inbox.History, entry => Assert.NotEqual(Guid.Empty, entry.Id));
        Assert.NotEqual(inbox.History[0].Id, inbox.History[1].Id);
    }

    [Fact]
    public void Enqueue_DefaultHistoryLimit_KeepsNewestFifty()
    {
        var inbox = new NotificationInbox();
        for (var i = 0; i <= 50; i++)
        {
            inbox.Enqueue(Alert(i.ToString()));
        }

        Assert.Equal(50, inbox.History.Count);
        Assert.Equal("50", inbox.History[0].Alert.Title);
        Assert.Equal("1", inbox.History[^1].Alert.Title);
    }

    [Fact]
    public void Enqueue_Replay_DoesNotRecordExtraHistory()
    {
        var inbox = new NotificationInbox();
        var alert = Alert("first");
        inbox.Enqueue(alert);
        var entry = Assert.Single(inbox.History);
        inbox.Clear();

        Assert.True(inbox.Enqueue(entry.Alert, recordHistory: false));

        Assert.Equal(alert, inbox.Current);
        Assert.Equal(entry, Assert.Single(inbox.History));
    }

    [Fact]
    public void Clear_RemovesCurrentAndPendingButKeepsHistory()
    {
        var inbox = new NotificationInbox();
        inbox.Enqueue(Alert("first"));
        inbox.Enqueue(Alert("second"));

        inbox.Clear();

        Assert.Null(inbox.Current);
        Assert.Equal(0, inbox.PendingCount);
        Assert.Null(inbox.Advance());
        Assert.Equal(2, inbox.History.Count);
        Assert.True(inbox.Enqueue(Alert("third")));
    }

    [Fact]
    public void ClearHistory_KeepsCurrentAndPending()
    {
        var inbox = new NotificationInbox();
        inbox.Enqueue(Alert("first"));
        inbox.Enqueue(Alert("second"));

        inbox.ClearHistory();

        Assert.Empty(inbox.History);
        Assert.Equal("first", inbox.Current!.Title);
        Assert.Equal(1, inbox.PendingCount);
        Assert.Equal("second", inbox.Advance()!.Title);
        Assert.Empty(inbox.History);
    }

    [Fact]
    public void History_CannotBeMutatedByCaller()
    {
        var inbox = new NotificationInbox();
        inbox.Enqueue(Alert("first"));
        var history = inbox.History;

        if (history is IList<NotificationHistoryEntry> list)
        {
            Assert.True(list.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => list.Clear());
            Assert.Throws<NotSupportedException>(() => list[0] = new NotificationHistoryEntry(Guid.NewGuid(), Alert("replacement")));
        }

        Assert.Equal("first", Assert.Single(inbox.History).Alert.Title);
    }

    [Fact]
    public void RedactContent_ReplacesContentEverywhereAndPreservesMetadataAndIds()
    {
        var inbox = new NotificationInbox();
        var first = Alert("first");
        var second = Alert("second");
        inbox.Enqueue(first);
        inbox.Enqueue(second);
        var ids = inbox.History.Select(entry => entry.Id).ToArray();
        var changes = 0;
        inbox.Changed += (_, _) => changes++;

        inbox.RedactContent();

        Assert.Equal(first with { Title = "Notification", Body = "" }, inbox.Current);
        Assert.Equal(ids, inbox.History.Select(entry => entry.Id));
        Assert.Equal(second with { Title = "Notification", Body = "" }, inbox.History[0].Alert);
        Assert.Equal(first with { Title = "Notification", Body = "" }, inbox.History[1].Alert);
        Assert.Equal(1, changes);
        Assert.Equal(second with { Title = "Notification", Body = "" }, inbox.Advance());
        Assert.All(inbox.History, entry => Assert.Equal("Notification", entry.Alert.Title));
        inbox.Clear();
        inbox.Enqueue(inbox.History[1].Alert, recordHistory: false);
        Assert.Equal("", inbox.Current!.Body);
        Assert.Equal("Notification", inbox.Current.Title);
    }

    [Fact]
    public void Changed_FiresOncePerMeaningfulMutationAndAfterStateChanges()
    {
        var inbox = new NotificationInbox();
        var states = new List<(string? Title, int Pending, int History)>();
        inbox.Changed += (_, _) => states.Add((inbox.Current?.Title, inbox.PendingCount, inbox.History.Count));

        inbox.Advance();
        inbox.Clear();
        inbox.ClearHistory();
        inbox.RedactContent();
        Assert.Empty(states);

        inbox.Enqueue(Alert("first"));
        inbox.Enqueue(Alert("second"));
        inbox.Advance();
        inbox.ClearHistory();
        inbox.RedactContent();
        inbox.RedactContent();
        inbox.Clear();
        inbox.Clear();

        Assert.Equal(new (string?, int, int)[]
        {
            ("first", 0, 1),
            ("first", 1, 2),
            ("second", 0, 2),
            ("second", 0, 0),
            ("Notification", 0, 0),
            (null, 0, 0),
        }, states);
    }

    [Theory]
    [InlineData(0, 50, "pendingLimit")]
    [InlineData(-1, 50, "pendingLimit")]
    [InlineData(100, 0, "historyLimit")]
    [InlineData(100, -1, "historyLimit")]
    public void Constructor_NonPositiveLimits_ThrowsExplicitRangeError(int pendingLimit, int historyLimit, string parameter)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new NotificationInbox(pendingLimit, historyLimit));

        Assert.Equal(parameter, error.ParamName);
    }

    [Fact]
    public void Enqueue_Null_ThrowsWithoutChangingState()
    {
        var inbox = new NotificationInbox();
        var changes = 0;
        inbox.Changed += (_, _) => changes++;

        Assert.Throws<ArgumentNullException>(() => inbox.Enqueue(null!));

        Assert.Null(inbox.Current);
        Assert.Empty(inbox.History);
        Assert.Equal(0, changes);
    }

    private static PetAlert Alert(string title) => new(
        "Chat", title, $"Private body for {title}",
        DateTimeOffset.Parse("2026-11-11T10:00:00+07:00"), true, "Chat.App");
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HyperPet.App.Tests.TestSupport;
using HyperPet.App.Views;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pet;

namespace HyperPet.App.Tests.Views;

[Collection("WPF UI")]
public sealed class HistoryWindowTests
{
    [Fact]
    public Task Replay_UsesSelectedAlert_WithoutDuplicatingHistory() => StaThread.RunAsync(() =>
    {
        var inbox = new NotificationInbox();
        inbox.Enqueue(Alert("First"));
        inbox.Enqueue(Alert("Second"));
        var window = new HistoryWindow(inbox, alert => { inbox.Enqueue(alert, recordHistory: false); return true; });
        try
        {
            ((Button)window.FindName("ReplayButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(2, inbox.PendingCount);
            Assert.Equal(2, inbox.History.Count);
            Assert.Equal("Second", inbox.Current!.Title);
            Assert.Equal("Second", inbox.Advance()!.Title);
            Assert.Equal("First", inbox.Advance()!.Title);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task ClearHistory_RemovesVisibleEntries_WithoutClearingQueue() => StaThread.RunAsync(() =>
    {
        var inbox = new NotificationInbox();
        inbox.Enqueue(Alert("First"));
        inbox.Enqueue(Alert("Second"));
        var window = new HistoryWindow(inbox, _ => true);
        try
        {
            ((Button)window.FindName("ClearHistoryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Empty(((ListBox)window.FindName("HistoryListBox")).Items);
            Assert.Equal(1, inbox.PendingCount);
            Assert.False(((Button)window.FindName("ReplayButton")).IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Redaction_UpdatesOpenHistory_AndKeepsSelection() => StaThread.RunAsync(() =>
    {
        var inbox = new NotificationInbox();
        inbox.Enqueue(Alert("Secret"));
        var window = new HistoryWindow(inbox, _ => true);
        try
        {
            var list = (ListBox)window.FindName("HistoryListBox");
            Guid selected = ((NotificationHistoryEntry)list.SelectedItem).Id;
            inbox.RedactContent();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Equal(selected, ((NotificationHistoryEntry)list.SelectedItem).Id);
            Assert.Equal("Notification", ((NotificationHistoryEntry)list.SelectedItem).Alert.Title);
            Assert.Equal(string.Empty, ((TextBox)window.FindName("SelectedBodyTextBox")).Text);
        }
        finally { window.Close(); }
    });

    private static PetAlert Alert(string title) => new("Zalo", title, "Secret body", DateTimeOffset.UtcNow);
}

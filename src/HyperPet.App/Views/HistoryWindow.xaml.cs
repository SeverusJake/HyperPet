using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pet;

namespace HyperPet.App.Views;

public partial class HistoryWindow : Window
{
    private readonly NotificationInbox _inbox;
    private readonly Func<PetAlert, bool> _replay;
    private readonly ObservableCollection<NotificationHistoryEntry> _entries = new();

    public HistoryWindow(NotificationInbox inbox, Func<PetAlert, bool> replay)
    {
        _inbox = inbox;
        _replay = replay;
        InitializeComponent();
        HistoryListBox.ItemsSource = _entries;
        _inbox.Changed += OnInboxChanged;
        Closed += (_, _) => _inbox.Changed -= OnInboxChanged;
        RefreshHistory();
    }

    private void OnInboxChanged(object? sender, EventArgs e) => RefreshHistory();

    private void RefreshHistory()
    {
        Guid? selectedId = (HistoryListBox.SelectedItem as NotificationHistoryEntry)?.Id;
        _entries.Clear();
        foreach (var entry in _inbox.History) { _entries.Add(entry); }
        HistoryListBox.SelectedItem = _entries.FirstOrDefault(entry => entry.Id == selectedId) ?? _entries.FirstOrDefault();
        ClearHistoryButton.IsEnabled = _entries.Count > 0;
        ReplayButton.IsEnabled = HistoryListBox.SelectedItem is not null;
        HistoryStatusText.Text = _entries.Count == 0 ? "No notifications in this session." : $"{_entries.Count} notifications";
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReplayButton is not null) { ReplayButton.IsEnabled = HistoryListBox.SelectedItem is not null; }
    }

    private void OnReplayClick(object sender, RoutedEventArgs e)
    {
        if (HistoryListBox.SelectedItem is not NotificationHistoryEntry entry) { return; }
        bool accepted = _replay(entry.Alert);
        HistoryStatusText.Text = accepted ? "Queued for display." : "Alerts are paused, this source is disabled, quiet hours are active, or the app is closing.";
    }

    private void OnClearHistoryClick(object sender, RoutedEventArgs e) => _inbox.ClearHistory();
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}

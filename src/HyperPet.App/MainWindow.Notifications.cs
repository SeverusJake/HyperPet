using System.Windows;
using HyperPet.App.Views;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pet;
using HyperPet.Core.Settings;

namespace HyperPet.App;

public partial class MainWindow
{
    private readonly NotificationInbox _notificationInbox = new();
    private HistoryWindow? _historyWindow;

    public void ShowAlert(PetAlert alert) => QueueAlert(alert, recordHistory: true);

    private bool QueueAlert(PetAlert alert, bool recordHistory)
    {
        if (!IsSourceEnabled(alert.Source)) return false;
        if (!CanShowAlerts())
        {
            if (!_tuckingAway) { DismissAlert(); }
            return false;
        }
        if (!_settings.ShowFullNotificationContent)
        {
            alert = alert with { Title = "Notification", Body = string.Empty };
        }
        if (_notificationInbox.Enqueue(alert, recordHistory)) { PresentAlert(alert); }
        return true;
    }

    private bool CanShowAlerts() => !_tuckingAway && !_settings.AlertsPaused
        && !(_settings.QuietHoursEnabled && QuietHoursSchedule.IsActive(
            TimeOnly.FromDateTime(DateTime.Now), _settings.QuietHoursStart, _settings.QuietHoursEnd));

    private bool IsSourceEnabled(NotificationSource source) => source == NotificationSource.InApp
        ? _settings.ReactToInAppNotifications : _settings.ReactToWindowsNotifications;

    private void PresentAlert(PetAlert alert)
    {
        _viewModel.CurrentAlert = alert;
        _alertActive = true;
        _debugTotalAlerts++;
        UpdateDebugOverlay();
        UpdateBubbleContent(alert);
        Bubble.Visibility = Visibility.Visible;
        UpdateNotificationPlacement();
        StopBehaviorTimers();
        StopInteractionTimers();
        _petAnimator?.Play("waving");
        _alertTimer.Stop();
        _alertTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.AlertDurationSeconds, 1, 600));
        if (!_bubbleHovered) { _alertTimer.Start(); }
    }

    private void UpdateBubbleContent(PetAlert alert)
    {
        _viewModel.CurrentAlert = alert;
        BubbleAppName.Text = alert.AppName;
        BubbleTitle.Text = alert.Title;
        BubbleBody.Text = alert.Body;
        BubbleContentScrollViewer.ScrollToTop();
        UpdateNotificationPlacement();
    }

    public void DismissAlert()
    {
        _alertTimer.Stop();
        if (!CanShowAlerts()) { _notificationInbox.Clear(); }
        PetAlert? next = _notificationInbox.Advance();
        if (next is not null)
        {
            PresentAlert(next);
            return;
        }
        _viewModel.CurrentAlert = null;
        _alertActive = false;
        Bubble.Visibility = Visibility.Collapsed;
        HideNotificationWindow();
        BubbleAppName.Text = string.Empty;
        BubbleTitle.Text = string.Empty;
        BubbleBody.Text = string.Empty;
        StartBehaviorMode();
    }

    private void OnInboxChanged(object? sender, EventArgs e)
    {
        PendingAlertsText.Text = _notificationInbox.PendingCount > 0 ? $"Queued: {_notificationInbox.PendingCount}" : string.Empty;
        PendingAlertsText.Visibility = _notificationInbox.PendingCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        Bubble.ToolTip = _notificationInbox.PendingCount > 0 ? "Click for next notification" : "Click to dismiss";
        UpdateNotificationPlacement();
    }

    internal void RefreshNotificationPreferences()
    {
        _viewModel.AlertsPaused = _settings.AlertsPaused;
        var previous = _notificationInbox.Current;
        if (!_settings.ReactToWindowsNotifications) _notificationInbox.RemoveSource(NotificationSource.Windows);
        if (!_settings.ReactToInAppNotifications) _notificationInbox.RemoveSource(NotificationSource.InApp);
        if (previous != _notificationInbox.Current)
        {
            if (_notificationInbox.Current is { } next) PresentAlert(next);
            else DismissAlert();
        }
        if (!_settings.ShowFullNotificationContent)
        {
            _notificationInbox.RedactContent();
            if (_notificationInbox.Current is { } current) { UpdateBubbleContent(current); }
        }
        if (!CanShowAlerts()) { DismissAlert(); }
    }

    public void OpenNotificationHistory()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(OpenNotificationHistory);
            return;
        }
        if (_tuckingAway) { return; }
        if (_historyWindow is not null)
        {
            _historyWindow.Activate();
            return;
        }
        _historyWindow = new HistoryWindow(_notificationInbox, alert => QueueAlert(alert, recordHistory: false))
        {
            Owner = this, Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _historyWindow.Closed += (_, _) => _historyWindow = null;
        _historyWindow.Show();
    }

    private void OnHistoryClick(object sender, RoutedEventArgs e)
    {
        OpenNotificationHistory();
        e.Handled = true;
    }
}

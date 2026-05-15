using System.Windows;
using HyperPet.Windows.Notifications;

namespace HyperPet.App.Views;

public partial class SetupWindow : Window
{
    private readonly Func<Task<NotificationAccessStatus>> _requestAccessAsync;

    public SetupWindow(Func<Task<NotificationAccessStatus>>? requestAccessAsync = null)
    {
        InitializeComponent();

        _requestAccessAsync = requestAccessAsync
            ?? (() => Task.FromResult(NotificationAccessStatus.Unspecified));
    }

    private async void OnRequestAccessClick(object sender, RoutedEventArgs e)
    {
        RequestAccessButton.IsEnabled = false;
        StatusText.Text = "Requesting notification access...";

        try
        {
            NotificationAccessStatus status = await _requestAccessAsync();

            StatusText.Text = status == NotificationAccessStatus.Allowed
                ? "Notification access is allowed."
                : "Notification access could not be checked or is not enabled.";
        }
        catch
        {
            StatusText.Text = "Could not request permission. Check Windows notification settings.";
        }
        finally
        {
            RequestAccessButton.IsEnabled = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

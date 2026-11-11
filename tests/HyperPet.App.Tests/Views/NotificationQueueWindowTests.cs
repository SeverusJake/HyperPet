using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HyperPet.App.Pets;
using HyperPet.App.Tests.TestSupport;
using HyperPet.Core.Pet;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

[Collection("WPF UI")]
public sealed class NotificationQueueWindowTests
{
    [Fact]
    public Task NextButton_ReceivesPointerHit_AboveVisiblePet() => StaThread.RunAsync(() =>
    {
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4);
        bitmap.Freeze();
        var states = new Dictionary<string, PetAnimationState>
        {
            ["idle"] = new() { Frames = 1, Fps = 1, Loop = true },
            ["waving"] = new() { Frames = 1, Fps = 1, Loop = true }
        };
        var frames = new Dictionary<string, IReadOnlyList<BitmapSource>>
        {
            ["idle"] = new[] { bitmap }, ["waving"] = new[] { bitmap }
        };
        var pet = new SpritePet(new PetDefinition { States = states }, frames);
        var window = new MainWindow(new HyperPetSettings(), _ => { }, () => { }, pet);
        try
        {
            window.Show();
            window.ShowAlert(Alert("Short"));
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var button = (Button)window.FindName("NextAlertButton");
            var root = (Grid)window.FindName("RootGrid");
            var point = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), root);
            var hit = root.InputHitTest(point) as DependencyObject;
            while (hit is not null && hit is not Button) { hit = VisualTreeHelper.GetParent(hit); }
            Assert.Same(button, hit);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Alerts_WaitTheirTurn_AndDismissAdvancesQueue() => StaThread.RunAsync(() =>
    {
        var window = new MainWindow(new HyperPetSettings(), _ => { }, () => { }, null);
        try
        {
            window.ShowAlert(Alert("First"));
            window.ShowAlert(Alert("Second"));
            Assert.Equal("First", ((TextBlock)window.FindName("BubbleTitle")).Text);

            window.DismissAlert();
            Assert.Equal("Second", ((TextBlock)window.FindName("BubbleTitle")).Text);
            window.DismissAlert();
            Assert.Equal(Visibility.Collapsed, ((Border)window.FindName("Bubble")).Visibility);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Pause_DiscardsPendingAlerts() => StaThread.RunAsync(() =>
    {
        var window = new MainWindow(new HyperPetSettings(), _ => { }, () => { }, null);
        try
        {
            window.ShowAlert(Alert("First"));
            window.ShowAlert(Alert("Second"));
            var pause = (MenuItem)window.FindName("PauseAlertsMenuItem");
            pause.IsChecked = true;
            pause.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            pause.IsChecked = false;
            pause.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            window.DismissAlert();

            Assert.Equal(Visibility.Collapsed, ((Border)window.FindName("Bubble")).Visibility);
        }
        finally { window.Close(); }
    });

    private static PetAlert Alert(string title) => new("Zalo", title, "Body", DateTimeOffset.UtcNow, false);

    [Fact]
    public Task PrivacyChange_RedactsCurrentAndPendingContent() => StaThread.RunAsync(() =>
    {
        var settings = new HyperPetSettings();
        var window = new MainWindow(settings, _ => { }, () => { }, null);
        try
        {
            window.ShowAlert(Alert("First secret"));
            window.ShowAlert(Alert("Second secret"));
            settings.ShowFullNotificationContent = false;
            window.RefreshNotificationPreferences();
            Assert.Equal("Notification", ((TextBlock)window.FindName("BubbleTitle")).Text);
            Assert.Equal(string.Empty, ((TextBlock)window.FindName("BubbleBody")).Text);
            settings.ShowFullNotificationContent = true;
            window.DismissAlert();
            Assert.Equal("Notification", ((TextBlock)window.FindName("BubbleTitle")).Text);
            Assert.Equal(string.Empty, ((TextBlock)window.FindName("BubbleBody")).Text);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task QuietHours_DiscardsQueuedAlertsBeforePromotingNext() => StaThread.RunAsync(() =>
    {
        var settings = new HyperPetSettings();
        var window = new MainWindow(settings, _ => { }, () => { }, null);
        try
        {
            window.ShowAlert(Alert("First"));
            window.ShowAlert(Alert("Second"));
            settings.QuietHoursEnabled = true;
            settings.QuietHoursStart = TimeOnly.FromDateTime(DateTime.Now.AddMinutes(-5)).ToString("HH:mm");
            settings.QuietHoursEnd = TimeOnly.FromDateTime(DateTime.Now.AddMinutes(5)).ToString("HH:mm");
            window.DismissAlert();
            settings.QuietHoursEnabled = false;
            window.DismissAlert();
            Assert.Equal(Visibility.Collapsed, ((Border)window.FindName("Bubble")).Visibility);
        }
        finally { window.Close(); }
    });
}

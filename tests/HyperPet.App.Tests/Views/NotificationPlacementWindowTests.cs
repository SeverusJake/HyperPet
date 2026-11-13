using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HyperPet.App.Pets;
using HyperPet.App.Tests.TestSupport;
using HyperPet.App.Views;
using HyperPet.Core.Pet;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

[Collection("WPF UI")]
public sealed class NotificationPlacementWindowTests
{
    [Fact]
    public Task Notification_UsesOwnedWindow_OutsidePet_WithoutResizingPet() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow();
        try
        {
            window.Show();
            var before = new Size(window.ActualWidth, window.ActualHeight);
            window.ShowAlert(Alert());
            Flush(window);
            var bubble = (Border)window.FindName("Bubble");
            var notification = Window.GetWindow(bubble);
            Assert.NotSame(window, notification);
            Assert.Same(window, notification.Owner);
            Assert.True(notification.IsVisible);
            Assert.False(notification.ShowActivated);
            Assert.False(notification.ShowInTaskbar);
            Assert.Equal(before, new Size(window.ActualWidth, window.ActualHeight));
            AssertSeparatedAndOnScreen(window);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task MovingPetToEveryCorner_RepositionsVisibleNotification() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow();
        try
        {
            window.Show();
            window.ShowAlert(Alert());
            Flush(window);
            var bubble = (Border)window.FindName("Bubble");
            Point before = bubble.PointToScreen(new Point());
            window.Left += 200;
            Flush(window);
            Assert.NotEqual(before, bubble.PointToScreen(new Point()));
            var area = window.GetWorkAreaForPoint(window.Left, window.Top);
            foreach (double left in new[] { area.Left, area.Right - window.Width })
            foreach (double top in new[] { area.Top, area.Bottom - window.Height })
            {
                window.Left = left; window.Top = top;
                Flush(window);
                AssertSeparatedAndOnScreen(window);
            }
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task ResizingPet_RepositionsNotification() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow();
        try
        {
            window.Show(); window.ShowAlert(Alert()); Flush(window);
            var petImage = (Image)window.FindName("PetImage");
            petImage.Width = 240; petImage.Height = 260;
            Flush(window);
            AssertSeparatedAndOnScreen(window);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task LongTitleAndBody_CanScrollToEnd_WithoutConsumingNotification() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow();
        try
        {
            window.Show();
            string body = string.Join("\n", Enumerable.Repeat("Long message to read completely.", 100)) + " END";
            string title = string.Join(" ", Enumerable.Repeat("Long title", 20));
            window.ShowAlert(Alert("Earlier message"));
            window.ShowAlert(Alert(title, body));
            Flush(window);
            var scroll = Assert.IsType<ScrollViewer>(window.FindName("BubbleContentScrollViewer"));
            Assert.True(scroll.ScrollableHeight > 0);
            scroll.ScrollToEnd(); Flush(window);
            Assert.True(scroll.VerticalOffset > 0);
            Assert.Equal(body, ((TextBlock)window.FindName("BubbleBody")).Text);
            Assert.Equal(title, ((TextBlock)window.FindName("BubbleTitle")).Text);
            AssertSeparatedAndOnScreen(window);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task ClickingMessageContent_AdvancesWithoutBeingSwallowedByScrollViewer() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow();
        try
        {
            window.Show(); window.ShowAlert(Alert("First")); window.ShowAlert(Alert("Second")); Flush(window);
            ((TextBlock)window.FindName("BubbleBody")).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseDownEvent
            });
            Assert.Equal("First", ((TextBlock)window.FindName("BubbleTitle")).Text);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task ClickingScrollBar_DoesNotAdvanceNotification() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow();
        try
        {
            window.Show(); window.ShowAlert(Alert("Second")); window.ShowAlert(Alert("First", new string('x', 5000))); Flush(window);
            var scroll = (ScrollViewer)window.FindName("BubbleContentScrollViewer");
            scroll.ApplyTemplate();
            var bar = Assert.IsType<System.Windows.Controls.Primitives.ScrollBar>(scroll.Template.FindName("PART_VerticalScrollBar", scroll));
            bar.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseDownEvent });
            Assert.Equal("First", ((TextBlock)window.FindName("BubbleTitle")).Text);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task ShortNotification_DoesNotNeedScrollBar() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow();
        try
        {
            window.Show();
            window.ShowAlert(Alert("Lịch họp đã cập nhật", "Cuộc họp chuyển sang 15:00. Nhớ xem lại tài liệu trước khi vào nhé."));
            Flush(window);
            Assert.Equal(0, ((ScrollViewer)window.FindName("BubbleContentScrollViewer")).ScrollableHeight);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task ClosingNotification_DismissesCurrent_AndKeepsQueuedMessage() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow();
        try
        {
            window.Show(); window.ShowAlert(Alert("Next message")); window.ShowAlert(Alert()); Flush(window);
            var notification = Window.GetWindow((Border)window.FindName("Bubble"));
            Assert.NotSame(window, notification);
            notification.Close(); Flush(window);
            Assert.Equal("Next message", ((TextBlock)window.FindName("BubbleTitle")).Text);
            Assert.True(notification.IsVisible);
            notification.Close(); Flush(window);
            Assert.False(notification.IsVisible);
            Assert.True(window.IsVisible);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task TuckAway_HidesNotification_AndExitClosesItsWindow() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow();
        window.Show(); window.ShowAlert(Alert()); Flush(window);
        var notification = Window.GetWindow((Border)window.FindName("Bubble"));
        Assert.NotSame(window, notification);
        window.TuckAway(); Flush(window);
        Assert.False(notification.IsVisible);
        window.ShowAlert(Alert());
        Assert.False(notification.IsVisible);
        window.Close();
        Assert.Null(notification.Content);
    });

    [Fact]
    public Task MonitorRefresh_RepositionsNotificationIntoUpdatedWorkArea() => StaThread.RunAsync(() =>
    {
        WorkArea area = DesktopMonitors.Enumerate().First(m => m.IsPrimary).PhysicalWorkArea;
        IReadOnlyList<DesktopMonitor> monitors = new[] { new DesktopMonitor("test", "Test", true, area) };
        var window = CreateWindow(monitorProvider: () => monitors);
        try
        {
            window.Show(); window.ShowAlert(Alert()); Flush(window);
            double newLeft = area.Left + (area.Right - area.Left) / 3;
            monitors = new[] { new DesktopMonitor("test", "Test", true, area with { Left = newLeft }) };
            window.RefreshMonitorPlacement(); Flush(window);
            var bubble = (Border)window.FindName("Bubble");
            Assert.True(bubble.PointToScreen(new Point()).X >= newLeft - 1);
            AssertSeparatedAndOnScreen(window);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Pause_HidesOwnedNotificationWindow() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow();
        try
        {
            window.Show(); window.ShowAlert(Alert()); window.ShowAlert(Alert("Pending")); Flush(window);
            var notification = Window.GetWindow((Border)window.FindName("Bubble"));
            var pause = (MenuItem)window.FindName("PauseAlertsMenuItem");
            pause.IsChecked = true; pause.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.False(notification.IsVisible);
            pause.IsChecked = false; pause.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.False(notification.IsVisible);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Hover_PausesExpiry_AndLeaveResumesIt() => StaThread.RunAsync(() =>
    {
        var window = CreateWindow(duration: 1);
        try
        {
            window.ShowAlert(Alert("First")); window.ShowAlert(Alert("Second"));
            var bubble = (Border)window.FindName("Bubble");
            bubble.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
            Pump(TimeSpan.FromMilliseconds(1150));
            Assert.Equal("Second", ((TextBlock)window.FindName("BubbleTitle")).Text);
            window.DismissAlert();
            Pump(TimeSpan.FromMilliseconds(1150));
            Assert.Equal("First", ((TextBlock)window.FindName("BubbleTitle")).Text);
            bubble.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
            Pump(TimeSpan.FromMilliseconds(1150));
            Assert.Equal(Visibility.Collapsed, bubble.Visibility);
        }
        finally { window.Close(); }
    });

    private static MainWindow CreateWindow(int duration = 600, Func<IReadOnlyList<DesktopMonitor>>? monitorProvider = null)
    {
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4);
        bitmap.Freeze();
        var states = new Dictionary<string, PetAnimationState>();
        var frames = new Dictionary<string, IReadOnlyList<BitmapSource>>();
        foreach (string name in new[] { "idle", "waving" })
        {
            states[name] = new() { Frames = 1, Fps = 1, Loop = true };
            frames[name] = new[] { bitmap };
        }
        return new MainWindow(new HyperPetSettings { AlertDurationSeconds = duration }, _ => { }, () => { },
            new SpritePet(new PetDefinition { States = states }, frames), requestShutdown: () => { }, monitorProvider: monitorProvider);
    }

    private static void AssertSeparatedAndOnScreen(MainWindow window)
    {
        var bubble = (Border)window.FindName("Bubble");
        var pet = (Image)window.FindName("PetImage");
        var petRect = new Rect(pet.PointToScreen(new Point()), pet.PointToScreen(new Point(pet.ActualWidth, pet.ActualHeight)));
        var bubbleRect = new Rect(bubble.PointToScreen(new Point()), bubble.PointToScreen(new Point(bubble.ActualWidth, bubble.ActualHeight)));
        Assert.False(petRect.IntersectsWith(bubbleRect), $"Pet {petRect}, notification {bubbleRect}");
        var center = new Point(petRect.Left + petRect.Width / 2, petRect.Top + petRect.Height / 2);
        DesktopMonitor monitor = DesktopMonitors.Resolve(DesktopMonitors.Enumerate(), "", center.X, center.Y)!;
        Assert.True(monitor.PhysicalWorkArea.Left <= bubbleRect.Left + 1);
        Assert.True(monitor.PhysicalWorkArea.Top <= bubbleRect.Top + 1);
        Assert.True(monitor.PhysicalWorkArea.Right >= bubbleRect.Right - 1);
        Assert.True(monitor.PhysicalWorkArea.Bottom >= bubbleRect.Bottom - 1);
    }

    private static void Flush(MainWindow window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void Pump(TimeSpan interval)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = interval };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    private static PetAlert Alert(string title = "Message", string body = "Body")
        => new("Zalo", title, body, DateTimeOffset.Now);
}

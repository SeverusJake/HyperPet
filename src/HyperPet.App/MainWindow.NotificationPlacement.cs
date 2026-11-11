using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using HyperPet.App.Views;

namespace HyperPet.App;

public partial class MainWindow
{
    private Window? _notificationWindow;
    private bool _updatingNotificationPlacement;
    private bool _closingNotificationWindow;
    private bool _bubbleHovered;

    private void InitializeNotificationWindow()
    {
        // Keep the existing controls/namescope, but give the bubble its own
        // surface so it can sit outside the fixed pet window's bounds.
        RootGrid.Children.Remove(Bubble);
        Bubble.HorizontalAlignment = HorizontalAlignment.Stretch;
        Bubble.VerticalAlignment = VerticalAlignment.Stretch;
        // ScrollViewer handles mouse-down to focus itself. Still accept clicks
        // on message text; the handler excludes buttons and scrollbar controls.
        Bubble.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnBubbleMouseLeftButtonDown), true);
        _notificationWindow = new Window
        {
            Title = "HyperPet notification",
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            Content = Bubble
        };
        _notificationWindow.Closing += (_, e) =>
        {
            if (_closingNotificationWindow) { return; }
            e.Cancel = true;
            DismissAlert();
        };
        _notificationWindow.DpiChanged += (_, _) => ScheduleNotificationPlacement();
        LocationChanged += (_, _) => UpdateNotificationPlacement();
        SizeChanged += (_, _) => UpdateNotificationPlacement();
        PetImage.SizeChanged += (_, _) => UpdateNotificationPlacement();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { UpdateNotificationPlacement(); }
            else { _notificationWindow?.Hide(); }
        };
        Bubble.MouseEnter += (_, _) => { _bubbleHovered = true; _alertTimer.Stop(); };
        Bubble.MouseLeave += (_, _) =>
        {
            _bubbleHovered = false;
            if (_alertActive && CanShowAlerts()) { _alertTimer.Start(); }
        };
        Closing += (_, _) => _closingNotificationWindow = true;
    }

    private void ScheduleNotificationPlacement()
    {
        if (Dispatcher.HasShutdownStarted || _closingNotificationWindow) { return; }
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateNotificationPlacement));
    }

    private void UpdateNotificationPlacement()
    {
        if (_updatingNotificationPlacement || _closingNotificationWindow || !_alertActive
            || _tuckingAway || !IsVisible || Bubble.Visibility != Visibility.Visible
            || _notificationWindow is null) { return; }

        _updatingNotificationPlacement = true;
        try
        {
            PetImage.UpdateLayout();
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            bool hasPet = PetImage.IsVisible && PetImage.ActualWidth > 0 && PetImage.ActualHeight > 0;
            Point physical = hasPet ? PetImage.PointToScreen(new Point()) : PointToScreen(new Point());
            var petBounds = new Rect(physical.X / dpi.DpiScaleX, physical.Y / dpi.DpiScaleY,
                hasPet ? PetImage.ActualWidth : GetWindowWidth(), hasPet ? PetImage.ActualHeight : GetWindowHeight());
            WorkArea workArea = GetWorkAreaForPoint(petBounds.Left + petBounds.Width / 2, petBounds.Top + petBounds.Height / 2);
            double availableWidth = workArea.Right - workArea.Left;
            double availableHeight = workArea.Bottom - workArea.Top;
            if (availableWidth <= 0 || availableHeight <= 0) { _notificationWindow.Hide(); return; }

            if (!_notificationWindow.IsVisible)
            {
                // The detached tree needs its first window layout before text
                // can be measured. Prepare it invisibly without taking focus.
                _notificationWindow.Opacity = 0;
                _notificationWindow.Owner = this;
                _notificationWindow.Width = Math.Min(280, availableWidth);
                _notificationWindow.Height = Math.Min(420, availableHeight);
                _notificationWindow.Left = workArea.Left;
                _notificationWindow.Top = workArea.Top;
                _notificationWindow.Show();
                _notificationWindow.UpdateLayout();
            }
            Bubble.Width = Math.Min(280, availableWidth);
            Bubble.MaxHeight = Math.Min(420, availableHeight);
            Bubble.Measure(new Size(Bubble.Width, Bubble.MaxHeight));
            // A fractional device-pixel height can lose the final text row and
            // create a scrollbar even for short content. Round the surface up.
            var desiredSize = new Size(Math.Ceiling(Bubble.DesiredSize.Width * dpi.DpiScaleX) / dpi.DpiScaleX,
                Math.Min(Bubble.MaxHeight, Math.Ceiling(Bubble.DesiredSize.Height * dpi.DpiScaleY) / dpi.DpiScaleY));
            Rect placement = NotificationPlacement.Compute(petBounds, desiredSize, workArea);
            if (placement.IsEmpty) { _notificationWindow.Hide(); return; }
            Bubble.Width = placement.Width;
            Bubble.MaxHeight = placement.Height;
            _notificationWindow.Width = placement.Width;
            _notificationWindow.Height = placement.Height;
            _notificationWindow.Left = placement.Left;
            _notificationWindow.Top = placement.Top;
            _notificationWindow.UpdateLayout();
            _notificationWindow.Opacity = 1;
        }
        finally { _updatingNotificationPlacement = false; }
    }

    private void HideNotificationWindow()
    {
        _bubbleHovered = false;
        _notificationWindow?.Hide();
    }

    private void CloseNotificationWindow()
    {
        _closingNotificationWindow = true;
        if (_notificationWindow is null) { return; }
        _notificationWindow.Content = null;
        _notificationWindow.Close();
        _notificationWindow = null;
    }
}

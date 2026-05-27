using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HyperPet.App.Notifications;

/// <summary>
/// A small, topmost, no-taskbar WPF window that mimics the in-app popup
/// notification shape used by apps like Zalo. Shown when the user presses 9
/// in debug mode so the <see cref="InAppNotificationWatcher"/> can detect it
/// and round-trip through HyperPet's notification pipeline.
/// </summary>
public sealed class DebugSimulatedPopupWindow : Window
{
    public DebugSimulatedPopupWindow(string sender, string preview, TimeSpan? autoDismissAfter = null)
    {
        Title = "HyperPet Debug Popup";
        Width = 360;
        Height = 92;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Bottom-right of work area to mimic Zalo's real notification position.
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;

        Content = BuildContent(sender, preview);

        if (autoDismissAfter is { } delay)
        {
            DispatcherTimer timer = new()
            {
                Interval = delay
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Close();
            };
            Loaded += (_, _) => timer.Start();
        }
    }

    private static UIElement BuildContent(string sender, string preview)
    {
        var senderText = new TextBlock
        {
            Text = sender,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var previewText = new TextBlock
        {
            Text = preview,
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = 12,
            Foreground = Brushes.DimGray,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(14, 12, 14, 12),
            Children = { senderText, previewText }
        };

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 2,
                Opacity = 0.25
            },
            Child = stack
        };
    }
}

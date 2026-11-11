using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using HyperPet.App.Tests.TestSupport;
using HyperPet.App.Views;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

[Collection("WPF UI")]
public sealed class MonitorPreferenceWindowTests
{
    private static readonly DesktopMonitor Primary = new("primary", "Primary", true, new WorkArea(0, 0, 1920, 1040));
    private static readonly DesktopMonitor Secondary = new("secondary", "Secondary", false, new WorkArea(1920, 0, 3840, 1040));

    [Fact]
    public Task CursorPoint_AtSharedDisplayEdge_UsesDisplayUnderCursor() => StaThread.RunAsync(() =>
    {
        var window = new MainWindow(new HyperPetSettings(), _ => { }, () => { }, null,
            monitorProvider: () => new[] { Primary, Secondary });
        try
        {
            double scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
            var area = window.GetWorkAreaForPoint(1919 / scale, 100);
            Assert.Equal(0, area.Left);
            Assert.Equal(1920, area.Right * scale, precision: 5);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Loaded_UsesPreferredMonitor_AndRecoversAfterDisconnect() => StaThread.RunAsync(() =>
    {
        IReadOnlyList<DesktopMonitor> monitors = new[] { Primary, Secondary };
        var settings = new HyperPetSettings { PreferredMonitorId = "secondary", PetLeft = 100, PetTop = 100 };
        var window = new MainWindow(settings, _ => { }, () => { }, null, monitorProvider: () => monitors);
        try
        {
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            double scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
            Assert.Equal(1920, window.Left * scale, precision: 5);
            Assert.Equal(100, window.Top);

            monitors = new[] { Primary };
            window.RefreshMonitorPlacement();
            Assert.Equal(1920, (window.Left + window.Width) * scale, precision: 5);
            Assert.Equal("secondary", settings.PreferredMonitorId);

            monitors = new[] { Primary, Secondary };
            window.RefreshMonitorPlacement();
            Assert.Equal(1920, window.Left * scale, precision: 5);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Automatic_PreservesMonitorAtNegativeCoordinates() => StaThread.RunAsync(() =>
    {
        var leftDisplay = Secondary with { PhysicalWorkArea = new WorkArea(-1920, 0, 0, 1040) };
        var settings = new HyperPetSettings { PetLeft = -1500, PetTop = 100 };
        var window = new MainWindow(settings, _ => { }, () => { }, null,
            monitorProvider: () => new[] { Primary, leftDisplay });
        try
        {
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.Equal(-1500, window.Left);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Settings_PreservesUnavailableMonitor_AndAppliesAutomaticChoice() => StaThread.RunAsync(() =>
    {
        var settings = new HyperPetSettings { PreferredMonitorId = "disconnected-fixture" };
        var window = new SettingsWindow(settings, _ => { });
        try
        {
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var picker = (ComboBox)window.FindName("MonitorPickerComboBox");
            Assert.Equal("disconnected-fixture", picker.SelectedValue);

            picker.SelectedValue = string.Empty;
            var apply = (Button)window.FindName("ApplyButton");
            Assert.True(apply.IsEnabled);
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(string.Empty, settings.PreferredMonitorId);
        }
        finally { window.Close(); }
    });
}

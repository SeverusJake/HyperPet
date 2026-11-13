using System.Windows;
using System.Windows.Controls;
using HyperPet.App.Notifications;
using System.Windows.Interop;
using HyperPet.App.Tests.TestSupport;
using HyperPet.App.Views;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

[Collection("WPF UI")]
public sealed class DebugNotificationWindowTests
{
    [Fact]
    public Task SelfScan_AllowsOnlyRegisteredDebugPopups_AndRemovesClosedHandles() => StaThread.RunAsync(() =>
    {
        var popup = new DebugSimulatedPopupWindow("Test sender", "Test body");
        var other = new Window { Width = 280, Height = 100, ShowInTaskbar = false };
        IntPtr handle = IntPtr.Zero;
        try
        {
            popup.Show(); other.Show(); handle = new WindowInteropHelper(popup).Handle;
            Assert.Equal("Test sender", popup.Title);
            Assert.True(DebugSimulatedPopupWindow.IsDebugPopup(handle));
            Assert.False(DebugSimulatedPopupWindow.IsDebugPopup(new WindowInteropHelper(other).Handle));
        }
        finally { popup.Close(); other.Close(); }
        Assert.False(DebugSimulatedPopupWindow.IsDebugPopup(handle));
    });

    [Fact]
    public Task ClosingDuringScan_CancelsTestWithoutWaitingForProvider() => StaThread.RunAsync(async () =>
    {
        var entered = new TaskCompletionSource(); var provider = new TaskCompletionSource();
        var main = new MainWindow(new() { DebugMode = true }, _ => { }, () => { }, null,
            scanInAppNow: _ => { entered.SetResult(); return provider.Task; });
        try
        {
            main.Show(); var running = main.RunDebugNotificationTestAsync(DebugNotificationRoute.InAppPopup, DebugNotificationScenario.Short);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); main.Close();
            Assert.Contains("cancelled", await running.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally { provider.TrySetResult(); if (main.IsVisible) main.Close(); }
    });
    [Theory]
    [InlineData(DebugNotificationScenario.Short, 1)]
    [InlineData(DebugNotificationScenario.Long, 1)]
    [InlineData(DebugNotificationScenario.Unicode, 1)]
    [InlineData(DebugNotificationScenario.Burst, 3)]
    [InlineData(DebugNotificationScenario.Repeated, 2)]
    public Task DirectSamples_ReachActualPetBubble(DebugNotificationScenario scenario, int expected) => StaThread.RunAsync(async () =>
    {
        var settings = new HyperPetSettings { DebugMode = true };
        var main = new MainWindow(settings, _ => { }, () => { }, null);
        try
        {
            main.Show(); var result = await main.RunDebugNotificationTestAsync(DebugNotificationRoute.Direct, scenario);
            Assert.Contains($"pet showed {expected}/{expected}", result);
            Assert.Equal(Visibility.Visible, ((Border)main.FindName("Bubble")).Visibility);
            Assert.NotEmpty(((TextBlock)main.FindName("BubbleBody")).Text);
        }
        finally { main.Close(); }
    });

    [Fact]
    public Task DisabledSource_IsReportedInsteadOfClaimingDelivery() => StaThread.RunAsync(async () =>
    {
        var main = new MainWindow(new() { DebugMode = true, ReactToWindowsNotifications = false }, _ => { }, () => { }, null);
        try { Assert.Contains("Windows notifications are disabled", await main.RunDebugNotificationTestAsync(DebugNotificationRoute.WindowsToast, DebugNotificationScenario.Short)); }
        finally { main.Close(); }
    });

    [Fact]
    public Task Settings_UsesSelectedScenario_AndCloseCancelsPendingTest() => StaThread.RunAsync(async () =>
    {
        int calls = 0; CancellationToken observed = default;
        var dialog = new SettingsWindow(new() { DebugMode = true }, _ => { }, runDebugTest: async (route, scenario, token) =>
        {
            calls++; observed = token; Assert.Equal(DebugNotificationRoute.InAppPopup, route);
            Assert.Equal(DebugNotificationScenario.Unicode, scenario);
            await Task.Delay(Timeout.Infinite, token); return "never";
        });
        ((ComboBox)dialog.FindName("DebugTestScenarioComboBox")).SelectedValue = DebugNotificationScenario.Unicode;
        var running = dialog.SendDebugTestAsync();
        await dialog.SendDebugTestAsync(); Assert.Equal(1, calls);
        Assert.False(((Button)dialog.FindName("SendDebugTestButton")).IsEnabled);
        dialog.Close(); await running; Assert.True(observed.IsCancellationRequested);
    });
}

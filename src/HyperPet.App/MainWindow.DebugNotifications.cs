using System.Windows.Threading;
using HyperPet.App.Notifications;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pet;

namespace HyperPet.App;

public partial class MainWindow
{
    private DebugNotificationTest? _debugNotificationTest;
    private CancellationTokenSource? _debugTestCancellation;
    private readonly Func<CancellationToken, Task>? _scanInAppNow;

    internal void ObserveDebugNotification(HyperNotification notification, bool shown) => _debugNotificationTest?.Observe(notification, shown);

    internal async Task<string> RunDebugNotificationTestAsync(DebugNotificationRoute route, DebugNotificationScenario scenario, CancellationToken cancellationToken = default)
    {
        if (_closed || _tuckingAway || !_settings.DebugMode) return "Enable and Apply Debug mode first.";
        if (_debugNotificationTest is not null) return "A notification test is already running.";
        bool windows = route == DebugNotificationRoute.WindowsToast;
        if (windows ? !_settings.ReactToWindowsNotifications : !_settings.ReactToInAppNotifications)
            return windows ? "Windows notifications are disabled. Enable and Apply that source first." : "In-app notifications are disabled. Enable and Apply that source first.";
        if (windows && _debugSimulator is null) return "Windows toast simulator is unavailable.";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        _debugTestCancellation = lifetime;
        var token = lifetime.Token;
        var run = new DebugNotificationTest(scenario);
        var popups = new List<DebugSimulatedPopupWindow>();
        _debugNotificationTest = run;
        try
        {
            var source = windows ? NotificationSource.Windows : NotificationSource.InApp;
            foreach (var message in run.Messages)
            {
                token.ThrowIfCancellationRequested();
                if (!_settings.DebugMode) return "Test cancelled: Debug mode was turned off.";
                if (route == DebugNotificationRoute.Direct)
                {
                    var notification = new HyperNotification(Guid.NewGuid().ToString("N"), "Sim", message.Title, message.Body,
                        DateTimeOffset.UtcNow, appUserModelId: "Sim", source: source);
                    var alert = new PetController().HandleNotification(notification, _settings);
                    run.Observe(notification, alert is not null && TryShowAlert(alert));
                }
                else if (route == DebugNotificationRoute.InAppPopup)
                {
                    var popup = new DebugSimulatedPopupWindow(message.Title, message.Body);
                    popups.Add(popup); popup.Show();
                }
                else _debugSimulator!.SimulateGenericNotification(message.Title, message.Body);
                await Task.Delay(150, token);
            }
            if (windows) _pollSoon?.Invoke(TimeSpan.FromSeconds(1));
            if (route == DebugNotificationRoute.InAppPopup && _scanInAppNow is not null)
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                // A blocked UIA provider must not trap the test window's Cancel/Close.
                await _scanInAppNow(token).WaitAsync(token);
            }
            await run.Completion.WaitAsync(TimeSpan.FromSeconds(12), token);
            return run.Summary;
        }
        catch (TimeoutException) { return run.Summary; }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested) { return "Test timed out. " + run.Summary; }
        catch (OperationCanceledException) { return "Notification test cancelled. " + run.Summary; }
        catch (Exception exception) { return "Notification test failed: " + exception.Message; }
        finally
        {
            foreach (var popup in popups) popup.Close();
            _debugNotificationTest = null; _debugTestCancellation = null;
            ReportPollStatus(run.Summary);
        }
    }
}

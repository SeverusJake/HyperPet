using HyperPet.Core.Notifications;
using HyperPet.Windows.Notifications;

namespace HyperPet.App.Notifications;

// Enable/permission/subscription operations belong to the UI dispatcher.
// Native events can arrive from any thread; their delivery is generation-checked
// again after posting, including a disable -> enable cycle.
internal sealed class MonitoringLifecycle(Func<INotificationListener> create, Action<Action> post,
    Action<Exception>? reportError = null) : INotificationListener, IDisposable
{
    private INotificationListener? _listener;
    private Task<NotificationAccessStatus>? _pendingAccess;
    private EventHandler<HyperNotification>? _handler;
    private volatile bool _enabled;
    private volatile bool _disposed;
    private bool _accessAllowed;
    private int _generation;
    public int Generation => Volatile.Read(ref _generation);
    public bool CanDeliver(int generation) => !_disposed && _enabled && Generation == generation;
    public event EventHandler<HyperNotification>? NotificationAdded;

    public void SetEnabled(bool enabled)
    {
        if (_disposed || _enabled == enabled) return;
        _enabled = enabled; Interlocked.Increment(ref _generation);
        if (!enabled) StopListening();
    }

    public async Task<NotificationAccessStatus> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        int generation = Generation;
        if (!CanDeliver(generation)) return NotificationAccessStatus.Unspecified;
        if (_listener is null)
        {
            if (!NotificationListenerFactory.TryCreate(create, out var listener, out var error))
            { if (error is not null) reportError?.Invoke(error); return NotificationAccessStatus.Unspecified; }
            _listener = listener;
        }
        Task<NotificationAccessStatus>? pending = null;
        try
        {
            pending = _pendingAccess ??= _listener!.RequestAccessAsync(CancellationToken.None);
            var result = await pending.WaitAsync(cancellationToken);
            if (!CanDeliver(generation)) return NotificationAccessStatus.Unspecified;
            _accessAllowed = result == NotificationAccessStatus.Allowed;
            if (!_accessAllowed) StopListening();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { reportError?.Invoke(exception); return NotificationAccessStatus.Unspecified; }
        finally { if (pending?.IsCompleted == true && ReferenceEquals(_pendingAccess, pending)) _pendingAccess = null; }
    }

    public void StartListening()
    {
        int generation = Generation;
        if (!CanDeliver(generation) || !_accessAllowed || _listener is null || _handler is not null) return;
        _handler = (_, notification) =>
        {
            if (!CanDeliver(generation)) return;
            try { post(() => { if (CanDeliver(generation)) NotificationAdded?.Invoke(this, notification); }); }
            catch (Exception exception) { reportError?.Invoke(exception); }
        };
        _listener.NotificationAdded += _handler;
        try { _listener.StartListening(); }
        catch (Exception exception) { StopListening(); reportError?.Invoke(exception); }
    }

    public void StopListening()
    {
        if (_handler is null || _listener is null) return;
        _listener.NotificationAdded -= _handler; _handler = null;
        try { _listener.StopListening(); }
        catch (Exception exception) { reportError?.Invoke(exception); }
    }

    public async Task<IReadOnlyList<HyperNotification>> GetActiveNotificationsAsync(CancellationToken cancellationToken = default)
    {
        int generation = Generation;
        if (!CanDeliver(generation) || !_accessAllowed || _listener is null) return Array.Empty<HyperNotification>();
        var result = await _listener.GetActiveNotificationsAsync(cancellationToken);
        return CanDeliver(generation) ? result : Array.Empty<HyperNotification>();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _enabled = false; Interlocked.Increment(ref _generation); StopListening();
    }
}

using System.Collections.Concurrent;

namespace HyperPet.App.Notifications;

internal sealed class AutomationWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private int _disposed;
    public AutomationWorker()
    {
        var thread = new Thread(() => { foreach (var action in _queue.GetConsumingEnumerable()) action(); })
        { IsBackground = true, Name = "HyperPet UI Automation" };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
    }
    public Task RunAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (Volatile.Read(ref _disposed) != 0) { completion.SetCanceled(); return completion.Task; }
        try
        {
            _queue.Add(() =>
            {
                try { action(); completion.TrySetResult(); }
                catch (Exception exception) { completion.TrySetException(exception); }
            });
        }
        catch (InvalidOperationException) { completion.TrySetCanceled(); }
        return completion.Task;
    }
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) _queue.CompleteAdding(); }
}

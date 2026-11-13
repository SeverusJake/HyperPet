namespace HyperPet.Windows.Notifications;

// A native callback may finish conversion after its subscription was removed.
// Do not publish that result to subscribers belonging to the next subscription.
internal sealed class NotificationCallbackGate
{
    private readonly object _gate = new();
    private int _generation;
    private bool _listening;

    public int Start() { lock (_gate) { _listening = true; return ++_generation; } }
    public void Stop() { lock (_gate) { _listening = false; ++_generation; } }

    public void Deliver<T>(int generation, Func<T?> read, Action<T> publish) where T : class
    {
        lock (_gate) { if (!_listening || generation != _generation) return; }
        var value = read(); // Provider calls must not hold the gate or block Stop.
        if (value is null) return;
        lock (_gate)
        {
            if (_listening && generation == _generation) publish(value);
        }
    }
}

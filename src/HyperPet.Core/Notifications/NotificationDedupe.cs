namespace HyperPet.Core.Notifications;

public sealed class NotificationDedupe
{
    private readonly Dictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly Queue<(string Id, DateTimeOffset At)> _order = new();
    private readonly TimeProvider _time;
    private readonly int _capacity;
    private readonly TimeSpan _ttl;
    private readonly object _gate = new();
    public NotificationDedupe(TimeProvider? timeProvider = null, int capacity = 4096, TimeSpan? ttl = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _time = timeProvider ?? TimeProvider.System; _capacity = capacity; _ttl = ttl ?? TimeSpan.FromHours(24);
        if (_ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl));
    }
    public bool ShouldAlert(HyperNotification notification)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            while (_order.TryPeek(out var entry) && now - entry.At >= _ttl)
            {
                _order.Dequeue(); _seen.Remove(entry.Id);
            }
            if (_seen.ContainsKey(notification.SourceId)) return false;
            while (_seen.Count >= _capacity && _order.TryDequeue(out var oldest)) _seen.Remove(oldest.Id);
            _seen.Add(notification.SourceId, now); _order.Enqueue((notification.SourceId, now));
            return true;
        }
    }
}

using HyperPet.Core.Pet;

namespace HyperPet.Core.Notifications;

public sealed class NotificationInbox
{
    private readonly int _pendingLimit;
    private readonly int _historyLimit;
    private readonly Queue<PetAlert> _pending = new();
    private readonly List<NotificationHistoryEntry> _history = new();

    public NotificationInbox(int pendingLimit = 100, int historyLimit = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pendingLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(historyLimit);
        _pendingLimit = pendingLimit;
        _historyLimit = historyLimit;
        History = _history.AsReadOnly();
    }

    public PetAlert? Current { get; private set; }
    public int PendingCount => _pending.Count;
    public IReadOnlyList<NotificationHistoryEntry> History { get; }
    public event EventHandler? Changed;

    public bool Enqueue(PetAlert alert, bool recordHistory = true)
    {
        ArgumentNullException.ThrowIfNull(alert);

        if (recordHistory)
        {
            _history.Insert(0, new NotificationHistoryEntry(Guid.NewGuid(), alert));
            if (_history.Count > _historyLimit)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }

        var startsCurrent = Current is null;
        if (startsCurrent)
        {
            Current = alert;
        }
        else
        {
            if (_pending.Count == _pendingLimit)
            {
                _pending.Dequeue();
            }

            _pending.Enqueue(alert);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return startsCurrent;
    }

    public PetAlert? Advance()
    {
        if (Current is null && _pending.Count == 0)
        {
            return null;
        }

        Current = _pending.TryDequeue(out var next) ? next : null;
        Changed?.Invoke(this, EventArgs.Empty);
        return Current;
    }

    public void Clear()
    {
        if (Current is null && _pending.Count == 0)
        {
            return;
        }

        Current = null;
        _pending.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearHistory()
    {
        if (_history.Count == 0)
        {
            return;
        }

        _history.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RedactContent()
    {
        var changed = false;
        if (Current is { } current)
        {
            Current = Redact(current);
            changed |= !ReferenceEquals(Current, current);
        }

        for (var i = _pending.Count; i > 0; i--)
        {
            var alert = _pending.Dequeue();
            var redacted = Redact(alert);
            _pending.Enqueue(redacted);
            changed |= !ReferenceEquals(redacted, alert);
        }

        for (var i = 0; i < _history.Count; i++)
        {
            var entry = _history[i];
            var redacted = Redact(entry.Alert);
            if (!ReferenceEquals(redacted, entry.Alert))
            {
                _history[i] = entry with { Alert = redacted };
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static PetAlert Redact(PetAlert alert) =>
        alert.Title == "Notification" && alert.Body == string.Empty
            ? alert
            : alert with { Title = "Notification", Body = string.Empty };
}

using HyperPet.Core.Pet;

namespace HyperPet.Core.Notifications;

public sealed class NotificationInbox
{
    private readonly int _pendingLimit;
    private readonly int _historyLimit;
    // Newest-first display: the latest alert is always Current. Displaced
    // alerts wait here, oldest first, and are revisited newest first.
    private readonly List<PetAlert> _pending = new();
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

    /// <summary>Makes <paramref name="alert"/> current. The alert it displaces
    /// waits behind it; when the wait list is full its oldest entry is dropped.</summary>
    public void Enqueue(PetAlert alert, bool recordHistory = true)
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

        if (Current is { } displaced)
        {
            if (_pending.Count == _pendingLimit)
            {
                _pending.RemoveAt(0);
            }

            _pending.Add(displaced);
        }

        Current = alert;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops the current alert and returns the newest waiting one, or null.</summary>
    public PetAlert? Advance()
    {
        if (Current is null && _pending.Count == 0)
        {
            return null;
        }

        Current = TakeNewestPending();
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

    public void RemoveSource(NotificationSource source)
    {
        bool changed = _pending.RemoveAll(alert => alert.Source == source) > 0 || Current?.Source == source;
        if (Current?.Source == source) Current = TakeNewestPending();
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RedactContent()
    {
        var changed = false;
        if (Current is { } current)
        {
            Current = Redact(current);
            changed |= !ReferenceEquals(Current, current);
        }

        for (var i = 0; i < _pending.Count; i++)
        {
            var redacted = Redact(_pending[i]);
            changed |= !ReferenceEquals(redacted, _pending[i]);
            _pending[i] = redacted;
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

    private PetAlert? TakeNewestPending()
    {
        if (_pending.Count == 0)
        {
            return null;
        }

        var next = _pending[^1];
        _pending.RemoveAt(_pending.Count - 1);
        return next;
    }

    private static PetAlert Redact(PetAlert alert) =>
        alert.Title == "Notification" && alert.Body == string.Empty
            ? alert
            : alert with { Title = "Notification", Body = string.Empty };
}

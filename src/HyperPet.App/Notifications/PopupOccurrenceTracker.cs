namespace HyperPet.App.Notifications;

internal sealed class PopupOccurrenceTracker
{
    // A popup whose text stays unreadable for this many consecutive scans is
    // still reported once, without content, rather than silently dropped.
    internal const int EmptyScansBeforeFallback = 2;

    private static readonly (string Title, string Body) Unreadable = (string.Empty, string.Empty);
    private readonly Dictionary<IntPtr, (string Title, string Body)> _content = new();
    private readonly Dictionary<IntPtr, int> _emptyScans = new();
    private readonly string _session = Guid.NewGuid().ToString("N");
    private long _sequence;

    public string? Observe(IntPtr handle, string title, string body)
    {
        bool known = _content.TryGetValue(handle, out var previous);
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            if (known) return null;
            int scans = _emptyScans.GetValueOrDefault(handle) + 1;
            _emptyScans[handle] = scans;
            if (scans < EmptyScansBeforeFallback) return null;
            _content[handle] = Unreadable;
            return NextId();
        }

        _emptyScans.Remove(handle);
        var current = (title, body);
        if (known && previous == current) return null;
        _content[handle] = current;
        // Text that becomes readable after the fallback belongs to the popup
        // already reported; do not alert twice for it.
        return known && previous == Unreadable ? null : NextId();
    }

    public void Prune(IEnumerable<IntPtr> liveHandles)
    {
        var live = liveHandles.ToHashSet();
        foreach (var handle in _content.Keys.Where(handle => !live.Contains(handle)).ToArray()) _content.Remove(handle);
        foreach (var handle in _emptyScans.Keys.Where(handle => !live.Contains(handle)).ToArray()) _emptyScans.Remove(handle);
    }

    private string NextId() => $"inapp:{_session}:{++_sequence}";
}

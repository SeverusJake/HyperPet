namespace HyperPet.App.Notifications;

internal sealed class PopupOccurrenceTracker
{
    private readonly Dictionary<IntPtr, (string Title, string Body)> _content = new();
    private readonly string _session = Guid.NewGuid().ToString("N");
    private long _sequence;
    public string? Observe(IntPtr handle, string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body)) return null;
        var current = (title, body);
        if (_content.TryGetValue(handle, out var previous) && previous == current) return null;
        _content[handle] = current;
        return $"inapp:{_session}:{++_sequence}";
    }
    public void Prune(IEnumerable<IntPtr> liveHandles)
    {
        var live = liveHandles.ToHashSet();
        foreach (var handle in _content.Keys.Where(handle => !live.Contains(handle)).ToArray()) _content.Remove(handle);
    }
}

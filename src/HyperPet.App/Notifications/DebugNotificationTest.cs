using HyperPet.Core.Notifications;

namespace HyperPet.App.Notifications;

public enum DebugNotificationRoute { Direct, InAppPopup, WindowsToast }
public enum DebugNotificationScenario { Short, Long, Unicode, Burst, Repeated }
internal sealed record DebugNotificationMessage(string Title, string Body);

internal sealed class DebugNotificationTest
{
    private readonly string _marker = $"[Test {Guid.NewGuid().ToString("N")[..6]}] ";
    private readonly HashSet<string> _received = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource _complete = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IReadOnlyList<DebugNotificationMessage> Messages { get; }
    public int Received => _received.Count;
    public int Shown { get; private set; }
    private int _mismatched;
    public Task Completion => _complete.Task;

    public DebugNotificationTest(DebugNotificationScenario scenario)
    {
        var title = _marker + "Debug sender";
        Messages = scenario switch
        {
            DebugNotificationScenario.Short => new[] { new DebugNotificationMessage(title, "A short test message.") },
            DebugNotificationScenario.Long => new[] { new DebugNotificationMessage(title,
                string.Join("\n", Enumerable.Range(1, 12).Select(i => $"Line {i}: This is a long notification to test wrapping, scrolling and keeping the bubble inside the screen."))) },
            DebugNotificationScenario.Unicode => new[] { new DebugNotificationMessage(title + " — Tiếng Việt", "Bạn nhận được tin nhắn thử nghiệm: ă â ê ô ơ ư đ — 👋 🐾 🎉") },
            DebugNotificationScenario.Burst => Enumerable.Range(1, 3).Select(i => new DebugNotificationMessage(title + $" #{i}", $"Burst message {i} of 3. The newest message should appear first.")).ToArray(),
            DebugNotificationScenario.Repeated => Enumerable.Range(1, 2).Select(_ => new DebugNotificationMessage(title, "Identical content, two distinct notifications.")).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }

    public void Observe(HyperNotification notification, bool shown)
    {
        if (!notification.Title.StartsWith(_marker, StringComparison.Ordinal) && !notification.Body.StartsWith(_marker, StringComparison.Ordinal)) return;
        if (!_received.Add(notification.SourceId)) return;
        if (!Messages.Any(message => message.Title == notification.Title && message.Body == notification.Body)) _mismatched++;
        if (shown) Shown++;
        if (Received >= Messages.Count) _complete.TrySetResult();
    }

    public string Summary => $"Received {Received}/{Messages.Count}; pet showed {Shown}/{Messages.Count}." +
        (_mismatched > 0 ? $" Unexpected decoded text in {_mismatched} message(s)." :
        Received < Messages.Count ? " Missing deliveries: check source, permission and polling." :
        Shown < Received ? " Received but hidden: check pause, quiet hours and app rules." : " Test passed.");
}

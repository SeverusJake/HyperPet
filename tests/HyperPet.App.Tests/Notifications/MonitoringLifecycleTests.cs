using HyperPet.App.Notifications;
using HyperPet.Core.Notifications;
using HyperPet.Windows.Notifications;

namespace HyperPet.App.Tests.Notifications;

public sealed class MonitoringLifecycleTests
{
    [Fact]
    public async Task ShutdownDuringPendingAccess_DiscardsCompletion()
    {
        var fake = new FakeListener(); var lifecycle = new MonitoringLifecycle(() => fake, action => action());
        lifecycle.SetEnabled(true); var pending = lifecycle.RequestAccessAsync(); lifecycle.Dispose();
        fake.Access.SetResult(NotificationAccessStatus.Allowed);
        Assert.Equal(NotificationAccessStatus.Unspecified, await pending);
        lifecycle.StartListening(); Assert.Equal(0, fake.Starts);
    }

    [Fact]
    public async Task BlockedAutomationWorker_DisposeDoesNotWaitForProvider()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var worker = new AutomationWorker();
        var pending = worker.RunAsync(() => { entered.Set(); release.Wait(); });
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            await Task.Run(worker.Dispose).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(pending.IsCompleted);
        }
        finally { release.Set(); await pending.WaitAsync(TimeSpan.FromSeconds(2)); worker.Dispose(); }
    }
    [Fact]
    public async Task DisabledSource_DoesNotCreateAdapter_AndCreationFailureCanRetry()
    {
        int created = 0; var fake = new FakeListener();
        using var lifecycle = new MonitoringLifecycle(() => { if (++created == 1) throw new InvalidOperationException("native"); return fake; }, action => action());
        await lifecycle.RequestAccessAsync(); Assert.Equal(0, created);
        lifecycle.SetEnabled(true);
        Assert.Equal(NotificationAccessStatus.Unspecified, await lifecycle.RequestAccessAsync());
        fake.Access.SetResult(NotificationAccessStatus.Allowed);
        Assert.Equal(NotificationAccessStatus.Allowed, await lifecycle.RequestAccessAsync()); Assert.Equal(2, created);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingAccess_RejectsOldGeneration_ButReenableCanShareRequest(bool reenable)
    {
        var fake = new FakeListener(); using var lifecycle = new MonitoringLifecycle(() => fake, action => action());
        lifecycle.SetEnabled(true); var old = lifecycle.RequestAccessAsync(); lifecycle.SetEnabled(false);
        Task<NotificationAccessStatus>? latest = null;
        if (reenable) { lifecycle.SetEnabled(true); latest = lifecycle.RequestAccessAsync(); }
        fake.Access.SetResult(NotificationAccessStatus.Allowed);
        Assert.Equal(NotificationAccessStatus.Unspecified, await old);
        if (latest is not null) Assert.Equal(NotificationAccessStatus.Allowed, await latest);
        Assert.Equal(1, fake.Requests);
    }

    [Fact]
    public async Task QueuedEventAndPoll_FromPreviousEnable_AreDiscarded()
    {
        var fake = new FakeListener(); var dispatch = new Queue<Action>(); int received = 0;
        using var lifecycle = new MonitoringLifecycle(() => fake, dispatch.Enqueue);
        lifecycle.NotificationAdded += (_, _) => received++;
        lifecycle.SetEnabled(true); fake.Access.SetResult(NotificationAccessStatus.Allowed); await lifecycle.RequestAccessAsync();
        lifecycle.StartListening(); lifecycle.StartListening();
        Assert.Equal(1, fake.Starts); fake.Emit(); var pendingPoll = lifecycle.GetActiveNotificationsAsync();
        lifecycle.SetEnabled(false); lifecycle.SetEnabled(true); lifecycle.StartListening();
        dispatch.Dequeue()(); fake.Poll.SetResult(new[] { FakeListener.Notification });
        Assert.Empty(await pendingPoll); Assert.Equal(0, received); Assert.Equal(1, fake.Stops);
        fake.Emit(); dispatch.Dequeue()(); Assert.Equal(1, received);
    }

    [Fact]
    public async Task DeniedOrShutdown_CannotSubscribeOrFinishOldRequest()
    {
        var fake = new FakeListener(); using var lifecycle = new MonitoringLifecycle(() => fake, action => action());
        lifecycle.SetEnabled(true); fake.Access.SetResult(NotificationAccessStatus.Denied);
        Assert.Equal(NotificationAccessStatus.Denied, await lifecycle.RequestAccessAsync());
        lifecycle.StartListening(); Assert.Equal(0, fake.Starts);
        lifecycle.Dispose(); lifecycle.SetEnabled(true); lifecycle.StartListening(); Assert.Equal(0, fake.Starts);
    }

    private sealed class FakeListener : INotificationListener
    {
        internal static readonly HyperNotification Notification = new("id", "app", "title", "body", DateTimeOffset.UtcNow, false);
        public readonly TaskCompletionSource<NotificationAccessStatus> Access = new();
        public readonly TaskCompletionSource<IReadOnlyList<HyperNotification>> Poll = new();
        public int Requests, Starts, Stops;
        public event EventHandler<HyperNotification>? NotificationAdded;
        public Task<NotificationAccessStatus> RequestAccessAsync(CancellationToken token = default) { Requests++; return Access.Task; }
        public Task<IReadOnlyList<HyperNotification>> GetActiveNotificationsAsync(CancellationToken token = default) => Poll.Task;
        public void StartListening() => Starts++;
        public void StopListening() => Stops++;
        public void Emit() => NotificationAdded?.Invoke(this, Notification);
    }
}

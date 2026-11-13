using HyperPet.Windows.Notifications;

namespace HyperPet.App.Tests.Notifications;

public sealed class NativeCallbackGenerationTests
{
    [Fact]
    public async Task ConversionFromOldSubscription_CannotPublishToNewSubscribers()
    {
        var gate = new NotificationCallbackGate(); int published = 0;
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        int old = gate.Start();
        var callback = Task.Run(() => gate.Deliver(old,
            () => { entered.Set(); release.Wait(); return "old notification"; }, _ => published++));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            gate.Stop(); int current = gate.Start(); release.Set(); await callback.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(0, published);
            gate.Deliver(current, () => "current notification", _ => published++); Assert.Equal(1, published);
        }
        finally { release.Set(); await callback.WaitAsync(TimeSpan.FromSeconds(2)); }
    }
}

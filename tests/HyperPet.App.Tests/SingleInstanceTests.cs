using HyperPet.App.Lifecycle;

namespace HyperPet.App.Tests;
public sealed class SingleInstanceTests
{
    [Fact]
    public void SecondOwner_IsRejected_AndCanActivateFirstOwner()
    {
        string key = "test-" + Guid.NewGuid().ToString("N");
        using var first = new SingleInstanceCoordinator(key);
        Assert.True(first.TryAcquire());
        using var activated = new ManualResetEventSlim();
        first.StartActivationListener(() => activated.Set());
        Exception? error = null;
        var second = new Thread(() =>
        {
            try
            {
                using var other = new SingleInstanceCoordinator(key);
                Assert.False(other.TryAcquire()); other.RequestActivationAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception) { error = exception; }
        });
        second.Start(); Assert.True(second.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(error); Assert.True(activated.Wait(TimeSpan.FromSeconds(3)));
    }
}

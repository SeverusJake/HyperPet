using HyperPet.App.Pets.Roaming;
using Xunit;

namespace HyperPet.App.Tests.Pets.Roaming;

public class WindowLedgeProviderShouldRebuildTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public void NoCache_AlwaysRebuilds()
    {
        Assert.True(WindowLedgeProvider.ShouldRebuild(DateTime.UtcNow, default, hasCache: false, Ttl));
    }

    [Fact]
    public void WithinTtl_DoesNotRebuild()
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(WindowLedgeProvider.ShouldRebuild(t0.AddMilliseconds(500), t0, hasCache: true, Ttl));
    }

    [Fact]
    public void AtOrPastTtl_Rebuilds()
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(WindowLedgeProvider.ShouldRebuild(t0.AddMilliseconds(1000), t0, hasCache: true, Ttl));
        Assert.True(WindowLedgeProvider.ShouldRebuild(t0.AddMilliseconds(1500), t0, hasCache: true, Ttl));
    }
}

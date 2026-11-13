using HyperPet.App.Lifecycle;

namespace HyperPet.App.Tests;

public sealed class LaunchContextTests
{
    [Fact]
    public void Development_UsesSeparateDataAndNeverManagesStartup()
    {
        var context = AppLaunchContext.Resolve(true, false, "roaming", "local");
        Assert.False(context.CanManageStartup);
        Assert.Equal(System.IO.Path.Combine("local", "HyperPet", "Dev"), context.DataDirectory);
    }
    [Fact]
    public void Portable_NeverTakesOverInstalledAutorun()
    {
        Assert.False(AppLaunchContext.Resolve(false, false, "roaming", "local").CanManageStartup);
        Assert.True(AppLaunchContext.Resolve(false, true, "roaming", "local").CanManageStartup);
    }
}

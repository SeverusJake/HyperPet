using System.IO;
using HyperPet.App.Lifecycle;

namespace HyperPet.App.Tests;

public sealed class UninstallCleanupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_RemovesStartupRegistration_EvenIfDataCleanupFails(bool failDataCleanup)
    {
        bool registered = true;
        bool dataCleanupAttempted = false;

        UninstallCleanup.Run(
            () => registered = false,
            () =>
            {
                dataCleanupAttempted = true;
                if (failDataCleanup) { throw new IOException("Locked user data"); }
            });

        Assert.False(registered);
        Assert.True(dataCleanupAttempted);
    }

    [Fact]
    public void Run_StillCleansUserData_WhenRegistryCleanupFails()
    {
        bool dataRemains = true;

        UninstallCleanup.Run(
            () => throw new UnauthorizedAccessException("Registry denied"),
            () => dataRemains = false);

        Assert.False(dataRemains);
    }
}

using HyperPet.Core.Settings;

namespace HyperPet.Core.Tests.Settings;

public sealed class SettingsRecoveryTests
{
    [Fact]
    public async Task LockedSettings_DisablesPersistenceAndStartup_WithoutChangingFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "HyperPet.Recovery", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.json");
        const string original = "{\"StartWithWindows\":false,\"SelectedPet\":\"saved\"}";
        await File.WriteAllTextAsync(path, original);
        try
        {
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = await new SettingsStore(directory).LoadSessionAsync();
                Assert.False(result.CanPersist); Assert.False(result.CanApplyStartup);
                Assert.NotNull(result.Warning);
            }
            Assert.Equal(original, await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(directory, true); }
    }
}

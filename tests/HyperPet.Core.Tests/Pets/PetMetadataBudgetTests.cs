using HyperPet.Core.Pets;

namespace HyperPet.Core.Tests.Pets;
public sealed class PetMetadataBudgetTests
{
    [Fact]
    public async Task OversizedMetadata_IsRejectedBeforeDeserialization()
    {
        string root = Path.Combine(Path.GetTempPath(), "HyperPet.Metadata", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "pet.json"), new string(' ', 1024 * 1024) + "{}");
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => PetDefinitionLoader.LoadAsync(root));
            Assert.Contains("1 MiB", error.Message);
        }
        finally { Directory.Delete(root, true); }
    }
}

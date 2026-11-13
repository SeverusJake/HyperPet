using System.IO;
using HyperPet.App.Pets;
using HyperPet.App.Tests.TestSupport;
using HyperPet.App.Tests.Views;

namespace HyperPet.App.Tests.Pets;

[Collection("WPF UI")]
public sealed class PetRemovalServiceTests
{
    [Theory]
    [InlineData("fallback")]
    [InlineData("rename")]
    [InlineData("commit")]
    [InlineData("rollback")]
    [InlineData("cleanup")]
    [InlineData("partial-cleanup")]
    [InlineData("none")]
    [InlineData("no-fallback")]
    [InlineData("wrong-id")]
    public Task Removal_PreservesDataUntilFallbackCommit_ReportsRecovery(string failure) => StaThread.RunAsync(async () =>
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "HyperPet.Remove", Guid.NewGuid().ToString("N"));
        string root = Path.Combine(sandbox, "Pets"), source = Path.Combine(root, "a");
        Directory.CreateDirectory(source); File.WriteAllText(Path.Combine(source, "marker"), "keep");
        File.WriteAllText(Path.Combine(source, "remaining"), "recover");
        var entry = new PetCatalogEntry("a", "A", source);
        var fallback = new PetCatalogEntry("b", "B", "b"); int commits = 0;
        var service = new PetRemovalService(new Faults(failure));
        try
        {
            var result = await service.RemoveAsync(root, entry, "a",
                failure == "no-fallback" ? new[] { entry } : new[] { entry, fallback },
                (_, _) => Task.FromResult(failure == "fallback" ? null : Prepared(failure == "wrong-id" ? "a" : "b")),
                _ => { Assert.False(Directory.Exists(source)); commits++; return failure is not ("commit" or "rollback"); }, CancellationToken.None);
            bool success = failure is "none" or "cleanup" or "partial-cleanup";
            Assert.Equal(success, result.Success);
            Assert.Equal(success || failure is "commit" or "rollback" ? 1 : 0, commits);
            if (failure == "rollback")
            {
                Assert.NotNull(result.RecoveryDirectory);
                Assert.Equal("keep", File.ReadAllText(Path.Combine(result.RecoveryDirectory!, "marker")));
                Assert.False(Directory.Exists(source));
            }
            else if (!success) Assert.Equal("keep", File.ReadAllText(Path.Combine(source, "marker")));
            else Assert.False(Directory.Exists(source));
            if (failure is "cleanup" or "partial-cleanup")
            {
                Assert.NotNull(result.RecoveryDirectory); Assert.NotNull(result.Error);
                Assert.Equal("recover", File.ReadAllText(Path.Combine(result.RecoveryDirectory!, "remaining")));
                Assert.Equal(failure == "cleanup", File.Exists(Path.Combine(result.RecoveryDirectory!, "marker")));
            }
        }
        finally { Directory.Delete(sandbox, true); }
    });

    [Fact]
    public Task InactiveRemoval_DoesNotLoadOrCommitFallback() => StaThread.RunAsync(async () =>
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "HyperPet.Remove", Guid.NewGuid().ToString("N"));
        string root = Path.Combine(sandbox, "Pets"), source = Path.Combine(root, "a"); Directory.CreateDirectory(source);
        try
        {
            var result = await new PetRemovalService().RemoveAsync(root, new("a", "A", source), "b", Array.Empty<PetCatalogEntry>(),
                (_, _) => throw new Exception("unexpected load"), _ => throw new Exception("unexpected commit"), CancellationToken.None);
            Assert.True(result.Success); Assert.False(Directory.Exists(source));
        }
        finally { Directory.Delete(sandbox, true); }
    });

    private static PreparedPetReload Prepared(string id = "b")
    {
        var pet = SettingsTransactionTests.MakePet(id);
        return new(pet, PetOverrides.SnapshotFps(pet), PetOverrides.SnapshotPlayMode(pet), 1);
    }
    private sealed class Faults(string failure) : PetRemovalFileOperations
    {
        private int _moves;
        public override void Move(string source, string target)
        {
            if (failure == "rename" || failure == "rollback" && ++_moves > 1) throw new IOException("locked");
            base.Move(source, target);
        }
        public override void Delete(string path)
        {
            if (failure == "partial-cleanup") File.Delete(Path.Combine(path, "marker"));
            if (failure is "cleanup" or "partial-cleanup") throw new IOException("locked");
            base.Delete(path);
        }
    }
}

using System.IO;
using System.Windows.Controls;
using HyperPet.App.Pets;
using HyperPet.App.Tests.TestSupport;
using HyperPet.App.Views;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

[Collection("WPF UI")]
public sealed class PetRemovalWindowTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public Task ActiveRemoval_CommitsFallbackOrKeepsFilesOnClose(bool closeWhilePreparing, bool staleSelectedId) => StaThread.RunAsync(async () =>
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "HyperPet.RemoveUI", Guid.NewGuid().ToString("N"));
        string root = Path.Combine(sandbox, "Pets"), source = Path.Combine(root, "a");
        Directory.CreateDirectory(source); File.WriteAllText(Path.Combine(source, "marker"), "keep");
        var settings = new HyperPetSettings { SelectedPet = staleSelectedId ? "missing" : "a" }; int saved = 0;
        var pending = new TaskCompletionSource<SpritePet?>(); var fallback = SettingsTransactionTests.MakePet("b");
        var current = SettingsTransactionTests.MakePet("a");
        var main = new MainWindow(settings, _ => { }, () => { }, current, petLoader: _ => pending.Task);
        var entry = new PetCatalogEntry("a", "A", source);
        var dialog = new SettingsWindow(settings, _ => { }, () => saved++, current,
            petCatalog: new[] { entry, new PetCatalogEntry("b", "B", "b") }, userPetsRoot: root,
            preparePet: main.PreparePetReloadAsync, canCommitPet: main.CanCommitPreparedPet, commitPet: main.CommitPreparedPet);
        try
        {
            main.Show(); var removal = dialog.RemovePetAsync(entry);
            Assert.False(await dialog.CommitChanges());
            if (closeWhilePreparing) dialog.Close();
            pending.SetResult(fallback); var result = await removal;
            Assert.Equal(!closeWhilePreparing, result.Success);
            Assert.Equal(closeWhilePreparing ? "a" : "b", settings.SelectedPet);
            Assert.Equal(closeWhilePreparing ? 0 : 1, saved);
            Assert.Equal(closeWhilePreparing, File.Exists(Path.Combine(source, "marker")));
            if (!closeWhilePreparing) Assert.Same(fallback.GetFrames("idle")[0], ((Image)main.FindName("PetImage")).Source);
        }
        finally { if (!closeWhilePreparing) dialog.Close(); main.Close(); Directory.Delete(sandbox, true); }
    });
}

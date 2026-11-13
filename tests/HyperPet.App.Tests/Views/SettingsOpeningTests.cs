using HyperPet.App.Pets;
using HyperPet.App.Tests.TestSupport;

namespace HyperPet.App.Tests.Views;

[Collection("WPF UI")]
public sealed class SettingsOpeningTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PendingDiscovery_CannotOpenDuplicateOrClosedOwner(bool closeOwner) => StaThread.RunAsync(async () =>
    {
        int discoveries = 0, dialogs = 0;
        var pending = new TaskCompletionSource<IReadOnlyList<PetCatalogEntry>>();
        var window = new MainWindow(new(), _ => { }, () => { }, null, rediscoverPets: () => { discoveries++; return pending.Task; });
        window.SettingsDialogPresenter = dialog => { dialogs++; dialog.Close(); };
        window.Show();
        try
        {
            var first = window.OpenSettingsAsync(); var second = window.OpenSettingsAsync();
            Assert.Equal(1, discoveries);
            if (closeOwner) window.Close();
            pending.SetResult(Array.Empty<PetCatalogEntry>());
            await Task.WhenAll(first, second);
            Assert.Equal(closeOwner ? 0 : 1, dialogs);
        }
        finally { if (!closeOwner) window.Close(); }
    });
}

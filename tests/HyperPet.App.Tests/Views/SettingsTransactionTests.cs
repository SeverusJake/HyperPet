using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HyperPet.App.Pets;
using HyperPet.App.Tests.TestSupport;
using HyperPet.App.Views;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

[Collection("WPF UI")]
public sealed class SettingsTransactionTests
{
    [Fact]
    public Task RequestedPetMissing_CannotPrepareAnUnrelatedPet() => StaThread.RunAsync(async () =>
    {
        var main = new MainWindow(new(), _ => { }, () => { }, MakePet("a"),
            petLoader: _ => Task.FromResult<SpritePet?>(MakePet("a")));
        try { Assert.Null(await main.PreparePetReloadAsync("missing-b", CancellationToken.None)); }
        finally { main.Close(); }
    });
    [Fact]
    public Task SuccessfulPetApply_UpdatesLivePetAndSelectionAndPersistsOnce() => StaThread.RunAsync(async () =>
    {
        var settings = new HyperPetSettings { SelectedPet = "a", StartWithWindows = false };
        var next = MakePet("b"); int saved = 0;
        var main = new MainWindow(settings, _ => { }, () => { }, MakePet("a"), petLoader: _ => Task.FromResult<SpritePet?>(next));
        var dialog = new SettingsWindow(settings, _ => { }, () => saved++,
            petCatalog: new[] { new PetCatalogEntry("a", "A", "a"), new PetCatalogEntry("b", "B", "b") },
            preparePet: main.PreparePetReloadAsync, canCommitPet: main.CanCommitPreparedPet, commitPet: main.CommitPreparedPet);
        try
        {
            main.Show(); ((ComboBox)dialog.FindName("PetPickerComboBox")).SelectedValue = "b";
            ((TextBox)dialog.FindName("PetSizeTextBox")).Text = "4";
            Assert.True(await dialog.CommitChanges()); Assert.Equal(1, saved);
            Assert.Equal("b", settings.SelectedPet); Assert.Equal(4, settings.PetSize);
            Assert.Same(next.GetFrames("idle")[0], ((Image)main.FindName("PetImage")).Source);
        }
        finally { dialog.Close(); main.Close(); }
    });
    [Fact]
    public Task ClosingDuringPreparation_DoesNotPersistOrChangeStartup() => StaThread.RunAsync(async () =>
    {
        var settings = new HyperPetSettings { SelectedPet = "a", StartWithWindows = false };
        var pending = new TaskCompletionSource<PreparedPetReload?>(); int effects = 0;
        var dialog = new SettingsWindow(settings, _ => effects++, () => effects++,
            petCatalog: new[] { new PetCatalogEntry("a", "A", "a"), new PetCatalogEntry("b", "B", "b") },
            preparePet: (_, _) => pending.Task, canCommitPet: (_, _) => true, commitPet: _ => effects++);
        ((ComboBox)dialog.FindName("PetPickerComboBox")).SelectedValue = "b";
        ((TextBox)dialog.FindName("PetSizeTextBox")).Text = "4";
        string before = JsonSerializer.Serialize(settings);
        var apply = dialog.CommitChanges();
        Assert.False(await dialog.CommitChanges()); // Repeated Apply while pending is rejected.
        dialog.Close(); pending.SetResult(null);
        Assert.False(await apply); Assert.Equal(0, effects);
        Assert.Equal(before, JsonSerializer.Serialize(settings));
    });

    [Fact]
    public Task SuccessfulApply_CommitsOnce_AndRestoresRemovedOverride() => StaThread.RunAsync(async () =>
    {
        var settings = new HyperPetSettings { SelectedPet = "a", StartWithWindows = false };
        var pet = MakePet("a"); var originalFps = PetOverrides.SnapshotFps(pet);
        settings.StateSpeedOverrides = new(StringComparer.OrdinalIgnoreCase)
        { ["a"] = new(StringComparer.OrdinalIgnoreCase) { ["IDLE"] = 20 } };
        PetOverrides.Apply(pet, settings);
        int saved = 0;
        var dialog = new SettingsWindow(settings, _ => { }, () => saved++, pet, originalFps, PetOverrides.SnapshotPlayMode(pet));
        try
        {
            typeof(SettingsWindow).GetMethod("OnDefaultClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(dialog, new object[] { dialog, new System.Windows.RoutedEventArgs() });
            Assert.True(await dialog.CommitChanges());
            Assert.Equal(1, saved); Assert.Equal(2, pet.Definition.States["idle"].Fps);
            Assert.False(settings.StateSpeedOverrides.ContainsKey("a"));
        }
        finally { dialog.Close(); }
    });
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FailedPreparationOrStartup_PreservesSharedSettingsAndPet(bool startupFails) => StaThread.RunAsync(async () =>
    {
        var settings = new HyperPetSettings { SelectedPet = "a", StartWithWindows = false };
        var original = MakePet("a"); int startup = 0, persist = 0;
        var main = new MainWindow(settings, _ => { }, () => { }, original,
            petLoader: _ => Task.FromResult(startupFails ? MakePet("b") : null));
        var dialog = new SettingsWindow(settings, _ => { startup++; throw new InvalidOperationException("startup failure"); },
            () => persist++, original,
            petCatalog: new[] { new PetCatalogEntry("a", "A", "a"), new PetCatalogEntry("b", "B", "b") },
            preparePet: main.PreparePetReloadAsync, canCommitPet: main.CanCommitPreparedPet, commitPet: main.CommitPreparedPet);
        dialog.CommitWarning = _ => { };
        string before = JsonSerializer.Serialize(settings);
        try
        {
            main.Show();
            ((ComboBox)dialog.FindName("PetPickerComboBox")).SelectedValue = "b";
            ((TextBox)dialog.FindName("PetSizeTextBox")).Text = "4";
            ((CheckBox)dialog.FindName("StartWithWindowsCheckBox")).IsChecked = true;
            Assert.False(await dialog.CommitChanges());
            Assert.Equal(before, JsonSerializer.Serialize(settings));
            Assert.Equal(startupFails ? 1 : 0, startup); Assert.Equal(0, persist);
            Assert.Same(original.GetFrames("idle")[0], ((Image)main.FindName("PetImage")).Source);
        }
        finally { dialog.Close(); main.Close(); }
    });

    internal static SpritePet MakePet(string id)
    {
        var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4); image.Freeze();
        return new(new PetDefinition { Id = id, States = new Dictionary<string, PetAnimationState> { ["idle"] = new() { Frames = 1, Fps = 2, Loop = true } } },
            new Dictionary<string, IReadOnlyList<BitmapSource>> { ["idle"] = new[] { image } });
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyperPet.App.Tests.TestSupport;
using HyperPet.App.Views;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

[Collection("WPF UI")]
public sealed class DebugModeWindowTests
{
    [Fact]
    public Task ReenableAfterHidingOverlay_ShowsAgainAndSynchronizesDialog() => StaThread.RunAsync(async () =>
    {
        var settings = new HyperPetSettings(); int saved = 0;
        var main = new MainWindow(settings, _ => { }, () => saved++, null);
        main.SettingsDialogPresenter = dialog =>
        {
            var checkbox = (CheckBox)dialog.FindName("DebugModeCheckBox");
            main.ToggleDebugMode(); Assert.True(checkbox.IsChecked);
            typeof(MainWindow).GetMethod("ToggleDebugOverlayPill", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(main, null);
            Assert.Equal(Visibility.Collapsed, ((Border)main.FindName("DebugOverlay")).Visibility);
            main.ToggleDebugMode(); Assert.False(checkbox.IsChecked);
            main.ToggleDebugMode(); Assert.True(checkbox.IsChecked);
            Assert.Equal(Visibility.Visible, ((Border)main.FindName("DebugOverlay")).Visibility);
            dialog.Close();
        };
        try { main.Show(); await main.OpenSettingsAsync(); Assert.Equal(3, saved); }
        finally { main.Close(); }
    });

    [Fact]
    public Task ShortcutDuringPendingApply_DoesNotChangeSharedSettings() => StaThread.RunAsync(async () =>
    {
        var settings = new HyperPetSettings { SelectedPet = "a" }; int toggles = 0;
        var pending = new TaskCompletionSource<HyperPet.App.Pets.PreparedPetReload?>();
        var dialog = new SettingsWindow(settings, _ => { },
            petCatalog: new[] { new HyperPet.App.Pets.PetCatalogEntry("a", "A", "a"), new HyperPet.App.Pets.PetCatalogEntry("b", "B", "b") },
            preparePet: (_, _) => pending.Task, canCommitPet: (_, _) => true, commitPet: _ => { }, toggleDebugMode: () => toggles++);
        ((ComboBox)dialog.FindName("PetPickerComboBox")).SelectedValue = "b";
        var applying = dialog.CommitChanges();
        dialog.ToggleDebugModeFromShortcut(); Assert.Equal(0, toggles); Assert.False(settings.DebugMode);
        dialog.Close(); pending.SetResult(null); Assert.False(await applying);
    });
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SettingsDebugToggle_AppliesAndPersists(bool initial) => StaThread.RunAsync(async () =>
    {
        var settings = new HyperPetSettings { DebugMode = initial }; int saved = 0;
        var dialog = new SettingsWindow(settings, _ => { }, () => saved++);
        try
        {
            var checkbox = (CheckBox)dialog.FindName("DebugModeCheckBox");
            Assert.Equal("Debug mode", checkbox.Content);
            checkbox.IsChecked = !initial;
            Assert.Equal(initial, settings.DebugMode);
            Assert.True(await dialog.CommitChanges());
            Assert.Equal(!initial, settings.DebugMode); Assert.Equal(1, saved);
        }
        finally { dialog.Close(); }
    });

    [Fact]
    public Task ShortcutInSettings_AppliesOnlyDebugImmediately() => StaThread.RunAsync(() =>
    {
        var settings = new HyperPetSettings(); int saved = 0, monitoring = 0;
        var main = new MainWindow(settings, _ => { }, () => saved++, null, applyMonitoringSettings: () => monitoring++);
        var dialog = new SettingsWindow(settings, _ => { }, toggleDebugMode: main.ToggleDebugMode);
        try
        {
            main.Show(); ((TextBox)dialog.FindName("PetSizeTextBox")).Text = "4";
            dialog.ToggleDebugModeFromShortcut();
            Assert.True(settings.DebugMode); Assert.True(((CheckBox)dialog.FindName("DebugModeCheckBox")).IsChecked);
            Assert.Equal(8, settings.PetSize); Assert.Equal("4", ((TextBox)dialog.FindName("PetSizeTextBox")).Text);
            Assert.Equal(Visibility.Visible, ((Border)main.FindName("DebugOverlay")).Visibility);
            Assert.Equal(1, saved); Assert.Equal(1, monitoring);
            dialog.ToggleDebugModeFromShortcut();
            Assert.False(settings.DebugMode); Assert.Equal(Visibility.Collapsed, ((Border)main.FindName("DebugOverlay")).Visibility);
            Assert.Equal(2, saved);
        }
        finally { dialog.Close(); main.Close(); }
    });

    [Theory]
    [InlineData(Key.D, ModifierKeys.Control | ModifierKeys.Alt, false, true, 1)]
    [InlineData(Key.D, ModifierKeys.Control | ModifierKeys.Alt, true, true, 0)]
    [InlineData(Key.D, ModifierKeys.None, false, false, 0)]
    [InlineData(Key.D, ModifierKeys.Control, false, false, 0)]
    [InlineData(Key.D, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, false, false, 0)]
    [InlineData(Key.F1, ModifierKeys.Control | ModifierKeys.Alt, false, false, 0)]
    public void Gesture_RequiresExactKeys_AndIgnoresAutoRepeat(Key key, ModifierKeys modifiers, bool repeat, bool handled, int calls)
    {
        int toggled = 0;
        Assert.Equal(handled, DebugModeShortcut.TryHandle(key, modifiers, repeat, () => toggled++));
        Assert.Equal(calls, toggled);
    }
}

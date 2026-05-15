using System.Windows;
using HyperPet.Core.Settings;
using HyperPet.Core.Pets;

namespace HyperPet.App.Views;

public partial class SettingsWindow : Window
{
    private readonly HyperPetSettings _settings;
    private readonly Action<bool> _applyStartupSetting;

    public SettingsWindow(HyperPetSettings settings, Action<bool> applyStartupSetting)
    {
        _settings = settings;
        _applyStartupSetting = applyStartupSetting;

        InitializeComponent();

        ShowFullContentCheckBox.IsChecked = settings.ShowFullNotificationContent;
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        PetBehaviorComboBox.SelectedIndex = settings.PetBehaviorMode == PetBehaviorMode.Desktop ? 1 : 0;
        AlertDurationSlider.Value = settings.AlertDurationSeconds;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        bool requestedStartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        PetBehaviorMode requestedPetBehaviorMode = PetBehaviorComboBox.SelectedIndex == 1
            ? PetBehaviorMode.Desktop
            : PetBehaviorMode.Calm;

        bool applied = SettingsWindowSettingsApplier.TryApply(
            _settings,
            ShowFullContentCheckBox.IsChecked == true,
            requestedPetBehaviorMode,
            (int)Math.Round(AlertDurationSlider.Value),
            requestedStartWithWindows,
            _applyStartupSetting,
            message =>
            {
                StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
                MessageBox.Show(
                    this,
                    message,
                    "HyperPet Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });

        if (!applied)
        {
            return;
        }

        DialogResult = true;
        Close();
    }
}

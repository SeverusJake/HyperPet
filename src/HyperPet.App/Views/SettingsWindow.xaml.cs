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
        bool previousStartWithWindows = _settings.StartWithWindows;
        bool requestedStartWithWindows = StartWithWindowsCheckBox.IsChecked == true;

        _settings.ShowFullNotificationContent = ShowFullContentCheckBox.IsChecked == true;
        _settings.PetBehaviorMode = PetBehaviorComboBox.SelectedIndex == 1
            ? PetBehaviorMode.Desktop
            : PetBehaviorMode.Calm;
        _settings.AlertDurationSeconds = (int)Math.Round(AlertDurationSlider.Value);

        if (requestedStartWithWindows != previousStartWithWindows)
        {
            try
            {
                _applyStartupSetting(requestedStartWithWindows);
                _settings.StartWithWindows = requestedStartWithWindows;
            }
            catch (Exception exception)
            {
                _settings.StartWithWindows = previousStartWithWindows;
                StartWithWindowsCheckBox.IsChecked = previousStartWithWindows;

                MessageBox.Show(
                    this,
                    $"HyperPet could not update the Windows startup setting. Startup was left unchanged, but your other settings were applied.\n\n{exception.Message}",
                    "HyperPet Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        DialogResult = true;
        Close();
    }
}

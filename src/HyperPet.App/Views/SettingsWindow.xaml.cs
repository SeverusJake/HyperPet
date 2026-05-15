using System.Windows;
using HyperPet.Core.Settings;

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
        AlertDurationSlider.Value = settings.AlertDurationSeconds;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _settings.ShowFullNotificationContent = ShowFullContentCheckBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _settings.AlertDurationSeconds = (int)Math.Round(AlertDurationSlider.Value);

        try
        {
            _applyStartupSetting(_settings.StartWithWindows);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"HyperPet could not update the Windows startup setting. Your other settings were saved.\n\n{exception.Message}",
                "HyperPet Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        DialogResult = true;
        Close();
    }
}

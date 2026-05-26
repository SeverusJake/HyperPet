using System.Collections.ObjectModel;
using System.Windows;
using HyperPet.App.ViewModels;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Views;

public partial class SettingsWindow : Window
{
    private readonly HyperPetSettings _settings;
    private readonly Action<bool> _applyStartupSetting;
    private readonly ObservableCollection<MessagingAppRuleViewModel> _messagingApps;

    public SettingsWindow(HyperPetSettings settings, Action<bool> applyStartupSetting)
    {
        _settings = settings;
        _applyStartupSetting = applyStartupSetting;

        InitializeComponent();

        ShowFullContentCheckBox.IsChecked = settings.ShowFullNotificationContent;
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        OnlyMessagingAppsCheckBox.IsChecked = settings.OnlyMessagingApps;
        OpenAppOnBubbleClickCheckBox.IsChecked = settings.OpenAppOnBubbleClick;
        EnableFrameControlsCheckBox.IsChecked = settings.EnableFrameControls;
        PetBehaviorComboBox.SelectedIndex = settings.PetBehaviorMode == PetBehaviorMode.Desktop ? 1 : 0;
        AlertDurationSlider.Value = settings.AlertDurationSeconds;

        _messagingApps = new ObservableCollection<MessagingAppRuleViewModel>(
            settings.MessagingApps.Select(rule => new MessagingAppRuleViewModel(rule)));
        MessagingAppsListBox.ItemsSource = _messagingApps;
    }

    private void OnAddMessagingAppClick(object sender, RoutedEventArgs e)
    {
        var displayName = NewAppNameTextBox.Text?.Trim() ?? string.Empty;
        var patterns = ParsePatterns(NewAppPatternsTextBox.Text);

        if (string.IsNullOrEmpty(displayName) && patterns.Count == 0)
        {
            return;
        }

        if (string.IsNullOrEmpty(displayName))
        {
            displayName = patterns[0];
        }

        if (patterns.Count == 0)
        {
            patterns.Add(displayName);
        }

        _messagingApps.Add(new MessagingAppRuleViewModel(new MessagingAppRule(displayName, patterns)));

        NewAppNameTextBox.Text = string.Empty;
        NewAppPatternsTextBox.Text = string.Empty;
    }

    private void OnRemoveMessagingAppClick(object sender, RoutedEventArgs e)
    {
        if (MessagingAppsListBox.SelectedItem is MessagingAppRuleViewModel selected)
        {
            _messagingApps.Remove(selected);
        }
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
            OnlyMessagingAppsCheckBox.IsChecked == true,
            OpenAppOnBubbleClickCheckBox.IsChecked == true,
            EnableFrameControlsCheckBox.IsChecked == true,
            _messagingApps.Select(vm => vm.ToModel()).ToList(),
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

    private static List<string> ParsePatterns(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new List<string>();
        }

        return input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

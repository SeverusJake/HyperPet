using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyperPet.App.ViewModels;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Views;

public partial class SettingsWindow : Window
{
    private readonly HyperPetSettings _settings;
    private readonly Action<bool> _applyStartupSetting;
    private readonly Action? _applySettings;
    private readonly ObservableCollection<MessagingAppRuleViewModel> _messagingApps;
    private bool _initializing = true;
    private bool _dirty;
    // Sticky: flips true on first user edit, never goes false again for the
    // lifetime of this dialog. Drives the Save button so Save stays clickable
    // even after Apply commits the current pending edits.
    private bool _anyEditMade;

    public SettingsWindow(
        HyperPetSettings settings,
        Action<bool> applyStartupSetting,
        Action? applySettings = null)
    {
        _settings = settings;
        _applyStartupSetting = applyStartupSetting;
        _applySettings = applySettings;

        InitializeComponent();

        ShowFullContentCheckBox.IsChecked = settings.ShowFullNotificationContent;
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        OpenAppOnBubbleClickCheckBox.IsChecked = settings.OpenAppOnBubbleClick;
        ReactToWindowsNotificationsCheckBox.IsChecked = settings.ReactToWindowsNotifications;
        ReactToInAppNotificationsCheckBox.IsChecked = settings.ReactToInAppNotifications;
        DebugModeCheckBox.IsChecked = settings.DebugMode;
        PetBehaviorComboBox.SelectedIndex = settings.PetBehaviorMode == PetBehaviorMode.Desktop ? 1 : 0;
        AlertDurationTextBox.Text = settings.AlertDurationSeconds.ToString();
        PetSizeTextBox.Text = settings.PetSize.ToString();
        WindowsPollIntervalTextBox.Text = settings.WindowsNotificationPollIntervalSeconds.ToString();
        InAppPollIntervalTextBox.Text = settings.InAppNotificationPollIntervalSeconds.ToString();

        _messagingApps = new ObservableCollection<MessagingAppRuleViewModel>(
            settings.MessagingApps.Select(rule => new MessagingAppRuleViewModel(rule)));
        MessagingAppsListBox.ItemsSource = _messagingApps;

        WireDirtyTracking();
        UpdateButtonState();

        // ListBox item containers materialize during the first layout pass and
        // their TwoWay {Binding Enabled, UpdateSourceTrigger=PropertyChanged}
        // writes back to the source on attach, firing PropertyChanged. That
        // would flip _dirty before the user ever touches a control. Defer the
        // flag flip past idle so those binding initializations are absorbed.
        Loaded += OnLoadedClearInitializing;
    }

    private void OnLoadedClearInitializing(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedClearInitializing;
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                _initializing = false;
                _dirty = false;
                _anyEditMade = false;
                UpdateButtonState();
            }),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void WireDirtyTracking()
    {
        ShowFullContentCheckBox.Click += OnAnyChange;
        StartWithWindowsCheckBox.Click += OnAnyChange;
        OpenAppOnBubbleClickCheckBox.Click += OnAnyChange;
        ReactToWindowsNotificationsCheckBox.Click += OnAnyChange;
        ReactToInAppNotificationsCheckBox.Click += OnAnyChange;
        DebugModeCheckBox.Click += OnAnyChange;

        PetBehaviorComboBox.SelectionChanged += OnAnyChange;

        AlertDurationTextBox.TextChanged += OnAnyChange;
        WindowsPollIntervalTextBox.TextChanged += OnAnyChange;
        InAppPollIntervalTextBox.TextChanged += OnAnyChange;
        PetSizeTextBox.TextChanged += OnAnyChange;

        _messagingApps.CollectionChanged += OnMessagingAppsCollectionChanged;
        foreach (var vm in _messagingApps)
        {
            vm.PropertyChanged += OnMessagingAppRulePropertyChanged;
        }
    }

    private void OnAnyChange(object? sender, EventArgs e) => MarkDirty();

    private void OnMessagingAppRulePropertyChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();

    private void OnMessagingAppsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (MessagingAppRuleViewModel vm in e.NewItems)
            {
                vm.PropertyChanged += OnMessagingAppRulePropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (MessagingAppRuleViewModel vm in e.OldItems)
            {
                vm.PropertyChanged -= OnMessagingAppRulePropertyChanged;
            }
        }

        MarkDirty();
    }

    private void MarkDirty()
    {
        if (_initializing)
        {
            return;
        }

        _dirty = true;
        _anyEditMade = true;
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        // Save: sticky-enabled after the first edit. Apply commits doesn't
        // disable Save so the user can still close-with-save in one click.
        // Apply: gated on current pending edits; disables after a successful
        // commit until the user edits again.
        SaveButton.IsEnabled = _anyEditMade;
        ApplyButton.IsEnabled = _dirty;
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

    private void OnNumericPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Reject any keystroke that is not a digit so the bound TextBox can
        // only contain a positive integer.
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private bool CommitChanges()
    {
        bool requestedStartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        PetBehaviorMode requestedPetBehaviorMode = PetBehaviorComboBox.SelectedIndex == 1
            ? PetBehaviorMode.Desktop
            : PetBehaviorMode.Calm;

        int alertDuration = ParseOrDefault(AlertDurationTextBox.Text, _settings.AlertDurationSeconds);
        int windowsInterval = ParseOrDefault(WindowsPollIntervalTextBox.Text, _settings.WindowsNotificationPollIntervalSeconds);
        int inAppInterval = ParseOrDefault(InAppPollIntervalTextBox.Text, _settings.InAppNotificationPollIntervalSeconds);
        int petSize = ParseOrDefault(PetSizeTextBox.Text, _settings.PetSize);

        bool applied = SettingsWindowSettingsApplier.TryApply(
            _settings,
            ShowFullContentCheckBox.IsChecked == true,
            requestedPetBehaviorMode,
            alertDuration,
            requestedStartWithWindows,
            OpenAppOnBubbleClickCheckBox.IsChecked == true,
            ReactToWindowsNotificationsCheckBox.IsChecked == true,
            ReactToInAppNotificationsCheckBox.IsChecked == true,
            windowsInterval,
            inAppInterval,
            petSize,
            DebugModeCheckBox.IsChecked == true,
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
            return false;
        }

        _applySettings?.Invoke();
        _dirty = false;
        UpdateButtonState();
        return true;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!CommitChanges())
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        CommitChanges();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        // Discard any pending edits; do not write to settings or persist.
        DialogResult = false;
        Close();
    }

    private static int ParseOrDefault(string? text, int fallback)
    {
        return int.TryParse(text, out var value) ? value : fallback;
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

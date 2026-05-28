using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyperPet.App.Pets;
using HyperPet.App.ViewModels;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pets;
using HyperPet.App.Update;
using HyperPet.Core.Settings;
using Velopack;

namespace HyperPet.App.Views;

public partial class SettingsWindow : Window
{
    private readonly HyperPetSettings _settings;
    private readonly Action<bool> _applyStartupSetting;
    private readonly Action? _applySettings;
    private readonly ObservableCollection<MessagingAppRuleViewModel> _messagingApps;
    private readonly ObservableCollection<StateSpeedRowViewModel> _stateSpeeds;
    private readonly SpritePet? _spritePet;
    private readonly IReadOnlyDictionary<string, int>? _originalStateFps;
    private readonly IReadOnlyDictionary<string, PlayMode>? _originalStatePlayMode;
    private readonly UpdateService? _updateService;
    private readonly Func<UpdateInfo, Task>? _promptAndApply;
    private bool _initializing = true;
    private bool _dirty;
    // Sticky: flips true on first user edit, never goes false again for the
    // lifetime of this dialog. Drives the Save button so Save stays clickable
    // even after Apply commits the current pending edits.
    private bool _anyEditMade;

    // General-tab factory defaults — used by the Default button to restore
    // the dialog to a known clean state. These mirror HyperPetSettings.CreateDefault().
    private const bool DefaultShowFullContent = true;
    private const bool DefaultStartWithWindows = false;
    private const bool DefaultOpenAppOnBubbleClick = true;
    private const bool DefaultReactToWindowsNotifications = true;
    private const bool DefaultReactToInAppNotifications = true;
    private const PetBehaviorMode DefaultPetBehaviorMode = PetBehaviorMode.Calm;
    private const bool DefaultDebugMode = false;
    private const int DefaultAlertDuration = 8;
    private const int DefaultPetSize = 8;
    private const int DefaultRunningSpeed = 2;
    private const int DefaultWindowsPollInterval = 30;
    private const int DefaultInAppPollInterval = 2;

    public SettingsWindow(
        HyperPetSettings settings,
        Action<bool> applyStartupSetting,
        Action? applySettings = null,
        SpritePet? spritePet = null,
        IReadOnlyDictionary<string, int>? originalStateFps = null,
        IReadOnlyDictionary<string, PlayMode>? originalStatePlayMode = null,
        UpdateService? updateService = null,
        Func<UpdateInfo, Task>? promptAndApply = null)
    {
        _settings = settings;
        _applyStartupSetting = applyStartupSetting;
        _applySettings = applySettings;
        _spritePet = spritePet;
        _originalStateFps = originalStateFps;
        _originalStatePlayMode = originalStatePlayMode;
        _updateService = updateService;
        _promptAndApply = promptAndApply;

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
        RunningSpeedTextBox.Text = settings.RunningSpeed.ToString();
        WindowsPollIntervalTextBox.Text = settings.WindowsNotificationPollIntervalSeconds.ToString();
        InAppPollIntervalTextBox.Text = settings.InAppNotificationPollIntervalSeconds.ToString();
        AutoUpdateCheckBox.IsChecked = settings.AutoUpdate;
        Title = $"Settings - {AppVersion.DisplayString}";
        AboutVersionText.Text = AppVersion.DisplayString;

        _messagingApps = new ObservableCollection<MessagingAppRuleViewModel>(
            settings.MessagingApps.Select(rule => new MessagingAppRuleViewModel(rule)));
        MessagingAppsListBox.ItemsSource = _messagingApps;

        _stateSpeeds = new ObservableCollection<StateSpeedRowViewModel>(BuildStateSpeedRows());
        StateSpeedItemsControl.ItemsSource = _stateSpeeds;

        WireDirtyTracking();
        UpdateButtonState();

        // ListBox item containers materialize during the first layout pass and
        // their TwoWay {Binding Enabled, UpdateSourceTrigger=PropertyChanged}
        // writes back to the source on attach, firing PropertyChanged. That
        // would flip _dirty before the user ever touches a control. Defer the
        // flag flip past idle so those binding initializations are absorbed.
        Loaded += OnLoadedClearInitializing;
    }

    private IEnumerable<StateSpeedRowViewModel> BuildStateSpeedRows()
    {
        if (_spritePet is null)
        {
            return Array.Empty<StateSpeedRowViewModel>();
        }

        return _spritePet.Definition.States
            .OrderBy(kv => kv.Value.Row)
            .Select(kv =>
            {
                int originalFps = _originalStateFps is not null && _originalStateFps.TryGetValue(kv.Key, out var o)
                    ? o
                    : kv.Value.Fps;
                PlayMode originalMode = _originalStatePlayMode is not null && _originalStatePlayMode.TryGetValue(kv.Key, out var m)
                    ? m
                    : kv.Value.PlayMode;
                return new StateSpeedRowViewModel(kv.Key, originalFps, kv.Value.Fps, originalMode, kv.Value.PlayMode);
            });
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
        AutoUpdateCheckBox.Click += OnAnyChange;

        PetBehaviorComboBox.SelectionChanged += OnAnyChange;

        AlertDurationTextBox.TextChanged += OnAnyChange;
        WindowsPollIntervalTextBox.TextChanged += OnAnyChange;
        InAppPollIntervalTextBox.TextChanged += OnAnyChange;
        PetSizeTextBox.TextChanged += OnAnyChange;
        RunningSpeedTextBox.TextChanged += OnAnyChange;

        _messagingApps.CollectionChanged += OnMessagingAppsCollectionChanged;
        foreach (var vm in _messagingApps)
        {
            vm.PropertyChanged += OnMessagingAppRulePropertyChanged;
        }

        foreach (var vm in _stateSpeeds)
        {
            vm.PropertyChanged += OnStateSpeedPropertyChanged;
        }
    }

    private void OnAnyChange(object? sender, EventArgs e) => MarkDirty();

    private void OnMessagingAppRulePropertyChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();

    private void OnStateSpeedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StateSpeedRowViewModel.Fps)
            || e.PropertyName == nameof(StateSpeedRowViewModel.PlayMode))
        {
            MarkDirty();
        }
    }

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

    private void OnSelectAllAppsClick(object sender, RoutedEventArgs e)
    {
        foreach (var vm in _messagingApps)
        {
            vm.Enabled = true;
        }
    }

    private void OnUnselectAllAppsClick(object sender, RoutedEventArgs e)
    {
        foreach (var vm in _messagingApps)
        {
            vm.Enabled = false;
        }
    }

    private async void OnCheckForUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_updateService is null || !_updateService.IsSupported)
        {
            UpdateStatusText.Text = "Updates are disabled for development builds.";
            return;
        }

        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking…";

        try
        {
            UpdateInfo? info = await _updateService.CheckAsync();
            if (info is null)
            {
                UpdateStatusText.Text = "You're on the latest version.";
            }
            else
            {
                string v = info.TargetFullRelease.Version.ToString();
                UpdateStatusText.Text = $"Update available: v{v}";
                if (_promptAndApply is not null)
                {
                    await _promptAndApply(info);
                }
            }
        }
        catch (Exception exception)
        {
            UpdateStatusText.Text = $"Could not check for updates: {exception.Message}";
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void OnResetStateSpeedClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is StateSpeedRowViewModel row)
        {
            row.ResetToDefault();
        }
    }

    private void OnDefaultClick(object sender, RoutedEventArgs e)
    {
        // Restore every tab's controls to factory defaults. Does NOT persist
        // yet — flips dirty, so the user can preview and click Apply / Save
        // to commit, or Close to discard.
        ShowFullContentCheckBox.IsChecked = DefaultShowFullContent;
        StartWithWindowsCheckBox.IsChecked = DefaultStartWithWindows;
        OpenAppOnBubbleClickCheckBox.IsChecked = DefaultOpenAppOnBubbleClick;
        ReactToWindowsNotificationsCheckBox.IsChecked = DefaultReactToWindowsNotifications;
        ReactToInAppNotificationsCheckBox.IsChecked = DefaultReactToInAppNotifications;
        DebugModeCheckBox.IsChecked = DefaultDebugMode;
        PetBehaviorComboBox.SelectedIndex = DefaultPetBehaviorMode == PetBehaviorMode.Desktop ? 1 : 0;
        AlertDurationTextBox.Text = DefaultAlertDuration.ToString();
        PetSizeTextBox.Text = DefaultPetSize.ToString();
        RunningSpeedTextBox.Text = DefaultRunningSpeed.ToString();
        WindowsPollIntervalTextBox.Text = DefaultWindowsPollInterval.ToString();
        InAppPollIntervalTextBox.Text = DefaultInAppPollInterval.ToString();
        AutoUpdateCheckBox.IsChecked = false;

        // Apps: restore factory list from MessagingAppRule.CreateDefaults().
        _messagingApps.Clear();
        foreach (var rule in MessagingAppRule.CreateDefaults())
        {
            _messagingApps.Add(new MessagingAppRuleViewModel(rule));
        }

        // State speeds: restore each row to its pet.json original fps.
        foreach (var row in _stateSpeeds)
        {
            row.ResetToDefault();
        }

        MarkDirty();
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
        int runningSpeed = ParseOrDefault(RunningSpeedTextBox.Text, _settings.RunningSpeed);

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
            runningSpeed,
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

        // After the main applier has run, write the state-speed overrides for
        // the current pet into _settings and mutate the live PetDefinition so
        // the change takes effect immediately (caller restarts the animator).
        _settings.AutoUpdate = AutoUpdateCheckBox.IsChecked == true;
        ApplyStateSpeedChanges();

        _applySettings?.Invoke();
        _dirty = false;
        UpdateButtonState();
        return true;
    }

    private void ApplyStateSpeedChanges()
    {
        if (_spritePet is null)
        {
            return;
        }

        string petId = _spritePet.Definition.Id;

        if (!_settings.StateSpeedOverrides.TryGetValue(petId, out var fpsPerPet))
        {
            fpsPerPet = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _settings.StateSpeedOverrides[petId] = fpsPerPet;
        }

        if (!_settings.StatePlayModeOverrides.TryGetValue(petId, out var modePerPet))
        {
            modePerPet = new Dictionary<string, PlayMode>(StringComparer.OrdinalIgnoreCase);
            _settings.StatePlayModeOverrides[petId] = modePerPet;
        }

        foreach (var row in _stateSpeeds)
        {
            int fps = Math.Clamp(row.Fps, 1, 60);
            row.Fps = fps;

            if (_spritePet.Definition.States.TryGetValue(row.StateName, out var state))
            {
                state.Fps = fps;
                state.PlayMode = row.PlayMode;
            }

            if (fps == row.OriginalFps)
            {
                fpsPerPet.Remove(row.StateName);
            }
            else
            {
                fpsPerPet[row.StateName] = fps;
            }

            if (row.PlayMode == row.OriginalPlayMode)
            {
                modePerPet.Remove(row.StateName);
            }
            else
            {
                modePerPet[row.StateName] = row.PlayMode;
            }
        }

        // Avoid keeping empty inner dicts in the settings file.
        if (fpsPerPet.Count == 0)
        {
            _settings.StateSpeedOverrides.Remove(petId);
        }
        if (modePerPet.Count == 0)
        {
            _settings.StatePlayModeOverrides.Remove(petId);
        }
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

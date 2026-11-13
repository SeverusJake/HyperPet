using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
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
    private readonly bool _canManageStartup;
    private readonly Action? _applySettings;
    private readonly ObservableCollection<MessagingAppRuleViewModel> _messagingApps;
    private readonly ObservableCollection<StateSpeedRowViewModel> _stateSpeeds;
    private SpritePet? _spritePet;
    private IReadOnlyDictionary<string, int>? _originalStateFps;
    private IReadOnlyDictionary<string, PlayMode>? _originalStatePlayMode;
    private readonly UpdateService? _updateService;
    private readonly Func<UpdateInfo, Task>? _promptAndApply;
    private IReadOnlyList<PetCatalogEntry> _petCatalog;
    private string _originalSelectedPet;
    private readonly Func<string, CancellationToken, Task<PreparedPetReload?>>? _preparePet;
    private readonly Func<PreparedPetReload, CancellationToken, bool>? _canCommitPet;
    private readonly Action<PreparedPetReload>? _commitPet;
    internal Action<string>? CommitWarning { get; set; }
    private void WarnCommit(string message)
    {
        if (CommitWarning is not null) CommitWarning(message);
        else MessageBox.Show(this, message, "HyperPet", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private readonly string _userPetsRoot;
    private readonly Func<Task<IReadOnlyList<PetCatalogEntry>>>? _rediscoverPets;
    private bool _initializing = true;
    private bool _dirty;
    private bool _committing;
    private bool _closed;
    private readonly CancellationTokenSource _lifetime = new();
    // Sticky: flips true on first user edit, never goes false again for the
    // lifetime of this dialog. Drives the Save button so Save stays clickable
    // even after Apply commits the current pending edits.
    private bool _anyEditMade;

    // Factory defaults come from the same model used by settings persistence.
    private static readonly HyperPetSettings Defaults = HyperPetSettings.CreateDefault();
    private static bool DefaultShowFullContent => Defaults.ShowFullNotificationContent;
    private static bool DefaultStartWithWindows => Defaults.StartWithWindows;
    private static bool DefaultReactToWindowsNotifications => Defaults.ReactToWindowsNotifications;
    private static bool DefaultReactToInAppNotifications => Defaults.ReactToInAppNotifications;
    private static PetBehaviorMode DefaultPetBehaviorMode => Defaults.PetBehaviorMode;
    private static bool DefaultDebugMode => Defaults.DebugMode;
    private static int DefaultAlertDuration => Defaults.AlertDurationSeconds;
    private static int DefaultPetSize => Defaults.PetSize;
    private static int DefaultRunningSpeed => Defaults.RunningSpeed;
    private static int DefaultWindowsPollInterval => Defaults.WindowsNotificationPollIntervalSeconds;
    private static int DefaultInAppPollInterval => Defaults.InAppNotificationPollIntervalSeconds;

    public SettingsWindow(
        HyperPetSettings settings,
        Action<bool> applyStartupSetting,
        Action? applySettings = null,
        SpritePet? spritePet = null,
        IReadOnlyDictionary<string, int>? originalStateFps = null,
        IReadOnlyDictionary<string, PlayMode>? originalStatePlayMode = null,
        UpdateService? updateService = null,
        Func<UpdateInfo, Task>? promptAndApply = null,
        IReadOnlyList<PetCatalogEntry>? petCatalog = null,
        string? userPetsRoot = null,
        Func<Task<IReadOnlyList<PetCatalogEntry>>>? rediscoverPets = null, bool canManageStartup = true,
        Func<string, CancellationToken, Task<PreparedPetReload?>>? preparePet = null,
        Func<PreparedPetReload, CancellationToken, bool>? canCommitPet = null,
        Action<PreparedPetReload>? commitPet = null)
    {
        _settings = settings;
        _applyStartupSetting = applyStartupSetting;
        _canManageStartup = canManageStartup;
        _applySettings = applySettings;
        _spritePet = spritePet;
        _originalStateFps = originalStateFps;
        _originalStatePlayMode = originalStatePlayMode;
        _updateService = updateService;
        _promptAndApply = promptAndApply;
        _petCatalog = petCatalog ?? Array.Empty<PetCatalogEntry>();
        _originalSelectedPet = settings.SelectedPet;
        _preparePet = preparePet; _canCommitPet = canCommitPet; _commitPet = commitPet;
        _userPetsRoot = userPetsRoot ?? string.Empty;
        _rediscoverPets = rediscoverPets;

        InitializeComponent();
        Closed += (_, _) => { _closed = true; _lifetime.Cancel(); };

        ShowFullContentCheckBox.IsChecked = settings.ShowFullNotificationContent;
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        StartWithWindowsCheckBox.IsEnabled = canManageStartup;
        if (!canManageStartup) StartWithWindowsCheckBox.ToolTip = "Only an installed app with readable settings can change Windows startup.";
        RememberPetPositionCheckBox.IsChecked = settings.RememberPetPosition;
        RefreshMonitors(settings.PreferredMonitorId);
        ReactToWindowsNotificationsCheckBox.IsChecked = settings.ReactToWindowsNotifications;
        ReactToInAppNotificationsCheckBox.IsChecked = settings.ReactToInAppNotifications;
        DebugModeCheckBox.IsChecked = settings.DebugMode;
        PetBehaviorComboBox.SelectedIndex = (int)settings.PetBehaviorMode;
        AlertDurationTextBox.Text = settings.AlertDurationSeconds.ToString();
        PetSizeTextBox.Text = settings.PetSize.ToString();
        RunningSpeedTextBox.Text = settings.RunningSpeed.ToString();
        WindowsPollIntervalTextBox.Text = settings.WindowsNotificationPollIntervalSeconds.ToString();
        InAppPollIntervalTextBox.Text = settings.InAppNotificationPollIntervalSeconds.ToString();
        AutoUpdateCheckBox.IsChecked = settings.AutoUpdate;
        QuietHoursCheckBox.IsChecked = settings.QuietHoursEnabled;
        QuietHoursStartTextBox.Text = settings.QuietHoursStart;
        QuietHoursEndTextBox.Text = settings.QuietHoursEnd;
        Title = $"Settings - {AppVersion.DisplayString}";
        AboutVersionText.Text = AppVersion.DisplayString;

        PetPickerComboBox.ItemsSource = _petCatalog;
        PetPickerComboBox.SelectedValue = settings.SelectedPet;
        if (PetPickerComboBox.SelectedItem is null && _petCatalog.Count > 0)
        {
            PetPickerComboBox.SelectedIndex = 0;
        }

        _messagingApps = new ObservableCollection<MessagingAppRuleViewModel>(
            settings.MessagingApps.Select(rule => new MessagingAppRuleViewModel(rule)));
        MessagingAppsListBox.ItemsSource = _messagingApps;

        _stateSpeeds = new ObservableCollection<StateSpeedRowViewModel>(BuildStateSpeedRows());
        StateSpeedItemsControl.ItemsSource = _stateSpeeds;

        WireDirtyTracking();
        UpdateButtonState();
        UpdateRemoveButtonState();

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
        RememberPetPositionCheckBox.Click += OnAnyChange;
        ReactToWindowsNotificationsCheckBox.Click += OnAnyChange;
        ReactToInAppNotificationsCheckBox.Click += OnAnyChange;
        DebugModeCheckBox.Click += OnAnyChange;
        AutoUpdateCheckBox.Click += OnAnyChange;
        QuietHoursCheckBox.Click += OnAnyChange;

        PetBehaviorComboBox.SelectionChanged += OnAnyChange;
        MonitorPickerComboBox.SelectionChanged += OnAnyChange;
        PetPickerComboBox.SelectionChanged += OnAnyChange;
        PetPickerComboBox.SelectionChanged += (_, _) => UpdateRemoveButtonState();

        AlertDurationTextBox.TextChanged += OnAnyChange;
        WindowsPollIntervalTextBox.TextChanged += OnAnyChange;
        InAppPollIntervalTextBox.TextChanged += OnAnyChange;
        PetSizeTextBox.TextChanged += OnAnyChange;
        RunningSpeedTextBox.TextChanged += OnAnyChange;
        QuietHoursStartTextBox.TextChanged += OnAnyChange;
        QuietHoursEndTextBox.TextChanged += OnAnyChange;

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

    private sealed record MonitorChoice(string Id, string DisplayName);

    private void RefreshMonitors(string selectedId)
    {
        var choices = new List<MonitorChoice> { new(string.Empty, "Automatic (current / last monitor)") };
        choices.AddRange(DesktopMonitors.Enumerate().Select(monitor => new MonitorChoice(monitor.Id, monitor.DisplayName)));
        if (!string.IsNullOrEmpty(selectedId)
            && !choices.Any(choice => string.Equals(choice.Id, selectedId, StringComparison.OrdinalIgnoreCase)))
        {
            choices.Add(new MonitorChoice(selectedId, $"Disconnected monitor ({selectedId})"));
        }
        MonitorPickerComboBox.ItemsSource = choices;
        MonitorPickerComboBox.SelectedValue = choices.First(choice =>
            string.Equals(choice.Id, selectedId, StringComparison.OrdinalIgnoreCase)).Id;
    }

    private void OnRefreshMonitorsClick(object sender, RoutedEventArgs e)
        => RefreshMonitors(MonitorPickerComboBox.SelectedValue as string ?? string.Empty);

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

    private void UpdateRemoveButtonState()
    {
        RemovePetButton.IsEnabled =
            PetPickerComboBox.SelectedItem is PetCatalogEntry entry
            && PetRemover.IsRemovable(entry.Directory, _userPetsRoot);
    }

    private async void OnAddPetFromZipClick(object sender, RoutedEventArgs e)
    {
        if (_committing || _closed) return;
        var dialog = new OpenFileDialog
        {
            Title = "Select a pet zip",
            Filter = "Pet zip (*.zip)|*.zip",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await RunPetOperationAsync(async () =>
        {
            var result = await PetImporter.ImportFromZipAsync(dialog.FileName, _userPetsRoot, ConfirmOverwrite, BuiltinPetIds(), _lifetime.Token);
            if (!_closed) await HandleImportResultAsync(result);
        });
    }

    private async void OnAddPetFromFolderClick(object sender, RoutedEventArgs e)
    {
        if (_committing || _closed) return;
        var dialog = new OpenFolderDialog
        {
            Title = "Select a pet folder",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await RunPetOperationAsync(async () =>
        {
            var result = await PetImporter.ImportFromFolderAsync(dialog.FolderName, _userPetsRoot, ConfirmOverwrite, BuiltinPetIds(), _lifetime.Token);
            if (!_closed) await HandleImportResultAsync(result);
        });
    }

    private IReadOnlySet<string> BuiltinPetIds() => _petCatalog
        .Where(entry => !PetRemover.IsRemovable(entry.Directory, _userPetsRoot))
        .Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private bool ConfirmOverwrite(string id)
    {
        return MessageBox.Show(
            this,
            $"A pet with id '{id}' already exists. Replace it?",
            "HyperPet",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private async Task HandleImportResultAsync(PetImportResult result)
    {
        if (!result.Success)
        {
            if (result.Message is not null && result.Message != "Import cancelled.")
            {
                MessageBox.Show(this, result.Message, "HyperPet", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        await RefreshCatalogAsync();
        if (_closed) return;
        MessageBox.Show(
            this,
            $"Added '{result.Entry!.DisplayName}'. Select it from the Pet list and click Apply to use it.",
            "HyperPet",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void OnRemovePetClick(object sender, RoutedEventArgs e)
    {
        if (_committing || _closed) return;
        if (PetPickerComboBox.SelectedItem is not PetCatalogEntry entry
            || !PetRemover.IsRemovable(entry.Directory, _userPetsRoot))
        {
            return;
        }

        if (MessageBox.Show(
                this,
                $"Remove the pet '{entry.DisplayName}'? This deletes its files.",
                "HyperPet",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await RemovePetAsync(entry);
        if (!_closed && result.Error is not null)
            WarnCommit(result.Error + (result.RecoveryDirectory is null ? "" : "\nRecovery folder: " + result.RecoveryDirectory));
    }

    internal async Task<PetRemovalResult> RemovePetAsync(PetCatalogEntry entry)
    {
        if (_committing || _closed) return new(false, "Another operation is active or the dialog is closed.", null);
        _committing = true; IsEnabled = false;
        PreparedPetReload? committedFallback = null;
        try
        {
            var result = await new PetRemovalService().RemoveAsync(_userPetsRoot, entry, _spritePet?.Definition.Id ?? _settings.SelectedPet, _petCatalog,
                (id, token) => _preparePet is null ? Task.FromResult<PreparedPetReload?>(null) : _preparePet(id, token),
                prepared =>
                {
                    if (_closed || _canCommitPet is null || _commitPet is null || !_canCommitPet(prepared, _lifetime.Token)) return false;
                    _commitPet(prepared);
                    _settings.SelectedPet = prepared.Pet.Definition.Id;
                    _originalSelectedPet = _settings.SelectedPet;
                    committedFallback = prepared;
                    return true;
                }, _lifetime.Token);
            if (result.Success)
            {
                if (!_closed && committedFallback is not null)
                    SetActivePet(new(committedFallback.Pet, committedFallback.OriginalFps, committedFallback.OriginalModes, null));
                _applySettings?.Invoke();
            }
            if (!_closed)
            {
                try { await RefreshCatalogAsync(); }
                catch (Exception exception) { return result with { Error = (result.Error ?? "") + "\nCatalog refresh failed: " + exception.Message }; }
            }
            return result;
        }
        finally { _committing = false; if (!_closed) IsEnabled = true; }
    }

    private async Task RunPetOperationAsync(Func<Task> operation)
    {
        if (_committing || _closed) return;
        _committing = true; IsEnabled = false;
        try { await operation(); }
        catch (Exception exception) { if (!_closed) WarnCommit(exception.Message); }
        finally { _committing = false; if (!_closed) IsEnabled = true; }
    }

    private async Task RefreshCatalogAsync()
    {
        if (_rediscoverPets is null)
        {
            return;
        }

        string? selected = PetPickerComboBox.SelectedValue as string;
        var catalog = await _rediscoverPets();
        if (_closed) return;
        _petCatalog = catalog;
        PetPickerComboBox.ItemsSource = _petCatalog;
        PetPickerComboBox.SelectedValue = selected;
        if (PetPickerComboBox.SelectedItem is null && _petCatalog.Count > 0)
        {
            PetPickerComboBox.SelectedIndex = 0;
        }
        UpdateRemoveButtonState();
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
        RememberPetPositionCheckBox.IsChecked = true;
        MonitorPickerComboBox.SelectedValue = string.Empty;
        ReactToWindowsNotificationsCheckBox.IsChecked = DefaultReactToWindowsNotifications;
        ReactToInAppNotificationsCheckBox.IsChecked = DefaultReactToInAppNotifications;
        DebugModeCheckBox.IsChecked = DefaultDebugMode;
        PetBehaviorComboBox.SelectedIndex = (int)DefaultPetBehaviorMode;
        AlertDurationTextBox.Text = DefaultAlertDuration.ToString();
        PetSizeTextBox.Text = DefaultPetSize.ToString();
        RunningSpeedTextBox.Text = DefaultRunningSpeed.ToString();
        QuietHoursCheckBox.IsChecked = false;
        QuietHoursStartTextBox.Text = "22:00";
        QuietHoursEndTextBox.Text = "07:00";
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

    internal async Task<bool> CommitChanges()
    {
        if (_committing || _closed) return false;
        foreach (var (box, minimum, maximum) in new[] {
            (AlertDurationTextBox, 1, 600), (WindowsPollIntervalTextBox, 5, 600),
            (InAppPollIntervalTextBox, 1, 60), (PetSizeTextBox, 1, 10), (RunningSpeedTextBox, 1, 20) })
        {
            if (!int.TryParse(box.Text, out int number) || number < minimum || number > maximum)
            {
                box.ToolTip = $"Enter a whole number from {minimum} to {maximum}.";
                box.BorderBrush = System.Windows.Media.Brushes.Red; box.Focus(); return false;
            }
            box.ClearValue(Control.BorderBrushProperty);
        }
        foreach (var box in new[] { QuietHoursStartTextBox, QuietHoursEndTextBox })
        {
            if (QuietHoursSchedule.TryParse(box.Text) is null)
            {
                box.ToolTip = "Enter time as HH:mm (00:00–23:59).";
                box.BorderBrush = System.Windows.Media.Brushes.Red; box.Focus(); return false;
            }
            box.ClearValue(Control.BorderBrushProperty);
        }
        bool requestedStartWithWindows = _canManageStartup ? StartWithWindowsCheckBox.IsChecked == true : _settings.StartWithWindows;
        PetBehaviorMode requestedPetBehaviorMode = PetBehaviorComboBox.SelectedIndex switch
        {
            1 => PetBehaviorMode.Running,
            2 => PetBehaviorMode.Desktop,
            _ => PetBehaviorMode.Calm,
        };

        int alertDuration = ParseOrDefault(AlertDurationTextBox.Text, _settings.AlertDurationSeconds);
        int windowsInterval = ParseOrDefault(WindowsPollIntervalTextBox.Text, _settings.WindowsNotificationPollIntervalSeconds);
        int inAppInterval = ParseOrDefault(InAppPollIntervalTextBox.Text, _settings.InAppNotificationPollIntervalSeconds);
        int petSize = ParseOrDefault(PetSizeTextBox.Text, _settings.PetSize);
        int runningSpeed = ParseOrDefault(RunningSpeedTextBox.Text, _settings.RunningSpeed);

        var candidate = SettingsDraft.Clone(_settings);
        bool applied = SettingsWindowSettingsApplier.TryApply(
            candidate,
            ShowFullContentCheckBox.IsChecked == true,
            requestedPetBehaviorMode,
            alertDuration,
            requestedStartWithWindows,
            _settings.OpenAppOnBubbleClick, // Preserve legacy setting; bubble clicks now always advance.
            ReactToWindowsNotificationsCheckBox.IsChecked == true,
            ReactToInAppNotificationsCheckBox.IsChecked == true,
            windowsInterval,
            inAppInterval,
            petSize,
            runningSpeed,
            DebugModeCheckBox.IsChecked == true,
            QuietHoursCheckBox.IsChecked == true,
            QuietHoursStartTextBox.Text,
            QuietHoursEndTextBox.Text,
            _messagingApps.Select(vm => vm.ToModel()).ToList(),
            _ => { }, WarnCommit);

        if (!applied)
        {
            return false;
        }

        candidate.AutoUpdate = AutoUpdateCheckBox.IsChecked == true;
        candidate.RememberPetPosition = RememberPetPositionCheckBox.IsChecked == true;
        candidate.PreferredMonitorId = MonitorPickerComboBox.SelectedValue as string ?? string.Empty;
        ApplyStateSpeedChanges(candidate);
        string? selectedPetId = PetPickerComboBox.SelectedValue as string;
        bool changingPet = !string.IsNullOrWhiteSpace(selectedPetId) &&
            !string.Equals(selectedPetId, _originalSelectedPet, StringComparison.OrdinalIgnoreCase);
        _committing = true; IsEnabled = false;
        HyperPetSettings? previous = null;
        bool startupApplied = false;
        bool committed = false;
        try
        {
            PreparedPetReload? prepared = null;
            if (changingPet)
            {
                if (_preparePet is null || _canCommitPet is null || _commitPet is null)
                    throw new InvalidOperationException("Pet switching is unavailable in this dialog.");
                prepared = await _preparePet(selectedPetId!, _lifetime.Token);
                if (_closed) return false;
                if (prepared is null || !_canCommitPet(prepared, _lifetime.Token))
                    throw new InvalidOperationException("Pet load failed or was superseded.");
                candidate.SelectedPet = selectedPetId!;
            }
            if (_closed) return false;
            previous = SettingsDraft.Clone(_settings);
            // Runtime-owned values may have changed during the preparation await.
            candidate.AlertsPaused = _settings.AlertsPaused;
            candidate.PetLeft = _settings.PetLeft; candidate.PetTop = _settings.PetTop;
            // Everything following preparation runs synchronously on the dispatcher.
            if (candidate.StartWithWindows != previous.StartWithWindows)
            {
                _applyStartupSetting(candidate.StartWithWindows);
                startupApplied = true;
            }
            SettingsDraft.CopyTo(candidate, _settings);
            if (prepared is not null)
            {
                _commitPet!(prepared);
                SetActivePet(new(prepared.Pet, prepared.OriginalFps, prepared.OriginalModes, null));
            }
            else ApplyLiveStateOverrides();
            _originalSelectedPet = _settings.SelectedPet;
            committed = true;
        }
        catch (Exception exception)
        {
            if (previous is not null) { SettingsDraft.CopyTo(previous, _settings); ApplyLiveStateOverrides(); }
            string warning = exception.Message;
            if (startupApplied)
            {
                try { _applyStartupSetting(previous!.StartWithWindows); }
                catch (Exception rollback) { warning += "\nStartup rollback failed: " + rollback.Message; }
            }
            if (!_closed) WarnCommit(warning);
            return false;
        }
        finally { _committing = false; if (!_closed) IsEnabled = true; }
        if (!committed) return false;
        _applySettings?.Invoke();
        _dirty = false;
        UpdateButtonState();

        return true;
    }

    private void SetActivePet(PetReloadResult result)
    {
        _spritePet = result.Pet; _originalStateFps = result.OriginalFps; _originalStatePlayMode = result.OriginalModes;
        foreach (var row in _stateSpeeds) row.PropertyChanged -= OnStateSpeedPropertyChanged;
        _stateSpeeds.Clear();
        foreach (var row in BuildStateSpeedRows()) { _stateSpeeds.Add(row); row.PropertyChanged += OnStateSpeedPropertyChanged; }
    }

    private void ApplyLiveStateOverrides()
    {
        if (_spritePet is null) return;
        foreach (var (name, state) in _spritePet.Definition.States)
        {
            if (_originalStateFps?.TryGetValue(name, out int fps) == true) state.Fps = fps;
            if (_originalStatePlayMode?.TryGetValue(name, out var mode) == true) state.PlayMode = mode;
        }
        PetOverrides.Apply(_spritePet, _settings);
    }

    private void ApplyStateSpeedChanges(HyperPetSettings candidate)
    {
        if (_spritePet is null)
        {
            return;
        }

        string petId = _spritePet.Definition.Id;

        if (!candidate.StateSpeedOverrides.TryGetValue(petId, out var fpsPerPet))
        {
            fpsPerPet = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            candidate.StateSpeedOverrides[petId] = fpsPerPet;
        }

        if (!candidate.StatePlayModeOverrides.TryGetValue(petId, out var modePerPet))
        {
            modePerPet = new Dictionary<string, PlayMode>(StringComparer.OrdinalIgnoreCase);
            candidate.StatePlayModeOverrides[petId] = modePerPet;
        }

        foreach (var row in _stateSpeeds)
        {
            int fps = Math.Clamp(row.Fps, 1, 60);
            row.Fps = fps;

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
            candidate.StateSpeedOverrides.Remove(petId);
        }
        if (modePerPet.Count == 0)
        {
            candidate.StatePlayModeOverrides.Remove(petId);
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!await CommitChanges())
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private async void OnApplyClick(object sender, RoutedEventArgs e)
    {
        await CommitChanges();
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

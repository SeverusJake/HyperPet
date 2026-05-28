using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using HyperPet.App.Pets.Roaming;
using HyperPet.App.Pets;
using HyperPet.Core.Settings;
using HyperPet.App.ViewModels;
using HyperPet.Core.Pet;
using HyperPet.Core.Pets;
using HyperPet.App.Views;
using HyperPet.App.Notifications;
using HyperPet.App.Update;
using HyperPet.Windows.Notifications;
using HyperPet.Windows.Startup;
using Velopack;

namespace HyperPet.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly HyperPetSettings _settings;
    private readonly Action<bool> _applyStartupSetting;
    private readonly Action _saveSettings;
    private readonly Action<TimeSpan>? _setPollInterval;
    private readonly Action<TimeSpan>? _pollSoon;
    private readonly DebugNotificationSimulator? _debugSimulator;
    private readonly Action? _applyMonitoringSettings;
    private readonly UpdateService? _updateService;
    private readonly IReadOnlyList<PetCatalogEntry> _petCatalog;
    private readonly IAppLauncher? _appLauncher;
    private readonly DispatcherTimer _alertTimer = new();
    private readonly DispatcherTimer _calmTimer = new();
    private readonly DispatcherTimer _movementTimer = new();
    private readonly DispatcherTimer _debugOverlayTimer = new();
    private readonly Random _random = new();
    private readonly PetAnimator? _petAnimator;
    private readonly SpritePet? _spritePet;
    private DesktopRoamController? _roamController;
    private string _lastRoamAnimation = string.Empty;
    private readonly IReadOnlyDictionary<string, int>? _originalStateFps;
    private readonly IReadOnlyDictionary<string, PlayMode>? _originalStatePlayMode;
    private bool _movingRight = true;
    private bool _alertActive;
    private TimeSpan _debugPollInterval = TimeSpan.FromSeconds(30);
    private DateTime _debugNextPollUtc = DateTime.UtcNow;
    private int _debugLastPollCount;
    private int _debugTotalAlerts;
    private string _debugStatus = "starting";
    private bool _debugOverlayHidden;

    public MainWindow(
        HyperPetSettings settings,
        Action<bool> applyStartupSetting,
        Action saveSettings,
        SpritePet? spritePet,
        IReadOnlyDictionary<string, int>? originalStateFps = null,
        IReadOnlyDictionary<string, PlayMode>? originalStatePlayMode = null,
        IAppLauncher? appLauncher = null,
        Action<TimeSpan>? setPollInterval = null,
        Action<TimeSpan>? pollSoon = null,
        DebugNotificationSimulator? debugSimulator = null,
        Action? applyMonitoringSettings = null,
        UpdateService? updateService = null,
        IReadOnlyList<PetCatalogEntry>? petCatalog = null)
    {
        _settings = settings;
        _applyStartupSetting = applyStartupSetting;
        _saveSettings = saveSettings;
        _spritePet = spritePet;
        _originalStateFps = originalStateFps;
        _originalStatePlayMode = originalStatePlayMode;
        _appLauncher = appLauncher;
        _setPollInterval = setPollInterval;
        _pollSoon = pollSoon;
        _debugSimulator = debugSimulator;
        _applyMonitoringSettings = applyMonitoringSettings;
        _updateService = updateService;
        _petCatalog = petCatalog ?? Array.Empty<PetCatalogEntry>();

        InitializeComponent();

        PauseAlertsMenuItem.IsChecked = settings.AlertsPaused;
        VersionMenuItem.Header = AppVersion.DisplayString;

        _viewModel = new MainWindowViewModel
        {
            AlertsPaused = settings.AlertsPaused
        };
        DataContext = _viewModel;

        _alertTimer.Tick += (_, _) => DismissAlert();
        _calmTimer.Tick += OnCalmTimerTick;
        _movementTimer.Tick += OnMovementTimerTick;
        _debugOverlayTimer.Interval = TimeSpan.FromSeconds(1);
        _debugOverlayTimer.Tick += OnDebugOverlayTimerTick;
        HelpOverlayText.Text = BuildHelpText();
        Loaded += (_, _) =>
        {
            ClampToWorkArea();
            ApplyDebugOverlayVisibility();
            _ = MaybeAutoCheckForUpdateAsync();
        };

        if (spritePet is null)
        {
            PetImage.Visibility = Visibility.Collapsed;
            return;
        }

        _petAnimator = new PetAnimator(spritePet, PetImage);
        ApplyPetSize();
        StartBehaviorMode();
    }

    private void ApplyPetSize()
    {
        const double BaselineWidth = 192.0;
        const double BaselineHeight = 208.0;
        const int BaselineSize = 8;

        int size = Math.Clamp(_settings.PetSize, 1, 10);
        double scale = size / (double)BaselineSize;

        PetImage.Width = BaselineWidth * scale;
        PetImage.Height = BaselineHeight * scale;
    }

    public void ShowAlert(PetAlert alert)
    {
        if (_viewModel.AlertsPaused)
        {
            return;
        }

        _viewModel.CurrentAlert = alert;
        _alertActive = true;
        _debugTotalAlerts++;
        UpdateDebugOverlay();
        BubbleAppName.Text = alert.AppName;
        BubbleTitle.Text = alert.Title;
        BubbleBody.Text = alert.Body;
        Bubble.Visibility = Visibility.Visible;
        StopBehaviorTimers();
        _petAnimator?.Play("waving");

        _alertTimer.Stop();
        _alertTimer.Interval = TimeSpan.FromSeconds(_settings.AlertDurationSeconds);
        _alertTimer.Start();
    }

    public void DismissAlert()
    {
        _alertTimer.Stop();
        _viewModel.CurrentAlert = null;
        _alertActive = false;
        Bubble.Visibility = Visibility.Collapsed;
        BubbleAppName.Text = string.Empty;
        BubbleTitle.Text = string.Empty;
        BubbleBody.Text = string.Empty;
        StartBehaviorMode();
    }

    private void StartBehaviorMode()
    {
        if (_petAnimator is null || _alertActive)
        {
            return;
        }

        StopBehaviorTimers();

        switch (_settings.PetBehaviorMode)
        {
            case PetBehaviorMode.Running:
                StartRunningMode();
                return;
            case PetBehaviorMode.Desktop:
                StartPerchMode();
                return;
            default:
                StartCalmMode();
                return;
        }
    }

    private void StartCalmMode()
    {
        _movementTimer.Stop();
        _petAnimator?.Play("idle");
        _calmTimer.Interval = TimeSpan.FromSeconds(6);
        _calmTimer.Start();
    }

    public void ClampToWorkArea()
    {
        Rect workArea = SystemParameters.WorkArea;
        double windowWidth = GetWindowWidth();
        double windowHeight = GetWindowHeight();
        double maxLeft = Math.Max(workArea.Left, workArea.Right - windowWidth);
        double maxTop = Math.Max(workArea.Top, workArea.Bottom - windowHeight);

        Left = Math.Clamp(Left, workArea.Left, maxLeft);
        Top = Math.Clamp(Top, workArea.Top, maxTop);
    }

    private void StartRunningMode()
    {
        _calmTimer.Stop();
        _petAnimator?.Play(_movingRight ? "runRight" : "runLeft");
        _movementTimer.Interval = TimeSpan.FromMilliseconds(33);
        _movementTimer.Start();
    }

    private void StartPerchMode()
    {
        _calmTimer.Stop();

        if (_spritePet is null)
        {
            return;
        }

        if (_roamController is null)
        {
            IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
            _roamController = new DesktopRoamController(new WindowLedgeProvider(hwnd), _random);
        }

        _roamController.PetWidth = GetWindowWidth();
        _roamController.PetHeight = GetWindowHeight();
        _roamController.WalkSpeed = Math.Clamp(_settings.RunningSpeed, 1, 20);
        _roamController.Start(Left, Top);
        _lastRoamAnimation = string.Empty;

        _movementTimer.Interval = TimeSpan.FromMilliseconds(33);
        _movementTimer.Start();
    }

    private void StopBehaviorTimers()
    {
        _calmTimer.Stop();
        _movementTimer.Stop();
    }

    private void OnCalmTimerTick(object? sender, EventArgs e)
    {
        _petAnimator?.Play(_random.NextDouble() < 0.25 ? "waiting" : "idle");
    }

    private void OnMovementTimerTick(object? sender, EventArgs e)
    {
        if (_settings.PetBehaviorMode == PetBehaviorMode.Desktop)
        {
            RoamTick();
            return;
        }

        Rect workArea = SystemParameters.WorkArea;
        double windowWidth = GetWindowWidth();
        int speed = Math.Clamp(_settings.RunningSpeed, 1, 20);
        double nextLeft = Left + (_movingRight ? speed : -speed);

        if (nextLeft <= workArea.Left)
        {
            nextLeft = workArea.Left;
            SetMovementDirection(true);
        }
        else if (nextLeft + windowWidth >= workArea.Right)
        {
            nextLeft = workArea.Right - windowWidth;
            SetMovementDirection(false);
        }

        Left = nextLeft;
    }

    private void RoamTick()
    {
        if (_roamController is null)
        {
            return;
        }

        _roamController.Tick();
        Left = _roamController.X;
        Top = _roamController.Y;

        if (_roamController.CurrentAnimation != _lastRoamAnimation)
        {
            _lastRoamAnimation = _roamController.CurrentAnimation;
            _petAnimator?.Play(_lastRoamAnimation);
        }
    }

    private double GetWindowWidth()
    {
        return ActualWidth > 0 ? ActualWidth : Width;
    }

    private double GetWindowHeight()
    {
        return ActualHeight > 0 ? ActualHeight : Height;
    }

    private void SetMovementDirection(bool movingRight)
    {
        if (_movingRight == movingRight)
        {
            return;
        }

        _movingRight = movingRight;
        _petAnimator?.Play(_movingRight ? "runRight" : "runLeft");
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            DismissAlert();
            e.Handled = true;
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        StopBehaviorTimers();

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            if (!_alertActive)
            {
                StartBehaviorMode();
            }
        }

        e.Handled = true;
    }

    private void OnBubbleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            DismissAlert();
            e.Handled = true;
            return;
        }

        if (!_settings.OpenAppOnBubbleClick || _appLauncher is null)
        {
            return;
        }

        var alert = _viewModel.CurrentAlert;
        if (alert is null || string.IsNullOrWhiteSpace(alert.AppUserModelId))
        {
            return;
        }

        if (_appLauncher.TryLaunch(alert.AppUserModelId))
        {
            DismissAlert();
            e.Handled = true;
        }
    }

    private void OnPauseAlertsClick(object sender, RoutedEventArgs e)
    {
        _settings.AlertsPaused = PauseAlertsMenuItem.IsChecked;
        _viewModel.AlertsPaused = _settings.AlertsPaused;

        if (_viewModel.AlertsPaused)
        {
            DismissAlert();
        }

        _saveSettings();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(
            _settings,
            _applyStartupSetting,
            RefreshFromSettings,
            _spritePet,
            _originalStateFps,
            _originalStatePlayMode,
            _updateService,
            PromptAndApplyUpdateAsync,
            _petCatalog)
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
    }

    private void RefreshFromSettings()
    {
        // Called by SettingsWindow on Save or Apply. Persists the new values
        // and pushes them into the running app (pet size, behavior, debug
        // overlay, monitor intervals, state-animation fps).
        _saveSettings();
        ApplyPetSize();
        // Restart the current animator state so any state-speed overrides
        // applied to the SpritePet definition take effect on the live pet
        // instead of waiting for the next state transition.
        if (_petAnimator is { } animator && !string.IsNullOrEmpty(animator.StateName))
        {
            animator.Play(animator.StateName);
        }
        StartBehaviorMode();
        ApplyDebugOverlayVisibility();
        _applyMonitoringSettings?.Invoke();
    }

    /// <summary>
    /// Sets the notification poll interval used to render the debug countdown.
    /// </summary>
    public void ConfigureDebugPolling(TimeSpan pollInterval)
    {
        _debugPollInterval = pollInterval;
        _debugNextPollUtc = DateTime.UtcNow + pollInterval;
    }

    /// <summary>
    /// Called by the notification monitor after each poll completes. Resets the
    /// countdown to the next poll and records the count seen.
    /// </summary>
    public void ReportPollComplete(int notificationCount)
    {
        _debugLastPollCount = notificationCount;
        _debugNextPollUtc = DateTime.UtcNow + _debugPollInterval;
        _debugStatus = "polled";
        UpdateDebugOverlay();
    }

    /// <summary>
    /// Called by the notification monitor when a poll fails or is skipped.
    /// </summary>
    public void ReportPollStatus(string status)
    {
        _debugStatus = status;
        UpdateDebugOverlay();
    }

    private void ApplyDebugOverlayVisibility()
    {
        bool showOverlay = _settings.DebugMode && !_debugOverlayHidden;

        if (showOverlay)
        {
            DebugOverlay.Visibility = Visibility.Visible;
            _debugOverlayTimer.Start();
            UpdateDebugOverlay();
        }
        else
        {
            _debugOverlayTimer.Stop();
            DebugOverlay.Visibility = Visibility.Collapsed;
        }

        // Help overlay only meaningful while DebugMode is on. Hide it when the
        // user disables Debug from settings.
        if (!_settings.DebugMode && HelpOverlay.Visibility == Visibility.Visible)
        {
            HelpOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void ToggleDebugOverlayPill()
    {
        _debugOverlayHidden = !_debugOverlayHidden;
        ApplyDebugOverlayVisibility();
    }

    private void ToggleHelpOverlay()
    {
        HelpOverlay.Visibility = HelpOverlay.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static string BuildHelpText()
    {
        return string.Join(
            Environment.NewLine,
            "Debug keys (focus pet, Debug on):",
            "  1   poll-soon 5s (one-shot)",
            "  2   set interval 2s",
            "  3   set interval 30s",
            "  4   toggle this help",
            "  5   toggle overlay pill",
            "  9   simulate messenger toast",
            "  0   simulate generic toast",
            "  F1  sprite frame prev",
            "  F2  sprite frame next",
            "  F3  sprite pause/resume");
    }

    private void OnDebugOverlayTimerTick(object? sender, EventArgs e)
    {
        UpdateDebugOverlay();
    }

    private void UpdateDebugOverlay()
    {
        if (!_settings.DebugMode)
        {
            return;
        }

        TimeSpan remaining = _debugNextPollUtc - DateTime.UtcNow;

        // When the countdown reaches zero but no poll arrives (monitor paused,
        // disabled, or stalled), wrap to the next interval so the overlay
        // visibly recounts instead of being stuck at 0s forever.
        if (remaining <= TimeSpan.Zero)
        {
            _debugNextPollUtc = DateTime.UtcNow + _debugPollInterval;
            remaining = _debugPollInterval;
        }

        int seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));

        DebugOverlayText.Text =
            $"next poll: {seconds}s  |  last: {_debugLastPollCount}  |  alerts: {_debugTotalAlerts}  |  {_debugStatus}";
    }

    private void TrySimulateNotification(bool messaging)
    {
        if (messaging)
        {
            // Press 9: spawn an in-process popup mimicking a Zalo-style toast
            // (small, topmost, no taskbar entry). The InAppNotificationWatcher
            // detects it the same way it detects real Zalo popups. This bypasses
            // the Windows toast pipeline entirely, so the OS does not show a
            // notification of its own.
            try
            {
                var popup = new DebugSimulatedPopupWindow(
                    sender: "Sky Breaking Master",
                    preview: "Where are you, my disciple?",
                    autoDismissAfter: TimeSpan.FromSeconds(5));
                popup.Show();
                ReportPollStatus("sim:inapp popup");
            }
            catch (Exception exception)
            {
                ReportPollStatus($"sim error: {exception.GetType().Name}");
            }
            return;
        }

        // Press 0: emit a real Windows toast under the generic debug AUMI.
        if (_debugSimulator is null)
        {
            ReportPollStatus("sim N/A");
            return;
        }

        try
        {
            _debugSimulator.SimulateGenericNotification();
            ReportPollStatus("sim:generic sent");
        }
        catch (Exception exception)
        {
            ReportPollStatus($"sim error: {exception.GetType().Name}");
        }
    }

    private async Task MaybeAutoCheckForUpdateAsync()
    {
        // Decision: auto-update default OFF; when ON, check once at launch.
        // Startup failures are silent (logged) — only manual checks show errors.
        if (!_settings.AutoUpdate || _updateService is null || !_updateService.IsSupported)
        {
            return;
        }

        try
        {
            UpdateInfo? info = await _updateService.CheckAsync();
            if (info is not null)
            {
                await PromptAndApplyUpdateAsync(info);
            }
        }
        catch (Exception)
        {
            // Silent on launch (offline, GitHub hiccup, etc.).
        }
    }

    /// <summary>
    /// Shared confirm → download → apply → restart flow used by both the
    /// launch auto-check and the About tab's manual check. Returns only if
    /// the user declines or the update fails (success restarts the process).
    /// </summary>
    public async Task PromptAndApplyUpdateAsync(UpdateInfo info)
    {
        if (_updateService is null)
        {
            return;
        }

        string newVersion = info.TargetFullRelease.Version.ToString();
        MessageBoxResult choice = MessageBox.Show(
            this,
            $"HyperPet v{newVersion} is available. Download and update now?\n\nThe app will restart to finish.",
            "HyperPet Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (choice != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _updateService.DownloadAndApplyAsync(info);
            // ApplyUpdatesAndRestart replaces the process; code below won't run.
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"The update could not be installed.\n\n{exception.Message}",
                "HyperPet Update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (!_settings.DebugMode)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.D1:
            case Key.NumPad1:
                // Trigger next poll in 5s without changing the steady-state
                // interval. Useful for testing without committing to a faster
                // poll rate.
                _pollSoon?.Invoke(TimeSpan.FromSeconds(5));
                _debugNextPollUtc = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                ReportPollStatus("poll-soon=5s");
                e.Handled = true;
                return;
            case Key.D2:
            case Key.NumPad2:
                _setPollInterval?.Invoke(TimeSpan.FromSeconds(2));
                ConfigureDebugPolling(TimeSpan.FromSeconds(2));
                ReportPollStatus("interval=2s");
                e.Handled = true;
                return;
            case Key.D3:
            case Key.NumPad3:
                _setPollInterval?.Invoke(TimeSpan.FromSeconds(30));
                ConfigureDebugPolling(TimeSpan.FromSeconds(30));
                ReportPollStatus("interval=30s");
                e.Handled = true;
                return;
            case Key.D4:
            case Key.NumPad4:
                ToggleHelpOverlay();
                e.Handled = true;
                return;
            case Key.D5:
            case Key.NumPad5:
                ToggleDebugOverlayPill();
                e.Handled = true;
                return;
            case Key.D9:
            case Key.NumPad9:
                TrySimulateNotification(messaging: true);
                e.Handled = true;
                return;
            case Key.D0:
            case Key.NumPad0:
                TrySimulateNotification(messaging: false);
                e.Handled = true;
                return;
        }

        if (_petAnimator is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.F3:
                _petAnimator.TogglePause();
                e.Handled = true;
                break;
            case Key.F2:
                _petAnimator.StepNext();
                e.Handled = true;
                break;
            case Key.F1:
                _petAnimator.StepPrevious();
                e.Handled = true;
                break;
        }
    }
}

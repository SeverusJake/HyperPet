using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using HyperPet.App.Notifications;
using HyperPet.App.Pets;
using HyperPet.App.Update;
using HyperPet.App.Views;
using HyperPet.Core.Diagnostics;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pet;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;
using HyperPet.Windows.Notifications;
using HyperPet.Windows.Startup;
using Velopack;

namespace HyperPet.App;

public partial class App : Application
{
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly StartupService _startupService = new();
    private SettingsStore? _settingsStore;
    private HyperPetSettings? _settings;
    private MainWindow? _mainWindow;
    private DispatcherTimer? _monitorTimer;
    private bool _monitorIterationRunning;
    private INotificationListener? _notificationListener;
    private InAppNotificationWatcher? _inAppWatcher;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private HyperPetLogger? _logger;
    private IReadOnlyList<PetCatalogEntry> _petCatalog = Array.Empty<PetCatalogEntry>();

    protected override async void OnStartup(StartupEventArgs e)
    {
        // MUST be the first thing the process does — wires Velopack's
        // install / uninstall / first-run / restart hooks. Late placement
        // breaks the update lifecycle.
        VelopackApp.Build().Run();

        base.OnStartup(e);

        string settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HyperPet");

        _logger = new HyperPetLogger(settingsDirectory);
        WireUnhandledExceptionHandlers(_logger);
        LogSessionHeader(_logger);

        Exception? settingsLoadException = null;

        try
        {
            _settingsStore = new SettingsStore(settingsDirectory);
            _settings = await _settingsStore.LoadAsync();
        }
        catch (Exception exception)
        {
            _logger.Error("Could not load HyperPet settings", exception);
            settingsLoadException = exception;
            _settings = HyperPetSettings.CreateDefault();
        }

        var petController = new PetController();
        var notificationDedupe = new NotificationDedupe();
        var notificationListener = new WindowsNotificationListener(_logger);
        _notificationListener = notificationListener;
        var appLauncher = new AppLauncher();
        var debugSimulator = new DebugNotificationSimulator(_logger);
        string petsRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Pets");
        _petCatalog = await PetCatalog.DiscoverAsync(petsRoot);
        PetCatalogEntry? selectedEntry = PetCatalog.Resolve(_petCatalog, _settings.SelectedPet);
        SpritePet? spritePet = await TryLoadSpritePetAsync(_logger, selectedEntry);

        // Snapshot original per-state fps + play mode BEFORE applying any
        // user overrides so the Settings dialog's "Reset" / "Default" can
        // restore pet.json values later in the session.
        Dictionary<string, int>? originalStateFps = null;
        Dictionary<string, PlayMode>? originalStatePlayMode = null;
        if (spritePet is not null)
        {
            originalStateFps = spritePet.Definition.States.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Fps,
                StringComparer.OrdinalIgnoreCase);

            originalStatePlayMode = spritePet.Definition.States.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.PlayMode,
                StringComparer.OrdinalIgnoreCase);

            ApplyStateSpeedOverrides(spritePet, _settings);
            ApplyStatePlayModeOverrides(spritePet, _settings);
        }

        var updateService = new UpdateService(_logger);
        _mainWindow = new MainWindow(
            _settings,
            ApplyStartupSetting,
            SaveSettings,
            spritePet,
            originalStateFps,
            originalStatePlayMode,
            appLauncher,
            SetPollInterval,
            PollSoon,
            debugSimulator,
            ApplyMonitoringSettings,
            updateService,
            _petCatalog)
        {
            Left = _settings.PetLeft,
            Top = _settings.PetTop
        };
        _mainWindow.ClampToWorkArea();
        _mainWindow.Show();

        if (settingsLoadException is not null)
        {
            MessageBox.Show(
                _mainWindow,
                "HyperPet could not load your saved settings, so default settings were used for this session.",
                "HyperPet Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        NotificationAccessStatus accessStatus = await TryRequestNotificationAccessAsync(notificationListener);

        if (accessStatus != NotificationAccessStatus.Allowed)
        {
            var setupWindow = new SetupWindow(async () =>
            {
                NotificationAccessStatus setupAccessStatus =
                    await TryRequestNotificationAccessAsync(notificationListener);

                if (setupAccessStatus == NotificationAccessStatus.Allowed)
                {
                    StartMonitoring(notificationListener, notificationDedupe, petController, _settings);
                }

                return setupAccessStatus;
            })
            {
                Owner = _mainWindow
            };

            setupWindow.Show();
            return;
        }

        StartMonitoring(notificationListener, notificationDedupe, petController, _settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Info("Session exit requested");
        _monitorTimer?.Stop();
        _inAppWatcher?.Dispose();
        _notificationListener?.StopListening();
        _shutdownCts.Cancel();

        if (_settingsStore is not null && _settings is not null && _mainWindow is not null)
        {
            _settings.PetLeft = _mainWindow.Left;
            _settings.PetTop = _mainWindow.Top;

            try
            {
                _settingsStore.Save(_settings);
            }
            catch (Exception exception)
            {
                _logger?.Error("Could not save HyperPet settings on exit", exception);
            }
        }

        _shutdownCts.Dispose();

        base.OnExit(e);
    }

    private void WireUnhandledExceptionHandlers(HyperPetLogger logger)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            logger.Fatal("DispatcherUnhandledException (UI thread)", args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.Fatal("UnobservedTaskException (background)", args.Exception);
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            logger.Fatal(
                $"AppDomain.UnhandledException (terminating={args.IsTerminating})",
                exception);
        };

        AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
        {
            // Logs every thrown exception, even if caught downstream. Useful
            // when process dies without any hook firing — at least we see the
            // last exception before silence.
            logger.Warn(
                $"FirstChanceException: {args.Exception.GetType().FullName}: {args.Exception.Message}");
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            logger.Info("ProcessExit");
        };
    }

    private static void LogSessionHeader(HyperPetLogger logger)
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            var os = RuntimeInformation.OSDescription;
            var fx = RuntimeInformation.FrameworkDescription;
            logger.Info($"=== HyperPet session start === version={version} os={os} framework={fx}");
        }
        catch (Exception exception)
        {
            logger.Warn("Could not collect session header info", exception);
        }
    }

    private async Task MonitorNotificationsIterationAsync(
        INotificationListener notificationListener,
        NotificationDedupe notificationDedupe,
        PetController petController,
        HyperPetSettings settings)
    {
        if (_monitorIterationRunning || _shutdownCts.IsCancellationRequested)
        {
            return;
        }

        _monitorIterationRunning = true;
        try
        {
            // Restore steady interval after a one-shot poll-soon fires.
            if (_pollSoonPending && _monitorTimer is not null)
            {
                _pollSoonPending = false;
                _monitorTimer.Interval = _steadyPollInterval;
            }

            if (!settings.ReactToWindowsNotifications)
            {
                _mainWindow?.ReportPollStatus("disabled");
                return;
            }

            _logger?.Info("monitor heartbeat");

            IReadOnlyList<HyperNotification> notifications =
                await notificationListener.GetActiveNotificationsAsync(_shutdownCts.Token);

            foreach (HyperNotification notification in notifications)
            {
                if (_shutdownCts.IsCancellationRequested)
                {
                    break;
                }

                DispatchIfNew(notification, notificationDedupe, petController, settings);
            }

            _mainWindow?.ReportPollComplete(notifications.Count);
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger?.Error("Notification monitor iteration failed", exception);
            _mainWindow?.ReportPollStatus("error");
        }
        finally
        {
            _monitorIterationRunning = false;
        }
    }

    private void DispatchIfNew(
        HyperNotification notification,
        NotificationDedupe notificationDedupe,
        PetController petController,
        HyperPetSettings settings)
    {
        if (!notificationDedupe.ShouldAlert(notification))
        {
            return;
        }

        if (AppRuleAutoDiscovery.TryRegister(settings, notification))
        {
            _logger?.Info($"Auto-discovered new app: {notification.AppName} (AUMI={notification.AppUserModelId})");
            SaveSettings();
        }

        PetAlert? alert = petController.HandleNotification(notification, settings);
        if (alert is null)
        {
            return;
        }

        try
        {
            _mainWindow?.ShowAlert(alert);
        }
        catch (Exception uiException)
        {
            _logger?.Error("ShowAlert threw on UI thread", uiException);
        }
    }

    private async Task<NotificationAccessStatus> TryRequestNotificationAccessAsync(
        INotificationListener notificationListener)
    {
        try
        {
            return await notificationListener.RequestAccessAsync(_shutdownCts.Token);
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
            return NotificationAccessStatus.Unspecified;
        }
        catch (Exception exception)
        {
            _logger?.Error("Could not request notification access", exception);
            return NotificationAccessStatus.Unspecified;
        }
    }

    private void StartMonitoring(
        INotificationListener notificationListener,
        NotificationDedupe notificationDedupe,
        PetController petController,
        HyperPetSettings settings)
    {
        if (_monitorTimer is not null || _shutdownCts.IsCancellationRequested)
        {
            return;
        }

        // Event-based: NotificationChanged fires when a notification is added.
        // Marshal to the UI/STA thread before processing.
        notificationListener.NotificationAdded += (_, notification) =>
        {
            if (!settings.ReactToWindowsNotifications)
            {
                return;
            }

            try
            {
                Dispatcher.InvokeAsync(() =>
                    DispatchIfNew(notification, notificationDedupe, petController, settings));
            }
            catch (Exception exception)
            {
                _logger?.Error("NotificationAdded dispatch failed", exception);
            }
        };
        notificationListener.StartListening();

        // Slow poll as fallback: catches notifications that arrived before
        // subscription and resyncs if NotificationChanged event is unsupported
        // (e.g., unpackaged desktop without background task registration).
        _steadyPollInterval = TimeSpan.FromSeconds(Math.Max(5, settings.WindowsNotificationPollIntervalSeconds));
        _monitorTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = _steadyPollInterval
        };
        _monitorTimer.Tick += async (_, _) =>
            await MonitorNotificationsIterationAsync(
                notificationListener,
                notificationDedupe,
                petController,
                settings);
        _monitorTimer.Start();

        _mainWindow?.ConfigureDebugPolling(_steadyPollInterval);

        // Kick off one immediate poll so we don't wait 30s for first sync.
        _ = MonitorNotificationsIterationAsync(
            notificationListener,
            notificationDedupe,
            petController,
            settings);

        StartInAppWatcher(notificationDedupe, petController, settings);
    }

    private void StartInAppWatcher(
        NotificationDedupe notificationDedupe,
        PetController petController,
        HyperPetSettings settings)
    {
        if (_inAppWatcher is not null)
        {
            return;
        }

        // Always include the current process so the debug press-9 simulator
        // (which spawns an in-process popup) round-trips through the same
        // pipeline that catches real third-party popups like Zalo.
        string ownProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        var watchList = (settings.WatchedInAppProcesses ?? new List<string>())
            .Concat(new[] { ownProcessName })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _inAppWatcher = new InAppNotificationWatcher(_logger);
        _inAppWatcher.SetWatchList(watchList);
        _inAppWatcher.SetInterval(TimeSpan.FromSeconds(Math.Max(1, settings.InAppNotificationPollIntervalSeconds)));

        // Exclude HyperPet's own MainWindow from popup detection.
        if (_mainWindow is not null)
        {
            void RegisterExclusion()
            {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    _inAppWatcher.ExcludeHandle(hwnd);
                }
            }

            if (_mainWindow.IsLoaded)
            {
                RegisterExclusion();
            }
            else
            {
                _mainWindow.Loaded += (_, _) => RegisterExclusion();
            }
        }

        _inAppWatcher.Detected += (_, notification) =>
        {
            if (!settings.ReactToInAppNotifications)
            {
                return;
            }

            try
            {
                DispatchIfNew(notification, notificationDedupe, petController, settings);
            }
            catch (Exception exception)
            {
                _logger?.Error("In-app watcher dispatch failed", exception);
            }
        };
        _inAppWatcher.Start();
    }

    private static async Task<SpritePet?> TryLoadSpritePetAsync(HyperPetLogger logger, PetCatalogEntry? entry)
    {
        if (entry is null)
        {
            logger.Error("No installed pets found under Assets/Pets.");
            return null;
        }

        try
        {
            return await SpritePetLoader.LoadAsync(entry.Directory);
        }
        catch (Exception exception)
        {
            logger.Error($"Could not load sprite pet '{entry.Id}' from '{entry.Directory}'", exception);
            return null;
        }
    }

    private static void ApplyStateSpeedOverrides(SpritePet spritePet, HyperPetSettings settings)
    {
        if (!settings.StateSpeedOverrides.TryGetValue(spritePet.Definition.Id, out var perPet))
        {
            return;
        }

        foreach (var (stateName, fps) in perPet)
        {
            if (spritePet.Definition.States.TryGetValue(stateName, out var state) && fps >= 1 && fps <= 60)
            {
                state.Fps = fps;
            }
        }
    }

    private static void ApplyStatePlayModeOverrides(SpritePet spritePet, HyperPetSettings settings)
    {
        if (!settings.StatePlayModeOverrides.TryGetValue(spritePet.Definition.Id, out var perPet))
        {
            return;
        }

        foreach (var (stateName, mode) in perPet)
        {
            if (spritePet.Definition.States.TryGetValue(stateName, out var state))
            {
                state.PlayMode = mode;
            }
        }
    }

    private void ApplyStartupSetting(bool enabled)
    {
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("HyperPet could not find the executable path for this session.");
        }

        _startupService.SetEnabled(enabled, executablePath);
    }

    private TimeSpan _steadyPollInterval = PollInterval;
    private bool _pollSoonPending;

    private void ApplyMonitoringSettings()
    {
        if (_settings is null)
        {
            return;
        }

        var winInterval = TimeSpan.FromSeconds(Math.Max(5, _settings.WindowsNotificationPollIntervalSeconds));
        SetPollInterval(winInterval);
        _mainWindow?.ConfigureDebugPolling(winInterval);

        var inAppInterval = TimeSpan.FromSeconds(Math.Max(1, _settings.InAppNotificationPollIntervalSeconds));
        _inAppWatcher?.SetInterval(inAppInterval);

        _logger?.Info($"Applied monitoring intervals: win={winInterval.TotalSeconds}s inApp={inAppInterval.TotalSeconds}s");
    }

    private void SetPollInterval(TimeSpan interval)
    {
        if (_monitorTimer is null)
        {
            return;
        }

        _steadyPollInterval = interval;
        _monitorTimer.Interval = interval;
        _pollSoonPending = false;
        _logger?.Info($"Poll interval changed to {interval.TotalSeconds}s");
    }

    private void PollSoon(TimeSpan delay)
    {
        if (_monitorTimer is null)
        {
            return;
        }

        _monitorTimer.Stop();
        _monitorTimer.Interval = delay;
        _pollSoonPending = true;
        _monitorTimer.Start();
        _logger?.Info($"Poll-soon scheduled in {delay.TotalSeconds}s (steady stays {_steadyPollInterval.TotalSeconds}s)");
    }

    private void SaveSettings()
    {
        if (_settingsStore is null || _settings is null)
        {
            return;
        }

        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception exception)
        {
            _logger?.Error("Could not save HyperPet settings", exception);

            MessageBox.Show(
                _mainWindow,
                "HyperPet could not save your settings right now. They may need to be set again next time.",
                "HyperPet Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}

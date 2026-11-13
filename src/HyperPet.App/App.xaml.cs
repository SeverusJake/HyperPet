using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using HyperPet.App.Notifications;
using HyperPet.App.Lifecycle;
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
    private bool _canPersistSettings;
    private bool _canApplyStartup;
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private DispatcherTimer? _monitorTimer;
    private bool _monitorIterationRunning;
    private Func<Task>? _enableWindowsMonitoring;
    private MonitoringLifecycle? _windowsLifecycle;
    private int _inAppDeliveryGeneration;
    private readonly NotificationBacklog _backlog = new();
    private SetupWindow? _setupWindow;
    private INotificationListener? _notificationListener;
    private InAppNotificationWatcher? _inAppWatcher;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private HyperPetLogger? _logger;
    private IReadOnlyList<PetCatalogEntry> _petCatalog = Array.Empty<PetCatalogEntry>();
    private string _builtinPetsRoot = string.Empty;
    private string _userPetsRoot = string.Empty;

    private AppLaunchContext _launchContext = null!;

    [STAThread]
    public static void Main()
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ =>
            {
                // Clean up user data (settings, logs, imported pets) so an
                // uninstall leaves nothing behind. Best-effort; the uninstaller
                // exits the process right after this hook.
                UninstallCleanup.Run(
                    () => new StartupService().SetEnabled(false, string.Empty),
                    () =>
                    {
                        string dataDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "HyperPet");
                        if (Directory.Exists(dataDir))
                        {
                            Directory.Delete(dataDir, recursive: true);
                        }
                    });
            })
            .Run();

#if DEBUG
        const bool development = true;
#else
        const bool development = false;
#endif
        var context = AppLaunchContext.Resolve(development, new UpdateService().IsSupported,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        using var instance = new SingleInstanceCoordinator(context.ProfileKey);
        if (!instance.TryAcquire()) { instance.RequestActivationAsync().GetAwaiter().GetResult(); return; }
        var app = new App { _launchContext = context };
        app.InitializeComponent();
        instance.StartActivationListener(() =>
        {
            if (!app.Dispatcher.HasShutdownStarted)
                app.Dispatcher.BeginInvoke(new Action(() => app._mainWindow?.Summon()));
        });
        app.Run();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string settingsDirectory = _launchContext.DataDirectory;

        _logger = new HyperPetLogger(settingsDirectory);
        WireUnhandledExceptionHandlers(_logger);
        LogSessionHeader(_logger);

        Exception? settingsLoadException = null;

        try
        {
            _settingsStore = new SettingsStore(settingsDirectory);
            var loaded = await _settingsStore.LoadSessionAsync();
            _settings = loaded.Settings;
            _canPersistSettings = loaded.CanPersist;
            _canApplyStartup = loaded.CanApplyStartup;
            if (loaded.Warning is not null) settingsLoadException = new IOException(loaded.Warning);
        }
        catch (Exception exception)
        {
            _logger.Error("Could not load HyperPet settings", exception);
            settingsLoadException = exception;
            _settings = HyperPetSettings.CreateDefault();
        }

        var petController = new PetController();
        try
        {
            if (_canApplyStartup && _launchContext.CanManageStartup) ApplyStartupSetting(_settings.StartWithWindows);
        }
        catch (Exception exception)
        {
            _logger.Error("Could not apply the Windows startup setting", exception);
        }

        var notificationDedupe = new NotificationDedupe();
        _windowsLifecycle = new MonitoringLifecycle(() => new WindowsNotificationListener(_logger),
            action => Dispatcher.InvokeAsync(action), exception => _logger.Warn("Windows notification operation unavailable", exception));
        _windowsLifecycle.SetEnabled(_settings.ReactToWindowsNotifications);
        _notificationListener = _windowsLifecycle;
        var notificationListener = _windowsLifecycle;
        var debugSimulator = new DebugNotificationSimulator(_logger);
        _builtinPetsRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Pets");
        _userPetsRoot = Path.Combine(settingsDirectory, "Pets");
        try { Directory.CreateDirectory(_userPetsRoot); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warn("User pets directory is unavailable; using readable pets only", exception);
        }
        _petCatalog = await PetCatalog.DiscoverAsync(_builtinPetsRoot, _userPetsRoot);
        PetCatalogEntry? selectedEntry = PetCatalog.Resolve(_petCatalog, _settings.SelectedPet);
        SpritePet? spritePet = await TryLoadSpritePetAsync(_logger, selectedEntry);
        if (spritePet is not null) _settings.SelectedPet = spritePet.Definition.Id;

        // Snapshot original per-state fps + play mode BEFORE applying any
        // user overrides so the Settings dialog's "Reset" / "Default" can
        // restore pet.json values later in the session.
        Dictionary<string, int>? originalStateFps = null;
        Dictionary<string, PlayMode>? originalStatePlayMode = null;
        if (spritePet is not null)
        {
            originalStateFps = PetOverrides.SnapshotFps(spritePet);
            originalStatePlayMode = PetOverrides.SnapshotPlayMode(spritePet);
            PetOverrides.Apply(spritePet, _settings);
        }

        var updateService = new UpdateService(_logger);
        _mainWindow = new MainWindow(
            _settings,
            ApplyStartupSetting,
            SaveSettings,
            spritePet,
            originalStateFps,
            originalStatePlayMode,
            SetPollInterval,
            PollSoon,
            debugSimulator,
            ApplyMonitoringSettings,
            updateService,
            _petCatalog,
            LoadPetByIdAsync,
            _userPetsRoot,
            () => PetCatalog.DiscoverAsync(_builtinPetsRoot, _userPetsRoot),
            canManageStartup: _canApplyStartup && _launchContext.CanManageStartup,
            scanInAppNow: token => _inAppWatcher?.ScanNowAsync(token) ?? Task.CompletedTask);
        _mainWindow.Show();

        _trayIcon = new TrayIcon(
            AppVersion.DisplayString,
            onCome: () => _mainWindow!.Summon(),
            onSettings: () => _mainWindow!.OpenSettings(),
            onCheckForUpdate: () => _ = _mainWindow!.CheckForUpdateFromTray(msg => _trayIcon?.Notify(msg)),
            onTuckAway: () => _mainWindow!.TuckAway(),
            onHistory: () => _mainWindow!.OpenNotificationHistory());

        if (settingsLoadException is not null)
        {
            MessageBox.Show(
                _mainWindow,
                "Saved settings could not be loaded. Defaults are used temporarily; this session will not overwrite your saved settings or Windows startup preference.",
                "HyperPet Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        StartInAppWatcher(notificationDedupe, petController, _settings);
        _enableWindowsMonitoring = async () =>
        {
            if (_shutdownCts.IsCancellationRequested || !_settings.ReactToWindowsNotifications) return;
            if (_setupWindow is { IsLoaded: true }) { _setupWindow.Activate(); return; }
            int generation = notificationListener.Generation;
            var accessStatus = await TryRequestNotificationAccessAsync(notificationListener);
            if (_shutdownCts.IsCancellationRequested || !notificationListener.CanDeliver(generation)) return;
            if (accessStatus == NotificationAccessStatus.Allowed)
            {
                StartMonitoring(notificationListener, notificationDedupe, petController, _settings);
                return;
            }
            if (_setupWindow is { IsLoaded: true }) { _setupWindow.Activate(); return; }
            _setupWindow = new SetupWindow(async () =>
            {
                int requestGeneration = notificationListener.Generation;
                var status = await TryRequestNotificationAccessAsync(notificationListener);
                if (!_shutdownCts.IsCancellationRequested && notificationListener.CanDeliver(requestGeneration) && status == NotificationAccessStatus.Allowed)
                    StartMonitoring(notificationListener, notificationDedupe, petController, _settings);
                return status;
            }) { Owner = _mainWindow };
            _setupWindow.Closed += (_, _) => _setupWindow = null;
            _setupWindow.SetInitialStatus(accessStatus); _setupWindow.Show();
        };
        if (_settings.ReactToWindowsNotifications) await _enableWindowsMonitoring();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Info("Session exit requested");
        _trayIcon?.Dispose();
        _monitorTimer?.Stop();
        _inAppWatcher?.Dispose();
        _windowsLifecycle?.Dispose();
        Interlocked.Increment(ref _inAppDeliveryGeneration);
        _shutdownCts.Cancel();

        if (_canPersistSettings && _settingsStore is not null && _settings is not null && _mainWindow is not null)
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

        // Do not log FirstChanceException globally: I/O failures in the logger
        // itself raise it before catch handlers run and can recurse indefinitely.

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
                if (_shutdownCts.IsCancellationRequested || !settings.ReactToWindowsNotifications)
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
        if (_backlog.IsBacklog(notification) || !notificationDedupe.ShouldAlert(notification))
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
            _mainWindow?.ObserveDebugNotification(notification, false);
            return;
        }

        try
        {
            bool shown = _mainWindow?.TryShowAlert(alert) == true;
            _mainWindow?.ObserveDebugNotification(notification, shown);
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
        if (_shutdownCts.IsCancellationRequested) return;
        // Toasts that arrived before this point were already seen by the user.
        if (_monitorTimer is null || !_monitorTimer.IsEnabled) _backlog.Reset();
        if (_monitorTimer is not null)
        {
            notificationListener.StartListening(); _monitorTimer.Start(); return;
        }

        // Event-based: NotificationChanged fires when a notification is added.
        // Marshal to the UI/STA thread before processing.
        notificationListener.NotificationAdded += (_, notification) =>
        {
            // MonitoringLifecycle posts to this dispatcher and rejects old enable generations.
            if (!_shutdownCts.IsCancellationRequested && settings.ReactToWindowsNotifications)
                DispatchIfNew(notification, notificationDedupe, petController, settings);
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

        // Watch the in-process simulator only in debug mode; production scans
        // should not subscribe to HyperPet's own accessibility tree.
        string ownProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        var watchList = (settings.WatchedInAppProcesses ?? new List<string>())
            .Concat(settings.DebugMode ? new[] { ownProcessName } : Array.Empty<string>())
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
            int generation = Volatile.Read(ref _inAppDeliveryGeneration);
            if (!settings.ReactToInAppNotifications)
            {
                return;
            }

            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (!_shutdownCts.IsCancellationRequested && settings.ReactToInAppNotifications && generation == Volatile.Read(ref _inAppDeliveryGeneration))
                        DispatchIfNew(notification, notificationDedupe, petController, settings);
                });
            }
            catch (Exception exception)
            {
                _logger?.Error("In-app watcher dispatch failed", exception);
            }
        };
        if (settings.ReactToInAppNotifications) _inAppWatcher.Start();
    }

    private async Task<SpritePet?> LoadPetByIdAsync(string petId)
    {
        var catalog = await PetCatalog.DiscoverAsync(_builtinPetsRoot, _userPetsRoot);
        // Explicit switching/removal fallback must never load an unrelated pet.
        var entry = catalog.FirstOrDefault(item => string.Equals(item.Id, petId, StringComparison.OrdinalIgnoreCase));
        return await TryLoadSpritePetAsync(_logger!, entry);
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


    private void ApplyStartupSetting(bool enabled)
    {
        if (!_canApplyStartup || !_launchContext.CanManageStartup)
        {
            if (enabled) throw new InvalidOperationException("Windows startup is managed only by the installed HyperPet app.");
            return;
        }
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

        _windowsLifecycle?.SetEnabled(_settings.ReactToWindowsNotifications);
        Interlocked.Increment(ref _inAppDeliveryGeneration);
        var inAppInterval = TimeSpan.FromSeconds(Math.Max(1, _settings.InAppNotificationPollIntervalSeconds));
        if (_inAppWatcher is not null)
        {
            _inAppWatcher.Stop();
            _inAppWatcher.SetInterval(inAppInterval);
            _inAppWatcher.SetWatchList((_settings.WatchedInAppProcesses ?? new())
                .Concat(_settings.DebugMode ? new[] { System.Diagnostics.Process.GetCurrentProcess().ProcessName } : Array.Empty<string>()));
            if (_settings.ReactToInAppNotifications) _inAppWatcher.Start();
        }
        if (_settings.ReactToWindowsNotifications)
        {
            _ = _enableWindowsMonitoring?.Invoke();
        }
        else { _monitorTimer?.Stop(); _notificationListener?.StopListening(); _setupWindow?.Close(); }

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
        if (!_canPersistSettings || _settingsStore is null || _settings is null)
        {
            return;
        }

        try
        {
            if (_mainWindow is not null)
            {
                _settings.PetLeft = _mainWindow.Left;
                _settings.PetTop = _mainWindow.Top;
            }

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

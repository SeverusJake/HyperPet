using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using HyperPet.App.Pets;
using HyperPet.App.Views;
using HyperPet.Core.Diagnostics;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pet;
using HyperPet.Core.Settings;
using HyperPet.Windows.Notifications;
using HyperPet.Windows.Startup;

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
    private HyperPetLogger? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
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
        SpritePet? spritePet = await TryLoadSpritePetAsync(_logger);

        _mainWindow = new MainWindow(_settings, ApplyStartupSetting, SaveSettings, spritePet, appLauncher)
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
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger?.Error("Notification monitor iteration failed", exception);
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
        _monitorTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _monitorTimer.Tick += async (_, _) =>
            await MonitorNotificationsIterationAsync(
                notificationListener,
                notificationDedupe,
                petController,
                settings);
        _monitorTimer.Start();

        // Kick off one immediate poll so we don't wait 30s for first sync.
        _ = MonitorNotificationsIterationAsync(
            notificationListener,
            notificationDedupe,
            petController,
            settings);
    }

    private static async Task<SpritePet?> TryLoadSpritePetAsync(HyperPetLogger logger)
    {
        string petDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Pets",
            "miku-kimono.codex-pet");

        try
        {
            return await SpritePetLoader.LoadAsync(petDirectory);
        }
        catch (Exception exception)
        {
            logger.Error($"Could not load sprite pet from '{petDirectory}'", exception);
            return null;
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

using System.Diagnostics;
using System.IO;
using System.Windows;
using HyperPet.App.Pets;
using HyperPet.App.Views;
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
    private Task? _monitorTask;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HyperPet");

        Exception? settingsLoadException = null;

        try
        {
            _settingsStore = new SettingsStore(settingsDirectory);
            _settings = await _settingsStore.LoadAsync();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Could not load HyperPet settings: {exception}");
            settingsLoadException = exception;
            _settings = HyperPetSettings.CreateDefault();
        }

        var petController = new PetController();
        var notificationDedupe = new NotificationDedupe();
        var notificationListener = new WindowsNotificationListener();
        SpritePet? spritePet = await TryLoadSpritePetAsync();

        _mainWindow = new MainWindow(_settings, ApplyStartupSetting, SaveSettings, spritePet)
        {
            Left = _settings.PetLeft,
            Top = _settings.PetTop
        };
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
                Debug.WriteLine($"Could not save HyperPet settings: {exception}");
            }
        }

        _shutdownCts.Dispose();

        base.OnExit(e);
    }

    private async Task MonitorNotificationsAsync(
        INotificationListener notificationListener,
        NotificationDedupe notificationDedupe,
        PetController petController,
        HyperPetSettings settings,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<HyperNotification> notifications =
                    await notificationListener.GetActiveNotificationsAsync(cancellationToken);

                foreach (HyperNotification notification in notifications)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!notificationDedupe.ShouldAlert(notification))
                    {
                        continue;
                    }

                    PetAlert? alert = petController.HandleNotification(notification, settings);
                    if (alert is null)
                    {
                        continue;
                    }

                    await Dispatcher.InvokeAsync(() => _mainWindow?.ShowAlert(alert));
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Notification monitor failed: {exception}");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
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
            Debug.WriteLine($"Could not request notification access: {exception}");
            return NotificationAccessStatus.Unspecified;
        }
    }

    private void StartMonitoring(
        INotificationListener notificationListener,
        NotificationDedupe notificationDedupe,
        PetController petController,
        HyperPetSettings settings)
    {
        if (_monitorTask is not null || _shutdownCts.IsCancellationRequested)
        {
            return;
        }

        _monitorTask = Task.Run(
            () => MonitorNotificationsAsync(
                notificationListener,
                notificationDedupe,
                petController,
                settings,
                _shutdownCts.Token));
    }

    private static async Task<SpritePet?> TryLoadSpritePetAsync()
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
            Debug.WriteLine($"Could not load sprite pet from '{petDirectory}': {exception}");
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
            Debug.WriteLine($"Could not save HyperPet settings: {exception}");

            MessageBox.Show(
                _mainWindow,
                "HyperPet could not save your settings right now. They may need to be set again next time.",
                "HyperPet Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}

using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HyperPet.App.Pets;
using HyperPet.Core.Settings;
using HyperPet.App.ViewModels;
using HyperPet.Core.Pet;
using HyperPet.Core.Pets;
using HyperPet.App.Views;

namespace HyperPet.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly HyperPetSettings _settings;
    private readonly Action<bool> _applyStartupSetting;
    private readonly Action _saveSettings;
    private readonly DispatcherTimer _alertTimer = new();
    private readonly DispatcherTimer _calmTimer = new();
    private readonly DispatcherTimer _movementTimer = new();
    private readonly Random _random = new();
    private readonly PetAnimator? _petAnimator;
    private bool _movingRight = true;
    private bool _alertActive;

    public MainWindow(
        HyperPetSettings settings,
        Action<bool> applyStartupSetting,
        Action saveSettings,
        SpritePet? spritePet)
    {
        _settings = settings;
        _applyStartupSetting = applyStartupSetting;
        _saveSettings = saveSettings;

        InitializeComponent();

        PauseAlertsMenuItem.IsChecked = settings.AlertsPaused;

        _viewModel = new MainWindowViewModel
        {
            AlertsPaused = settings.AlertsPaused
        };
        DataContext = _viewModel;

        _alertTimer.Tick += (_, _) => DismissAlert();
        _calmTimer.Tick += OnCalmTimerTick;
        _movementTimer.Tick += OnMovementTimerTick;
        Loaded += (_, _) => ClampToWorkArea();

        if (spritePet is null)
        {
            PetImage.Visibility = Visibility.Collapsed;
            return;
        }

        _petAnimator = new PetAnimator(spritePet, PetImage);
        StartBehaviorMode();
    }

    public void ShowAlert(PetAlert alert)
    {
        if (_viewModel.AlertsPaused)
        {
            return;
        }

        _viewModel.CurrentAlert = alert;
        _alertActive = true;
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

        if (_settings.PetBehaviorMode == PetBehaviorMode.Desktop)
        {
            StartDesktopMode();
            return;
        }

        StartCalmMode();
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

    private void StartDesktopMode()
    {
        _calmTimer.Stop();
        _petAnimator?.Play(_movingRight ? "runRight" : "runLeft");
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
        Rect workArea = SystemParameters.WorkArea;
        double windowWidth = GetWindowWidth();
        double nextLeft = Left + (_movingRight ? 2 : -2);

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
        var settingsWindow = new SettingsWindow(_settings, _applyStartupSetting)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true)
        {
            _saveSettings();
            StartBehaviorMode();
        }
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}

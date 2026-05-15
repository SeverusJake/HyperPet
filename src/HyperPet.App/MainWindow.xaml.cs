using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HyperPet.Core.Settings;
using HyperPet.App.ViewModels;
using HyperPet.Core.Pet;
using HyperPet.App.Views;

namespace HyperPet.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly HyperPetSettings _settings;
    private readonly Action<bool> _applyStartupSetting;
    private readonly Action _saveSettings;
    private readonly DispatcherTimer _alertTimer = new();

    public MainWindow(HyperPetSettings settings, Action<bool> applyStartupSetting, Action saveSettings)
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
    }

    public void ShowAlert(PetAlert alert)
    {
        if (_viewModel.AlertsPaused)
        {
            return;
        }

        _viewModel.CurrentAlert = alert;
        BubbleAppName.Text = alert.AppName;
        BubbleTitle.Text = alert.Title;
        BubbleBody.Text = alert.Body;
        Bubble.Visibility = Visibility.Visible;

        _alertTimer.Stop();
        _alertTimer.Interval = TimeSpan.FromSeconds(_settings.AlertDurationSeconds);
        _alertTimer.Start();
    }

    public void DismissAlert()
    {
        _alertTimer.Stop();
        _viewModel.CurrentAlert = null;
        Bubble.Visibility = Visibility.Collapsed;
        BubbleAppName.Text = string.Empty;
        BubbleTitle.Text = string.Empty;
        BubbleBody.Text = string.Empty;
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

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
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
        }
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}

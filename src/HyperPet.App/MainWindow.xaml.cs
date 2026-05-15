using System.Windows;
using System.Windows.Input;
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

    public MainWindow(HyperPetSettings settings, Action<bool> applyStartupSetting)
    {
        _settings = settings;
        _applyStartupSetting = applyStartupSetting;

        InitializeComponent();

        PauseAlertsMenuItem.IsChecked = settings.AlertsPaused;

        _viewModel = new MainWindowViewModel
        {
            AlertsPaused = settings.AlertsPaused
        };
        DataContext = _viewModel;
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
    }

    public void DismissAlert()
    {
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
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings, _applyStartupSetting)
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}

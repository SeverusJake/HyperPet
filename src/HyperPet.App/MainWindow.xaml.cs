using System.Windows;
using System.Windows.Input;
using HyperPet.App.ViewModels;
using HyperPet.Core.Pet;
using HyperPet.App.Views;

namespace HyperPet.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel();
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
            return;
        }

        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void OnPauseAlertsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.AlertsPaused = PauseAlertsMenuItem.IsChecked;

        if (_viewModel.AlertsPaused)
        {
            DismissAlert();
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow
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

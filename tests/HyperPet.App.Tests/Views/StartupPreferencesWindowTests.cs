using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HyperPet.App.Views;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

public sealed class StartupPreferencesWindowTests
{
    [Fact]
    public Task Apply_CommitsRememberPositionPreference() => RunOnStaThread(() =>
    {
        var settings = new HyperPetSettings();
        var window = new SettingsWindow(settings, _ => { });
        try
        {
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var remember = (CheckBox)window.FindName("RememberPetPositionCheckBox");
            var apply = (Button)window.FindName("ApplyButton");

            Assert.True(remember.IsChecked);
            Assert.False(apply.IsEnabled);
            remember.IsChecked = false;
            remember.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(apply.IsEnabled);
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.False(settings.RememberPetPosition);
            Assert.False(apply.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task Default_EnablesStartupPreferencesOnlyAfterApply() => RunOnStaThread(() =>
    {
        var settings = new HyperPetSettings { StartWithWindows = false, RememberPetPosition = false };
        var window = new SettingsWindow(settings, _ => { });
        try
        {
            var defaults = FindButtons(window).Single(button => Equals(button.Content, "Default"));
            defaults.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.True(((CheckBox)window.FindName("StartWithWindowsCheckBox")).IsChecked);
            Assert.True(((CheckBox)window.FindName("RememberPetPositionCheckBox")).IsChecked);
            Assert.False(settings.StartWithWindows);
            Assert.False(settings.RememberPetPosition);

            ((Button)window.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.True(settings.StartWithWindows);
            Assert.True(settings.RememberPetPosition);
        }
        finally
        {
            window.Close();
        }
    });

    [Theory]
    [InlineData(true, 120, 140)]
    [InlineData(false, 80, 80)]
    public Task Loaded_RestoresPositionAccordingToPreference(bool remember, double left, double top) => RunOnStaThread(() =>
    {
        var settings = new HyperPetSettings { RememberPetPosition = remember, PetLeft = 120, PetTop = 140 };
        var window = new MainWindow(settings, _ => { }, () => { }, spritePet: null);
        try
        {
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.Equal(left, window.Left);
            Assert.Equal(top, window.Top);
        }
        finally
        {
            window.Close();
        }
    });

    private static IEnumerable<Button> FindButtons(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is Button button)
            {
                yield return button;
            }
            foreach (var descendant in FindButtons(child))
            {
                yield return descendant;
            }
        }
    }

    private static Task RunOnStaThread(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}

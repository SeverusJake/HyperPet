using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HyperPet.App.Pets;
using HyperPet.App.Tests.TestSupport;
using HyperPet.App.Views;
using HyperPet.Core.Pet;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;
[Collection("WPF UI")]
public sealed class RemediationWindowTests
{
    [Fact]
    public Task CancelledDialogReload_DoesNotSwapVisiblePet() => StaThread.RunAsync(async () =>
    {
        var pending = new TaskCompletionSource<SpritePet?>(); var original = MakePet("a");
        var window = new MainWindow(new(), _ => { }, () => { }, original, petLoader: _ => pending.Task);
        try
        {
            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            using var cancellation = new CancellationTokenSource();
            var load = window.ReloadPetAsync("b", cancellation.Token);
            cancellation.Cancel(); pending.SetResult(MakePet("b"));
            Assert.False((await load).Success);
            Assert.Same(original.GetFrames("idle")[0], ((Image)window.FindName("PetImage")).Source);
        }
        finally { window.Close(); }
    });
    [Fact]
    public Task SlowOldReload_CannotReplaceNewerPet() => StaThread.RunAsync(async () =>
    {
        var b = new TaskCompletionSource<SpritePet?>(); var c = new TaskCompletionSource<SpritePet?>();
        var petC = MakePet("c");
        var window = new MainWindow(new(), _ => { }, () => { }, MakePet("a"), petLoader: id => id == "b" ? b.Task : c.Task);
        try
        {
            var old = window.ReloadPetAsync("b"); var latest = window.ReloadPetAsync("c");
            c.SetResult(petC); Assert.True((await latest).Success);
            b.SetResult(MakePet("b")); Assert.False((await old).Success);
            Assert.Same(petC.GetFrames("idle")[0], ((Image)window.FindName("PetImage")).Source);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task QuietHours_ClearsHoveredBubbleWithoutAnotherNotification() => StaThread.RunAsync(() =>
    {
        var settings = new HyperPetSettings { AlertDurationSeconds = 600 };
        var window = new MainWindow(settings, _ => { }, () => { }, MakePet("pet"));
        try
        {
            window.Show();
            window.ShowAlert(new PetAlert("App", "Title", "Body", DateTimeOffset.UtcNow));
            var bubble = (Border)window.FindName("Bubble");
            bubble.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
            settings.QuietHoursEnabled = true;
            settings.QuietHoursStart = DateTime.Now.AddMinutes(-5).ToString("HH:mm");
            settings.QuietHoursEnd = DateTime.Now.AddMinutes(5).ToString("HH:mm");
            var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
            Assert.Equal(Visibility.Collapsed, bubble.Visibility);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task DefaultPolling_MatchesPersistedModel() => StaThread.RunAsync(() =>
    {
        var window = new SettingsWindow(new(), _ => { });
        try
        {
            typeof(SettingsWindow).GetMethod("OnDefaultClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(window, new object[] { window, new RoutedEventArgs() });
            Assert.Equal(new HyperPetSettings().InAppNotificationPollIntervalSeconds.ToString(), ((TextBox)window.FindName("InAppPollIntervalTextBox")).Text);
        }
        finally { window.Close(); }
    });

    private static SpritePet MakePet(string id)
    {
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4); bitmap.Freeze();
        return new(new PetDefinition { Id = id, States = new Dictionary<string, PetAnimationState> { ["idle"] = new() { Frames = 1, Fps = 1, Loop = true } } },
            new Dictionary<string, IReadOnlyList<BitmapSource>> { ["idle"] = new[] { bitmap } });
    }
}

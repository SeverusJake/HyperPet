using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HyperPet.App.Pets;
using HyperPet.App.Tests.TestSupport;
using HyperPet.Core.Pet;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

[Collection("WPF UI")]
public sealed class TuckAwayTests
{
    [Fact]
    public Task DismissAlert_DuringTuckAway_DoesNotReplaceExitAnimation() => StaThread.RunAsync(() =>
    {
        var pet = CreatePet();
        var window = new MainWindow(new HyperPetSettings(), _ => { }, () => { }, pet);
        try
        {
            window.ShowAlert(CreateAlert());
            window.TuckAway();
            window.DismissAlert();

            Assert.Same(pet.GetFrames("failed")[0], ((Image)window.FindName("PetImage")).Source);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task NewAlert_DuringTuckAway_DoesNotReplaceExitAnimation() => StaThread.RunAsync(() =>
    {
        var pet = CreatePet();
        var window = new MainWindow(new HyperPetSettings(), _ => { }, () => { }, pet);
        try
        {
            window.TuckAway();
            window.ShowAlert(CreateAlert());

            Assert.Same(pet.GetFrames("failed")[0], ((Image)window.FindName("PetImage")).Source);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(2, false, true)]
    [InlineData(1, false, true)]
    [InlineData(2, true, true)]
    [InlineData(2, false, false)]
    public Task TuckAway_RequestsShutdownOnce_EvenWithoutAnimationCompletion(int frameCount, bool loop, bool hasFailed)
        => StaThread.RunAsync(() =>
    {
        int shutdownCount = 0;
        var pet = CreatePet(frameCount, loop, hasFailed);
        var window = new MainWindow(new HyperPetSettings(), _ => { }, () => { }, pet,
            requestShutdown: () => shutdownCount++);
        try
        {
            window.ShowAlert(CreateAlert());
            window.TuckAway();
            window.TuckAway();
            window.DismissAlert();
            window.ShowAlert(CreateAlert());

            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);

            Assert.Equal(1, shutdownCount);
        }
        finally { window.Close(); }
    });

    private static PetAlert CreateAlert() => new("Zalo", "Test", "Message", DateTimeOffset.UtcNow, false);

    private static SpritePet CreatePet(int frameCount = 2, bool failedLoop = false, bool hasFailed = true)
    {
        var states = new Dictionary<string, PetAnimationState>();
        var frames = new Dictionary<string, IReadOnlyList<BitmapSource>>();
        foreach (string name in new[] { "idle", "waving", "failed" })
        {
            if (name == "failed" && !hasFailed) { continue; }
            states[name] = new PetAnimationState { Frames = frameCount, Fps = 20, Loop = name != "failed" || failedLoop };
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4);
            bitmap.Freeze();
            frames[name] = Enumerable.Repeat(bitmap, frameCount).ToArray();
        }
        return new SpritePet(new PetDefinition { States = states }, frames);
    }
}

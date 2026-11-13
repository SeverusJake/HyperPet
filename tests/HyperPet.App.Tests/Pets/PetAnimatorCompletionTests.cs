using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HyperPet.App.Pets;
using HyperPet.App.Tests.TestSupport;
using HyperPet.Core.Pets;

namespace HyperPet.App.Tests.Pets;

[Collection("WPF UI")]
public sealed class PetAnimatorCompletionTests
{
    [Fact]
    public Task SingleFrameNonLoop_CompletesExactlyOnce() => StaThread.RunAsync(() =>
    {
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4); bitmap.Freeze();
        var pet = new SpritePet(new PetDefinition { States = new Dictionary<string, PetAnimationState> { ["idle"] = new() { Frames = 1, Fps = 60, Loop = false } } },
            new Dictionary<string, IReadOnlyList<BitmapSource>> { ["idle"] = new[] { bitmap } });
        var animator = new PetAnimator(pet, new Image()); int completed = 0;
        animator.Completed += _ => completed++; animator.Play("idle");
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
        animator.Stop(); Assert.Equal(1, completed);
    });
}

using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HyperPet.Core.Pets;

namespace HyperPet.App.Pets;

public sealed class PetAnimator
{
    private readonly SpritePet _spritePet;
    private readonly Image _image;
    private readonly DispatcherTimer _timer;
    private IReadOnlyList<BitmapSource> _frames = Array.Empty<BitmapSource>();
    private PetAnimationState? _state;
    private int _frameIndex;

    public PetAnimator(SpritePet spritePet, Image image)
    {
        _spritePet = spritePet ?? throw new ArgumentNullException(nameof(spritePet));
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _timer = new DispatcherTimer(DispatcherPriority.Render, _image.Dispatcher);
        _timer.Tick += OnTick;
    }

    public string StateName { get; private set; } = string.Empty;

    public void Play(string stateName)
    {
        _timer.Stop();

        StateName = stateName;
        _state = _spritePet.Definition.GetState(stateName);
        _frames = _spritePet.GetFrames(stateName);
        _frameIndex = 0;

        if (_frames.Count == 0)
        {
            _image.Source = null;
            return;
        }

        _image.Source = _frames[_frameIndex];

        if (_frames.Count <= 1)
        {
            return;
        }

        int fps = Math.Max(1, _state.Fps);
        _timer.Interval = TimeSpan.FromSeconds(1.0 / fps);
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_frames.Count == 0 || _state is null)
        {
            _timer.Stop();
            return;
        }

        if (_frameIndex >= _frames.Count - 1)
        {
            if (!_state.Loop)
            {
                _timer.Stop();
                return;
            }

            _frameIndex = 0;
        }
        else
        {
            _frameIndex++;
        }

        _image.Source = _frames[_frameIndex];
    }
}

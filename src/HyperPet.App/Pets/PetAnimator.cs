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
    // Direction for PingPong (and the underlying iterator for Forward/Reverse).
    // +1 means moving toward the higher frame index, -1 toward 0.
    private int _direction = 1;
    private bool _paused;

    public PetAnimator(SpritePet spritePet, Image image)
    {
        _spritePet = spritePet ?? throw new ArgumentNullException(nameof(spritePet));
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _timer = new DispatcherTimer(DispatcherPriority.Render, _image.Dispatcher);
        _timer.Tick += OnTick;
    }

    public string StateName { get; private set; } = string.Empty;

    public bool IsPaused => _paused;

    public int FrameIndex => _frameIndex;

    public int FrameCount => _frames.Count;

    /// <summary>FPS of the currently-playing state, or 1 when none.</summary>
    public int CurrentFps => _state?.Fps ?? 1;

    /// <summary>
    /// Raised once when a non-looping state reaches its natural end. The
    /// argument is the finished state name. Never raised for looping states.
    /// Handlers may call <see cref="Play"/> safely (fired after the frame is
    /// rendered, as the last action of the tick).
    /// </summary>
    public event Action<string>? Completed;

    public void Play(string stateName)
    {
        _timer.Stop();

        StateName = stateName;
        _state = _spritePet.Definition.GetState(stateName);
        _frames = _spritePet.GetFrames(stateName);

        InitializeForPlayMode();

        if (_frames.Count == 0)
        {
            _image.Source = null;
            return;
        }

        _image.Source = _frames[_frameIndex];

        if (_paused)
        {
            return;
        }

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

    public void TogglePause()
    {
        _paused = !_paused;

        if (_paused)
        {
            _timer.Stop();
            return;
        }

        if (_frames.Count <= 1 || _state is null)
        {
            return;
        }

        int fps = Math.Max(1, _state.Fps);
        _timer.Interval = TimeSpan.FromSeconds(1.0 / fps);
        _timer.Start();
    }

    public void StepNext()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        _paused = true;
        _timer.Stop();
        _frameIndex = (_frameIndex + 1) % _frames.Count;
        _image.Source = _frames[_frameIndex];
    }

    public void StepPrevious()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        _paused = true;
        _timer.Stop();
        _frameIndex = (_frameIndex - 1 + _frames.Count) % _frames.Count;
        _image.Source = _frames[_frameIndex];
    }

    private void InitializeForPlayMode()
    {
        if (_state is null || _frames.Count == 0)
        {
            _frameIndex = 0;
            _direction = 1;
            return;
        }

        switch (_state.PlayMode)
        {
            case PlayMode.Reverse:
                _frameIndex = _frames.Count - 1;
                _direction = -1;
                break;
            case PlayMode.PingPong:
                _frameIndex = 0;
                _direction = 1;
                break;
            case PlayMode.Forward:
            default:
                _frameIndex = 0;
                _direction = 1;
                break;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_frames.Count == 0 || _state is null || _paused)
        {
            _timer.Stop();
            return;
        }

        bool completed = AdvanceFrame();

        if (_frames.Count > 0)
        {
            _image.Source = _frames[_frameIndex];
        }

        if (completed)
        {
            string finished = StateName;
            _timer.Stop();
            Completed?.Invoke(finished);
        }
    }

    /// <summary>Advances one frame. Returns true when a non-looping state has
    /// reached its natural end (caller stops the timer and raises Completed).</summary>
    private bool AdvanceFrame()
    {
        if (_state is null)
        {
            return false;
        }

        PlaybackResult step = PlaybackStep.Next(
            _state.PlayMode, _frameIndex, _direction, _frames.Count);

        if (step.Completed && !_state.Loop)
        {
            // Non-looping state finished: hold on the current (last displayed)
            // frame, matching the previous behavior across all play modes.
            return true;
        }

        if (step.Completed && _state.Loop)
        {
            _frameIndex = _state.PlayMode switch
            {
                PlayMode.Reverse => _frames.Count - 1,
                PlayMode.PingPong => step.Index,
                _ => 0,
            };
            _direction = step.Direction;
            return false;
        }

        _frameIndex = step.Index;
        _direction = step.Direction;
        return false;
    }
}

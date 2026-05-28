namespace HyperPet.App.Pets.Roaming;

public enum RoamPhase
{
    Walking,
    Jumping,
}

/// <summary>
/// Pure state machine for Desktop (window-perch) mode. Decides the pet's
/// desired position and animation each tick from an <see cref="ILedgeProvider"/>.
/// Knows nothing about WPF; MainWindow applies X/Y and plays CurrentAnimation.
/// </summary>
public sealed class DesktopRoamController
{
    private const int JumpTicks = 18;
    private const double JumpArcHeight = 60.0;

    private readonly ILedgeProvider _provider;
    private readonly Random _random;

    private Ledge? _current;
    private int _direction = 1;          // +1 right, -1 left
    private RoamPhase _phase = RoamPhase.Walking;

    // Jump state.
    private Ledge? _target;
    private double _jumpStartX, _jumpStartY, _jumpEndX, _jumpEndY;
    private int _jumpTick;

    public DesktopRoamController(ILedgeProvider provider, Random random)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public double PetWidth { get; set; } = 192;
    public double PetHeight { get; set; } = 208;
    public int WalkSpeed { get; set; } = 2;

    public double X { get; private set; }
    public double Y { get; private set; }
    public string CurrentAnimation { get; private set; } = "idle";
    public RoamPhase Phase => _phase;

    /// <summary>Pick a starting ledge near the given position and begin walking.</summary>
    public void Start(double currentX, double currentY)
    {
        var ledges = _provider.GetLedges();
        _current = NearestLedge(ledges, currentX, currentY);
        _phase = RoamPhase.Walking;
        _direction = _random.Next(2) == 0 ? 1 : -1;

        if (_current is null)
        {
            X = currentX;
            Y = currentY;
            CurrentAnimation = "idle";
            return;
        }

        X = ClampX(currentX, _current);
        Y = _current.TopY - PetHeight;
        CurrentAnimation = _direction > 0 ? "runRight" : "runLeft";
    }

    public void Tick()
    {
        if (_phase == RoamPhase.Jumping)
        {
            JumpTick();
            return;
        }

        WalkTick();
    }

    private void WalkTick()
    {
        if (_current is null)
        {
            Start(X, Y);
            return;
        }

        Ledge? refreshed = _provider.TryRefresh(_current);
        if (refreshed is null)
        {
            BeginRecoveryJump();
            return;
        }

        _current = refreshed;
        Y = _current.TopY - PetHeight;

        double leftBound = _current.Left;
        double rightBound = _current.Right - PetWidth;

        if (rightBound <= leftBound)
        {
            BeginHopOrFlip();
            return;
        }

        X = Math.Clamp(X, leftBound, rightBound);
        double next = X + _direction * WalkSpeed;

        if (next < leftBound || next > rightBound)
        {
            BeginHopOrFlip();
            return;
        }

        X = next;
        CurrentAnimation = _direction > 0 ? "runRight" : "runLeft";
    }

    private void BeginHopOrFlip()
    {
        Ledge? target = ChooseTarget();
        if (target is null)
        {
            _direction = -_direction;
            CurrentAnimation = _direction > 0 ? "runRight" : "runLeft";
            return;
        }

        StartJump(target);
    }

    private void BeginRecoveryJump()
    {
        var ledges = _provider.GetLedges();
        Ledge? target = NearestLedge(ledges, X, Y);
        if (target is null)
        {
            CurrentAnimation = "idle";
            return;
        }

        _current = null;
        StartJump(target);
    }

    private void StartJump(Ledge target)
    {
        _target = target;
        _jumpStartX = X;
        _jumpStartY = Y;
        _jumpEndX = ClampX(X, target);
        _jumpEndY = target.TopY - PetHeight;
        _jumpTick = 0;
        _phase = RoamPhase.Jumping;
        CurrentAnimation = "jumping";
    }

    private void JumpTick()
    {
        _jumpTick++;
        double t = _jumpTick / (double)JumpTicks;

        if (t >= 1.0 && _target is not null)
        {
            _current = _target;
            X = _jumpEndX;
            Y = _jumpEndY;
            _phase = RoamPhase.Walking;
            double mid = _current.Left + _current.Width / 2.0;
            _direction = X < mid ? 1 : -1;
            CurrentAnimation = _direction > 0 ? "runRight" : "runLeft";
            _target = null;
            return;
        }

        X = Lerp(_jumpStartX, _jumpEndX, t);
        Y = Lerp(_jumpStartY, _jumpEndY, t) - JumpArcHeight * Math.Sin(Math.PI * t);
        CurrentAnimation = "jumping";
    }

    private Ledge? ChooseTarget()
    {
        var ledges = _provider.GetLedges();
        var candidates = ledges.Where(l => _current is null || !l.IsSameSurface(_current)).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        candidates.Sort((a, b) => DistanceTo(a).CompareTo(DistanceTo(b)));
        int pickWindow = Math.Min(3, candidates.Count);
        return candidates[_random.Next(pickWindow)];
    }

    private double DistanceTo(Ledge ledge)
    {
        double nearestX = Math.Clamp(X, ledge.Left, Math.Max(ledge.Left, ledge.Right - PetWidth));
        double dx = nearestX - X;
        double dy = (ledge.TopY - PetHeight) - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private Ledge? NearestLedge(IReadOnlyList<Ledge> ledges, double x, double y)
    {
        Ledge? best = null;
        double bestDist = double.MaxValue;
        foreach (var ledge in ledges)
        {
            double nearestX = Math.Clamp(x, ledge.Left, Math.Max(ledge.Left, ledge.Right - PetWidth));
            double dx = nearestX - x;
            double dy = (ledge.TopY - PetHeight) - y;
            double dist = dx * dx + dy * dy;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = ledge;
            }
        }

        return best;
    }

    private double ClampX(double x, Ledge ledge)
    {
        double rightBound = Math.Max(ledge.Left, ledge.Right - PetWidth);
        return Math.Clamp(x, ledge.Left, rightBound);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}

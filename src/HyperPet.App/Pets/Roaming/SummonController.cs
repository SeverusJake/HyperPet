namespace HyperPet.App.Pets.Roaming;

/// <summary>
/// Pure state machine for the "Come" behavior's movement. Steps the pet from
/// its current position straight toward a fixed target, up to WalkSpeed pixels
/// per Tick along both axes, and reports the run direction. Knows nothing about
/// WPF, jumping, waving, or hover — MainWindow owns that sequencing.
/// </summary>
public sealed class SummonController
{
    private double _targetX;
    private double _targetY;
    private string _facing = "runRight";

    public int WalkSpeed { get; set; } = 5;

    public double X { get; private set; }
    public double Y { get; private set; }
    public bool Arrived { get; private set; }
    public string CurrentAnimation => _facing;

    public void Start(double currentX, double currentY, double targetX, double targetY)
    {
        X = currentX;
        Y = currentY;
        _targetX = targetX;
        _targetY = targetY;
        Arrived = false;
        UpdateFacing(targetX - currentX);
    }

    public void Tick()
    {
        if (Arrived)
        {
            return;
        }

        double dx = _targetX - X;
        double dy = _targetY - Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        double speed = Math.Max(1, WalkSpeed);

        if (distance <= speed || distance == 0)
        {
            X = _targetX;
            Y = _targetY;
            Arrived = true;
            return;
        }

        double ratio = speed / distance;
        X += dx * ratio;
        Y += dy * ratio;
        UpdateFacing(dx);
    }

    private void UpdateFacing(double dx)
    {
        if (dx > 0)
        {
            _facing = "runRight";
        }
        else if (dx < 0)
        {
            _facing = "runLeft";
        }
        // dx == 0: keep last facing.
    }
}

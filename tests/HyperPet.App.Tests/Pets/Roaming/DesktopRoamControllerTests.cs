using HyperPet.App.Pets.Roaming;
using Xunit;

namespace HyperPet.App.Tests.Pets.Roaming;

public class DesktopRoamControllerTests
{
    private sealed class FakeLedgeProvider : ILedgeProvider
    {
        public List<Ledge> Ledges { get; set; } = new();
        public HashSet<Ledge> Vanished { get; } = new();

        public IReadOnlyList<Ledge> GetLedges() => Ledges;

        public Ledge? TryRefresh(Ledge ledge)
        {
            if (Vanished.Contains(ledge))
            {
                return null;
            }

            return Ledges.FirstOrDefault(l => l.IsSameSurface(ledge)) ?? ledge;
        }
    }

    private static DesktopRoamController MakeController(FakeLedgeProvider provider, int seed = 0)
    {
        return new DesktopRoamController(provider, new Random(seed))
        {
            PetWidth = 10,
            PetHeight = 20,
            WalkSpeed = 5,
        };
    }

    [Fact]
    public void Start_PlacesPetOnNearestLedge_AtTopMinusHeight()
    {
        var near = new Ledge(null, 0, 100, 200);
        var far = new Ledge(null, 0, 100, 500);
        var provider = new FakeLedgeProvider { Ledges = { near, far } };
        var c = MakeController(provider);

        c.Start(10, 190);

        Assert.Equal(180, c.Y);
        Assert.InRange(c.X, 0, 90);
        Assert.Equal(RoamPhase.Walking, c.Phase);
    }

    [Fact]
    public void WalkTick_AdvancesXByWalkSpeed()
    {
        var ledge = new Ledge(null, 0, 200, 100);
        var provider = new FakeLedgeProvider { Ledges = { ledge } };
        var c = MakeController(provider, seed: 1);
        c.Start(50, 80);

        double before = c.X;
        c.Tick();

        Assert.Equal(5, Math.Abs(c.X - before));
        Assert.Equal(80, c.Y);
    }

    [Fact]
    public void SingleLedge_FlipsDirectionAtEnd_DoesNotJump()
    {
        var ledge = new Ledge(null, 0, 30, 100);
        var provider = new FakeLedgeProvider { Ledges = { ledge } };
        var c = MakeController(provider, seed: 2);
        c.Start(18, 80);

        for (int i = 0; i < 20; i++)
        {
            c.Tick();
            Assert.Equal(RoamPhase.Walking, c.Phase);
            Assert.InRange(c.X, 0, 20);
        }
    }

    [Fact]
    public void ReachingEnd_WithSecondLedge_StartsJump()
    {
        var a = new Ledge(null, 0, 30, 100);
        var b = new Ledge(null, 200, 300, 100);
        var provider = new FakeLedgeProvider { Ledges = { a, b } };
        var c = MakeController(provider, seed: 3);
        c.Start(18, 80);

        bool jumped = false;
        for (int i = 0; i < 10 && !jumped; i++)
        {
            c.Tick();
            if (c.Phase == RoamPhase.Jumping)
            {
                jumped = true;
            }
        }

        Assert.True(jumped);
        Assert.Equal("jumping", c.CurrentAnimation);
    }

    [Fact]
    public void Jump_CompletesAndLandsOnTarget()
    {
        var a = new Ledge(null, 0, 30, 100);
        var b = new Ledge(null, 200, 300, 140);
        var provider = new FakeLedgeProvider { Ledges = { a, b } };
        var c = MakeController(provider, seed: 3);
        c.Start(18, 80);

        // 80 ticks > 4 full JumpTicks (18) cycles; guarantees at least one
        // complete jump and a subsequent walking phase regardless of seed.
        for (int i = 0; i < 80; i++)
        {
            c.Tick();
        }

        Assert.Equal(RoamPhase.Walking, c.Phase);
        Assert.True(c.Y == 80 || c.Y == 120);
    }

    [Fact]
    public void JumpMidpoint_LiftsAboveStraightLine()
    {
        var a = new Ledge(null, 0, 30, 100);
        var b = new Ledge(null, 200, 300, 100);
        var provider = new FakeLedgeProvider { Ledges = { a, b } };
        var c = MakeController(provider, seed: 3);
        c.Start(18, 80);

        for (int i = 0; i < 5 && c.Phase != RoamPhase.Jumping; i++)
        {
            c.Tick();
        }
        Assert.Equal(RoamPhase.Jumping, c.Phase);

        double minY = double.MaxValue;
        for (int i = 0; i < 9; i++)
        {
            c.Tick();
            minY = Math.Min(minY, c.Y);
        }
        Assert.True(minY < 80, $"expected lift above 80, got {minY}");
    }

    [Fact]
    public void VanishedLedge_TriggersRecoveryJump()
    {
        var a = new Ledge(null, 0, 200, 100);
        var b = new Ledge(null, 400, 600, 100);
        var provider = new FakeLedgeProvider { Ledges = { a, b } };
        var c = MakeController(provider, seed: 4);
        c.Start(50, 80);
        Assert.Equal(RoamPhase.Walking, c.Phase);

        provider.Vanished.Add(a);
        provider.Ledges.Remove(a);

        c.Tick();

        Assert.Equal(RoamPhase.Jumping, c.Phase);
        Assert.Equal("jumping", c.CurrentAnimation);
    }

    [Fact]
    public void Walking_ClampsXIntoResizedLedge()
    {
        var ledge = new Ledge(null, 0, 200, 100);
        var provider = new FakeLedgeProvider { Ledges = { ledge } };
        var c = MakeController(provider, seed: 5);
        c.Start(150, 80);

        var shrunk = new Ledge(null, 0, 60, 100);
        provider.Ledges[0] = shrunk;

        c.Tick();

        Assert.InRange(c.X, 0, 50);
    }
}

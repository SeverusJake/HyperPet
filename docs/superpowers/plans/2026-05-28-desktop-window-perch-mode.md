# Desktop Window-Perch Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Desktop behavior mode where the pet walks along window top edges (plus taskbar and screen-top) and jumps between them, riding windows as they move.

**Architecture:** A pure `DesktopRoamController` state machine (Walking/Jumping) consumes an `ILedgeProvider` abstraction so its geometry is unit-testable; `WindowLedgeProvider` supplies real ledges via Win32. MainWindow drives the controller from its 33ms movement timer in Desktop mode. The `PetBehaviorMode` enum is renamed/extended (numeric serialization keeps old saves valid).

**Tech Stack:** C# / .NET 8 WPF, Win32 P/Invoke (user32, dwmapi), xUnit.

**Spec:** `docs/superpowers/specs/2026-05-28-desktop-window-perch-mode-design.md`

---

## File Structure

**Create:**
- `src/HyperPet.App/Pets/Roaming/Ledge.cs` — record describing a walkable surface.
- `src/HyperPet.App/Pets/Roaming/ILedgeProvider.cs` — abstraction over perch surfaces.
- `src/HyperPet.App/Pets/Roaming/DesktopRoamController.cs` — pure roam state machine.
- `src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs` — Win32 ledge source.
- `tests/HyperPet.App.Tests/Pets/Roaming/DesktopRoamControllerTests.cs` — controller tests + FakeLedgeProvider.

**Modify:**
- `src/HyperPet.Core/Pets/PetBehaviorMode.cs` — `{ Calm=0, Running=1, Desktop=2 }`.
- `src/HyperPet.App/MainWindow.xaml.cs` — mode switch, controller wiring, rename patrol method.
- `src/HyperPet.App/Views/SettingsWindow.xaml` — third combo item.
- `src/HyperPet.App/Views/SettingsWindow.xaml.cs` — index↔enum mapping.
- `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs` — Desktop persists.

---

### Task 1: Enum migration (preserve behavior)

Renames the patrol value to `Running` and adds `Desktop`. Keeps Calm + Running working; Desktop temporarily falls back to Calm until Task 5 wires the controller. Numeric serialization means old saved `1` now binds to `Running` (same patrol) — no migration code needed.

**Files:**
- Modify: `src/HyperPet.Core/Pets/PetBehaviorMode.cs`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs`

- [ ] **Step 1: Update the enum**

Replace the entire body of `src/HyperPet.Core/Pets/PetBehaviorMode.cs`:

```csharp
namespace HyperPet.Core.Pets;

public enum PetBehaviorMode
{
    Calm = 0,
    Running = 1,
    Desktop = 2,
}
```

- [ ] **Step 2: Fix MainWindow's mode check**

In `src/HyperPet.App/MainWindow.xaml.cs`, `StartBehaviorMode()` currently is:

```csharp
        if (_settings.PetBehaviorMode == PetBehaviorMode.Desktop)
        {
            StartDesktopMode();
            return;
        }

        StartCalmMode();
```

Replace with (Desktop temporarily routes to Calm; Task 5 replaces this):

```csharp
        switch (_settings.PetBehaviorMode)
        {
            case PetBehaviorMode.Running:
                StartRunningMode();
                return;
            default:
                StartCalmMode();
                return;
        }
```

- [ ] **Step 3: Rename StartDesktopMode → StartRunningMode**

In the same file, rename the method `StartDesktopMode` to `StartRunningMode` (only the declaration; the new switch already calls `StartRunningMode`):

```csharp
    private void StartRunningMode()
    {
        _calmTimer.Stop();
        _petAnimator?.Play(_movingRight ? "runRight" : "runLeft");
        _movementTimer.Interval = TimeSpan.FromMilliseconds(33);
        _movementTimer.Start();
    }
```

- [ ] **Step 4: Fix SettingsWindow combo mapping (load + commit)**

In `src/HyperPet.App/Views/SettingsWindow.xaml.cs`, the constructor sets:

```csharp
        PetBehaviorComboBox.SelectedIndex = settings.PetBehaviorMode == PetBehaviorMode.Desktop ? 1 : 0;
```

Replace with (enum values 0/1/2 line up with combo item order):

```csharp
        PetBehaviorComboBox.SelectedIndex = (int)settings.PetBehaviorMode;
```

And in `CommitChanges()`:

```csharp
        PetBehaviorMode requestedPetBehaviorMode = PetBehaviorComboBox.SelectedIndex == 1
            ? PetBehaviorMode.Desktop
            : PetBehaviorMode.Calm;
```

Replace with:

```csharp
        PetBehaviorMode requestedPetBehaviorMode = PetBehaviorComboBox.SelectedIndex switch
        {
            1 => PetBehaviorMode.Running,
            2 => PetBehaviorMode.Desktop,
            _ => PetBehaviorMode.Calm,
        };
```

- [ ] **Step 5: Build + test**

Run: `dotnet build -c Release -nologo`
Expected: `Build succeeded.` 0 errors.
Run: `dotnet test -c Release -nologo`
Expected: all pass (existing 37). The applier/store tests using `PetBehaviorMode.Desktop` still compile (value exists) and round-trip numerically.

- [ ] **Step 6: Commit**

```bash
git add src/HyperPet.Core/Pets/PetBehaviorMode.cs src/HyperPet.App/MainWindow.xaml.cs src/HyperPet.App/Views/SettingsWindow.xaml.cs
git commit -m "refactor: PetBehaviorMode { Calm, Running, Desktop } with safe numeric migration"
```

---

### Task 2: `Ledge` + `ILedgeProvider`

**Files:**
- Create: `src/HyperPet.App/Pets/Roaming/Ledge.cs`
- Create: `src/HyperPet.App/Pets/Roaming/ILedgeProvider.cs`

- [ ] **Step 1: Create the Ledge record**

`src/HyperPet.App/Pets/Roaming/Ledge.cs`:

```csharp
namespace HyperPet.App.Pets.Roaming;

/// <summary>
/// A horizontal surface the pet can stand and walk on. Backed by a window
/// (Hwnd set) or synthetic (taskbar / screen-top, Hwnd null).
/// </summary>
public sealed record Ledge(IntPtr? Hwnd, double Left, double Right, double TopY)
{
    public double Width => Right - Left;

    /// <summary>
    /// Identity for "is this the same surface" comparisons. Window ledges are
    /// identified by handle; synthetic ledges by their geometry.
    /// </summary>
    public bool IsSameSurface(Ledge other)
    {
        if (Hwnd is not null || other.Hwnd is not null)
        {
            return Hwnd == other.Hwnd;
        }

        return TopY == other.TopY && Left == other.Left;
    }
}
```

- [ ] **Step 2: Create the provider interface**

`src/HyperPet.App/Pets/Roaming/ILedgeProvider.cs`:

```csharp
namespace HyperPet.App.Pets.Roaming;

/// <summary>
/// Supplies the set of surfaces the pet can perch on, and refreshes a single
/// ledge's current geometry.
/// </summary>
public interface ILedgeProvider
{
    /// <summary>All current perch surfaces (windows + taskbar + screen-top).</summary>
    IReadOnlyList<Ledge> GetLedges();

    /// <summary>
    /// Re-reads a window-backed ledge's current rectangle. Returns null when
    /// the window is gone, minimized, or hidden. Synthetic ledges (null Hwnd)
    /// return themselves unchanged.
    /// </summary>
    Ledge? TryRefresh(Ledge ledge);
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/Pets/Roaming/Ledge.cs src/HyperPet.App/Pets/Roaming/ILedgeProvider.cs
git commit -m "feat: Ledge record and ILedgeProvider abstraction for perch surfaces"
```

---

### Task 3: `DesktopRoamController` (TDD)

The testable core. Write the controller and tests together; tests use a `FakeLedgeProvider`.

**Files:**
- Create: `src/HyperPet.App/Pets/Roaming/DesktopRoamController.cs`
- Create: `tests/HyperPet.App.Tests/Pets/Roaming/DesktopRoamControllerTests.cs`

- [ ] **Step 1: Write the controller**

`src/HyperPet.App/Pets/Roaming/DesktopRoamController.cs`:

```csharp
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
            // No ledge yet — try to acquire one.
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

        // Ledge narrower than the pet: nowhere to walk, hop away.
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
            // Only the current ledge exists — turn around.
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
            // Nothing to stand on (should not happen: taskbar/screen-top exist).
            CurrentAnimation = "idle";
            return;
        }

        _current = null; // We are airborne; current is invalid.
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
            // Face toward the interior of the landed ledge.
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

        // Order by distance from the pet to each ledge's nearest x within its span.
        candidates.Sort((a, b) => DistanceTo(a).CompareTo(DistanceTo(b)));

        // Random pick among the few nearest to keep roaming varied.
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
```

- [ ] **Step 2: Write the tests + FakeLedgeProvider**

`tests/HyperPet.App.Tests/Pets/Roaming/DesktopRoamControllerTests.cs`:

```csharp
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

            // Synthetic ledges (null Hwnd) and present ledges return themselves.
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

        Assert.Equal(180, c.Y); // 200 - 20
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

        Assert.Equal(5, Math.Abs(c.X - before)); // WalkSpeed = 5
        Assert.Equal(80, c.Y); // 100 - 20
    }

    [Fact]
    public void SingleLedge_FlipsDirectionAtEnd_DoesNotJump()
    {
        var ledge = new Ledge(null, 0, 30, 100); // width 30, pet 10 -> rightBound 20
        var provider = new FakeLedgeProvider { Ledges = { ledge } };
        var c = MakeController(provider, seed: 2);
        c.Start(18, 80);

        // Walk until we hit an end; with one ledge it must stay Walking (flip).
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

        // Tick enough to start and finish a jump (JumpTicks=18 + a few walk ticks).
        for (int i = 0; i < 40; i++)
        {
            c.Tick();
        }

        // Eventually must be walking on some ledge with Y synced to a top.
        Assert.Equal(RoamPhase.Walking, c.Phase);
        Assert.True(c.Y == 80 || c.Y == 120); // 100-20 or 140-20
    }

    [Fact]
    public void JumpMidpoint_LiftsAboveStraightLine()
    {
        var a = new Ledge(null, 0, 30, 100);
        var b = new Ledge(null, 200, 300, 100);
        var provider = new FakeLedgeProvider { Ledges = { a, b } };
        var c = MakeController(provider, seed: 3);
        c.Start(18, 80);

        // Advance to a jump.
        for (int i = 0; i < 5 && c.Phase != RoamPhase.Jumping; i++)
        {
            c.Tick();
        }
        Assert.Equal(RoamPhase.Jumping, c.Phase);

        // Tick to roughly the middle of the arc and confirm Y rose above 80.
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

        // Make the current ledge (a, nearest to x=50) vanish.
        provider.Vanished.Add(a);
        provider.Ledges.Remove(a);

        c.Tick(); // refresh returns null -> recovery jump

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

        // Shrink the ledge so the pet's X is now out of bounds.
        var shrunk = new Ledge(null, 0, 60, 100); // rightBound = 50
        provider.Ledges[0] = shrunk;

        c.Tick();

        Assert.InRange(c.X, 0, 50);
    }
}
```

- [ ] **Step 3: Run the tests**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj -c Release -nologo`
Expected: all green (the 8 new roam tests + prior App tests).

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/Pets/Roaming/DesktopRoamController.cs tests/HyperPet.App.Tests/Pets/Roaming/DesktopRoamControllerTests.cs
git commit -m "feat: DesktopRoamController roam state machine with tests"
```

---

### Task 4: `WindowLedgeProvider` (Win32)

**Files:**
- Create: `src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs`

- [ ] **Step 1: Create the provider**

`src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows;

namespace HyperPet.App.Pets.Roaming;

/// <summary>
/// Supplies perch ledges from live Win32 state: visible top-level captioned
/// windows, the taskbar top edge, and the primary work-area top.
/// </summary>
public sealed class WindowLedgeProvider : ILedgeProvider
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_CAPTION = 0x00C00000;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const int DWMWA_CLOAKED = 14;
    private const int MinLedgeWidth = 80;

    private readonly IntPtr _selfHwnd;

    public WindowLedgeProvider(IntPtr selfHwnd)
    {
        _selfHwnd = selfHwnd;
    }

    public IReadOnlyList<Ledge> GetLedges()
    {
        var ledges = new List<Ledge>();

        EnumWindows((hwnd, _) =>
        {
            if (Qualifies(hwnd) && GetWindowRect(hwnd, out RECT r))
            {
                double width = r.Right - r.Left;
                if (width >= MinLedgeWidth)
                {
                    ledges.Add(new Ledge(hwnd, r.Left, r.Right, r.Top));
                }
            }

            return true;
        }, IntPtr.Zero);

        // Taskbar top edge.
        IntPtr tray = FindWindow("Shell_TrayWnd", null);
        if (tray != IntPtr.Zero && GetWindowRect(tray, out RECT t))
        {
            ledges.Add(new Ledge(null, t.Left, t.Right, t.Top));
        }

        // Screen (work area) top.
        Rect wa = SystemParameters.WorkArea;
        ledges.Add(new Ledge(null, wa.Left, wa.Right, wa.Top));

        return ledges;
    }

    public Ledge? TryRefresh(Ledge ledge)
    {
        if (ledge.Hwnd is not IntPtr hwnd)
        {
            return ledge; // synthetic, static
        }

        if (!Qualifies(hwnd) || !GetWindowRect(hwnd, out RECT r))
        {
            return null;
        }

        return new Ledge(hwnd, r.Left, r.Right, r.Top);
    }

    private bool Qualifies(IntPtr hwnd)
    {
        if (hwnd == _selfHwnd || hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (!IsWindowVisible(hwnd) || IsIconic(hwnd))
        {
            return false;
        }

        long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        if ((style & WS_CAPTION) != WS_CAPTION)
        {
            return false;
        }

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        if (IsCloaked(hwnd))
        {
            return false;
        }

        return true;
    }

    private static bool IsCloaked(IntPtr hwnd)
    {
        try
        {
            int cloaked = 0;
            int hr = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, sizeof(int));
            return hr == 0 && cloaked != 0;
        }
        catch (DllNotFoundException)
        {
            return false; // dwmapi missing (won't happen on supported Windows)
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs
git commit -m "feat: WindowLedgeProvider supplying ledges from Win32 windows + taskbar + screen-top"
```

---

### Task 5: MainWindow wiring

**Files:**
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Add usings + fields**

In `src/HyperPet.App/MainWindow.xaml.cs`, add to the usings:

```csharp
using System.Windows.Interop;
using HyperPet.App.Pets.Roaming;
```

Add fields next to `_petAnimator`:

```csharp
    private DesktopRoamController? _roamController;
    private string _lastRoamAnimation = string.Empty;
```

- [ ] **Step 2: Replace StartBehaviorMode's switch to handle Desktop**

The switch added in Task 1 currently routes Desktop → default (Calm). Replace the whole `StartBehaviorMode` body's switch with:

```csharp
        switch (_settings.PetBehaviorMode)
        {
            case PetBehaviorMode.Running:
                StartRunningMode();
                return;
            case PetBehaviorMode.Desktop:
                StartPerchMode();
                return;
            default:
                StartCalmMode();
                return;
        }
```

- [ ] **Step 3: Add StartPerchMode**

Add this method right after `StartRunningMode`:

```csharp
    private void StartPerchMode()
    {
        _calmTimer.Stop();

        if (_spritePet is null)
        {
            return;
        }

        if (_roamController is null)
        {
            IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
            _roamController = new DesktopRoamController(new WindowLedgeProvider(hwnd), _random);
        }

        _roamController.PetWidth = GetWindowWidth();
        _roamController.PetHeight = GetWindowHeight();
        _roamController.WalkSpeed = Math.Clamp(_settings.RunningSpeed, 1, 20);
        _roamController.Start(Left, Top);
        _lastRoamAnimation = string.Empty;

        _movementTimer.Interval = TimeSpan.FromMilliseconds(33);
        _movementTimer.Start();
    }
```

- [ ] **Step 4: Branch the movement tick between Running and Desktop**

`OnMovementTimerTick` currently does only the patrol. Replace its body with a branch:

```csharp
    private void OnMovementTimerTick(object? sender, EventArgs e)
    {
        if (_settings.PetBehaviorMode == PetBehaviorMode.Desktop)
        {
            RoamTick();
            return;
        }

        Rect workArea = SystemParameters.WorkArea;
        double windowWidth = GetWindowWidth();
        int speed = Math.Clamp(_settings.RunningSpeed, 1, 20);
        double nextLeft = Left + (_movingRight ? speed : -speed);

        if (nextLeft <= workArea.Left)
        {
            nextLeft = workArea.Left;
            SetMovementDirection(true);
        }
        else if (nextLeft + windowWidth >= workArea.Right)
        {
            nextLeft = workArea.Right - windowWidth;
            SetMovementDirection(false);
        }

        Left = nextLeft;
    }

    private void RoamTick()
    {
        if (_roamController is null)
        {
            return;
        }

        _roamController.Tick();
        Left = _roamController.X;
        Top = _roamController.Y;

        if (_roamController.CurrentAnimation != _lastRoamAnimation)
        {
            _lastRoamAnimation = _roamController.CurrentAnimation;
            _petAnimator?.Play(_lastRoamAnimation);
        }
    }
```

- [ ] **Step 5: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/HyperPet.App/MainWindow.xaml.cs
git commit -m "feat: drive DesktopRoamController from MainWindow in Desktop mode"
```

---

### Task 6: Settings combo item + persistence test

**Files:**
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml`
- Modify: `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`

- [ ] **Step 1: Add the third combo item**

In `src/HyperPet.App/Views/SettingsWindow.xaml`, the Pet behavior combo currently is:

```xml
                            <ComboBox x:Name="PetBehaviorComboBox"
                                      Margin="0,6,0,0"
                                      SelectedIndex="0">
                                <ComboBoxItem Content="Calm" />
                                <ComboBoxItem Content="Running" />
                            </ComboBox>
```

Add a third item:

```xml
                            <ComboBox x:Name="PetBehaviorComboBox"
                                      Margin="0,6,0,0"
                                      SelectedIndex="0">
                                <ComboBoxItem Content="Calm" />
                                <ComboBoxItem Content="Running" />
                                <ComboBoxItem Content="Desktop" />
                            </ComboBox>
```

- [ ] **Step 2: Add a persistence assertion for Desktop**

In `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`, add a new test after the round-trip test:

```csharp
    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsDesktopBehaviorMode()
    {
        var directory = CreateTempDirectory();
        var store = new SettingsStore(directory);
        var expected = new HyperPetSettings { PetBehaviorMode = PetBehaviorMode.Desktop };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(PetBehaviorMode.Desktop, actual.PetBehaviorMode);
    }
```

- [ ] **Step 3: Build + full test run**

Run: `dotnet build -c Release -nologo`
Expected: `Build succeeded.` 0 errors.
Run: `dotnet test -c Release -nologo`
Expected: all pass (prior + 8 roam + 1 new persistence test).

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/Views/SettingsWindow.xaml tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs
git commit -m "feat: Desktop option in pet behavior combo + persistence test"
```

---

### Task 7: Manual verification

**Files:** none.

- [ ] **Step 1: Run the app**

Run: `dotnet run --project src/HyperPet.App/HyperPet.App.csproj -c Release`

- [ ] **Step 2: Verify perch behavior**

1. Open 2-3 normal windows (Explorer, browser, Notepad).
2. Right-click pet → Settings → General → Pet behavior → **Desktop** → Apply.
3. Pet should walk along a window's top edge, then jump (arc, jumping sprite) to another window.
4. Drag a window the pet stands on — pet rides its top edge.
5. Close that window while the pet is on it — pet jumps to a remaining surface.
6. Minimize all windows — pet walks the taskbar top / screen top.
7. Switch behavior to Running, then Calm — movement changes immediately.
8. Trigger a notification (or press 9 in Debug) — pet waves, then resumes roaming after dismiss.

If any step misbehaves, return to the relevant task.

---

## Self-Review

**1. Spec coverage:**
- Walk + hop loop → Task 3 (`WalkTick`/`BeginHopOrFlip`/`JumpTick`).
- Perch surfaces = windows + taskbar + screen-top → Task 4 (`GetLedges`).
- Ride moving window; recover on vanish → Task 3 (`WalkTick` refresh sync + `BeginRecoveryJump`), Task 4 (`TryRefresh`).
- Parabolic jump with jumping sprite → Task 3 (`JumpTick`, `CurrentAnimation="jumping"`).
- Enum migration numeric-safe → Task 1.
- Settings combo third item + mapping → Task 1 (mapping) + Task 6 (item).
- MainWindow drive + rename patrol → Tasks 1 & 5.
- Tests: controller geometry/state → Task 3; Desktop persistence → Task 6. WindowLedgeProvider manual-only → Task 7. Matches spec's testing section.

**2. Placeholder scan:** No TODO/TBD/"similar to". Every code step is complete. No "handle edge cases" hand-waves — recovery, narrow-ledge, single-ledge, resize clamp all coded.

**3. Type consistency:**
- `Ledge(IntPtr? Hwnd, double Left, double Right, double TopY)` + `IsSameSurface` — defined Task 2, used in controller (Task 3) and provider (Task 4).
- `ILedgeProvider.GetLedges()` / `TryRefresh(Ledge)` — defined Task 2, implemented Task 4, consumed Task 3.
- `DesktopRoamController(ILedgeProvider, Random)` + `PetWidth/PetHeight/WalkSpeed/X/Y/CurrentAnimation/Phase/Start/Tick` — defined Task 3, used by MainWindow Task 5 with matching members (`PetWidth`, `PetHeight`, `WalkSpeed`, `Start(Left, Top)`, `Tick()`, `X`, `Y`, `CurrentAnimation`).
- `PetBehaviorMode.Running` / `.Desktop` — defined Task 1, used Tasks 5 & 6.
- `StartRunningMode` (renamed) / `StartPerchMode` (new) — Task 1 renames + switch calls `StartRunningMode`; Task 5 adds `StartPerchMode` and the Desktop switch arm.
- `WindowInteropHelper.EnsureHandle()` returns the HWND used by `WindowLedgeProvider(IntPtr)` — Task 5 ↔ Task 4 match.

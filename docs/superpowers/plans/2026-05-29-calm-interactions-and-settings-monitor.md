# Calm Interactions + Settings Side/Monitor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Calm-mode mouse reactions (hover/click/drag animations + periodic review), and make Settings open to the left of the pet on the pet's own monitor.

**Architecture:** Pure helpers first (`SettingsPlacement` left-first, `MonitorWorkArea` math seam) with unit tests; then MainWindow wires a Calm-only manual-drag state machine and uses the pet's monitor work area to place Settings.

**Tech Stack:** C# .NET 8 WPF, Win32 (user32 MonitorFromPoint/GetMonitorInfo), xUnit.

**Spec:** `docs/superpowers/specs/2026-05-29-calm-interactions-and-settings-monitor-design.md`

---

## File Structure

- Modify: `src/HyperPet.App/Views/SettingsPlacement.cs` — left-first preference.
- Modify: `tests/HyperPet.App.Tests/Views/SettingsPlacementTests.cs` — left-first expectations.
- Create: `src/HyperPet.App/Views/MonitorWorkArea.cs` — monitor work-area lookup (Win32) + pure conversion seam.
- Create: `tests/HyperPet.App.Tests/Views/MonitorWorkAreaTests.cs` — test the pure conversion.
- Modify: `src/HyperPet.App/MainWindow.xaml` — Grid mouse handlers.
- Modify: `src/HyperPet.App/MainWindow.xaml.cs` — calm interaction state machine, calm tick→review, settings monitor placement.

---

### Task 1: SettingsPlacement left-first

**Files:**
- Modify: `src/HyperPet.App/Views/SettingsPlacement.cs`
- Modify: `tests/HyperPet.App.Tests/Views/SettingsPlacementTests.cs`

- [ ] **Step 1: Flip the preference to left-first**

In `src/HyperPet.App/Views/SettingsPlacement.cs`, replace the block:
```csharp
        double rightX = petLeft + petWidth + Gap;
        double left = rightX + settingsWidth <= waRight
            ? rightX
            : petLeft - Gap - settingsWidth;
```
with:
```csharp
        double leftX = petLeft - Gap - settingsWidth;
        double left = leftX >= waLeft
            ? leftX                               // left of pet
            : petLeft + petWidth + Gap;           // fallback: right of pet
```
Also update the class doc comment first line to: `Computes where to open the
Settings window relative to the pet: to the pet's left, falling back to its
right when the left would overflow the work area. Both axes are clamped into
the work area.`

- [ ] **Step 2: Rewrite the tests for left-first**

Replace the body of `tests/HyperPet.App.Tests/Views/SettingsPlacementTests.cs` with:
```csharp
using HyperPet.App.Views;
using Xunit;

namespace HyperPet.App.Tests.Views;

public class SettingsPlacementTests
{
    private const double WaL = 0, WaT = 0, WaR = 1920, WaB = 1040;

    [Fact]
    public void Compute_PlacesLeftOfPet_WhenRoom()
    {
        // Pet at x=800, settings 500 wide: left = 800-8-500 = 292 (>=0) -> left.
        var p = SettingsPlacement.Compute(800, 100, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(800 - 8 - 500, p.Left);
        Assert.Equal(100, p.Top);
    }

    [Fact]
    public void Compute_FallsBackRight_WhenLeftUnderflows()
    {
        // Pet near left edge: left would be negative -> place right of pet.
        var p = SettingsPlacement.Compute(50, 100, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(50 + 200 + 8, p.Left);
    }

    [Fact]
    public void Compute_ClampsRightIntoWorkArea_WhenPetFarRight()
    {
        // Narrow work area; pet near right so right fallback would overflow ->
        // clamp into [waLeft, waRight-settingsWidth].
        // WaR=600, settings 500 -> maxLeft=100. Pet at 60: leftX=60-8-500=-448<0
        // -> right = 60+200+8 = 268 -> clamp to 100.
        var p = SettingsPlacement.Compute(60, 100, 200, 200, 500, 640, WaL, WaT, 600, WaB);
        Assert.Equal(100, p.Left);
    }

    [Fact]
    public void Compute_ClampsTop_WhenPetNearBottom()
    {
        var p = SettingsPlacement.Compute(800, 1000, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(WaB - 640, p.Top);   // 1040-640 = 400
    }

    [Fact]
    public void Compute_LeftExactFit_StaysLeft()
    {
        // leftX == waLeft exactly -> still left (>=). petLeft = 8+500 = 508.
        var p = SettingsPlacement.Compute(508, 0, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(0, p.Left);
    }

    [Fact]
    public void Compute_RespectsNonZeroWorkAreaOrigin()
    {
        // Second-monitor-style work area starting at x=1920. Pet at 2200.
        // leftX = 2200-8-500 = 1692 < 1920 -> right = 2200+200+8 = 2408.
        var p = SettingsPlacement.Compute(2200, 100, 200, 200, 500, 640, 1920, 0, 3840, 1040);
        Assert.Equal(2408, p.Left);
        Assert.Equal(100, p.Top);
    }
}
```

- [ ] **Step 3: Test**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj -c Release -nologo`
Expected: green (6 placement tests + others). If an arithmetic expectation is off, the implementation is source of truth — verify the math and report rather than silently editing.

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/Views/SettingsPlacement.cs tests/HyperPet.App.Tests/Views/SettingsPlacementTests.cs
git commit -m "feat: Settings opens to the left of the pet (fallback right)"
```

---

### Task 2: MonitorWorkArea helper + test

**Files:**
- Create: `src/HyperPet.App/Views/MonitorWorkArea.cs`
- Create: `tests/HyperPet.App.Tests/Views/MonitorWorkAreaTests.cs`

- [ ] **Step 1: Implement the helper**

`src/HyperPet.App/Views/MonitorWorkArea.cs`:
```csharp
using System.Runtime.InteropServices;

namespace HyperPet.App.Views;

public readonly record struct WorkArea(double Left, double Top, double Right, double Bottom);

/// <summary>
/// Resolves the work area (in DIPs) of the monitor that contains a given DIP
/// point, so windows can be placed on the same monitor as the pet rather than
/// always the primary one (which is what SystemParameters.WorkArea reports).
/// </summary>
public static class MonitorWorkArea
{
    private const int MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>
    /// Work area of the monitor under the given DIP point. Returns null when
    /// the OS lookup fails (caller should fall back to the primary work area).
    /// </summary>
    public static WorkArea? ForPoint(double dipX, double dipY, double dpiScaleX, double dpiScaleY)
    {
        if (dpiScaleX <= 0 || dpiScaleY <= 0)
        {
            return null;
        }

        var pt = new POINT
        {
            X = (int)Math.Round(dipX * dpiScaleX),
            Y = (int)Math.Round(dipY * dpiScaleY),
        };

        IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero)
        {
            return null;
        }

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref info))
        {
            return null;
        }

        return FromPhysical(info.rcWork, dpiScaleX, dpiScaleY);
    }

    /// <summary>
    /// Pure conversion of a physical-pixel work rect to DIPs. Exposed for tests.
    /// </summary>
    internal static WorkArea FromPhysical(RECT rcWork, double dpiScaleX, double dpiScaleY)
    {
        return new WorkArea(
            rcWork.Left / dpiScaleX,
            rcWork.Top / dpiScaleY,
            rcWork.Right / dpiScaleX,
            rcWork.Bottom / dpiScaleY);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, int dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
```

- [ ] **Step 2: Test the pure conversion**

`tests/HyperPet.App.Tests/Views/MonitorWorkAreaTests.cs`:
```csharp
using HyperPet.App.Views;
using Xunit;

namespace HyperPet.App.Tests.Views;

public class MonitorWorkAreaTests
{
    [Fact]
    public void FromPhysical_NoScaling_PassesThrough()
    {
        var rc = new MonitorWorkArea.RECT { Left = 1920, Top = 0, Right = 3840, Bottom = 1040 };
        var wa = MonitorWorkArea.FromPhysical(rc, 1.0, 1.0);
        Assert.Equal(1920, wa.Left);
        Assert.Equal(0, wa.Top);
        Assert.Equal(3840, wa.Right);
        Assert.Equal(1040, wa.Bottom);
    }

    [Fact]
    public void FromPhysical_DividesByDpiScale()
    {
        var rc = new MonitorWorkArea.RECT { Left = 0, Top = 0, Right = 3840, Bottom = 2160 };
        var wa = MonitorWorkArea.FromPhysical(rc, 2.0, 2.0);
        Assert.Equal(0, wa.Left);
        Assert.Equal(0, wa.Top);
        Assert.Equal(1920, wa.Right);
        Assert.Equal(1080, wa.Bottom);
    }
}
```
`RECT` and `FromPhysical` are `internal`; `HyperPet.App.Tests` already has
`InternalsVisibleTo` (added in v0.5.0), so these are visible.

- [ ] **Step 3: Build + test**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj -c Release -nologo`
Expected: green.

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/Views/MonitorWorkArea.cs tests/HyperPet.App.Tests/Views/MonitorWorkAreaTests.cs
git commit -m "feat: MonitorWorkArea resolves a point's monitor work area in DIPs"
```

---

### Task 3: Use the pet's monitor when placing Settings

**Files:**
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Add the using for DPI/Media**

`MainWindow.xaml.cs` already has `using System.Windows;`. Add at the top with
the other usings:
```csharp
using System.Windows.Media;
```
(`VisualTreeHelper` / `DpiScale` live in `System.Windows.Media`.)

- [ ] **Step 2: Replace the work-area source in OnSettingsClick**

In `OnSettingsClick`, the placement block currently is:
```csharp
        settingsWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        Rect wa = SystemParameters.WorkArea;
        Placement placement = SettingsPlacement.Compute(
            Left, Top, GetWindowWidth(), GetWindowHeight(),
            settingsWindow.Width, settingsWindow.Height,
            wa.Left, wa.Top, wa.Right, wa.Bottom);
        settingsWindow.Left = placement.Left;
        settingsWindow.Top = placement.Top;
        settingsWindow.ShowDialog();
```
Replace with (use the pet's monitor; fall back to primary if the lookup fails):
```csharp
        settingsWindow.WindowStartupLocation = WindowStartupLocation.Manual;

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        WorkArea? monitor = MonitorWorkArea.ForPoint(
            Left + GetWindowWidth() / 2, Top + GetWindowHeight() / 2,
            dpi.DpiScaleX, dpi.DpiScaleY);

        Rect primary = SystemParameters.WorkArea;
        WorkArea wa = monitor ?? new WorkArea(primary.Left, primary.Top, primary.Right, primary.Bottom);

        Placement placement = SettingsPlacement.Compute(
            Left, Top, GetWindowWidth(), GetWindowHeight(),
            settingsWindow.Width, settingsWindow.Height,
            wa.Left, wa.Top, wa.Right, wa.Bottom);
        settingsWindow.Left = placement.Left;
        settingsWindow.Top = placement.Top;
        settingsWindow.ShowDialog();
```
`WorkArea`, `MonitorWorkArea`, `Placement`, `SettingsPlacement` are all in
`HyperPet.App.Views` (already imported in this file).

- [ ] **Step 3: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/MainWindow.xaml.cs
git commit -m "fix: open Settings on the pet's monitor, not always the primary"
```

---

### Task 4: Calm-mode interactions (hover/click/drag + review)

**Files:**
- Modify: `src/HyperPet.App/MainWindow.xaml`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Add Grid mouse handlers in XAML**

In `src/HyperPet.App/MainWindow.xaml`, the root grid is:
```xml
    <Grid MouseLeftButtonDown="OnMouseLeftButtonDown">
```
Change to:
```xml
    <Grid MouseLeftButtonDown="OnMouseLeftButtonDown"
          MouseMove="OnGridMouseMove"
          MouseLeftButtonUp="OnGridMouseLeftButtonUp"
          MouseEnter="OnGridMouseEnter"
          MouseLeave="OnGridMouseLeave"
          LostMouseCapture="OnGridLostMouseCapture"
          Background="Transparent">
```
(`Background="Transparent"` ensures the whole grid area is hit-testable for
hover/move even where there's no child element. The window is already
`AllowsTransparency`.)

- [ ] **Step 2: Add calm-interaction state fields**

In `MainWindow.xaml.cs`, next to the other interaction-ish fields (after
`private bool _movingRight = true;`), add:
```csharp
    private bool _calmPressed;
    private bool _calmDragging;
    private System.Windows.Point _calmPressScreen;
    private double _calmPressLeft;
    private double _calmPressTop;
    private double _calmLastX;
    private const double CalmDragThreshold = 4;
    private readonly DispatcherTimer _calmJumpTimer = new();
```

- [ ] **Step 3: Wire the jump timer in the constructor**

In the constructor, where the other timers are wired (near
`_calmTimer.Tick += OnCalmTimerTick;`), add:
```csharp
        _calmJumpTimer.Tick += OnCalmJumpTimerTick;
```

- [ ] **Step 4: Add a Calm-interactive guard helper**

Add this private helper (near `StartBehaviorMode`):
```csharp
    private bool IsCalmInteractive()
        => _settings.PetBehaviorMode == PetBehaviorMode.Calm
           && _petAnimator is not null
           && !_alertActive;
```

- [ ] **Step 5: Branch OnMouseLeftButtonDown for Calm manual drag**

Replace the current `OnMouseLeftButtonDown` body:
```csharp
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            DismissAlert();
            e.Handled = true;
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        StopBehaviorTimers();

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            if (!_alertActive)
            {
                StartBehaviorMode();
            }
        }

        e.Handled = true;
    }
```
with:
```csharp
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            DismissAlert();
            e.Handled = true;
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        // Calm mode: manual drag so we can sample direction and tell a click
        // (jump) from a drag (run). Other modes keep the simple DragMove path.
        if (IsCalmInteractive())
        {
            _calmPressed = true;
            _calmDragging = false;
            _calmPressScreen = PointToScreen(e.GetPosition(this));
            _calmPressLeft = Left;
            _calmPressTop = Top;
            _calmLastX = _calmPressScreen.X;
            _calmJumpTimer.Stop();
            StopBehaviorTimers();
            CaptureMouse();
            e.Handled = true;
            return;
        }

        StopBehaviorTimers();

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            if (!_alertActive)
            {
                StartBehaviorMode();
            }
        }

        e.Handled = true;
    }
```

- [ ] **Step 6: Add the move / up / capture-lost / hover / jump-timer handlers**

Add these methods (place them right after `OnMouseLeftButtonDown`):
```csharp
    private void OnGridMouseMove(object sender, MouseEventArgs e)
    {
        if (!_calmPressed)
        {
            return;
        }

        System.Windows.Point cur = PointToScreen(e.GetPosition(this));
        double dxTotal = cur.X - _calmPressScreen.X;
        double dyTotal = cur.Y - _calmPressScreen.Y;

        if (!_calmDragging && Math.Abs(dxTotal) + Math.Abs(dyTotal) > CalmDragThreshold)
        {
            _calmDragging = true;
        }

        if (_calmDragging)
        {
            Left = _calmPressLeft + dxTotal;
            Top = _calmPressTop + dyTotal;

            if (cur.X > _calmLastX + 0.5)
            {
                PlayIfChanged("runRight");
            }
            else if (cur.X < _calmLastX - 0.5)
            {
                PlayIfChanged("runLeft");
            }

            _calmLastX = cur.X;
        }
    }

    private void OnGridMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_calmPressed)
        {
            return;
        }

        ReleaseMouseCapture();
        bool wasDragging = _calmDragging;
        _calmPressed = false;
        _calmDragging = false;

        if (wasDragging)
        {
            StartBehaviorMode();
        }
        else
        {
            PlayJumpThenIdle();
        }
    }

    private void OnGridLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_calmPressed)
        {
            return;
        }

        _calmPressed = false;
        _calmDragging = false;
        StartBehaviorMode();
    }

    private void OnGridMouseEnter(object sender, MouseEventArgs e)
    {
        if (!IsCalmInteractive() || _calmPressed)
        {
            return;
        }

        StopBehaviorTimers();
        _petAnimator?.Play("waiting");
    }

    private void OnGridMouseLeave(object sender, MouseEventArgs e)
    {
        if (!IsCalmInteractive() || _calmPressed)
        {
            return;
        }

        StartBehaviorMode();
    }

    private void PlayIfChanged(string state)
    {
        if (_petAnimator is { } a && a.StateName != state)
        {
            a.Play(state);
        }
    }

    private void PlayJumpThenIdle()
    {
        _petAnimator?.Play("jumping");
        _calmJumpTimer.Stop();
        _calmJumpTimer.Interval = JumpDuration();
        _calmJumpTimer.Start();
    }

    private void OnCalmJumpTimerTick(object? sender, EventArgs e)
    {
        _calmJumpTimer.Stop();
        if (IsCalmInteractive())
        {
            StartBehaviorMode();
        }
    }

    private TimeSpan JumpDuration()
    {
        var state = _spritePet?.Definition.GetState("jumping");
        if (state is not null && state.Fps > 0 && state.Frames > 0)
        {
            return TimeSpan.FromSeconds(state.Frames / (double)state.Fps);
        }

        return TimeSpan.FromMilliseconds(800);
    }
```

- [ ] **Step 7: Change the periodic calm animation to review**

`OnCalmTimerTick` is currently:
```csharp
    private void OnCalmTimerTick(object? sender, EventArgs e)
    {
        _petAnimator?.Play(_random.NextDouble() < 0.25 ? "waiting" : "idle");
    }
```
Change the `"waiting"` to `"review"`:
```csharp
    private void OnCalmTimerTick(object? sender, EventArgs e)
    {
        _petAnimator?.Play(_random.NextDouble() < 0.25 ? "review" : "idle");
    }
```

- [ ] **Step 8: Build + full test**

Run: `dotnet build -c Release -nologo` → 0 errors.
Run: `dotnet test -c Release -nologo` → all pass.

- [ ] **Step 9: Commit**

```bash
git add src/HyperPet.App/MainWindow.xaml src/HyperPet.App/MainWindow.xaml.cs
git commit -m "feat: calm-mode hover/click/drag animations + review idle"
```

---

### Task 5: Release v0.5.1

**Files:**
- Modify: `src/HyperPet.App/HyperPet.App.csproj`

- [ ] **Step 1: Bump version to 0.5.1**

In `src/HyperPet.App/HyperPet.App.csproj`, replace every `0.5.0` with `0.5.1`
(Version, AssemblyVersion `0.5.1.0`, FileVersion `0.5.1.0`,
InformationalVersion `0.5.1`).

- [ ] **Step 2: Build + full test**

Run: `dotnet test -c Release -nologo`
Expected: all pass.

- [ ] **Step 3: Commit + push**

```bash
git add src/HyperPet.App/HyperPet.App.csproj
git commit -m "feat: calm interactions + settings side/monitor (v0.5.1)"
git push origin main
```

- [ ] **Step 4: Publish + pack (keep prior Releases for delta)**

```bash
dotnet publish src/HyperPet.App/HyperPet.App.csproj -c Release -r win-x64 --self-contained true -o publish/velopack/0.5.1 -nologo
vpk pack --packId HyperPet --packVersion 0.5.1 --packDir publish/velopack/0.5.1 --mainExe HyperPet.exe
```
If `vpk pack` fails on post-process with "user-mapped section open" (transient
Defender lock), delete the partial 0.5.1 artifacts
(`rm -f Releases/HyperPet-0.5.1-*.nupkg Releases/releases.win.json Releases/assets.win.json Releases/HyperPet-win-Setup.exe Releases/HyperPet-win-Portable.zip Releases/RELEASES`)
keeping prior full nupkgs, and re-run the same `vpk pack` once.

- [ ] **Step 5: Version-stamped copies**

```bash
cp Releases/HyperPet-win-Setup.exe Releases/HyperPet-v0.5.1-win-Setup.exe
cp Releases/HyperPet-win-Portable.zip Releases/HyperPet-v0.5.1-win-Portable.zip
```

- [ ] **Step 6: Tag + push**

```bash
git tag -a v0.5.1 -m "HyperPet v0.5.1"
git push origin v0.5.1
```

- [ ] **Step 7: GitHub release**

```bash
gh release create v0.5.1 \
  "Releases/HyperPet-v0.5.1-win-Setup.exe" \
  "Releases/HyperPet-v0.5.1-win-Portable.zip" \
  "Releases/HyperPet-0.5.1-full.nupkg" \
  "Releases/HyperPet-0.5.1-delta.nupkg" \
  "Releases/releases.win.json" \
  "Releases/assets.win.json" \
  "Releases/RELEASES" \
  --title "HyperPet v0.5.1" \
  --notes "Calm mode now reacts to the mouse: hover plays a waiting pose, a quick click makes the pet jump, and dragging runs it left/right with your cursor. The idle loop occasionally plays a new review animation. Settings opens to the left of the pet (or right when there's no room) and on the same monitor as the pet. Auto-update users get a small delta; otherwise run HyperPet-v0.5.1-win-Setup.exe."
```

- [ ] **Step 8: Manual verification** (per spec): calm hover/click/drag/review; other modes unchanged; double-click still dismisses; Settings left-first and on the pet's monitor.

---

## Self-Review

**Spec coverage:**
- Calm hover→waiting, click→jump→idle, drag→run L/R, review idle → Task 4.
- Calm-only gating + manual drag (other modes keep DragMove) → Task 4 (IsCalmInteractive + branch).
- LostMouseCapture cleanup, play-on-change, JumpDuration fallback → Task 4.
- Settings left-first → Task 1.
- Settings same-monitor → Tasks 2-3.
- Tests: placement left-first (6), MonitorWorkArea conversion (2) → Tasks 1-2. Win32/input manual → Task 5 step 8.
- Release version-stamped → Task 5.

**Placeholder scan:** none. Every code step shows full code. Transient-pack-fail recovery is explicit.

**Type consistency:**
- `SettingsPlacement.Compute(...)` → `Placement` unchanged signature; left-first body only.
- `MonitorWorkArea.ForPoint(double,double,double,double)` → `WorkArea?`; `FromPhysical(RECT,double,double)` → `WorkArea`; both used in Tasks 2-3. `WorkArea(Left,Top,Right,Bottom)` consumed by `SettingsPlacement.Compute` in Task 3.
- `DpiScale`/`VisualTreeHelper.GetDpi` from `System.Windows.Media` (added Task 3 step 1).
- Calm fields (`_calmPressed`, `_calmDragging`, `_calmPressScreen`, `_calmPressLeft/Top`, `_calmLastX`, `_calmJumpTimer`) defined Task 4 step 2, used in steps 5-6.
- `_spritePet.Definition.GetState("jumping")` — `GetState` is the existing method PetAnimator uses; `PetAnimationState` has `Fps`/`Frames`. Consistent.
- New XAML handlers `OnGridMouseMove/OnGridMouseLeftButtonUp/OnGridMouseEnter/OnGridMouseLeave/OnGridLostMouseCapture` all defined in Task 4 step 6.

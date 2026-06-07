# Tray Icon, Come / Tuck Away, and Setup Icon — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a system-tray icon (Come · Settings · Check for update · Tuck Away), two new pet behaviors (Come = run to cursor-monitor center → jump → wave until hover; Tuck Away = play `failed` → quit), and a proper Setup.exe icon.

**Architecture:** A pure `PlaybackStep` helper makes frame-advance logic unit-testable and powers a new `PetAnimator.Completed` event. A pure `SummonController` (mirrors `DesktopRoamController`) computes the walk-to-center motion. `MainWindow` owns sequencing (Come/Tuck Away) and hover handling; a WinForms `NotifyIcon` (`TrayIcon`) created in `App` invokes public MainWindow methods. Velopack `Setup.exe` gets its icon via `vpk pack --icon`, wrapped in a committed `pack.ps1`.

**Tech Stack:** .NET 8, WPF, WinForms interop (`NotifyIcon`), xUnit, Velopack (`vpk`).

Spec: `docs/superpowers/specs/2026-06-05-tray-icon-come-tuckaway-setup-icon-design.md`

---

## File Structure

- **Create** `src/HyperPet.App/Pets/PlaybackStep.cs` — pure frame-advance math (next index/direction + completed flag).
- **Create** `tests/HyperPet.App.Tests/Pets/PlaybackStepTests.cs` — unit tests for `PlaybackStep`.
- **Modify** `src/HyperPet.App/Pets/PetAnimator.cs` — use `PlaybackStep`; add `Completed` event.
- **Create** `src/HyperPet.App/Pets/Roaming/SummonController.cs` — pure walk-to-target controller.
- **Create** `tests/HyperPet.App.Tests/Pets/Roaming/SummonControllerTests.cs` — unit tests.
- **Modify** `src/HyperPet.App/MainWindow.xaml.cs` — Come/Tuck Away sequencing, hover bypass, `OpenSettings()`, `CheckForUpdateFromTray()`, animator `Completed` wiring, `_movementTimer` branch.
- **Create** `src/HyperPet.App/TrayIcon.cs` — WinForms NotifyIcon wrapper.
- **Modify** `src/HyperPet.App/HyperPet.App.csproj` — add `<UseWindowsForms>true</UseWindowsForms>`.
- **Modify** `src/HyperPet.App/App.xaml.cs` — create/dispose `TrayIcon`.
- **Create** `pack.ps1` (repo root) — Velopack pack command incl. `--icon`.

---

## Task 1: Pure `PlaybackStep` frame-advance helper

Extracts the index/direction logic currently inline in `PetAnimator.AdvanceFrame` so it is unit-testable and can report completion.

**Files:**
- Create: `src/HyperPet.App/Pets/PlaybackStep.cs`
- Test: `tests/HyperPet.App.Tests/Pets/PlaybackStepTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/HyperPet.App.Tests/Pets/PlaybackStepTests.cs
using HyperPet.App.Pets;
using HyperPet.Core.Pets;
using Xunit;

namespace HyperPet.App.Tests.Pets;

public class PlaybackStepTests
{
    [Fact]
    public void Forward_Advances_UntilLastFrame_ThenCompletes()
    {
        // 3 frames, forward. index 0 -> 1 -> 2 (not complete), then at last -> complete.
        var s1 = PlaybackStep.Next(PlayMode.Forward, index: 0, direction: 1, frameCount: 3);
        Assert.Equal(1, s1.Index);
        Assert.False(s1.Completed);

        var s2 = PlaybackStep.Next(PlayMode.Forward, index: 1, direction: 1, frameCount: 3);
        Assert.Equal(2, s2.Index);
        Assert.False(s2.Completed);

        var s3 = PlaybackStep.Next(PlayMode.Forward, index: 2, direction: 1, frameCount: 3);
        Assert.True(s3.Completed);
        Assert.Equal(2, s3.Index); // stays on last frame when completed
    }

    [Fact]
    public void Reverse_Decrements_UntilFirstFrame_ThenCompletes()
    {
        var s1 = PlaybackStep.Next(PlayMode.Reverse, index: 2, direction: -1, frameCount: 3);
        Assert.Equal(1, s1.Index);
        Assert.False(s1.Completed);

        var s2 = PlaybackStep.Next(PlayMode.Reverse, index: 0, direction: -1, frameCount: 3);
        Assert.True(s2.Completed);
        Assert.Equal(0, s2.Index);
    }

    [Fact]
    public void PingPong_Flips_AtEnd_AndCompletesOnForwardEnd()
    {
        // Going forward, hitting the last frame completes (one full forward pass).
        var atEnd = PlaybackStep.Next(PlayMode.PingPong, index: 2, direction: 1, frameCount: 3);
        Assert.True(atEnd.Completed);
        Assert.Equal(-1, atEnd.Direction); // direction flipped for any subsequent looping

        // Mid-sequence forward just advances.
        var mid = PlaybackStep.Next(PlayMode.PingPong, index: 0, direction: 1, frameCount: 3);
        Assert.Equal(1, mid.Index);
        Assert.False(mid.Completed);
    }

    [Fact]
    public void SingleFrame_CompletesImmediately()
    {
        var s = PlaybackStep.Next(PlayMode.Forward, index: 0, direction: 1, frameCount: 1);
        Assert.True(s.Completed);
        Assert.Equal(0, s.Index);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj --filter PlaybackStepTests`
Expected: FAIL — `PlaybackStep` does not exist (compile error).

- [ ] **Step 3: Implement `PlaybackStep`**

```csharp
// src/HyperPet.App/Pets/PlaybackStep.cs
using HyperPet.Core.Pets;

namespace HyperPet.App.Pets;

/// <summary>
/// Pure frame-advance math for one animator tick. Returns the next frame
/// index, the next direction (for PingPong), and whether a non-looping pass
/// has reached its natural end. Keeps the index on the final frame when the
/// pass completes (callers that loop decide whether to wrap).
/// </summary>
public readonly record struct PlaybackResult(int Index, int Direction, bool Completed);

public static class PlaybackStep
{
    public static PlaybackResult Next(PlayMode mode, int index, int direction, int frameCount)
    {
        if (frameCount <= 1)
        {
            return new PlaybackResult(0, direction, true);
        }

        int last = frameCount - 1;

        switch (mode)
        {
            case PlayMode.Reverse:
                if (index <= 0)
                {
                    return new PlaybackResult(0, direction, true);
                }
                return new PlaybackResult(index - 1, direction, false);

            case PlayMode.PingPong:
                int next = index + direction;
                if (next > last)
                {
                    // Completed a forward pass; flip for any subsequent loop.
                    return new PlaybackResult(Math.Max(0, last - 1), -1, true);
                }
                if (next < 0)
                {
                    return new PlaybackResult(Math.Min(last, 1), 1, true);
                }
                return new PlaybackResult(next, direction, false);

            case PlayMode.Forward:
            default:
                if (index >= last)
                {
                    return new PlaybackResult(last, direction, true);
                }
                return new PlaybackResult(index + 1, direction, false);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj --filter PlaybackStepTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/HyperPet.App/Pets/PlaybackStep.cs tests/HyperPet.App.Tests/Pets/PlaybackStepTests.cs
git commit -m "feat: add pure PlaybackStep frame-advance helper"
```

---

## Task 2: `PetAnimator.Completed` event (rewire `AdvanceFrame`)

Make `AdvanceFrame` delegate to `PlaybackStep` and raise a `Completed` event once when a non-looping state finishes. No unit test here (PetAnimator is WPF/Dispatcher-bound; logic is covered by Task 1). Verified via build + later manual run.

**Files:**
- Modify: `src/HyperPet.App/Pets/PetAnimator.cs`

- [ ] **Step 1: Add the `Completed` event**

After the `CurrentFps` property (around line 38), add:

```csharp
    /// <summary>
    /// Raised once when a non-looping state reaches its natural end. The
    /// argument is the finished state name. Never raised for looping states.
    /// Handlers may call <see cref="Play"/> safely (fired after the frame is
    /// rendered, as the last action of the tick).
    /// </summary>
    public event Action<string>? Completed;
```

- [ ] **Step 2: Rewrite `OnTick` to fire `Completed`**

Replace the existing `OnTick` body (lines ~151-161) with:

```csharp
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
```

- [ ] **Step 3: Rewrite `AdvanceFrame` to use `PlaybackStep` and return `bool`**

Replace the entire `AdvanceFrame` method (the `switch` over `_state.PlayMode`, lines ~163-236) with:

```csharp
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
            // Hold on the final frame; signal completion.
            _frameIndex = step.Index;
            _direction = step.Direction;
            return true;
        }

        if (step.Completed && _state.Loop)
        {
            // Wrap for the next loop iteration.
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
```

Note: `using HyperPet.App.Pets;` is unnecessary — `PlaybackStep` is in the same namespace (`HyperPet.App.Pets`).

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run the full App test suite (no regressions)**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj`
Expected: PASS (existing tests + Task 1).

- [ ] **Step 6: Commit**

```bash
git add src/HyperPet.App/Pets/PetAnimator.cs
git commit -m "feat: raise PetAnimator.Completed when non-looping states finish"
```

---

## Task 3: `SummonController` (pure walk-to-target)

**Files:**
- Create: `src/HyperPet.App/Pets/Roaming/SummonController.cs`
- Test: `tests/HyperPet.App.Tests/Pets/Roaming/SummonControllerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/HyperPet.App.Tests/Pets/Roaming/SummonControllerTests.cs
using HyperPet.App.Pets.Roaming;
using Xunit;

namespace HyperPet.App.Tests.Pets.Roaming;

public class SummonControllerTests
{
    private static SummonController Make() => new() { WalkSpeed = 10 };

    [Fact]
    public void FacesRight_WhenTargetIsToTheRight()
    {
        var c = Make();
        c.Start(0, 0, targetX: 100, targetY: 0);
        c.Tick();
        Assert.Equal("runRight", c.CurrentAnimation);
        Assert.Equal(10, c.X);
        Assert.Equal(0, c.Y);
        Assert.False(c.Arrived);
    }

    [Fact]
    public void FacesLeft_WhenTargetIsToTheLeft()
    {
        var c = Make();
        c.Start(100, 0, targetX: 0, targetY: 0);
        c.Tick();
        Assert.Equal("runLeft", c.CurrentAnimation);
        Assert.Equal(90, c.X);
    }

    [Fact]
    public void Tick_MovesAlongBothAxes()
    {
        var c = Make();
        c.Start(0, 0, targetX: 30, targetY: 40); // distance 50, speed 10 -> 1/5 of the way
        c.Tick();
        Assert.Equal(6, c.X, 3);
        Assert.Equal(8, c.Y, 3);
    }

    [Fact]
    public void SnapsToTarget_AndSetsArrived_WhenWithinOneStep()
    {
        var c = Make();
        c.Start(0, 0, targetX: 4, targetY: 3); // distance 5 < speed 10
        c.Tick();
        Assert.Equal(4, c.X, 3);
        Assert.Equal(3, c.Y, 3);
        Assert.True(c.Arrived);
    }

    [Fact]
    public void Arrived_StaysAtTarget_OnSubsequentTicks()
    {
        var c = Make();
        c.Start(0, 0, targetX: 4, targetY: 3);
        c.Tick();
        c.Tick();
        Assert.Equal(4, c.X, 3);
        Assert.Equal(3, c.Y, 3);
        Assert.True(c.Arrived);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj --filter SummonControllerTests`
Expected: FAIL — `SummonController` does not exist.

- [ ] **Step 3: Implement `SummonController`**

```csharp
// src/HyperPet.App/Pets/Roaming/SummonController.cs
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj --filter SummonControllerTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/HyperPet.App/Pets/Roaming/SummonController.cs tests/HyperPet.App.Tests/Pets/Roaming/SummonControllerTests.cs
git commit -m "feat: add pure SummonController for Come movement"
```

---

## Task 4: MainWindow — Come & Tuck Away sequencing

Wire the animator `Completed` event, add summon state + fields, the `_movementTimer` branch, the public `Summon()` / `TuckAway()` methods, and the hover bypass.

**Files:**
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Add fields**

Near the other movement fields (after `_roamController` at line ~45), add:

```csharp
    private SummonController? _summonController;
    private bool _summoned;
    private bool _summonWalking;   // true while walking to center (movement ticking)
    private bool _tuckingAway;     // true after Tuck Away requested, awaiting "failed" end
```

Ensure the file has `using HyperPet.App.Pets.Roaming;` (it already references `DesktopRoamController`, so the using is present).

- [ ] **Step 2: Subscribe to `Completed` at both animator creation sites**

After line ~146 `_petAnimator = new PetAnimator(spritePet, PetImage);` add:

```csharp
        _petAnimator.Completed += OnPetAnimationCompleted;
```

After line ~789 `_petAnimator = new PetAnimator(pet, PetImage);` add the same line:

```csharp
        _petAnimator.Completed += OnPetAnimationCompleted;
```

- [ ] **Step 3: Add the completion handler**

Add this method near `RoamTick` (after line ~329):

```csharp
    private void OnPetAnimationCompleted(string finishedState)
    {
        if (_tuckingAway && finishedState == "failed")
        {
            _tuckingAway = false;
            Application.Current.Shutdown();
            return;
        }

        if (_summoned && finishedState == "jumping")
        {
            // Arrived + jumped: settle into a looping wave until the user hovers.
            _petAnimator?.Play("waving");
        }
    }
```

- [ ] **Step 4: Branch the movement timer to tick the summon walk**

In `OnMovementTimerTick` (line ~286), make the summon walk take priority. Replace the opening of the method:

```csharp
    private void OnMovementTimerTick(object? sender, EventArgs e)
    {
        if (_summonWalking)
        {
            SummonTick();
            return;
        }

        if (_settings.PetBehaviorMode == PetBehaviorMode.Desktop)
        {
            RoamTick();
            return;
        }
```

(Leave the rest of the method — the running-mode bounce logic — unchanged.)

- [ ] **Step 5: Add `SummonTick`**

Add after `RoamTick` (line ~329):

```csharp
    private void SummonTick()
    {
        if (_summonController is null)
        {
            _summonWalking = false;
            return;
        }

        _summonController.Tick();
        Left = _summonController.X;
        Top = _summonController.Y;

        if (_summonController.CurrentAnimation != _lastRoamAnimation)
        {
            _lastRoamAnimation = _summonController.CurrentAnimation;
            _petAnimator?.Play(_lastRoamAnimation);
        }

        if (_summonController.Arrived)
        {
            _summonWalking = false;
            _movementTimer.Stop();
            _petAnimator?.Play("jumping"); // in place; OnPetAnimationCompleted -> waving
        }
    }
```

- [ ] **Step 6: Add the public `Summon()` method**

Add near `OnSettingsClick` (after line ~740):

```csharp
    /// <summary>Tray "Come": run to the center of the cursor's monitor, jump on
    /// arrival, then wave (looping) until the user hovers the pet.</summary>
    public void Summon()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(Summon);
            return;
        }

        if (_petAnimator is null || _alertActive)
        {
            return;
        }

        StopBehaviorTimers();
        _hoverActive = false;

        double petW = GetWindowWidth();
        double petH = GetWindowHeight();

        // Resolve the cursor's monitor work area (DIPs).
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        var cursor = System.Windows.Forms.Cursor.Position; // physical px
        double cursorDipX = cursor.X / dpi.DpiScaleX;
        double cursorDipY = cursor.Y / dpi.DpiScaleY;

        WorkArea? monitor = MonitorWorkArea.ForPoint(
            cursorDipX, cursorDipY, dpi.DpiScaleX, dpi.DpiScaleY);
        Rect primary = SystemParameters.WorkArea;
        WorkArea wa = monitor ?? new WorkArea(primary.Left, primary.Top, primary.Right, primary.Bottom);

        double targetX = wa.Left + ((wa.Right - wa.Left) - petW) / 2.0;
        double targetY = wa.Top + ((wa.Bottom - wa.Top) - petH) / 2.0;

        _summonController ??= new SummonController();
        _summonController.WalkSpeed = Math.Clamp(_settings.RunningSpeed, 1, 20);
        _summonController.Start(Left, Top, targetX, targetY);

        _summoned = true;
        _summonWalking = true;
        _lastRoamAnimation = string.Empty;

        _movementTimer.Interval = TimeSpan.FromMilliseconds(33);
        _movementTimer.Start();
    }
```

`MonitorWorkArea` / `WorkArea` live in `HyperPet.App.Views`. Add `using HyperPet.App.Views;` to the top of MainWindow.xaml.cs if not already present (it is used at line ~726 already, so the using exists).

- [ ] **Step 7: Add the public `TuckAway()` method**

Add right after `Summon()`:

```csharp
    /// <summary>Tray "Tuck Away": play the failed animation, then quit.</summary>
    public void TuckAway()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(TuckAway);
            return;
        }

        StopBehaviorTimers();
        _summoned = false;
        _summonWalking = false;

        if (_petAnimator is null)
        {
            Application.Current.Shutdown();
            return;
        }

        _tuckingAway = true;
        _petAnimator.Play("failed"); // OnPetAnimationCompleted -> Shutdown
    }
```

- [ ] **Step 8: Bypass the hover lifecycle while summoned**

At the very top of `OnGridMouseEnter` (line ~535), before the `IsCalmInteractive` guard, add:

```csharp
    private void OnGridMouseEnter(object sender, MouseEventArgs e)
    {
        DebugInteraction("hover enter");

        if (_summoned)
        {
            // End the summon: return to the configured behavior on hover,
            // regardless of mode, skipping the normal 2s waiting lifecycle.
            _summoned = false;
            _summonWalking = false;
            _movementTimer.Stop();
            StartBehaviorMode();
            return;
        }

        if (!IsCalmInteractive() || _calmPressed)
        {
            return;
        }
```

(Leave the rest of `OnGridMouseEnter` unchanged.)

- [ ] **Step 9: Guard `StopBehaviorTimers` does not strand summon flags**

`StopBehaviorTimers` (line ~275) stops `_calmTimer` and `_movementTimer`. `StartBehaviorMode` calls it. That is correct — when we resume normal mode on hover, flags are already cleared in Step 8. No change needed; this step is a verification read.

- [ ] **Step 10: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj`
Expected: Build succeeded, 0 errors. (Will fail until Task 6 adds `<UseWindowsForms>`, because `System.Windows.Forms.Cursor` is referenced here. If the build fails only on `System.Windows.Forms`, proceed to Task 6 then return to verify.)

- [ ] **Step 11: Commit**

```bash
git add src/HyperPet.App/MainWindow.xaml.cs
git commit -m "feat: Come and Tuck Away pet behaviors in MainWindow"
```

---

## Task 5: MainWindow — extract `OpenSettings()` and add `CheckForUpdateFromTray()`

**Files:**
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Extract `OpenSettings()` from `OnSettingsClick`**

Rename the body: keep `OnSettingsClick` as a thin event handler and move its logic into a public method. Replace `OnSettingsClick` (lines ~704-740) with:

```csharp
    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    /// <summary>Opens the Settings dialog on the pet's monitor. Used by the pet
    /// context menu and the tray menu.</summary>
    public void OpenSettings()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(OpenSettings);
            return;
        }

        var settingsWindow = new SettingsWindow(
            _settings,
            _applyStartupSetting,
            RefreshFromSettings,
            _spritePet,
            _originalStateFps,
            _originalStatePlayMode,
            _updateService,
            PromptAndApplyUpdateAsync,
            _petCatalog,
            ReloadPetAsync,
            _userPetsRoot,
            _rediscoverPets)
        {
            Owner = this
        };

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
    }
```

- [ ] **Step 2: Add `CheckForUpdateFromTray()`**

Add after `PromptAndApplyUpdateAsync` (line ~987+). It needs the tray's `NotifyIcon` to show balloons, so accept an `Action<string>` for user-facing messages (the tray supplies it):

```csharp
    /// <summary>Manual update check from the tray. Reports outcome via the
    /// supplied notify callback (tray balloon). Manual checks surface errors,
    /// unlike the silent launch check.</summary>
    public async Task CheckForUpdateFromTray(Action<string> notify)
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(() => CheckForUpdateFromTray(notify));
            return;
        }

        if (_updateService is null || !_updateService.IsSupported)
        {
            notify("Updates are not available in this build.");
            return;
        }

        try
        {
            UpdateInfo? info = await _updateService.CheckAsync();
            if (info is null)
            {
                notify("HyperPet is up to date.");
                return;
            }

            await PromptAndApplyUpdateAsync(info);
        }
        catch (Exception)
        {
            notify("Could not check for updates. Check your connection and try again.");
        }
    }
```

- [ ] **Step 3: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj`
Expected: Build succeeded (modulo the `System.Windows.Forms` reference resolved in Task 6).

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/MainWindow.xaml.cs
git commit -m "refactor: extract OpenSettings, add CheckForUpdateFromTray"
```

---

## Task 6: TrayIcon (WinForms NotifyIcon) + csproj + App wiring

**Files:**
- Modify: `src/HyperPet.App/HyperPet.App.csproj`
- Create: `src/HyperPet.App/TrayIcon.cs`
- Modify: `src/HyperPet.App/App.xaml.cs`

- [ ] **Step 1: Enable WinForms in the csproj**

In `src/HyperPet.App/HyperPet.App.csproj`, inside the first `<PropertyGroup>` (next to `<UseWPF>true</UseWPF>`), add:

```xml
    <UseWindowsForms>true</UseWindowsForms>
```

- [ ] **Step 2: Implement `TrayIcon`**

```csharp
// src/HyperPet.App/TrayIcon.cs
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HyperPet.App;

/// <summary>
/// System-tray icon shown while HyperPet runs. Right-click menu:
/// Come, Settings, Check for update, Tuck Away. Double-click opens Settings.
/// Dispose on app exit to avoid a ghost icon.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(
        string tooltip,
        Action onCome,
        Action onSettings,
        Action onCheckForUpdate,
        Action onTuckAway)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Come", null, (_, _) => onCome());
        menu.Items.Add("Settings", null, (_, _) => onSettings());
        menu.Items.Add("Check for update", null, (_, _) => onCheckForUpdate());
        menu.Items.Add("Tuck Away", null, (_, _) => onTuckAway());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = Truncate(tooltip, 63), // NotifyIcon.Text max length is 63
            Visible = true,
            ContextMenuStrip = menu,
        };

        _icon.DoubleClick += (_, _) => onSettings();
    }

    /// <summary>Show a tray balloon (used for update-check outcomes).</summary>
    public void Notify(string message)
    {
        _icon.BalloonTipTitle = "HyperPet";
        _icon.BalloonTipText = message;
        _icon.ShowBalloonTip(4000);
    }

    private static Icon LoadIcon()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "HyperPet.ico");
        if (File.Exists(path))
        {
            return new Icon(path);
        }

        return SystemIcons.Application;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
```

Note: `HyperPet.ico` must exist next to the exe at runtime. The csproj already has `<Resource Include="Assets\HyperPet.ico" />` (embeds it as a WPF resource) but NOT a file copy. Add a copy so `AppContext.BaseDirectory\Assets\HyperPet.ico` exists.

- [ ] **Step 3: Copy the .ico to the output directory**

In `src/HyperPet.App/HyperPet.App.csproj`, in the `<ItemGroup>` that has the `Resource`/`Content` includes, add:

```xml
    <None Include="Assets\HyperPet.ico" Link="Assets\HyperPet.ico" CopyToOutputDirectory="PreserveNewest" />
```

(The existing `<Resource Include="Assets\HyperPet.ico" />` stays — one feeds WPF, this one puts a copy on disk for `NotifyIcon`.)

- [ ] **Step 4: Create and dispose the tray icon in `App`**

In `src/HyperPet.App/App.xaml.cs`, add a field near `_mainWindow` (line ~26 region):

```csharp
    private TrayIcon? _trayIcon;
```

After `_mainWindow.Show();` (line ~139), add:

```csharp
        _trayIcon = new TrayIcon(
            $"HyperPet {AppVersion.DisplayString}",
            onCome: () => _mainWindow.Summon(),
            onSettings: () => _mainWindow.OpenSettings(),
            onCheckForUpdate: () => _ = _mainWindow.CheckForUpdateFromTray(msg => _trayIcon?.Notify(msg)),
            onTuckAway: () => _mainWindow.TuckAway());
```

In `OnExit` (line ~178), after `_logger?.Info("Session exit requested");` add:

```csharp
        _trayIcon?.Dispose();
```

`AppVersion` is in `HyperPet.App` (`AppVersion.cs`); already used by MainWindow. Add `using` only if the compiler complains (same namespace, so unnecessary).

- [ ] **Step 5: Build the whole solution**

Run: `dotnet build HyperPet.sln`
Expected: Build succeeded, 0 errors (this resolves the `System.Windows.Forms.Cursor` reference from Task 4).

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: PASS (all projects).

- [ ] **Step 7: Manual smoke test**

Run: `./build.bat` (or launch the built exe).
Verify:
- Tray icon appears with the HyperPet icon and `HyperPet vX.Y.Z` tooltip.
- Right-click → all four items present.
- **Come** → pet runs to the center of the cursor's monitor, jumps, then waves looping; hovering the pet returns it to normal behavior.
- **Settings** and **double-click** → open the Settings dialog.
- **Check for update** → shows a balloon (up-to-date / not available / error as applicable).
- **Tuck Away** → pet plays the failed animation, then the app exits and the tray icon disappears.

- [ ] **Step 8: Commit**

```bash
git add src/HyperPet.App/HyperPet.App.csproj src/HyperPet.App/TrayIcon.cs src/HyperPet.App/App.xaml.cs
git commit -m "feat: system tray icon with Come/Settings/Update/Tuck Away"
```

---

## Task 7: Setup.exe icon + `pack.ps1`

**Files:**
- Create: `pack.ps1` (repo root)

- [ ] **Step 1: Write `pack.ps1`**

```powershell
# pack.ps1 — build the Velopack release (Setup.exe + nupkg) for a given version.
# Usage: ./pack.ps1 -Version 0.5.4
param(
    [Parameter(Mandatory = $true)][string]$Version
)

$ErrorActionPreference = 'Stop'

$packDir = "publish/velopack/$Version"
if (-not (Test-Path $packDir)) {
    throw "Pack dir not found: $packDir. Publish the app to that folder first."
}

vpk pack `
    --packId HyperPet `
    --packVersion $Version `
    --packDir $packDir `
    --mainExe HyperPet.exe `
    --icon "src/HyperPet.App/Assets/HyperPet.ico"

Write-Host "Packed HyperPet $Version. Output in Releases/." -ForegroundColor Green
```

- [ ] **Step 2: Verify the `--icon` flag is accepted by the installed vpk**

Run: `vpk pack --help`
Expected: output lists an `--icon` option (path to `.ico` for Setup.exe). If the installed vpk version names it differently, update `pack.ps1` to match — the `.ico` path is the substantive part.

- [ ] **Step 3 (optional, when releasing): produce a real package and confirm the icon**

Run: `./pack.ps1 -Version <next-version>` (after publishing the app to `publish/velopack/<next-version>`).
Expected: `Releases/HyperPet-win-Setup.exe` shows the HyperPet icon in Explorer.

- [ ] **Step 4: Commit**

```bash
git add pack.ps1
git commit -m "build: add pack.ps1 with Setup.exe icon (vpk --icon)"
```

---

## Self-Review Notes

- **Spec coverage:** tray icon + 4-item menu (Task 6), double-click=Settings (Task 6), Come incl. cursor-monitor center / jump / wave / hover-resume (Tasks 3-4), Tuck Away (Task 4), `PetAnimator.Completed` (Tasks 1-2), `SummonController` + tests (Task 3), Setup.exe icon + pack.ps1 (Task 7), OpenSettings/CheckForUpdate refactor (Task 5). All covered.
- **No-clamp during summon:** `SummonTick` sets `Left/Top` directly and never calls `ClampToWorkArea` (spec requirement) — vertical centering preserved.
- **Hover bypass:** `_summoned` branch added at the top of `OnGridMouseEnter` before the `IsCalmInteractive` guard, so it works regardless of behavior mode.
- **Type consistency:** `Summon()`, `TuckAway()`, `OpenSettings()`, `CheckForUpdateFromTray(Action<string>)`, `PlaybackStep.Next`, `PlaybackResult`, `SummonController` (Start/Tick/X/Y/Arrived/CurrentAnimation/WalkSpeed) are used identically across tasks.
- **Build-order caveat:** Task 4 references `System.Windows.Forms.Cursor`, which only resolves after Task 6 enables WinForms. Noted in Task 4 Step 10; full green build at Task 6 Step 5.

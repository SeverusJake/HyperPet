# Calm-Mode Interactions + Settings Monitor/Side

Date: 2026-05-29
Status: Approved (pending spec review)
Author: claude (with user)
Target release: v0.5.1

## Problem

In Calm mode the pet only idles. The user wants it to react to the mouse:
hover, click, and drag should play distinct animations. Separately, the
Settings window opens on the wrong side (should prefer left) and on the wrong
monitor (always the primary, even when the pet is on a second monitor).

## Goals

1. Calm-mode mouse reactions:
   - Hover (pointer over pet, no button) → `waiting`.
   - Click (press+release, no real movement) → `jumping` once, then back to idle.
   - Drag (press + move past threshold) → `runLeft`/`runRight` by drag
     direction while dragging (and the window moves), back to idle on release.
   - No interaction → normal idle; the periodic calm animation changes from
     `waiting` to `review`.
2. Settings opens to the **left** of the pet (fallback right).
3. Settings opens on the **same monitor** as the pet.

## Decisions (from brainstorming)

- Interactions apply in **Calm mode only**. Running/Desktop keep current
  behavior.
- Click vs drag uses a **move threshold** (~4 px). Click → jump once → idle.
  Drag → directional run → idle on release. Hover (no press) → waiting.
- Periodic calm animation: `review` (was `waiting`).
- Settings side: left first, fallback right.

## Non-goals

- Interactions in Running/Desktop modes.
- New animation states (uses existing `idle`, `waiting`, `jumping`,
  `runLeft`, `runRight`, `review`).
- Per-monitor-DPI exactness on mixed-DPI setups beyond landing on the correct
  monitor (small offset acceptable; the requirement is the correct monitor).

## Design

### A. Calm-mode interactions — `MainWindow`

All interaction handlers are gated on
`_settings.PetBehaviorMode == PetBehaviorMode.Calm && _petAnimator is not null && !_alertActive`.
Outside Calm (or during an alert) they no-op and the existing behavior runs.

State fields:
```csharp
private bool _calmPressed;
private bool _calmDragging;
private Point _calmPressScreen;     // mouse screen pos at press
private double _calmPressLeft;       // window Left at press
private double _calmPressTop;        // window Top at press
private double _calmLastX;           // last mouse X for direction
private readonly DispatcherTimer _calmJumpTimer = new();   // returns to idle after a jump
private const double CalmDragThreshold = 4;
```

**Hover** — handlers on the root `Grid` (already has `MouseLeftButtonDown`):
- `MouseEnter`: if Calm-interactive and not pressed → `StopBehaviorTimers()`,
  `_petAnimator.Play("waiting")`.
- `MouseLeave`: if Calm-interactive and not pressed and not dragging →
  `StartBehaviorMode()` (returns to calm idle loop).

**Press/move/release** — only when Calm-interactive; otherwise fall through to
the existing drag path. The existing `OnMouseLeftButtonDown` keeps the
double-click→`DismissAlert` shortcut for all modes. Restructure so that, in
Calm mode, instead of `DragMove()` we do a manual drag:

- `OnMouseLeftButtonDown` (Calm-interactive, single click, button pressed):
  - `_calmPressed = true; _calmDragging = false;`
  - `_calmPressScreen = PointToScreen(e.GetPosition(this));`
  - `_calmPressLeft = Left; _calmPressTop = Top; _calmLastX = _calmPressScreen.X;`
  - `CaptureMouse();` (capture on the window) ; `StopBehaviorTimers();`
  - `e.Handled = true;` (do NOT call `DragMove()` in Calm mode)
- `OnMouseMove` (new handler): if `_calmPressed`:
  - `Point cur = PointToScreen(e.GetPosition(this));`
  - `double dxTotal = cur.X - _calmPressScreen.X; double dyTotal = cur.Y - _calmPressScreen.Y;`
  - If `!_calmDragging` and `Math.Abs(dxTotal) + Math.Abs(dyTotal) > CalmDragThreshold` → `_calmDragging = true;`
  - If `_calmDragging`:
    - `Left = _calmPressLeft + dxTotal; Top = _calmPressTop + dyTotal;`
    - direction: `if (cur.X > _calmLastX + 0.5) Play("runRight"); else if (cur.X < _calmLastX - 0.5) Play("runLeft");`
      (only call Play when the animation name changes — track `_lastRoamAnimation`-style local, or compare `_petAnimator.StateName`).
    - `_calmLastX = cur.X;`
- `OnMouseLeftButtonUp` (new handler): if `_calmPressed`:
  - `ReleaseMouseCapture();`
  - if `_calmDragging` → resume calm: `StartBehaviorMode();`
  - else (click, no drag) → `PlayJumpThenIdle();`
  - `_calmPressed = false; _calmDragging = false;`

`PlayJumpThenIdle()`:
```csharp
_petAnimator?.Play("jumping");
_calmJumpTimer.Stop();
// jumping is loop=false; give it its run length then resume calm.
_calmJumpTimer.Interval = JumpDuration();  // frames/fps of the jumping state, fallback 800ms
_calmJumpTimer.Start();   // Tick: stop timer + StartBehaviorMode()
```
`JumpDuration()` reads the sprite's `jumping` state (`Frames`/`Fps`) from
`_spritePet?.Definition`; fallback `TimeSpan.FromMilliseconds(800)`.

Wire `_calmJumpTimer.Tick` once in the ctor: stop + if Calm-interactive
`StartBehaviorMode()`.

Persisting position: dragging sets `Left/Top`; existing exit-save already
persists `PetLeft/PetTop`, so no change there.

**Non-Calm modes**: `OnMouseLeftButtonDown` keeps its current body
(double-click dismiss, then `DragMove()` with `StartBehaviorMode()` in the
finally). Implement by branching at the top: if Calm-interactive run the manual
path above; else run the existing path. `OnMouseMove`/`OnMouseLeftButtonUp`
early-return when `!_calmPressed`.

**Periodic calm animation** — `OnCalmTimerTick`:
```csharp
_petAnimator?.Play(_random.NextDouble() < 0.25 ? "review" : "idle");
```
(was `"waiting"` → now `"review"`).

XAML: the root `Grid` needs `MouseEnter`, `MouseLeave`, `MouseMove`,
`MouseLeftButtonUp` handlers added (it already has `MouseLeftButtonDown`).

### B. Settings side: left first — `SettingsPlacement`

Swap the preference in `SettingsPlacement.Compute`:
```csharp
double leftX = petLeft - Gap - settingsWidth;
double left = leftX >= waLeft
    ? leftX
    : petLeft + petWidth + Gap;     // fallback: right of pet
// clamp unchanged
```
Update `SettingsPlacementTests` to the left-first expectations.

### C. Same monitor — `MonitorWorkArea` (new) + MainWindow

`SystemParameters.WorkArea` is the **primary** monitor only. Add
`src/HyperPet.App/Views/MonitorWorkArea.cs`:

```csharp
public readonly record struct WorkArea(double Left, double Top, double Right, double Bottom);

public static class MonitorWorkArea
{
    // Returns the work area (DIP) of the monitor containing the DIP point.
    public static WorkArea ForPoint(double dipX, double dipY, double dpiScaleX, double dpiScaleY);
}
```
Implementation: convert the DIP point to physical px (`* dpiScale`),
`MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST)`, `GetMonitorInfo` →
`rcWork` (px), convert back to DIP (`/ dpiScale`), return as `WorkArea`.

`MainWindow.OnSettingsClick`: replace `SystemParameters.WorkArea` usage with:
```csharp
DpiScale dpi = VisualTreeHelper.GetDpi(this);
WorkArea wa = MonitorWorkArea.ForPoint(
    Left + GetWindowWidth() / 2, Top + GetWindowHeight() / 2,
    dpi.DpiScaleX, dpi.DpiScaleY);
Placement placement = SettingsPlacement.Compute(
    Left, Top, GetWindowWidth(), GetWindowHeight(),
    settingsWindow.Width, settingsWindow.Height,
    wa.Left, wa.Top, wa.Right, wa.Bottom);
```
The pet's monitor is found from the pet's center point; Settings is clamped to
that monitor's work area, so it stays on the same screen as the pet.

## Error handling

- `MonitorFromPoint`/`GetMonitorInfo` failure → fall back to
  `SystemParameters.WorkArea` (primary) so Settings still opens.
- Jump timer: if mode changed away from Calm before it ticks, the tick's
  Calm-interactive guard prevents overriding Running/Desktop.
- Mouse capture released in `OnMouseLeftButtonUp`; if capture is lost
  (`LostMouseCapture`), treat as release — add a `LostMouseCapture` handler
  that mirrors the up-path cleanup (`_calmPressed=false`, resume calm).

## Testing

- `SettingsPlacementTests`: rewrite for left-first — left when room; right
  fallback when left underflows; clamps on both axes; exact-fit-left stays
  left.
- Calm interactions (input/animation) and `MonitorWorkArea` (Win32) → manual
  verification.

## Manual verification

- Calm mode: hover → waiting; quick click → one jump then idle; drag left →
  runLeft and the pet follows the cursor; drag right → runRight; release →
  idle; leave it alone → occasional `review` animation.
- Switch to Running/Desktop: dragging still repositions as before; no jump on
  click; double-click still dismisses a bubble.
- Settings opens to the left of the pet; pet near the left edge → opens right.
- Move the pet to a second monitor, open Settings → it appears on that monitor.

## Files

- Modify: `src/HyperPet.App/MainWindow.xaml` (Grid mouse handlers)
- Modify: `src/HyperPet.App/MainWindow.xaml.cs` (calm interaction state machine, calm tick → review, settings monitor)
- Modify: `src/HyperPet.App/Views/SettingsPlacement.cs` (left-first)
- Modify: `tests/HyperPet.App.Tests/Views/SettingsPlacementTests.cs` (left-first)
- Create: `src/HyperPet.App/Views/MonitorWorkArea.cs`

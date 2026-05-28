# Desktop (Window-Perch) Behavior Mode

Date: 2026-05-28
Status: Approved (pending spec review)
Author: claude (with user)

## Problem

HyperPet has two behavior modes: Calm (stationary idle/waiting) and Running
(mechanical left-right patrol along the bottom). We want a third, livelier
mode where the pet treats open windows as terrain — walking along window top
edges and hopping between them — so it feels like it lives on your desktop.

## Goal

Add a **Desktop** behavior mode in which the pet walks along the top edges of
open windows (plus the taskbar and the screen top), and jumps between these
"ledges" with a parabolic arc. The pet rides a window's edge when the window
moves, and recovers gracefully when a window it was on disappears.

## Decisions (from brainstorming)

1. **Core loop:** Walk a ledge, then hop to another nearby ledge. Continuous roaming.
2. **Perch surfaces:** Visible top-level app windows + the taskbar top edge + the screen (work-area) top. Taskbar + screen-top guarantee there is always somewhere to walk.
3. **Dynamic windows:** Ride the current window's edge as it moves/resizes; if it closes or minimizes, hop/fall to the nearest remaining ledge.
4. **Hop style:** Parabolic jump arc using the existing `jumping` sprite state.

## Non-goals

- Per-monitor screen-top ledges. v1 uses one screen-top ledge across the primary work area. Windows on other monitors are still individually perchable.
- Walking along window *sides* or *bottoms*. Top edges only.
- Pixel-perfect collision with overlapping windows (z-order). v1 treats every qualifying window's top edge as an independent ledge regardless of occlusion.
- Replacing Calm or Running. Desktop is additive.

## Enum migration (must preserve saved settings)

`PetBehaviorMode` is currently `{ Calm, Desktop }` and is serialized
**numerically** by System.Text.Json (no `JsonStringEnumConverter` is applied;
the corrupt-value test stores `999`). Saved files therefore contain
`"PetBehaviorMode": 0` (Calm) or `1` (the current patrol).

Change the enum to:

```csharp
public enum PetBehaviorMode
{
    Calm = 0,
    Running = 1,
    Desktop = 2,
}
```

- Old persisted `1` (previously named `Desktop`, behaviorally the patrol) now
  binds to `Running = 1` — the patrol — which is exactly correct.
- New `Desktop = 2` is the window-perch mode.
- Do **not** add `JsonStringEnumConverter` to this enum; numeric serialization
  is what makes the rename safe.

The Settings combo maps SelectedIndex 0/1/2 → `Calm` / `Running` / `Desktop`.

## Architecture

The window-perch behavior is isolated in a controller that consumes an
abstraction over "where can the pet stand," so the geometry/state logic is
unit-testable without Win32.

### `Ledge` (record, `HyperPet.App.Pets.Roaming`)

A horizontal surface the pet can stand on.

```csharp
public sealed record Ledge(IntPtr? Hwnd, double Left, double Right, double TopY)
{
    public double Width => Right - Left;
}
```

- `Hwnd` is the source window handle, or `null` for synthetic ledges
  (taskbar, screen-top) that don't need rect refresh.
- `TopY` is the surface's screen Y. The pet's window Top is placed at
  `TopY - petHeight` so it stands *on* the edge.

### `ILedgeProvider` (`HyperPet.App.Pets.Roaming`)

```csharp
public interface ILedgeProvider
{
    /// All current perch surfaces: qualifying windows + taskbar + screen-top.
    IReadOnlyList<Ledge> GetLedges();

    /// Re-reads a window-backed ledge's current rect. Returns null if the
    /// window is gone, minimized, or hidden. Synthetic ledges return themselves.
    Ledge? TryRefresh(Ledge ledge);
}
```

### `WindowLedgeProvider : ILedgeProvider` (`HyperPet.App.Pets.Roaming`)

Win32 implementation.

- `GetLedges()`:
  - `EnumWindows`, keep windows that are: visible (`IsWindowVisible`), not
    minimized (`!IsIconic`), have a caption (`WS_CAPTION`), are not tool
    windows (`!WS_EX_TOOLWINDOW`), are not DWM-cloaked
    (`DwmGetWindowAttribute` `DWMWA_CLOAKED` == 0), are not HyperPet's own
    window (excluded HWND), and have width ≥ a small minimum (e.g. 80px).
  - For each, make `Ledge(hwnd, rect.Left, rect.Right, rect.Top)`.
  - Add the taskbar: `FindWindow("Shell_TrayWnd", null)` → `GetWindowRect` →
    `Ledge(null, rect.Left, rect.Right, rect.Top)`.
  - Add screen-top: `Ledge(null, workArea.Left, workArea.Right, workArea.Top)`
    using `SystemParameters.WorkArea`.
- `TryRefresh(ledge)`:
  - If `ledge.Hwnd is null` → return `ledge` (synthetic, static).
  - Else if the window fails the qualify checks (closed/minimized/hidden) →
    return `null`.
  - Else return a new `Ledge` with the window's current rect.
- The MainWindow HWND to exclude is passed into the provider's constructor.

Reuse the Win32 signatures already present in
`InAppNotificationWatcher` (EnumWindows, IsWindowVisible, GetWindowRect,
GetWindowThreadProcessId, GetWindowLongPtr) — duplicate the minimal P/Invokes
needed here rather than coupling the two classes. Add `IsIconic`,
`FindWindow`, and `DwmGetWindowAttribute`.

### `DesktopRoamController` (`HyperPet.App.Pets.Roaming`)

Pure state machine. Owns the pet's logical position and decides movement each
tick. Knows nothing about WPF — it reports desired position + which animation
to play, and MainWindow applies it.

```csharp
public enum RoamPhase { Walking, Jumping }

public sealed class DesktopRoamController
{
    public DesktopRoamController(ILedgeProvider provider, Random random);

    // Pet footprint, set by MainWindow from the sprite size.
    public double PetWidth { get; set; }
    public double PetHeight { get; set; }
    public int WalkSpeed { get; set; } = 2;   // px per tick (from RunningSpeed)

    // Current desired top-left of the pet window.
    public double X { get; private set; }
    public double Y { get; private set; }

    // Which sprite to show: "runRight" | "runLeft" | "jumping".
    public string CurrentAnimation { get; private set; }

    /// Choose a starting ledge near the pet's current position and begin walking.
    public void Start(double currentX, double currentY);

    /// Advance one tick. Updates X/Y and CurrentAnimation.
    public void Tick();
}
```

Behavior:

- **Start:** snapshot ledges; pick the ledge whose top is nearest the pet's
  current Y and whose X-range is closest; place the pet on it; face a random
  direction; phase = Walking.
- **Walking tick:**
  - `TryRefresh` the current ledge. If `null` → begin a recovery jump to the
    nearest available ledge (see Jumping). 
  - Otherwise sync `Y = ledge.TopY - PetHeight` (ride vertical moves) and
    clamp X into `[ledge.Left, ledge.Right - PetWidth]` (ride horizontal
    resize/move).
  - Step `X += direction * WalkSpeed`. `CurrentAnimation` = runRight/runLeft.
  - If the next step would pass the ledge end, pick a target ledge and switch
    to Jumping. Target selection: from `GetLedges()`, exclude the current
    ledge, prefer ledges within a horizontal reach window ahead in the facing
    direction; if none, pick the nearest by Euclidean distance between edge
    endpoints; random tie-break. If the only ledge is the current one, flip
    direction and keep walking.
- **Jumping:** interpolate over a fixed tick count `JumpTicks` (e.g. 18):
  - `t` from 0→1. `X = lerp(startX, endX, t)`.
  - `Y = lerp(startY, endY, t) - JumpArcHeight * sin(pi * t)` (parabolic lift).
  - `CurrentAnimation = "jumping"`.
  - On `t >= 1`: land — set current ledge = target, `Y = ledge.TopY - PetHeight`,
    face toward the side it landed on, phase = Walking.
- **Recovery jump:** same Jumping mechanics, target = nearest valid ledge to
  the pet's current position; if `GetLedges()` is somehow empty (shouldn't be,
  taskbar+screen-top always present), hold position and retry next tick.

Determinism for tests: all randomness goes through the injected `Random`.

### MainWindow integration (`MainWindow.xaml.cs`)

- Construct a `WindowLedgeProvider` (needs MainWindow HWND via
  `new WindowInteropHelper(this).Handle`, available after `Loaded`) and a
  `DesktopRoamController`. Build them lazily on first entry to Desktop mode
  (HWND is valid by then) or in the `Loaded` handler.
- Rename the existing `StartDesktopMode()` to `StartRunningMode()` (it is the
  patrol). Add `StartPerchMode()` that initializes the controller
  (`PetWidth/PetHeight` from `PetImage`, `WalkSpeed` from
  `_settings.RunningSpeed`), calls `controller.Start(Left, Top)`, and starts
  the movement timer.
- `StartBehaviorMode()` switches:
  ```csharp
  switch (_settings.PetBehaviorMode)
  {
      case PetBehaviorMode.Running: StartRunningMode(); break;
      case PetBehaviorMode.Desktop: StartPerchMode(); break;
      default:                      StartCalmMode();   break;
  }
  ```
- The movement timer tick: if mode is Running → existing patrol tick; if
  Desktop → `controller.Tick(); Left = controller.X; Top = controller.Y;`
  and `_petAnimator?.Play(controller.CurrentAnimation)` only when the
  animation name changes (avoid restarting the sprite every 33ms).
- Existing interrupts already funnel through `StartBehaviorMode()` (alert
  show/dismiss, drag start/end), so Desktop mode resumes automatically. On
  resume, `controller.Start(Left, Top)` re-snaps to the nearest ledge.

### Settings UI

- `SettingsWindow.xaml`: add a third `ComboBoxItem` "Desktop" after "Running".
- `SettingsWindow.xaml.cs`:
  - Load: `PetBehaviorComboBox.SelectedIndex = (int)settings.PetBehaviorMode;`
    (Calm=0, Running=1, Desktop=2 line up with item order).
  - Commit: `requestedPetBehaviorMode = (PetBehaviorMode)PetBehaviorComboBox.SelectedIndex;`
    replacing the current `== 1 ? Desktop : Calm` logic.
  - Default button: reset to index 0 (Calm) — unchanged intent.

## Error handling

- Window vanishes mid-walk → recovery jump (Decision 3).
- Empty ledge set → impossible by Decision 2, but controller holds position
  and retries rather than throwing.
- HWND not yet available → defer controller creation until Loaded / first
  Desktop entry.
- Sprite missing (`_petAnimator is null`) → Desktop mode does nothing (guard
  like the other modes).

## Testing

- **`DesktopRoamControllerTests`** with a `FakeLedgeProvider` and a seeded
  `Random`:
  - Start places the pet on the nearest ledge (Y = TopY - PetHeight).
  - Walking advances X by WalkSpeed in the facing direction.
  - Reaching a ledge end transitions to Jumping and selects a target.
  - Jump interpolation: midpoint X is between start/end and Y is lifted above
    the straight-line midpoint; on completion the pet lands on the target
    ledge (Y synced).
  - Ledge horizontal resize: pet X is clamped within the refreshed bounds.
  - Vanished current ledge (provider returns null on refresh) triggers a
    recovery jump toward a remaining ledge.
  - Single-ledge world: pet flips direction at the end instead of jumping.
- **Settings round-trip**: `PetBehaviorMode.Desktop` persists (numeric 2) and
  reloads. Extend `SettingsStoreTests`.
- Win32 `WindowLedgeProvider` is not unit-tested (needs a live desktop);
  covered by manual verification.

## Manual verification

- Open several windows; switch pet to Desktop mode. Pet walks a window top,
  jumps to another, repeats. Drag a window the pet is on — pet rides it. Close
  that window — pet hops to a neighbor. Minimize everything — pet walks the
  taskbar / screen top. Switch back to Calm/Running — behavior changes
  immediately. Trigger a notification — pet waves, then resumes roaming.

## Files

- Create: `src/HyperPet.App/Pets/Roaming/Ledge.cs`
- Create: `src/HyperPet.App/Pets/Roaming/ILedgeProvider.cs`
- Create: `src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs`
- Create: `src/HyperPet.App/Pets/Roaming/DesktopRoamController.cs`
- Create: `tests/HyperPet.App.Tests/Pets/Roaming/DesktopRoamControllerTests.cs`
  (with `FakeLedgeProvider`)
- Modify: `src/HyperPet.Core/Pets/PetBehaviorMode.cs` (rename + add member)
- Modify: `src/HyperPet.App/MainWindow.xaml.cs` (mode switch, controller drive,
  rename StartDesktopMode → StartRunningMode)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml` (combo item)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs` (index↔enum mapping)
- Modify: `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`
  (Desktop persists)
- Check usages of `PetBehaviorMode.Desktop` across the solution after rename
  (PetController, tests) and update to `Running` where they meant the patrol.

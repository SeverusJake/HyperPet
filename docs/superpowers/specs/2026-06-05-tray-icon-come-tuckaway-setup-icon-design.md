# Tray Icon, Come / Tuck Away Behaviors, and Setup Icon — Design

**Date:** 2026-06-05
**Status:** Approved (pending spec review)

## Summary

Three related additions to HyperPet (WPF + .NET 8, Velopack updater):

1. **System tray icon** while the app runs, with a right-click menu of exactly four
   items: **Come · Settings · Check for update · Tuck Away**. Double-click = Settings.
2. **Two new pet behaviors** triggered from the tray:
   - **Come** — pet runs to the center of the cursor's monitor, jumps on arrival,
     then waves (looping) until the user hovers it, then returns to normal behavior.
   - **Tuck Away** — pet plays the `failed` animation, then the app quits.
3. **Setup.exe icon** — the Velopack `Setup.exe` currently has a generic icon; set it
   via `vpk pack --icon`, and commit a `pack.ps1` so the release command is repeatable.

## Context

- Pet animation states (present in every pet, e.g. `miku-kimono`): `idle`, `runRight`,
  `runLeft`, `waving`, `jumping`, `failed`, `waiting`, `running`, `review`. All states
  the new behaviors need already exist.
- `PetAnimator` (`src/HyperPet.App/Pets/PetAnimator.cs`) plays states. Non-looping
  states stop their timer when the last frame is reached (in `AdvanceFrame`, the
  `!_state.Loop` branches at the Forward/Reverse/PingPong cases). There is **no**
  completion callback today.
- `DesktopRoamController` (`src/HyperPet.App/Pets/Roaming/DesktopRoamController.cs`) is a
  **pure**, WPF-agnostic state machine: it computes desired `X`/`Y` and
  `CurrentAnimation` each `Tick()`; MainWindow applies them. It has a unit test. The new
  movement controller mirrors this pattern.
- `MainWindow` drives behavior via `_movementTimer` (33 ms). Modes: Running, Desktop
  (perch/roam), Calm. `StartBehaviorMode()` dispatches by `_settings.PetBehaviorMode`.
- Hover lifecycle: `OnGridMouseEnter` (MainWindow.xaml.cs:535) enforces a 2 s minimum of
  `waiting`, then finishes the cycle before returning to idle. The Come behavior must
  bypass this and route hover to "resume normal behavior".
- Multi-monitor: the codebase already targets the pet's / a specific monitor
  (`MonitorWorkArea.ForPoint`, used at MainWindow.xaml.cs:726). "Center of the screen"
  resolves to the **cursor's** monitor (decision below).
- Settings dialog is opened from the pet context menu via `OnSettingsClick`
  (MainWindow.xaml.cs:704). Update flow: `_updateService.CheckAsync()` →
  `PromptAndApplyUpdateAsync(info)` (MainWindow.xaml.cs:987), already used by launch
  auto-check and the About tab.
- Velopack packaging is run manually; the command lives only in a plan doc:
  `vpk pack --packId HyperPet --packVersion X --packDir publish/velopack/X --mainExe HyperPet.exe`.

## Decisions

- **Come target monitor:** cursor's monitor. The tray is clicked near the cursor, so the
  pet comes to the screen the user is looking at.
- **Tray library:** WinForms `NotifyIcon` (no new NuGet; add `<UseWindowsForms>true</UseWindowsForms>`).
- **Double-click tray:** Open Settings (Windows convention).
- **Menu:** exactly four items — Come, Settings, Check for update, Tuck Away. The earlier
  "Show / Hide pet" and standalone "Quit" items are intentionally dropped; Tuck Away is
  the graceful exit.
- **Animation chaining:** add a `Completed` event to `PetAnimator` rather than scheduling
  duration timers. Chaining off the animation's natural end ("play `failed`, *then*
  quit") is exactly what the existing calm duration-timers do **not** provide.

## Components

### 1. `PetAnimator.Completed` event (new primitive)

Add to `PetAnimator`:

```csharp
/// <summary>Raised once when a non-looping state reaches its final frame.
/// Argument is the finished state name. Not raised for looping states.</summary>
public event Action<string>? Completed;
```

Fire it at the three points in `AdvanceFrame` where a non-loop state currently calls
`_timer.Stop()` (Forward, Reverse, PingPong end branches). Capture the state name before
firing. Fire exactly once per non-loop playthrough; never for looping states. Guard
against re-entrancy if a handler calls `Play(...)` (read/clear `_state` ordering so a new
`Play` inside the handler is not clobbered).

Both new behaviors subscribe in MainWindow. Subscription is set up once (when the animator
is created/assigned) and routes to the active sequence handler.

### 2. `SummonController` (pure movement state machine)

New file `src/HyperPet.App/Pets/Roaming/SummonController.cs`, mirroring
`DesktopRoamController`'s shape (pure, no WPF, constructor-injected `Random` not needed —
movement is deterministic toward a fixed target).

Responsibilities — **movement only**:

- Inputs: `Start(double currentX, double currentY, double targetX, double targetY)`;
  property `WalkSpeed`.
- `Tick()` steps `X`/`Y` from the current position toward the target by up to `WalkSpeed`
  pixels along the straight-line vector (both axes move; vertical centering included).
- `CurrentAnimation` = `runRight` when target is to the right, `runLeft` when to the left
  (decided by horizontal sign of the remaining delta; ties keep last facing).
- `Arrived` (bool) becomes true when the remaining distance is within one `WalkSpeed`
  step; on that tick, snap `X`/`Y` exactly to the target.

It does **not** know about animation sequencing, hover, jumping, or waving — MainWindow
owns those.

### 3. MainWindow — "Come" sequence

State: add `private bool _summoned;` and `private SummonController? _summonController;`.

Flow:

1. Tray "Come" invokes a public `MainWindow.Summon()` (marshalled to the UI thread).
2. `Summon()`:
   - Capture cursor position (`System.Windows.Forms.Cursor.Position`, or Win32
     `GetCursorPos`), convert to WPF/DIP coordinates.
   - Resolve the cursor's monitor work area via `MonitorWorkArea.ForPoint`.
   - Target = work-area center minus half the pet size:
     `targetX = wa.Left + (wa.Width - petW) / 2`, `targetY = wa.Top + (wa.Height - petH) / 2`.
   - `StopBehaviorTimers()`; set `_summoned = true`.
   - Create/seed `_summonController.Start(Left, Top, targetX, targetY)` with
     `WalkSpeed = Clamp(_settings.RunningSpeed, 1, 20)`.
   - Drive it on `_movementTimer` (33 ms). Each tick applies `Left = ctrl.X; Top = ctrl.Y`
     and plays `CurrentAnimation` on change. **Do not** call `ClampToWorkArea` during the
     summon (it would pin the pet back to the bottom edge and defeat vertical centering).
3. On `ctrl.Arrived`: stop the movement timer; `_petAnimator.Play("jumping")` (in-place —
   no positional ledge arc; position stays at target).
4. On `Completed("jumping")` while summoned: `_petAnimator.Play("waving")` (loops).
5. `OnGridMouseEnter`: if `_summoned`, clear `_summoned` and call `StartBehaviorMode()`
   (resume the configured mode) and **return early**, bypassing the normal 2 s `waiting`
   hover lifecycle.

The `_movementTimer` Tick handler must branch on summon state so it ticks the
`SummonController` (not the roam/running logic) while a summon is active.

### 4. MainWindow — "Tuck Away" sequence

1. Tray "Tuck Away" invokes public `MainWindow.TuckAway()` (UI thread).
2. `StopBehaviorTimers()`; clear `_summoned`; `_petAnimator.Play("failed")`.
3. On `Completed("failed")`: `Application.Current.Shutdown()`.

Guard: if the animator is null/unavailable, shut down immediately.

### 5. Tray icon — `TrayIcon.cs` (WinForms `NotifyIcon`)

- `HyperPet.App.csproj`: add `<UseWindowsForms>true</UseWindowsForms>` (keep `UseWPF`).
- New `src/HyperPet.App/TrayIcon.cs`, `IDisposable`. Owns a `NotifyIcon` and a
  `ContextMenuStrip`. Constructor takes the four callbacks (`onCome`, `onSettings`,
  `onCheckForUpdate`, `onTuckAway`) and a tooltip/icon.
- Icon: load `HyperPet.ico` (embedded resource / app dir). Tooltip = `HyperPet vX.Y.Z`
  from `AppVersion.DisplayString`.
- Menu (top → bottom): **Come**, **Settings**, **Check for update**, **Tuck Away**.
- `DoubleClick` on the icon → Settings.
- Lifecycle: created in `App.OnStartup` after MainWindow exists; `Dispose()` in
  `App.OnExit` (a NotifyIcon left undisposed leaves a ghost icon until hover). Single
  instance.
- Threading: NotifyIcon events fire on the WPF UI thread (shared message loop), so
  callbacks may touch windows directly; still marshal via `Dispatcher` defensively.

MainWindow refactor:

- Extract the body of `OnSettingsClick` into a public `OpenSettings()`; both the pet
  context menu and the tray call it.
- Add public `CheckForUpdateFromTray()`: `await _updateService.CheckAsync()`; if info →
  `PromptAndApplyUpdateAsync`; if none → tray balloon "HyperPet is up to date"; on error
  → balloon/message (manual checks surface errors, unlike the silent launch check). No-op
  if `_updateService` is null/unsupported (dev runs).

### 6. Setup.exe icon + `pack.ps1`

- Add `--icon "src/HyperPet.App/Assets/HyperPet.ico"` to the `vpk pack` invocation. This
  sets the **Setup.exe** icon; the installed app's exe and shortcut icons already come
  from `<ApplicationIcon>` and are unchanged.
- Commit `pack.ps1` at repo root capturing the full, parameterized command, e.g.:

  ```powershell
  param([Parameter(Mandatory)][string]$Version)
  vpk pack `
    --packId HyperPet `
    --packVersion $Version `
    --packDir "publish/velopack/$Version" `
    --mainExe HyperPet.exe `
    --icon "src/HyperPet.App/Assets/HyperPet.ico"
  ```

  (Match the actual existing pack arguments when implementing; the `--icon` flag is the
  substantive addition.)

## Data flow

```
Tray menu click ──▶ MainWindow public method (Summon / OpenSettings /
                    CheckForUpdateFromTray / TuckAway)  [UI thread]
Summon ──▶ SummonController.Tick (via _movementTimer) ──▶ Left/Top + Play(anim)
        ──▶ Arrived ──▶ Play("jumping")
        ──▶ PetAnimator.Completed("jumping") ──▶ Play("waving") [loop]
        ──▶ OnGridMouseEnter (_summoned) ──▶ StartBehaviorMode()
TuckAway ──▶ Play("failed") ──▶ PetAnimator.Completed("failed") ──▶ Shutdown()
```

## Error handling / edge cases

- **Already summoned, Come again:** restart the summon from the current position toward a
  freshly computed target (re-seed controller). Harmless.
- **Come while an alert is active:** mirror existing guards — `StartBehaviorMode` bails on
  `_alertActive`; Summon should likewise no-op (or defer) while `_alertActive`.
- **Hover during the walk (before arrival):** treat like the arrived case — `_summoned`
  hover resumes normal behavior; clear the summon cleanly (stop movement timer).
- **Tuck Away with null animator:** shut down immediately.
- **Update check on dev run / offline:** `_updateService` null or `!IsSupported` →
  balloon "Updates not available" / silent no-op; network error → error balloon.
- **NotifyIcon disposal:** must dispose on exit and on `Shutdown()` to avoid a ghost tray
  icon.

## Testing

- **Unit — `SummonController`:** reaches the target within `WalkSpeed`; snaps exactly on
  arrival; `CurrentAnimation` is `runRight` for a right-of target and `runLeft` for a
  left-of target; `Arrived` flips true at the end.
- **Unit — `PetAnimator.Completed`:** fires exactly once for a non-loop state at the final
  frame, with the correct state name; never fires for a looping state; calling `Play`
  inside the handler is safe.
- **Manual:** tray icon appears on launch and is removed on Tuck Away/quit; each menu item
  works; double-click opens Settings; Come centers on the cursor's monitor, jumps, waves,
  and resumes on hover; Setup.exe shows the HyperPet icon.

## Out of scope

- Show/Hide pet toggle and a standalone Quit item (superseded by Tuck Away).
- Changing the existing pet context menu items (Version, Pause Alerts, Settings, Quit
  remain as-is).
- Per-pet custom Come/Tuck Away animations beyond the existing shared states.

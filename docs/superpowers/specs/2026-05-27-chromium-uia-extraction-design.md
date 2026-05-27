# Chromium UIA Extraction for In-App Notifications

Date: 2026-05-27
Status: Approved
Author: claude (with user)

## Problem

`InAppNotificationWatcher` extracts notification text via the UI Automation
(UIA) `Name` property walk on each detected popup HWND. For Chromium /
Electron apps like Zalo, this walk only returns wrapper element names such
as `Zalo`, `Chrome Legacy Window`, and other host-element junk that v0.3.5
already filters out. The actual sender and message text is rendered by the
Chromium compositor and is only exposed in the UIA tree when an
accessibility client has subscribed deeply enough to flip Chromium into
`AXMode::kWebContents`.

Observed symptom (v0.3.5):

- Bubble shows `Zalo / New message` (fallback) instead of
  `Phuc cool bro / Ha`.
- Second message on the same notification HWND is not re-emitted because
  the content hash of the empty extraction never changes.

## Goal

Show the real sender + message text in the pet's bubble for Zalo and
other Chromium-based in-app notifications, with the first message after
process discovery already showing the correct content.

## Non-goals

- OCR fallback. Not pursuing — adds dependencies and ~50-150ms per popup.
- Reverse-engineering Zalo's internal APIs or WebSocket protocol.
- Detection of in-app notifications from non-Chromium native apps beyond
  what the current watcher already handles.

## Approach

Wake the Chromium UIA tree by subscribing a no-op UIA event handler
scoped to each watched process's root window. Chromium detects the
listener and enables `AXMode::kWebContents`. Pre-activate when the
watcher resolves a process's PID for the first time so the accessibility
tree is populated before the first popup scan.

## Components

### New: `HyperPet.App.Notifications.ChromiumAccessibilityActivator`

File: `src/HyperPet.App/Notifications/ChromiumAccessibilityActivator.cs`.

Responsibilities:

- Track which process IDs have already had their UIA tree woken.
- Subscribe a no-op UIA property-changed handler on the process's main
  window, scoped to the full subtree, on first activation.
- Allow pruning entries whose process is no longer alive (Zalo restart).
- Remove all UIA event handlers on dispose to prevent leaks.

Public surface:

```csharp
public sealed class ChromiumAccessibilityActivator : IDisposable
{
    public ChromiumAccessibilityActivator(HyperPetLogger? logger = null);

    /// Subscribes a UIA listener on the process's main window if it has
    /// not been activated yet. No-op if the process has no main window
    /// HWND (e.g., the app has not finished starting). Safe to call on
    /// every scan.
    public void EnsureActivated(int pid);

    /// Drops tracking entries whose pid is not in <paramref name="livePids"/>.
    /// Called from the watcher after PID resolution.
    public void PruneStale(IEnumerable<int> livePids);

    public void Dispose();
}
```

Implementation notes:

- Resolve main HWND via `Process.GetProcessById(pid).MainWindowHandle`.
- If the HWND is `IntPtr.Zero`, skip — process may not be done initializing.
- Use `AutomationElement.FromHandle(mainHwnd)`. If null, skip.
- Subscribe handler:
  ```csharp
  Automation.AddAutomationPropertyChangedEventHandler(
      element,
      TreeScope.Subtree,
      _noOpHandler,
      AutomationElement.NameProperty);
  ```
- `_noOpHandler` is `static void NoOp(object sender, AutomationPropertyChangedEventArgs e) { }` — discards all events.
- Wrap activation in `try/catch (Exception)`; log via `HyperPetLogger.Warn`. Never let UIA errors crash the watcher.
- `Dispose` calls `Automation.RemoveAllEventHandlers()` and clears the activated-pid set.

### Modified: `InAppNotificationWatcher`

File: `src/HyperPet.App/Notifications/InAppNotificationWatcher.cs`.

Changes:

1. Add field:
   ```csharp
   private readonly ChromiumAccessibilityActivator _activator;
   ```
   Initialize in the constructor: `_activator = new ChromiumAccessibilityActivator(logger);`.

2. `ResolveWatchedPids()`:
   - After populating `result`, call `_activator.EnsureActivated(pid)` for each pid in `result.Keys`.
   - After the loop, call `_activator.PruneStale(result.Keys)`.

3. `Start()`:
   - Before `_timer.Start()`, call `ResolveWatchedPids()` once (discard result) so activation happens before the first scan. This eliminates the cold-start gap where the first Zalo message would emit before Chromium has populated its tree.

4. `Dispose()`:
   - Call `_activator.Dispose()` before `_seenHandles.Clear()`.

5. `ExtractText` is unchanged. It already filters junk names and the app name itself; with the woken tree it now sees real content.

## Data flow

```
Watcher.Start()
  -> ResolveWatchedPids()
       -> Process.GetProcessesByName("Zalo")  -> pids: { 1234 }
       -> activator.EnsureActivated(1234)
            -> Process.GetProcessById(1234).MainWindowHandle = 0xABCDE
            -> AutomationElement.FromHandle(0xABCDE)
            -> Automation.AddAutomationPropertyChangedEventHandler(...)
            -> _activatedPids.Add(1234)
       -> activator.PruneStale({1234})
  -> _timer.Start()

Tick
  -> Scan()
       -> ResolveWatchedPids()  (re-activates new pids, prunes dead ones)
       -> EnumWindows + IsPopupCandidate + owner check
       -> ExtractText(hwnd, "Zalo")
            (UIA tree is now populated; CollectNames returns real names)
       -> content hash compared against _seenHandles[hwnd]
       -> EmitFor(...) if changed or new
```

## Edge cases

- **Process has no `MainWindowHandle` yet.** Skip activation; next scan re-tries. Tracked pid not added until activation succeeds.
- **Process restarts.** New pid. `PruneStale` drops the old entry on the next scan, and `EnsureActivated` re-subscribes for the new pid.
- **Process exits while pid still tracked.** `PruneStale` drops it next scan. The dangling UIA subscription is invalidated by the OS when the process dies; no manual unsubscribe needed.
- **Multiple Zalo processes (unlikely but possible).** Each pid activates independently.
- **Activation throws** (UIA subsystem failure, COM error). Caught, logged, skipped. Next scan retries — pid not added to set on failure.
- **`Automation.RemoveAllEventHandlers()` on dispose**. Affects only event handlers registered by this process — does not impact other UIA clients on the system.

## Risks

- **Chromium memory overhead.** Documented at ~5-15 MB per Chromium process when `kWebContents` accessibility is active. Acceptable for a desktop pet.
- **UIA event volume.** `TreeScope.Subtree` on a busy app may fire many property events. The handler is a no-op, so the cost is event dispatch only — measured negligible in similar tools.
- **UIA marshaling cost.** UIA event handlers can serialize calls on the UIA thread pool. No-op handler returns immediately. Safe.
- **Activation lag on the very first PID resolution.** Even with pre-activate at `Start()`, Chromium needs a short interval (~50-200ms) after subscription before its tree fully reflects content. In practice, the watcher's poll interval (1s+) and the fact that messages arrive after the user has been chatting for a while make this immaterial. If we observe a real cold-start miss, we can add a one-shot 250ms delay between activation and the first scan.

## Testing

- **Manual happy path**: Open Zalo with HyperPet running. Receive a message from another account. Bubble shows `Zalo / <sender name> / <message text>`. Send a second message from the same chat. Bubble re-emits with the new message.
- **Manual restart**: Kill Zalo, relaunch it. New pid gets activated. Receive a message. Bubble shows content correctly.
- **Manual no-Chromium**: Confirm non-Chromium watched apps (none currently configured by default; user-added in settings) are unaffected. Activation tries to subscribe but no harm if the app does not respond to it.
- **No unit tests.** UIA requires a real running process; unit-test scaffolding would be heavier than the production code. Add the manual checklist to the release PR.

## Out of scope

- Activating accessibility for apps that aren't being polled by the
  watcher (e.g., Edge tabs the user isn't waiting on).
- Falling back to OCR when UIA still returns empty.
- Detecting the difference between Chromium and non-Chromium watched
  apps before activating. We always try; failures are logged and ignored.

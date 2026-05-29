# Performance Reductions + Settings Placement

Date: 2026-05-29
Status: Approved (pending spec review)
Author: claude (with user)
Target release: v0.5.0

## Problem

The pet uses noticeable CPU and RAM. Three causes: redundant cross-process
UI-Automation walks in the in-app watcher, repeated full window enumeration
during Desktop roaming, and an unbounded retained-notification list that grows
over time. Separately, the Settings window opens centered over the pet,
covering it — it should open beside the pet.

## Goals

1. Cut steady-state CPU from the in-app watcher.
2. Cut CPU from window enumeration during Desktop mode.
3. Stop RAM growth from retained WinRT notifications.
4. Open Settings to the right of the pet (fallback left), never covering it.
5. Upload version-stamped Setup.exe and Portable.zip with each release.

## Decisions (from brainstorming)

- Keep movement at 30Hz (33ms); do not lower framerate. Win comes from caching.
- Settings placement: right of pet, fallback left, clamped to the work area.
- Release artifacts: version-stamped Setup + Portable names.

## Non-goals

- Lowering the movement framerate.
- Making Chromium accessibility opt-in (kept always-on; its cost is one-time
  per process and removing it would regress Zalo text extraction).
- Reworking the transparent layered MainWindow.

## Changes

### 1. InAppNotificationWatcher — skip redundant UI-Automation walks

File: `src/HyperPet.App/Notifications/InAppNotificationWatcher.cs`.

Today `Scan()` calls `ExtractText(hwnd, appName)` (a UI-Automation control-tree
walk, cross-process COM) for every candidate window on every scan, then hashes
the text to dedupe. A static popup is therefore re-walked every interval.

Add a per-hwnd cache of the last window rect and extracted text:

```csharp
private readonly Dictionary<IntPtr, (RECT Rect, string Title, string Body)> _extractCache = new();
```

In the per-window scan body, after a window passes the candidate + owner
checks and `GetWindowRect` is read:
- If `_extractCache` has this hwnd AND the cached rect equals the current rect,
  reuse the cached `(title, body)` — skip `ExtractText`.
- Otherwise call `ExtractText`, store `(currentRect, title, body)` in the cache.

Then proceed with the existing content-hash + `_seenHandles` emit logic using
the (possibly cached) title/body. Prune `_extractCache` entries for handles no
longer live in the same place `_seenHandles` is pruned.

`IsPopupCandidate` already calls `GetWindowRect`; expose the rect to the scan
loop (or call `GetWindowRect` once in the loop and pass it down) so we have the
current rect for the cache comparison without a second call.

Default interval: change `HyperPetSettings.InAppNotificationPollIntervalSeconds`
default from `2` to `4`. Existing saved settings keep their value; only new
installs get 4. Update the corresponding default assertion in
`SettingsStoreTests`.

### 2. WindowLedgeProvider — cache enumeration with a short TTL

File: `src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs`.

`GetLedges()` runs `EnumWindows` + `DwmGetWindowAttribute` per window. Cache the
returned list and re-enumerate only when the cache is older than a TTL:

```csharp
private IReadOnlyList<Ledge>? _cache;
private DateTime _cacheUtc;
private static readonly TimeSpan CacheTtl = TimeSpan.FromMilliseconds(1000);
```

`GetLedges()`: if `_cache is not null` and `DateTime.UtcNow - _cacheUtc < CacheTtl`,
return `_cache`; else rebuild, store, stamp, return. Make the TTL/clock
injectable via an internal constructor so it can be unit-tested with a fake
clock:

```csharp
public WindowLedgeProvider(IntPtr selfHwnd) : this(selfHwnd, () => DateTime.UtcNow) { }
internal WindowLedgeProvider(IntPtr selfHwnd, Func<DateTime> clock) { ... }
```

`TryRefresh(ledge)` is unchanged (single-window, cheap) so the pet still rides
a moving window between enumerations.

Note: the enumeration itself is Win32 and not unit-tested; the **cache gate**
(returns cached within TTL, re-enumerates after) is the testable seam — but
since rebuilding calls EnumWindows, a pure unit test would still hit Win32.
Therefore extract the cache decision into a tiny pure helper that IS tested:

```csharp
internal static bool ShouldRebuild(DateTime nowUtc, DateTime cacheUtc, bool hasCache, TimeSpan ttl)
    => !hasCache || (nowUtc - cacheUtc) >= ttl;
```

`GetLedges` uses `ShouldRebuild(...)`. Unit-test `ShouldRebuild` (no cache →
true; within TTL → false; past TTL → true).

### 3. WindowsNotificationListener — bound the retained list

File: `src/HyperPet.Windows/Notifications/WindowsNotificationListener.cs`.

`retainedNotifications` (a `List<UserNotification>`) is appended to and never
trimmed, to keep COM objects alive and avoid the GC-finalizer CFG crash. Cap it
to the most recent N:

```csharp
private const int MaxRetained = 256;
// after adding new notifications:
if (retainedNotifications.Count > MaxRetained)
{
    retainedNotifications.RemoveRange(0, retainedNotifications.Count - MaxRetained);
}
```

Removing the oldest still lets them be collected later; the crash only occurred
for objects released on the finalizer thread immediately after a poll, so
keeping a rolling buffer of recent ones preserves the safety while bounding RAM.

The field is `private readonly List<UserNotification> retainedNotifications`.
It is appended at two sites, each already inside `lock (retainedNotifications)`:
`OnNotificationChanged` (`.Add(notification)`) and `GetActiveNotificationsAsync`
(`.AddRange(notifications)`). Add a private `TrimRetained()` that does the
`RemoveRange` cap above and call it inside both locks right after the append.

### 4. Settings placement beside the pet

New: `src/HyperPet.App/Views/SettingsPlacement.cs` — pure geometry, testable.

```csharp
public readonly record struct Placement(double Left, double Top);

public static class SettingsPlacement
{
    // Prefer right of the pet; if it overflows workArea.Right, place left of
    // the pet; clamp both axes into the work area.
    public static Placement Compute(
        double petLeft, double petTop, double petWidth, double petHeight,
        double settingsWidth, double settingsHeight,
        double waLeft, double waTop, double waRight, double waBottom)
    {
        const double Gap = 8;
        double rightX = petLeft + petWidth + Gap;
        double left = rightX + settingsWidth <= waRight
            ? rightX
            : petLeft - Gap - settingsWidth;     // fallback: left of pet

        double maxLeft = Math.Max(waLeft, waRight - settingsWidth);
        left = Math.Clamp(left, waLeft, maxLeft);

        double top = petTop;
        double maxTop = Math.Max(waTop, waBottom - settingsHeight);
        top = Math.Clamp(top, waTop, maxTop);

        return new Placement(left, top);
    }
}
```

`MainWindow.OnSettingsClick`: set `settingsWindow.WindowStartupLocation =
WindowStartupLocation.Manual;` then compute placement from the pet's `Left/Top`
+ `GetWindowWidth()/GetWindowHeight()`, the settings window's `Width/Height`
(500×640 from XAML), and `SystemParameters.WorkArea`; assign
`settingsWindow.Left/Top`; then `ShowDialog()`. Remove
`WindowStartupLocation="CenterOwner"` from `SettingsWindow.xaml` (Manual wins
when set in code, but drop the XAML value to avoid confusion).

### 5. Release: version-stamped Setup + Portable

Release process change only (documented in the plan). After `vpk pack`:
- Copy `Releases/HyperPet-win-Setup.exe` → `Releases/HyperPet-v0.5.0-win-Setup.exe`.
- Copy `Releases/HyperPet-win-Portable.zip` → `Releases/HyperPet-v0.5.0-win-Portable.zip`.
- `gh release create` uploads the **versioned** Setup + Portable, plus the
  unversioned `releases.win.json`, `assets.win.json`, `RELEASES`, and the
  `*.nupkg` files (the updater channel needs those exact names).

## Testing

- `SettingsPlacementTests`: right placement when room; fallback left when right
  overflows; clamp to work area on both axes; pet at far right; pet at far left.
- `WindowLedgeProviderShouldRebuildTests` (or co-located): no cache → rebuild;
  within TTL → no rebuild; at/after TTL → rebuild.
- `SettingsStoreTests`: in-app default is now 4 (update assertion);
  round-trip unchanged.
- Win32 paths (watcher cache behavior, ledge enumeration, retained-list cap)
  are verified manually.

## Manual verification

- CPU: with Zalo open and a static popup, confirm CPU is lower than before over
  a minute (Task Manager) — no continuous UI-Automation churn.
- Desktop mode roams normally; pet still rides a dragged window.
- Leave the app running a while; retained memory stops climbing.
- Open Settings: it appears to the right of the pet; move the pet to the far
  right of the screen and reopen — it appears to the left; never centered over
  the pet.
- Release assets show version-stamped Setup + Portable.

## Files

- Modify: `src/HyperPet.App/Notifications/InAppNotificationWatcher.cs`
- Modify: `src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs`
- Modify: `src/HyperPet.Windows/Notifications/WindowsNotificationListener.cs`
- Modify: `src/HyperPet.Core/Settings/HyperPetSettings.cs` (default 4)
- Create: `src/HyperPet.App/Views/SettingsPlacement.cs`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs` (placement on open)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml` (drop CenterOwner)
- Create: `tests/HyperPet.App.Tests/Views/SettingsPlacementTests.cs`
- Create: `tests/HyperPet.App.Tests/Pets/Roaming/WindowLedgeProviderShouldRebuildTests.cs`
- Modify: `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs` (default 4)

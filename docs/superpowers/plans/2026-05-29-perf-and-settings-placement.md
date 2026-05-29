# Performance + Settings Placement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut CPU (in-app UIA walks, window enumeration) and RAM (retained notifications), and open Settings beside the pet instead of over it.

**Architecture:** Per-hwnd rect cache skips redundant UI-Automation walks; a 1s TTL gate caches window enumeration; the retained WinRT list is capped to a rolling buffer; a pure `SettingsPlacement` computes a right/left position used when opening Settings. Pure helpers are unit-tested; Win32 paths are manual.

**Tech Stack:** C# .NET 8 WPF, xUnit.

**Spec:** `docs/superpowers/specs/2026-05-29-perf-and-settings-placement-design.md`

---

## File Structure

- Create: `src/HyperPet.App/Views/SettingsPlacement.cs` — pure geometry.
- Create: `tests/HyperPet.App.Tests/Views/SettingsPlacementTests.cs`
- Create: `tests/HyperPet.App.Tests/Pets/Roaming/WindowLedgeProviderShouldRebuildTests.cs`
- Modify: `src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs` — `ShouldRebuild` + TTL cache.
- Modify: `src/HyperPet.App/Notifications/InAppNotificationWatcher.cs` — rect cache.
- Modify: `src/HyperPet.Windows/Notifications/WindowsNotificationListener.cs` — cap retained list.
- Modify: `src/HyperPet.Core/Settings/HyperPetSettings.cs` — in-app default 4.
- Modify: `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs` — default 4 assertion.
- Modify: `src/HyperPet.App/MainWindow.xaml.cs` — place Settings on open.
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml` — drop CenterOwner.

---

### Task 1: SettingsPlacement (pure) + tests

**Files:**
- Create: `src/HyperPet.App/Views/SettingsPlacement.cs`
- Create: `tests/HyperPet.App.Tests/Views/SettingsPlacementTests.cs`

- [ ] **Step 1: Implement**

`src/HyperPet.App/Views/SettingsPlacement.cs`:

```csharp
namespace HyperPet.App.Views;

public readonly record struct Placement(double Left, double Top);

/// <summary>
/// Computes where to open the Settings window relative to the pet: to the
/// pet's right, falling back to its left when the right would overflow the
/// work area. Both axes are clamped into the work area.
/// </summary>
public static class SettingsPlacement
{
    private const double Gap = 8;

    public static Placement Compute(
        double petLeft, double petTop, double petWidth, double petHeight,
        double settingsWidth, double settingsHeight,
        double waLeft, double waTop, double waRight, double waBottom)
    {
        double rightX = petLeft + petWidth + Gap;
        double left = rightX + settingsWidth <= waRight
            ? rightX
            : petLeft - Gap - settingsWidth;

        double maxLeft = Math.Max(waLeft, waRight - settingsWidth);
        left = Math.Clamp(left, waLeft, maxLeft);

        double top = petTop;
        double maxTop = Math.Max(waTop, waBottom - settingsHeight);
        top = Math.Clamp(top, waTop, maxTop);

        return new Placement(left, top);
    }
}
```

- [ ] **Step 2: Tests**

`tests/HyperPet.App.Tests/Views/SettingsPlacementTests.cs`:

```csharp
using HyperPet.App.Views;
using Xunit;

namespace HyperPet.App.Tests.Views;

public class SettingsPlacementTests
{
    // Work area 0,0 .. 1920,1040. Pet 200x200. Settings 500x640.
    private const double WaL = 0, WaT = 0, WaR = 1920, WaB = 1040;

    [Fact]
    public void Compute_PlacesRightOfPet_WhenRoom()
    {
        var p = SettingsPlacement.Compute(300, 100, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(300 + 200 + 8, p.Left);   // right of pet
        Assert.Equal(100, p.Top);               // aligned to pet top
    }

    [Fact]
    public void Compute_FallsBackLeft_WhenRightOverflows()
    {
        // Pet near right edge: right placement would exceed WaR -> go left.
        var p = SettingsPlacement.Compute(1700, 100, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(1700 - 8 - 500, p.Left);   // left of pet
    }

    [Fact]
    public void Compute_ClampsLeftIntoWorkArea_WhenPetFarLeft()
    {
        // Pet at far left: left fallback would be negative -> clamp to WaL.
        // (Right also overflows because pet width pushes settings off only if
        // near right; here right fits, so this checks the clamp path via a
        // narrow work area.)
        var p = SettingsPlacement.Compute(10, 100, 200, 200, 500, 640, WaL, WaT, /*WaR*/ 600, WaB);
        // rightX = 10+200+8=218; 218+500=718 > 600 -> left = 10-8-500 = -498 -> clamp 0..(600-500=100)
        Assert.Equal(0, p.Left);
    }

    [Fact]
    public void Compute_ClampsTop_WhenPetNearBottom()
    {
        var p = SettingsPlacement.Compute(300, 1000, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(WaB - 640, p.Top);   // 1040-640 = 400
    }

    [Fact]
    public void Compute_RightExactFit_StaysRight()
    {
        // rightX + settingsWidth == waRight exactly -> still right (<=).
        // petLeft + petWidth + 8 + 500 == 1920  => petLeft = 1920-500-8-200 = 1212
        var p = SettingsPlacement.Compute(1212, 0, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(1212 + 200 + 8, p.Left);
    }
}
```

- [ ] **Step 3: Test**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj -c Release -nologo`
Expected: green (5 new placement tests + prior).

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/Views/SettingsPlacement.cs tests/HyperPet.App.Tests/Views/SettingsPlacementTests.cs
git commit -m "feat: SettingsPlacement computes right/left position beside the pet"
```

---

### Task 2: WindowLedgeProvider enumeration cache

**Files:**
- Modify: `src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs`
- Create: `tests/HyperPet.App.Tests/Pets/Roaming/WindowLedgeProviderShouldRebuildTests.cs`

- [ ] **Step 1: Add the pure cache-gate + clock + cache fields**

In `src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs`, add fields and a clock to the class (next to `_selfHwnd`):

```csharp
    private readonly Func<DateTime> _clock;
    private IReadOnlyList<Ledge>? _cache;
    private DateTime _cacheUtc;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMilliseconds(1000);
```

Replace the existing constructor:
```csharp
    public WindowLedgeProvider(IntPtr selfHwnd)
    {
        _selfHwnd = selfHwnd;
    }
```
with a clock-injecting pair:
```csharp
    public WindowLedgeProvider(IntPtr selfHwnd) : this(selfHwnd, () => DateTime.UtcNow)
    {
    }

    internal WindowLedgeProvider(IntPtr selfHwnd, Func<DateTime> clock)
    {
        _selfHwnd = selfHwnd;
        _clock = clock;
    }
```

Add the pure gate (place near the bottom of the class with the other helpers):
```csharp
    internal static bool ShouldRebuild(DateTime nowUtc, DateTime cacheUtc, bool hasCache, TimeSpan ttl)
        => !hasCache || (nowUtc - cacheUtc) >= ttl;
```

- [ ] **Step 2: Use the cache in GetLedges**

`GetLedges()` currently starts by building a `List<Ledge>`. Wrap it: at the top of `GetLedges()`, add:
```csharp
        DateTime now = _clock();
        if (!ShouldRebuild(now, _cacheUtc, _cache is not null, CacheTtl))
        {
            return _cache!;
        }
```
and at the end, before `return ledges;`, store the cache:
```csharp
        _cache = ledges;
        _cacheUtc = now;
        return ledges;
```
(Replace the existing `return ledges;` with the three lines above.)

`TryRefresh` is unchanged.

- [ ] **Step 3: Tests for the gate**

`tests/HyperPet.App.Tests/Pets/Roaming/WindowLedgeProviderShouldRebuildTests.cs`:

```csharp
using HyperPet.App.Pets.Roaming;
using Xunit;

namespace HyperPet.App.Tests.Pets.Roaming;

public class WindowLedgeProviderShouldRebuildTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public void NoCache_AlwaysRebuilds()
    {
        Assert.True(WindowLedgeProvider.ShouldRebuild(DateTime.UtcNow, default, hasCache: false, Ttl));
    }

    [Fact]
    public void WithinTtl_DoesNotRebuild()
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(WindowLedgeProvider.ShouldRebuild(t0.AddMilliseconds(500), t0, hasCache: true, Ttl));
    }

    [Fact]
    public void AtOrPastTtl_Rebuilds()
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(WindowLedgeProvider.ShouldRebuild(t0.AddMilliseconds(1000), t0, hasCache: true, Ttl));
        Assert.True(WindowLedgeProvider.ShouldRebuild(t0.AddMilliseconds(1500), t0, hasCache: true, Ttl));
    }
}
```

`ShouldRebuild` is `internal` — `HyperPet.App.Tests` must already see internals (the project references HyperPet.App and uses internal types elsewhere). If a visibility error occurs, add `[assembly: InternalsVisibleTo("HyperPet.App.Tests")]` to `src/HyperPet.App/AssemblyInfo` or via a `<ItemGroup><AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo"><_Parameter1>HyperPet.App.Tests</_Parameter1></AssemblyAttribute></ItemGroup>` in the csproj. Check first: if tests already reference any `internal` member of HyperPet.App they pass without it; otherwise add the attribute.

- [ ] **Step 4: Build + test**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj -c Release -nologo`
Expected: green.

- [ ] **Step 5: Commit**

```bash
git add src/HyperPet.App/Pets/Roaming/WindowLedgeProvider.cs tests/HyperPet.App.Tests/Pets/Roaming/WindowLedgeProviderShouldRebuildTests.cs
git commit -m "perf: cache window enumeration with a 1s TTL in WindowLedgeProvider"
```

---

### Task 3: InAppNotificationWatcher rect cache + default interval

**Files:**
- Modify: `src/HyperPet.App/Notifications/InAppNotificationWatcher.cs`
- Modify: `src/HyperPet.Core/Settings/HyperPetSettings.cs`
- Modify: `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`

- [ ] **Step 1: Add the extract cache field**

In `InAppNotificationWatcher.cs`, next to `_seenHandles`:
```csharp
    // hwnd -> last window rect + extracted text. Skips the cross-process UI
    // Automation walk when a popup hasn't moved/resized since last scan.
    private readonly Dictionary<IntPtr, (RECT Rect, string Title, string Body)> _extractCache = new();
```

- [ ] **Step 2: Use the cache in the scan loop**

The scan loop currently has:
```csharp
                liveHandles.Add(hwnd);

                // Resolve display name first so ExtractText can filter the
                // app name out as junk.
                bool isSelfProc = string.Equals(processName, _selfProcessName, StringComparison.OrdinalIgnoreCase);
                string appName = isSelfProc ? "Sim" : processName;

                (string title, string body) = ExtractText(hwnd, appName);
                string contentHash = HashText(title + "" + body);
```
Replace that block with one that reuses cached text when the rect is unchanged:
```csharp
                liveHandles.Add(hwnd);

                // Resolve display name first so ExtractText can filter the
                // app name out as junk.
                bool isSelfProc = string.Equals(processName, _selfProcessName, StringComparison.OrdinalIgnoreCase);
                string appName = isSelfProc ? "Sim" : processName;

                GetWindowRect(hwnd, out RECT rect);
                string title, body;
                if (_extractCache.TryGetValue(hwnd, out var cached) && RectEquals(cached.Rect, rect))
                {
                    title = cached.Title;
                    body = cached.Body;
                }
                else
                {
                    (title, body) = ExtractText(hwnd, appName);
                    _extractCache[hwnd] = (rect, title, body);
                }

                string contentHash = HashText(title + "" + body);
```

- [ ] **Step 3: Prune the extract cache with seen handles**

The stale-prune block at the end of `Scan()` is:
```csharp
        var stale = _seenHandles.Keys.Where(h => !liveHandles.Contains(h)).ToList();
        foreach (var h in stale)
        {
            _seenHandles.Remove(h);
        }
```
Replace with:
```csharp
        var stale = _seenHandles.Keys.Where(h => !liveHandles.Contains(h)).ToList();
        foreach (var h in stale)
        {
            _seenHandles.Remove(h);
        }

        var staleExtract = _extractCache.Keys.Where(h => !liveHandles.Contains(h)).ToList();
        foreach (var h in staleExtract)
        {
            _extractCache.Remove(h);
        }
```

- [ ] **Step 4: Add RectEquals helper**

Add near the other private static helpers (e.g. just above the `RECT` struct definition):
```csharp
    private static bool RectEquals(RECT a, RECT b)
        => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
```

- [ ] **Step 5: Clear the extract cache on Dispose**

In `Dispose()` (currently `Stop(); _activator.Dispose(); _seenHandles.Clear();`), add:
```csharp
        _extractCache.Clear();
```

- [ ] **Step 6: Raise the default in-app interval**

In `src/HyperPet.Core/Settings/HyperPetSettings.cs`, change:
```csharp
    public int InAppNotificationPollIntervalSeconds { get; set; } = 2;
```
to:
```csharp
    public int InAppNotificationPollIntervalSeconds { get; set; } = 4;
```

- [ ] **Step 7: Update the defaults test**

In `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`, the defaults test asserts:
```csharp
        Assert.Equal(2, settings.InAppNotificationPollIntervalSeconds);
```
Change to:
```csharp
        Assert.Equal(4, settings.InAppNotificationPollIntervalSeconds);
```

- [ ] **Step 8: Build + full test**

Run: `dotnet build -c Release -nologo` → 0 errors.
Run: `dotnet test -c Release -nologo` → all pass.

- [ ] **Step 9: Commit**

```bash
git add src/HyperPet.App/Notifications/InAppNotificationWatcher.cs src/HyperPet.Core/Settings/HyperPetSettings.cs tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs
git commit -m "perf: skip redundant UI Automation walks for unchanged in-app popups; default in-app interval 4s"
```

---

### Task 4: Cap retained WinRT notifications

**Files:**
- Modify: `src/HyperPet.Windows/Notifications/WindowsNotificationListener.cs`

- [ ] **Step 1: Add the cap constant + trim helper**

Near the `retainedNotifications` field declaration, add:
```csharp
    private const int MaxRetained = 256;
```
Add a private method (anywhere in the class):
```csharp
    private void TrimRetained()
    {
        // Caller already holds the lock on retainedNotifications.
        if (retainedNotifications.Count > MaxRetained)
        {
            retainedNotifications.RemoveRange(0, retainedNotifications.Count - MaxRetained);
        }
    }
```

- [ ] **Step 2: Call it after both append sites**

In `OnNotificationChanged`, inside `lock (retainedNotifications)` after `retainedNotifications.Add(notification);`:
```csharp
                    retainedNotifications.Add(notification);
                    TrimRetained();
```

In `GetActiveNotificationsAsync`, inside `lock (retainedNotifications)` after `retainedNotifications.AddRange(notifications);`:
```csharp
            retainedNotifications.AddRange(notifications);
            TrimRetained();
```

- [ ] **Step 3: Build**

Run: `dotnet build src/HyperPet.Windows/HyperPet.Windows.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.Windows/Notifications/WindowsNotificationListener.cs
git commit -m "perf: cap retained WinRT notification list to a rolling 256 to bound RAM"
```

---

### Task 5: Open Settings beside the pet

**Files:**
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Drop CenterOwner from the XAML**

In `src/HyperPet.App/Views/SettingsWindow.xaml`, remove the line:
```xml
        WindowStartupLocation="CenterOwner"
```
(Leave Width/Height/ResizeMode/Icon/Title as-is.)

- [ ] **Step 2: Position the window in OnSettingsClick**

In `src/HyperPet.App/MainWindow.xaml.cs`, `OnSettingsClick` constructs `settingsWindow` and then calls `settingsWindow.ShowDialog();`. Between construction and `ShowDialog()`, add placement (uses the pure `SettingsPlacement`):

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

`HyperPet.App.Views` is already used (SettingsWindow lives there); `SettingsPlacement`/`Placement` are in that namespace. Add `using HyperPet.App.Views;` if not already present (it is — `SettingsWindow` is constructed here).

- [ ] **Step 3: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/Views/SettingsWindow.xaml src/HyperPet.App/MainWindow.xaml.cs
git commit -m "feat: open Settings beside the pet (right, fallback left) instead of centered"
```

---

### Task 6: Release v0.5.0 (version-stamped Setup + Portable)

**Files:**
- Modify: `src/HyperPet.App/HyperPet.App.csproj`

- [ ] **Step 1: Bump version to 0.5.0**

Replace every `0.4.6` with `0.5.0` in `src/HyperPet.App/HyperPet.App.csproj` (Version, AssemblyVersion `0.5.0.0`, FileVersion `0.5.0.0`, InformationalVersion `0.5.0`).

- [ ] **Step 2: Build + full test**

Run: `dotnet test -c Release -nologo`
Expected: all pass.

- [ ] **Step 3: Commit + push**

```bash
git add src/HyperPet.App/HyperPet.App.csproj
git commit -m "feat: perf reductions + settings placement (v0.5.0)"
git push origin main
```

- [ ] **Step 4: Publish + pack (keep prior Releases for delta)**

```bash
dotnet publish src/HyperPet.App/HyperPet.App.csproj -c Release -r win-x64 --self-contained true -o publish/velopack/0.5.0 -nologo
vpk pack --packId HyperPet --packVersion 0.5.0 --packDir publish/velopack/0.5.0 --mainExe HyperPet.exe
```
Expected: `Releases/` gains `HyperPet-0.5.0-full.nupkg`, `HyperPet-0.5.0-delta.nupkg`, `HyperPet-win-Setup.exe`, `HyperPet-win-Portable.zip`, `releases.win.json`.
If `vpk pack` fails on post-process with "user-mapped section open" (transient Defender lock on the new Setup.exe), delete the partial 0.5.0 artifacts (`rm -f Releases/HyperPet-0.5.0-*.nupkg Releases/releases.win.json Releases/assets.win.json Releases/HyperPet-win-Setup.exe Releases/HyperPet-win-Portable.zip Releases/RELEASES`) keeping prior full nupkgs, and re-run the same `vpk pack` once.

- [ ] **Step 5: Make version-stamped copies of Setup + Portable**

```bash
cp Releases/HyperPet-win-Setup.exe Releases/HyperPet-v0.5.0-win-Setup.exe
cp Releases/HyperPet-win-Portable.zip Releases/HyperPet-v0.5.0-win-Portable.zip
```

- [ ] **Step 6: Tag + push**

```bash
git tag -a v0.5.0 -m "HyperPet v0.5.0"
git push origin v0.5.0
```

- [ ] **Step 7: GitHub release (versioned Setup + Portable; unversioned updater files)**

```bash
gh release create v0.5.0 \
  "Releases/HyperPet-v0.5.0-win-Setup.exe" \
  "Releases/HyperPet-v0.5.0-win-Portable.zip" \
  "Releases/HyperPet-0.5.0-full.nupkg" \
  "Releases/HyperPet-0.5.0-delta.nupkg" \
  "Releases/releases.win.json" \
  "Releases/assets.win.json" \
  "Releases/RELEASES" \
  --title "HyperPet v0.5.0" \
  --notes "Performance: fewer CPU cycles from in-app notification scanning and Desktop-mode window enumeration; bounded memory for retained notifications. Settings now opens beside the pet (right, or left when there's no room) instead of on top of it. App icon fix for notification/Start-menu entries. Auto-update users get a small delta; otherwise run HyperPet-v0.5.0-win-Setup.exe."
```

Note: the updater reads `releases.win.json` + the `*.nupkg` (unversioned names), so those stay as emitted; the version-stamped Setup/Portable are the human download names.

- [ ] **Step 8: Manual verification** (per spec): CPU lower with a static Zalo popup; Desktop roam still rides dragged windows; memory stops climbing over time; Settings opens to the side; release shows versioned Setup + Portable.

---

## Self-Review

**Spec coverage:**
- In-app rect cache + default 4s → Task 3.
- Ledge enumeration TTL cache + `ShouldRebuild` test → Task 2.
- Retained-list cap → Task 4.
- `SettingsPlacement` + open beside pet + drop CenterOwner → Tasks 1, 5.
- Version-stamped Setup/Portable → Task 6 (steps 5, 7).
- Tests: placement (5), ShouldRebuild (3), in-app default (1) → Tasks 1-3. Win32 paths manual → Task 6 step 8.

**Placeholder scan:** none. Every code step is concrete. The InternalsVisibleTo note in Task 2 includes the exact attribute to add if needed.

**Type consistency:**
- `SettingsPlacement.Compute(...)` → `Placement(double Left, double Top)`; used in Task 5 with `settingsWindow.Width/Height`, `GetWindowWidth()/GetWindowHeight()`, `SystemParameters.WorkArea` (Rect with Left/Top/Right/Bottom). Matches.
- `WindowLedgeProvider.ShouldRebuild(DateTime, DateTime, bool, TimeSpan)` defined Task 2, used by `GetLedges` + tests.
- `RECT` is the existing private struct in InAppNotificationWatcher; `RectEquals(RECT, RECT)` added in Task 3; `_extractCache` value tuple `(RECT, string, string)` consistent.
- `GetWindowRect` P/Invoke already exists in InAppNotificationWatcher (used by `IsPopupCandidate`); Task 3 calls it again in the loop — same signature.

# Chromium UIA Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make HyperPet show real sender + message text in the bubble for Zalo and other Chromium-based in-app notifications by waking Chromium's UI Automation tree.

**Architecture:** Add a new `ChromiumAccessibilityActivator` that subscribes a no-op UIA property-changed event handler scoped to each watched process's main window. Wire it into `InAppNotificationWatcher` so activation happens at startup and on every PID resolution. No changes to extraction logic — `ExtractText` already filters junk; with the woken tree it sees real names.

**Tech Stack:** C# / .NET 8 WPF (net8.0-windows10.0.19041.0), `System.Windows.Automation` UIA managed wrapper, `System.Diagnostics.Process`, existing `HyperPet.Core.Diagnostics.HyperPetLogger`.

**Spec:** `docs/superpowers/specs/2026-05-27-chromium-uia-extraction-design.md`

---

## File Structure

**Create:**
- `src/HyperPet.App/Notifications/ChromiumAccessibilityActivator.cs` — owns UIA subscription lifecycle per PID. ~80 lines.

**Modify:**
- `src/HyperPet.App/Notifications/InAppNotificationWatcher.cs` — own a `ChromiumAccessibilityActivator`, call it from `ResolveWatchedPids` and `Start`, dispose it.

**No tests:** UIA requires a real running process; spec explicitly opts out of unit tests and lists a manual checklist instead.

---

### Task 1: Create `ChromiumAccessibilityActivator`

**Files:**
- Create: `src/HyperPet.App/Notifications/ChromiumAccessibilityActivator.cs`

- [ ] **Step 1: Create the file with the full implementation**

```csharp
using System.Diagnostics;
using System.Windows.Automation;
using HyperPet.Core.Diagnostics;

namespace HyperPet.App.Notifications;

/// <summary>
/// Wakes the UI Automation tree on watched Chromium / Electron apps
/// (Zalo, Discord, Slack, etc.) by subscribing a no-op UIA property
/// changed event handler scoped to the process's main window with
/// <see cref="TreeScope.Subtree"/>. Chromium detects the listener and
/// upgrades its accessibility mode to <c>kWebContents</c>, which makes
/// the popup's real text content visible to the watcher's UIA walk.
/// </summary>
public sealed class ChromiumAccessibilityActivator : IDisposable
{
    private readonly HyperPetLogger? _logger;
    private readonly HashSet<int> _activatedPids = new();
    private readonly AutomationPropertyChangedEventHandler _noOpHandler;
    private bool _disposed;

    public ChromiumAccessibilityActivator(HyperPetLogger? logger = null)
    {
        _logger = logger;
        _noOpHandler = NoOp;
    }

    /// <summary>
    /// Subscribes a UIA listener on the process's main window if it has
    /// not been activated yet. No-op if the process has no main window
    /// HWND yet (e.g., still starting). Safe to call on every scan.
    /// </summary>
    public void EnsureActivated(int pid)
    {
        if (_disposed || _activatedPids.Contains(pid))
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            IntPtr mainHwnd = process.MainWindowHandle;
            if (mainHwnd == IntPtr.Zero)
            {
                // Process hasn't created its main window yet; retry on the
                // next scan without marking the pid as activated.
                return;
            }

            AutomationElement? element = AutomationElement.FromHandle(mainHwnd);
            if (element is null)
            {
                return;
            }

            Automation.AddAutomationPropertyChangedEventHandler(
                element,
                TreeScope.Subtree,
                _noOpHandler,
                AutomationElement.NameProperty);

            _activatedPids.Add(pid);
            _logger?.Info($"ChromiumAccessibilityActivator: woke UIA tree for pid={pid}");
        }
        catch (ArgumentException)
        {
            // Process exited between resolution and activation.
        }
        catch (InvalidOperationException)
        {
            // Process exited or has no HWND.
        }
        catch (Exception exception)
        {
            _logger?.Warn($"ChromiumAccessibilityActivator: activation failed for pid={pid}", exception);
        }
    }

    /// <summary>
    /// Drops tracking entries whose pid is not in <paramref name="livePids"/>.
    /// Called from the watcher after PID resolution so restarts of Zalo
    /// pick up the new pid on the next scan.
    /// </summary>
    public void PruneStale(IEnumerable<int> livePids)
    {
        if (_disposed)
        {
            return;
        }

        var live = new HashSet<int>(livePids);
        _activatedPids.RemoveWhere(pid => !live.Contains(pid));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Automation.RemoveAllEventHandlers();
        }
        catch (Exception exception)
        {
            _logger?.Warn("ChromiumAccessibilityActivator: RemoveAllEventHandlers failed", exception);
        }

        _activatedPids.Clear();
    }

    private static void NoOp(object sender, AutomationPropertyChangedEventArgs e)
    {
        // Subscription presence alone wakes Chromium accessibility;
        // we intentionally discard events.
    }
}
```

- [ ] **Step 2: Build to confirm the new file compiles in isolation**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` with 0 warnings, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/HyperPet.App/Notifications/ChromiumAccessibilityActivator.cs
git commit -m "feat: ChromiumAccessibilityActivator wakes UIA tree on watched processes"
```

---

### Task 2: Wire activator into `InAppNotificationWatcher`

**Files:**
- Modify: `src/HyperPet.App/Notifications/InAppNotificationWatcher.cs`

- [ ] **Step 1: Add the activator field and initialize it in the constructor**

Locate this block near the top of the class:

```csharp
    private readonly DispatcherTimer _timer;
    private readonly HyperPetLogger? _logger;
```

Add a field immediately after `_logger`:

```csharp
    private readonly ChromiumAccessibilityActivator _activator;
```

Locate the constructor body — currently:

```csharp
    public InAppNotificationWatcher(HyperPetLogger? logger = null)
    {
        _logger = logger;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += (_, _) => Scan();
    }
```

Add the activator construction after `_logger = logger;`:

```csharp
    public InAppNotificationWatcher(HyperPetLogger? logger = null)
    {
        _logger = logger;
        _activator = new ChromiumAccessibilityActivator(logger);
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += (_, _) => Scan();
    }
```

- [ ] **Step 2: Pre-activate at `Start` so the first scan already has the tree**

Locate:

```csharp
    public void Start()
    {
        if (_watchedProcessNames.Count == 0)
        {
            return;
        }

        _timer.Start();
    }
```

Replace with:

```csharp
    public void Start()
    {
        if (_watchedProcessNames.Count == 0)
        {
            return;
        }

        // Pre-activate now so the first scan sees a populated Chromium
        // accessibility tree instead of host-element junk. Discard the
        // result; the side effect (UIA subscription) is what we want.
        _ = ResolveWatchedPids();

        _timer.Start();
    }
```

- [ ] **Step 3: Wire `EnsureActivated` + `PruneStale` into `ResolveWatchedPids`**

Locate `ResolveWatchedPids`:

```csharp
    private Dictionary<int, string> ResolveWatchedPids()
    {
        var result = new Dictionary<int, string>();

        foreach (var name in _watchedProcessNames)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        result[process.Id] = name;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception exception)
            {
                _logger?.Warn($"InAppNotificationWatcher: lookup failed for '{name}'", exception);
            }
        }

        return result;
    }
```

Replace with:

```csharp
    private Dictionary<int, string> ResolveWatchedPids()
    {
        var result = new Dictionary<int, string>();

        foreach (var name in _watchedProcessNames)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        result[process.Id] = name;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception exception)
            {
                _logger?.Warn($"InAppNotificationWatcher: lookup failed for '{name}'", exception);
            }
        }

        // Wake Chromium UIA tree on each watched process, and drop entries
        // whose pid is no longer alive (Zalo restart, etc.).
        foreach (var pid in result.Keys)
        {
            _activator.EnsureActivated(pid);
        }

        _activator.PruneStale(result.Keys);

        return result;
    }
```

- [ ] **Step 4: Dispose the activator in `Dispose`**

Locate:

```csharp
    public void Dispose()
    {
        Stop();
        _seenHandles.Clear();
    }
```

Replace with:

```csharp
    public void Dispose()
    {
        Stop();
        _activator.Dispose();
        _seenHandles.Clear();
    }
```

- [ ] **Step 5: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` with 0 warnings, 0 errors.

- [ ] **Step 6: Run the test suite to confirm nothing else broke**

Run: `dotnet test -c Release -nologo`
Expected: `Passed!` totals — 30 tests pass (28 Core + 2 App).

- [ ] **Step 7: Commit**

```bash
git add src/HyperPet.App/Notifications/InAppNotificationWatcher.cs
git commit -m "feat: wire ChromiumAccessibilityActivator into InAppNotificationWatcher"
```

---

### Task 3: Manual verification

**Files:** none — verification only.

- [ ] **Step 1: Launch HyperPet**

Run: `dotnet run --project src/HyperPet.App/HyperPet.App.csproj -c Release`

- [ ] **Step 2: Confirm Zalo message bubble shows real text**

1. Have Zalo running and signed in.
2. From a second account or device, send a message to the test user (e.g., `Phuc cool bro` says `Ha`).
3. The pet bubble should display:
   - App name line: `Zalo`
   - Title: sender name (e.g., `Phuc cool bro`)
   - Body: message text (e.g., `Ha`)

Expected: bubble shows sender + message, **not** `Zalo / New message` and **not** `Zalo / Chrome Legacy Window`.

- [ ] **Step 3: Confirm second message re-emits**

Send another message in the same conversation (e.g., `Yeah`). The bubble should re-emit with the new text inside the poll interval.

- [ ] **Step 4: Confirm restart of Zalo still works**

1. Quit Zalo.
2. Relaunch Zalo and sign in.
3. Send another message from the other account.
4. Bubble should still pick up sender + body (new pid, activator re-subscribes).

- [ ] **Step 5: Confirm non-Zalo notifications are unaffected**

1. Trigger a Windows toast (press `0` in debug mode if `Debug` is enabled, or trigger any toast-emitting app).
2. Bubble should appear as before for Windows toasts.
3. Right-click the pet — context menu opens with no bubble.
4. Hover over Zalo's toolbar buttons — tooltips show, no bubble.

If any step fails, return to the failing task and adjust.

---

### Task 4: Release v0.3.6

**Files:**
- Modify: `src/HyperPet.App/HyperPet.App.csproj`

- [ ] **Step 1: Bump the version to 0.3.6**

In `src/HyperPet.App/HyperPet.App.csproj`, replace every occurrence of `0.3.5` with `0.3.6` (Version, AssemblyVersion, FileVersion, InformationalVersion).

- [ ] **Step 2: Build + test**

Run: `dotnet test -c Release -nologo`
Expected: 30/30 pass.

- [ ] **Step 3: Commit + push**

```bash
git add src/HyperPet.App/HyperPet.App.csproj
git commit -m "$(cat <<'EOF'
feat: wake Chromium UIA tree for real Zalo message text (v0.3.6)

InAppNotificationWatcher now constructs a ChromiumAccessibilityActivator
and pre-activates UIA on every watched process at Start() and on each
PID resolution. Chromium detects the subscription and upgrades to
kWebContents accessibility mode, which exposes the popup's real sender
and message text to ExtractText's UIA walk. Bubbles for Zalo and other
Chromium / Electron apps now show "<sender> / <message>" instead of
"<app> / New message".
EOF
)"
git push origin main
```

- [ ] **Step 4: Publish self-contained**

Run:
```bash
dotnet publish src/HyperPet.App/HyperPet.App.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o publish/v0.3.6 -nologo
```

Expected: `HyperPet.App -> ...\publish\v0.3.6\` line.

- [ ] **Step 5: Zip**

Run (PowerShell):
```bash
powershell -NoProfile -Command "Compress-Archive -Path 'publish/v0.3.6/*' -DestinationPath 'dist/HyperPet-v0.3.6-win-x64.zip' -Force"
```

Expected: `dist/HyperPet-v0.3.6-win-x64.zip` ~70 MB.

- [ ] **Step 6: Tag + push tag**

```bash
git tag -a v0.3.6 -m "HyperPet v0.3.6"
git push origin v0.3.6
```

- [ ] **Step 7: GitHub release**

```bash
gh release create v0.3.6 "dist/HyperPet-v0.3.6-win-x64.zip" \
  --title "HyperPet v0.3.6" \
  --notes "$(cat <<'EOF'
## Real sender + message in bubble for Zalo

### What changed
- New `ChromiumAccessibilityActivator` subscribes a no-op UI Automation event handler on each watched process's main window. Chromium / Electron apps (Zalo, Discord, Slack) detect the subscription and switch to `kWebContents` accessibility mode, exposing real popup text.
- `InAppNotificationWatcher` pre-activates on startup and on every PID resolution, then prunes activated pids that no longer exist (Zalo restarts).

### Effect
- Zalo bubbles now show `<sender> / <message>` (e.g. `Phuc cool bro / Ha`) instead of `Zalo / New message`.
- Combined with the v0.3.5 content-hash re-emit, every new message on the same notification HWND triggers a bubble.
EOF
)"
```

Expected: release URL printed.

---

## Self-Review

**Spec coverage:**
- New `ChromiumAccessibilityActivator` with `EnsureActivated`, `PruneStale`, `Dispose`, `NoOp` handler → Task 1.
- Watcher field + constructor wiring → Task 2 step 1.
- Pre-activate at `Start()` → Task 2 step 2.
- Activator called from `ResolveWatchedPids` + `PruneStale` → Task 2 step 3.
- Watcher disposes activator → Task 2 step 4.
- `ExtractText` unchanged (spec mandate) → no task needed.
- Manual test plan (happy path, restart, non-Chromium) → Task 3.
- Release artifacts → Task 4.

**Placeholder scan:** No "TODO", no "implement later", no "similar to Task N". Every code change has full code.

**Type consistency:** `ChromiumAccessibilityActivator` constructor signature `(HyperPetLogger? logger = null)` matches the watcher's `new ChromiumAccessibilityActivator(logger)` call. `EnsureActivated(int pid)`, `PruneStale(IEnumerable<int>)`, `Dispose()` all called with matching arguments.

**No tests for the new code:** matches the spec's explicit "No unit tests" decision (UIA needs a real process). The manual checklist replaces them.

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
    private readonly Dictionary<int, (AutomationElement Element, DateTime Started, IntPtr Hwnd)> _activatedPids = new();
    private readonly Dictionary<int, DateTime> _retryAfter = new();
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
        if (_disposed || (_retryAfter.TryGetValue(pid, out var retry) && DateTime.UtcNow < retry))
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            IntPtr mainHwnd = process.MainWindowHandle;
            DateTime started = process.StartTime.ToUniversalTime();
            if (_activatedPids.TryGetValue(pid, out var active))
            {
                if (active.Started == started && active.Hwnd == mainHwnd) return;
                try { Automation.RemoveAutomationPropertyChangedEventHandler(active.Element, _noOpHandler); } catch { }
                _activatedPids.Remove(pid);
            }
            if (mainHwnd == IntPtr.Zero)
            {
                // Electron helpers often never own a main window. Back off instead
                // of querying their UIA tree on every scan.
                _retryAfter[pid] = DateTime.UtcNow.AddSeconds(10);
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

            _activatedPids.Add(pid, (element, started, mainHwnd));
            _retryAfter.Remove(pid);
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
        foreach (int pid in _retryAfter.Keys.Where(pid => !live.Contains(pid)).ToArray()) _retryAfter.Remove(pid);
        foreach (int pid in _activatedPids.Keys.Where(pid => !live.Contains(pid)).ToArray())
        {
            try { Automation.RemoveAutomationPropertyChangedEventHandler(_activatedPids[pid].Element, _noOpHandler); }
            catch { }
            _activatedPids.Remove(pid);
        }
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
            foreach (var element in _activatedPids.Values)
                try { Automation.RemoveAutomationPropertyChangedEventHandler(element.Element, _noOpHandler); } catch { }
        }
        catch (Exception exception)
        {
            _logger?.Warn("ChromiumAccessibilityActivator: subscription cleanup failed", exception);
        }

        _activatedPids.Clear();
        _retryAfter.Clear();
    }

    private static void NoOp(object sender, AutomationPropertyChangedEventArgs e)
    {
        // Subscription presence alone wakes Chromium accessibility;
        // we intentionally discard events.
    }
}

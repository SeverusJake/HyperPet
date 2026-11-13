# Deployment validation — 2026-11-12

## Evidence from this implementation

- Windows x64 self-contained publish succeeds; included Core/Desktop runtime 10.0.15.
- Embedded executable manifest contains PerMonitorV2/PerMonitor; WPF owns startup. WinForms analyzer warning is suppressed only for this intentional WPF manifest setup.
- Velopack 1.0.1 packages Setup and Portable locally and explicitly verifies `VelopackApp.Run()` in `HyperPet.App.App::Main()`.
- Current-user `Get-AppxPackage -Name '*HyperPet*'` returned no package. The checked-in MSIX manifest was previously not registered by any build/release step.
- Manifest template now names HyperPet.exe and 0.7.1.0. A future identity pipeline must derive version from the committed project, not assume this template grants identity.
- In-app monitoring is independent of Windows permission. Disabled Windows source does not request access at launch; enabling it later requests access. Setup displays the request result already obtained.

## Remaining validation and decision

**2026-11-13 handoff:** The owner elected to perform deployment checks manually later. These items remain open; follow-up unit tests of fake permission/subscription adapters do not close them. Miku copyright/provenance investigation is excluded from the requested follow-up scope.

No isolated Windows test VM or second physical display was available. This change does not install a package, trust a certificate, alter notification permission, or run an uninstaller on the user's actual profile.

1. On a disposable Windows profile/VM, verify current Velopack identity/access status and consent/revoke/regrant.
2. Evaluate Velopack + external-location identity versus a full MSIX distribution against the actual UserNotificationListener capability, updates, login startup, uninstall and rollback.
3. Prefer preserving Velopack only if that combination passes end-to-end. A full MSIX switch changes the updater/distribution and needs the owner's choice based on those results.
4. Test clean install → consent → notification → update → login startup → uninstall, plus Windows permission denied with in-app source still active.
5. Verify 100/125/150/200% DPI, mixed-DPI monitor transitions, unplug/replug and taskbar/work-area changes on physical displays.

Deployment identity and physical monitor behavior remain **unverified**, not “fixed because unit tests pass”. Current local packages are verification builds of an uncommitted branch, not a new published release.

## Manual results to record

Record Windows build, app version/commit, installed/portable mode, package identity, expected/actual result and relevant log excerpt for each row. Use an isolated profile for install/permission/autorun/uninstall checks. Keep all rows unchecked until the owner runs them.

| Done | Scenario | Expected outcome |
|---|---|---|
| [ ] | Clean install, first launch, consent allowed | Pet appears; supported Windows listener receives an actual notification; in-app source operates independently |
| [ ] | Deny, revoke, then regrant consent | Clear permission state; no duplicate listener after retry; in-app source remains usable |
| [ ] | Windows source off, in-app source on | No Windows consent request or Windows alerts; watched popup still alerts |
| [ ] | Toggle source off/on during permission request, poll and incoming event | No old-cycle alert after re-enable; one active subscription |
| [ ] | Upgrade from previous installed version | Pet/settings preserved; version and update channel correct; no second instance |
| [ ] | Login startup; portable and debug launch | Installed startup preference honored; portable/debug do not take over installed autorun |
| [ ] | Uninstall after installed test | Installed files/startup registration removed as documented; inspect current data-deletion behavior in the disposable profile |
| [ ] | Repeated real notifications, provider restart and sustained WinRT/GC stress | No access violation, frozen UI or unbounded retained wrappers; capture crash dump if native failure occurs |
| [ ] | 100/125/150/200%, mixed-DPI move, unplug/replug, work-area changes | Pet stays on usable work area; Settings controls/footer remain reachable |

Installed/portable logs: `%APPDATA%/HyperPet/hyperpet.log` and rotated `.1`; debug logs: `%LOCALAPPDATA%/HyperPet/Dev/hyperpet.log`. Share only needed metadata/error excerpts; do not include user notification contents. If writable logging is unavailable, record that explicitly instead of treating a missing log as a passing result.

References: [Microsoft notification listener](https://learn.microsoft.com/en-us/windows/uwp/design/shell/tiles-and-notifications/notification-listener), [external-location identity](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps), [Velopack WPF entry point](https://docs.velopack.io/getting-started/csharp).

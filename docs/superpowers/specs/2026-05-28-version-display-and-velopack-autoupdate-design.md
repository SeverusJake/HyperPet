# Version Display + Velopack Auto-Update

Date: 2026-05-28
Status: Approved (pending spec review)
Author: claude (with user)

## Problem

HyperPet has no visible version and no update mechanism. Users can't tell
which build they run, and new releases require manually finding the GitHub
release, downloading a zip, and replacing files. We want (1) the version
shown in-app and (2) a one-click, confirm-gated auto-update backed by
Velopack.

## Goals

1. Show the running version in two places: a new **About** tab in Settings
   and a disabled header item in the pet's right-click context menu.
2. Add a **Check for update** button on the About tab (manual path).
3. Add an **Auto-update** checkbox on the General tab (default OFF). When
   on, HyperPet checks for updates once at launch and, if one is found,
   prompts immediately.
4. Both paths require explicit user confirmation before any download or
   apply. Updates are delivered via Velopack from GitHub Releases.

## Non-goals

- Background polling on an interval. Auto-update checks only at launch.
- Prerelease / beta channels. Stable releases only.
- Silent (no-confirm) updates. Every update is user-confirmed.
- Migrating existing v0.3.x zip-format releases to Velopack retroactively.

## Decisions (from brainstorming)

- Auto-update default: **OFF**.
- On auto-check finding an update: **prompt immediately at launch**.
- Startup check failure (offline, etc.): **silent**, log only. Manual
  check (About tab) surfaces errors to the user.
- Update engine: **Velopack** (delta downloads, atomic apply, rollback).

## Architecture

### New: `AppVersion` (static helper)

File: `src/HyperPet.App/AppVersion.cs`.

```csharp
public static class AppVersion
{
    // Reads AssemblyInformationalVersion (e.g. "0.3.8"), trimming any
    // "+<gitsha>" build-metadata suffix the SDK may append.
    public static string Current { get; }

    // "HyperPet v0.3.8" — used by context menu header and About tab.
    public static string DisplayString { get; }
}
```

### New: `UpdateService` (wraps Velopack)

File: `src/HyperPet.App/Update/UpdateService.cs`.

Responsibilities: own a Velopack `UpdateManager` pointed at the GitHub repo,
guard against dev/non-installed runs, expose check / download+apply.

```csharp
public sealed class UpdateService
{
    public UpdateService(HyperPetLogger? logger = null);

    // False when running from a raw build / dotnet run (UpdateManager
    // .IsInstalled == false). About tab shows "dev build" and disables
    // the button; startup auto-check is skipped.
    public bool IsSupported { get; }

    // Returns the available UpdateInfo, or null when already up to date.
    // Throws on network / source errors (caller decides how loud to be).
    public Task<UpdateInfo?> CheckAsync();

    // Downloads then applies + restarts. Never returns on success
    // (process is replaced/restarted).
    public Task DownloadAndApplyAsync(UpdateInfo info, Action<int>? progress = null);
}
```

Internals:
- `new UpdateManager(new GithubSource("https://github.com/SeverusJake/HyperPet", null, false), logger: ...)`.
- `IsSupported => _manager.IsInstalled`.
- `CheckAsync` => `_manager.CheckForUpdatesAsync()`.
- `DownloadAndApplyAsync` => `await _manager.DownloadUpdatesAsync(info, progress)` then `_manager.ApplyUpdatesAndRestart(info)`.

### App startup wiring (`App.xaml.cs` / entry)

1. **`VelopackApp.Build().Run();`** is the FIRST statement executed by the
   process, before any WPF/window initialization. This handles Velopack's
   install / uninstall / first-run / restart hooks. Missing or late
   placement breaks the update lifecycle.
2. After the main window is shown, if `settings.AutoUpdate && updateService.IsSupported`:
   - Run `CheckAsync()` on a background task.
   - On result non-null: marshal to UI thread, show the confirm dialog.
   - On exception: log and swallow (silent — decision 3).

### Confirm + progress flow (shared by manual and auto)

Both paths converge on one method, e.g. `MainWindow.PromptAndApplyUpdate(UpdateInfo info)`:

1. `MessageBox.Show` (Yes/No): "HyperPet v{info.TargetFullRelease.Version}
   is available. Download and update now? The app will restart."
2. If No: return (no download).
3. If Yes: call `UpdateService.DownloadAndApplyAsync` with a progress
   callback that updates a lightweight status (About tab status text when
   triggered from there; a simple modal/disabled-state otherwise).
4. On success the process restarts automatically (Velopack). On failure:
   log and show an error MessageBox.

### Settings model (`HyperPetSettings`)

```csharp
/// When true, HyperPet checks GitHub for a newer release once at launch
/// and prompts to install it. Manual checks via the About tab are always
/// available regardless of this flag. Default off.
public bool AutoUpdate { get; set; } = false;
```

### Settings dialog UI

- **General tab**: new checkbox `Auto-update (check on launch)` wired into
  the existing dirty-tracking + commit path (one more bool in the applier).
- **About tab** (new, last tab): 
  - `TextBlock` showing `AppVersion.DisplayString`.
  - `Check for update` button.
  - Status `TextBlock`: idle → "" ; checking → "Checking…" ; up to date →
    "You're on the latest version." ; found → "Update available: v{X}" with
    the confirm flow; error → "Could not check for updates: {message}" ;
    dev build → "Updates are disabled for development builds."
  - Window title also set to `Settings - {AppVersion.DisplayString}`.

### Context menu (`MainWindow.xaml`)

Add a disabled header `MenuItem` at the top showing
`AppVersion.DisplayString`, followed by a `Separator`, then the existing
items (Pause alerts / Settings / Quit).

## Packaging migration

Current pipeline: `dotnet publish` (single-file self-contained) → zip →
GitHub release. New pipeline for Velopack:

1. Install the Velopack CLI: `dotnet tool install -g vpk`.
2. Publish to a folder (not single-file — Velopack handles its own bundling):
   `dotnet publish src/HyperPet.App/HyperPet.App.csproj -c Release -r win-x64 --self-contained true -o publish/velopack/<version>`
3. Pack:
   `vpk pack --packId HyperPet --packVersion <version> --packDir publish/velopack/<version> --mainExe HyperPet.exe`
4. Outputs land in `Releases/`: `HyperPet-win-Setup.exe`, `*.nupkg`,
   `releases.win.json` (and delta nupkgs on subsequent versions).
5. Upload all `Releases/` assets to the GitHub release for the tag.
6. Velopack's `GithubSource` reads `releases.win.json` to find updates.

First Velopack release is a new baseline. Existing v0.3.x users install
`HyperPet-win-Setup.exe` once (installs to `%LocalAppData%\HyperPet`,
per-user, no admin). All later updates are delta + seamless.

## Edge cases

- **Dev build / not installed**: `UpdateService.IsSupported == false`.
  About tab shows the dev message and disables the button; startup
  auto-check skipped. No exceptions.
- **Offline at launch (auto)**: `CheckAsync` throws → logged, swallowed.
- **Offline on manual check**: error shown in About status text.
- **Already up to date**: `CheckAsync` returns null → "You're on the latest
  version."
- **Download fails mid-way**: Velopack handles resume/cleanup; on terminal
  failure we log + show an error and leave the install untouched.
- **User declines confirm**: nothing downloaded.

## Testing

- **Unit**: `AppVersion.Current` parsing (strips `+sha` suffix; falls back
  to "0.0.0" when attribute missing). Add to `HyperPet.App.Tests`.
- **Settings round-trip**: `AutoUpdate` persists through SettingsStore
  save/load (extend existing `SettingsStoreTests`).
- **Manual (requires two Velopack releases)**: install vN via Setup.exe,
  publish vN+1, launch with Auto-update on → prompt appears → accept →
  app downloads delta, restarts on vN+1. Repeat with Auto-update off using
  the About tab button. Verify offline behavior (silent at launch, error
  on manual).
- Velopack's own machinery is not unit-tested by us.

## Out of scope

- Interval/background polling.
- Channels (beta/stable split).
- Code signing the installer (can be added later via `vpk pack --signing*`).

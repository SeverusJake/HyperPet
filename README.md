# HyperPet

HyperPet is a Windows desktop pet that watches Windows notifications and shows the notification content that Windows exposes to the app.

## Requirements

- Windows 10 build 19041 or newer
- .NET 10 SDK (see global.json)

Self-contained builds include the pinned .NET 10 servicing runtime. Windows must also be within the runtime vendor's supported OS lifecycle; the build 19041 value is the minimum Windows API contract, not a support promise for every old Windows edition.

## Build

```powershell
dotnet build HyperPet.sln
```

## Release File Names

For every new release, both distribution downloads must include the version:

- `HyperPet-vX.Y.Z-win-Setup.exe`
- `HyperPet-vX.Y.Z-win-Portable.zip`

`pack.ps1 -Version X.Y.Z -PackDirectory <published-app> -OutputDirectory <empty-output>` creates versioned downloads in isolated output. Restore the pinned CLI with `dotnet tool restore` first. Pass `-PreviousPackage <HyperPet-X.Y.Z-full.nupkg>` (the previous release's full package) to `pack.ps1` or `release.ps1` to also build the delta update package; without it, updates download the full package.

`release.ps1 -Version X.Y.Z -BuildOnly` verifies tests/audit and creates artifacts, SHA256SUMS.txt and build-manifest.json from a **clean commit whose project version already matches**. It never edits/commits source, creates tags, or pushes branches. Publication requires the explicit `-Publish` flag and an existing remote tag pointing at that exact commit. Keep Velopack update package/manifest names unchanged. Preview builds from an uncommitted working tree are for local verification only.

## Test

```powershell
dotnet test HyperPet.sln
```

## Run

```powershell
dotnet run --project src/HyperPet.App/HyperPet.App.csproj
```

`dotnet run` is useful for UI smoke testing. Windows notification-listener access may be unavailable without package identity; see Notification Access below.

## Startup and Position

Installed releases can manage **Start with Windows**, on by default for new settings. Development/portable runs do not take over the installed app's autorun entry. Debug builds use `%LOCALAPPDATA%/HyperPet/Dev`; installed/portable releases keep `%APPDATA%/HyperPet`. One process runs per user/profile; later launches activate the existing pet.
Saved startup preferences are preserved. **Remember pet position on launch** is
also on by default. Turn it off to open at the default position instead. Saved
positions are kept within the nearest available monitor's work area.

Use **Pet monitor** to choose a specific display, or **Automatic** to use the
pet's current/last monitor. Apply moves the pet into the chosen monitor. If that
display is disconnected, the pet moves to the nearest available display; the
saved preference is retained for when the display reconnects. Running, Desktop
roaming, and Come honor the selected display. In Automatic mode, Come uses the
monitor under the cursor. Refresh updates the monitor list in Settings.

## Notification Queue and History

The newest notification is always the one shown: a new arrival replaces the
bubble immediately, and the one it displaced waits behind it. Click the
notification (or let it expire) to go back to the previous one, newest first;
clicking the last one closes the bubble. At most 100 earlier alerts wait; if
full, the oldest is dropped. Windows notifications that arrived before HyperPet
started (or before Windows notifications were re-enabled) are not shown.

The notification automatically chooses space above, right, left, or below the
pet and stays within its monitor's work area. It follows the pet without
changing the pet window's size. Long titles and messages can be scrolled;
hovering over the notification pauses its expiry timer until you leave it.

Open **Notification history** from the pet or tray menu, or click the small
history icon at the bubble's top right. The newest 50 accepted notifications are available to read or show
again. Replaying a notification does not create another history entry. Clear
history removes the history list without discarding pending alerts.

History is kept only in memory for the current session and is not written to
disk. Pause, quiet hours, and exiting discard pending alerts. Turning off
**Show full notification content** also redacts current, queued, and historical
message text; turning it back on does not restore already-redacted content.

## Notification Access

Windows may ask for notification access permission. Some apps hide or trim notification content before Windows exposes it, so HyperPet cannot guarantee that the available content is the original full notification text.

The current distribution uses Velopack, which does not itself register the included MSIX manifest. Windows notification-listener access may therefore be unavailable in this deployment. The manifest is a packaging template, not proof of runtime identity. See `docs/reviews/2026-11-12-deployment-validation.md` for the deployment validation still required. In-app popup monitoring starts independently, including when Windows access is denied or unavailable.

## Sprite Images

Spritesheets are decoded and cropped with SkiaSharp (MIT license). PNG and WebP,
including transparent sprites, are covered by loader tests. No ImageSharp or
Six Labors license key is required. SkiaSharp's license and third-party notices
are included in build and publish output.

## Manual MVP Checklist

- The pet appears as a topmost transparent desktop overlay.
- The pet can be dragged to a new position.
- The right-click menu opens.
- Settings can be opened and updated.
- Setup guidance appears when notification access is missing.
- An app notification triggers a speech bubble.
- The speech bubble shows the notification fields Windows exposes.
- A duplicate active notification is not shown repeatedly.
- Restarting the app preserves the pet position and settings.
- Tuck Away exits even while an alert is visible or the exit animation cannot complete.
- Uninstall removes the HyperPet entry from the current user's Windows startup Run key.

## Pet import safety

Import validates metadata, actual image/crop bounds and canonical paths before install. Built-in IDs cannot be overwritten. The old pet is renamed to a recovery backup only after staging validates; failed promotion restores it, or preserves and reports the backup path if recovery itself fails.

Limits: pet.json 1 MiB; ZIP 100 MiB compressed; copy/extraction 256 MiB total; 64 MiB per file; 1,024 files; depth 8; 32 states; 256 total frames; image edge 8,192 pixels and total 16,777,216 pixels; decoded frames 256 MiB; FPS 1–60. Symbolic links, junctions, overlapping source/target folders and ambiguous ZIPs with multiple pet.json files are rejected.

## Contributor references

Read `AGENTS.md`, `docs/README.md`, and the current remediation status before older design/implementation records. Generated output is ignored. Run `pwsh -NoProfile -File tests/ReleaseScripts.Tests.ps1` for release-script regression checks; it needs no third-party PowerShell test framework.

# HyperPet

HyperPet is a Windows desktop pet that watches Windows notifications and shows the notification content that Windows exposes to the app.

## Requirements

- Windows 10 build 19041 or newer
- .NET 8 SDK

This repo also builds with the installed .NET SDK 10.

## Build

```powershell
dotnet build HyperPet.sln
```

## Test

```powershell
dotnet test HyperPet.sln
```

## Run

```powershell
dotnet run --project src/HyperPet.App/HyperPet.App.csproj
```

`dotnet run` is useful for UI smoke testing. Real Windows notification listener permission requires an MSIX/package identity with the `userNotificationListener` capability declared in `src/HyperPet.App/Package.appxmanifest`.

## Startup and Position

In Settings → General, **Start with Windows** is on by default for new settings.
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

Notifications wait their turn instead of replacing the current speech bubble.
Click **Next** to advance early, or **Dismiss** when the queue is empty. At most
100 pending alerts are retained; if full, the oldest pending alert is dropped.

Open **Notification history** from the pet or tray menu, or click **History** in
the bubble. The newest 50 accepted notifications are available to read or show
again. Replaying a notification does not create another history entry. Clear
history removes the history list without discarding pending alerts.

History is kept only in memory for the current session and is not written to
disk. Pause, quiet hours, and exiting discard pending alerts. Turning off
**Show full notification content** also redacts current, queued, and historical
message text; turning it back on does not restore already-redacted content.

## Notification Access

Windows may ask for notification access permission. Some apps hide or trim notification content before Windows exposes it, so HyperPet cannot guarantee that the available content is the original full notification text.

If Windows does not show HyperPet in notification access settings, run HyperPet from a packaged install that uses the included `Package.appxmanifest`. An unpackaged development run can start the pet window but may not receive notification-listener permission.

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

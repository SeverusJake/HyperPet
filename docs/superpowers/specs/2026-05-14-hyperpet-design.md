# HyperPet Design

## Goal

HyperPet is a Windows desktop pet app similar to CodexPet or Shimeji. It lives on the desktop and reacts when Windows notifications arrive, including app notifications such as Discord when Windows exposes their content.

The MVP focuses on a native Windows experience: a small animated pet, a notification bubble, permission setup, and basic user controls.

## Scope

### In Scope

- Native Windows desktop app.
- Transparent always-on-top pet window.
- Sprite-based pet animation.
- Windows notification listener integration.
- Full notification content display when the Windows API exposes it.
- Notification deduplication for the current app session.
- Drag-to-move pet.
- Right-click pet menu.
- First-run setup wizard for notification access.
- Settings persisted locally as JSON.
- Optional launch at Windows startup, off by default.

### Out of Scope

- Persistent notification history.
- Custom notification inbox.
- Discord-specific API integration.
- OCR or screen scraping.
- Live2D or 3D pet rendering.
- Sounds and center-screen interruption.
- Cross-platform support.

## Product Behavior

On first launch, HyperPet shows a setup wizard that explains why notification access is needed and helps the user grant Windows notification listener permission. If permission is missing later, the pet shows a setup bubble instead of failing silently.

When a new Windows notification appears, HyperPet reads the app name, title, body, timestamp, and related fields exposed by the Windows notification APIs. The pet performs a subtle alert animation near its current position, such as waving or hopping, and shows a speech bubble beside the pet.

The speech bubble shows the full notification content available to HyperPet. This is not guaranteed to be the full original message for every app, because Windows and individual apps may trim, hide, or withhold some notification content.

Clicking the pet or bubble attempts to activate the related notification or app. If activation is unavailable, the bubble dismisses.

## Architecture

### `HyperPet.App`

WPF application shell. Owns startup, dependency wiring, tray icon, settings window, first-run setup wizard, and the transparent pet window.

### `NotificationService`

Wraps Windows User Notification Listener APIs. Responsibilities:

- Request or check notification listener permission.
- Retrieve current active notifications.
- Emit new notification events.
- Normalize Windows notification objects into a HyperPet notification model.
- Avoid duplicate alerts during the current app session.

### `PetController`

Coordinates pet behavior. Responsibilities:

- Track pet state: idle, alert, waving, bubble visible, paused.
- Receive notification events.
- Choose the appropriate animation and bubble content.
- Respect pause and settings values.

### `SpriteRenderer`

Loads frame-based PNG sprite assets from `Assets/Pets/<pet-name>/`. Handles frame timing and animation selection. The MVP uses simple sprite sheets or ordered PNG frames so skins can be replaced later.

### `SettingsStore`

Reads and writes local JSON settings. Initial settings:

- Selected pet skin.
- Alert bubble duration.
- Whether alerts are paused.
- Whether full content display is enabled.
- Whether launch at Windows startup is enabled.

### `StartupService`

Handles optional Windows startup registration. The setting is off by default and controlled by the right-click menu or settings window.

## UI

### Pet Window

- Frameless transparent WPF window.
- Always on top.
- Draggable by the user.
- Positioned from saved settings when possible.
- Small enough to avoid blocking normal desktop work.

### Notification Bubble

- Appears beside the pet.
- Contains app name, notification title, body text, and timestamp when available.
- Auto-hides after the configured duration.
- Does not store notification content after dismissal.

### Right-Click Menu

- Pause alerts.
- Change pet.
- Settings.
- Start with Windows toggle.
- Quit.

### Setup Wizard

- Explains notification permission.
- Opens the relevant Windows settings flow when possible.
- Provides a test/check step so the user knows whether permission is active.

## Data Flow

1. `HyperPet.App` starts.
2. `SettingsStore` loads settings or creates defaults.
3. `NotificationService` checks listener permission.
4. If permission is missing, setup UI is shown.
5. When permission exists, `NotificationService` monitors active notifications.
6. New notifications are normalized and deduplicated.
7. `PetController` receives notification event.
8. `SpriteRenderer` plays alert animation.
9. Pet bubble displays available notification content.
10. Click attempts notification/app activation; failure dismisses the bubble.

## Error Handling

- Missing permission: show setup prompt.
- Notification content unavailable: show app name and any available title/body fields.
- Activation unavailable: dismiss bubble without crashing.
- Corrupt settings file: keep a backup copy and recreate defaults.
- Missing pet assets: fall back to the bundled default pet.

## Privacy

HyperPet displays full available notification content because this is the requested behavior. It does not save notification history in the MVP. Notification text exists only in memory while the bubble is visible and while the notification event is being processed.

The settings UI should make full content display explicit so users understand private messages may appear on screen.

## Testing

### Unit Tests

- Settings default creation.
- Settings load/save round trip.
- Corrupt settings fallback.
- Notification deduplication.
- Notification model normalization.
- Notification-to-pet-state mapping.

### Manual Tests

- First-run permission flow.
- Permission missing recovery.
- Discord notification appears.
- Bubble shows full content when Windows exposes it.
- Duplicate notification does not trigger repeated alerts.
- Pet drag works.
- Right-click menu works.
- Pause alerts suppresses pet reactions.
- Startup setting persists.
- App restart preserves settings.

## MVP Acceptance Criteria

- App builds and launches on Windows.
- Pet appears as transparent always-on-top desktop window.
- User can drag pet and open right-click menu.
- App can guide user through notification listener permission setup.
- New Windows notifications trigger subtle pet animation.
- Bubble displays full available notification content.
- Bubble click attempts to open related notification or app.
- App does not persist notification history.
- Settings persist across restarts.

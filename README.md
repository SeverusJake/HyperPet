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

## Notification Access

Windows may ask for notification access permission. Some apps hide or trim notification content before Windows exposes it, so HyperPet cannot guarantee that the available content is the original full notification text.

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

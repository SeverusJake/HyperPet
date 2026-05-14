# HyperPet MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Windows HyperPet MVP: a transparent draggable WPF desktop pet that reacts to Windows notifications and shows full available notification content.

**Architecture:** Use a WPF app for the desktop pet shell and small class libraries for testable core logic. Keep Windows notification access behind an interface so deduplication, settings, and pet state can be tested without Windows toast fixtures.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit, Windows App SDK/WinRT notification APIs where needed, JSON settings via `System.Text.Json`.

---

## File Structure

- Create `HyperPet.sln`: solution root.
- Create `src/HyperPet.App/HyperPet.App.csproj`: WPF app.
- Create `src/HyperPet.App/App.xaml`: WPF application entry.
- Create `src/HyperPet.App/App.xaml.cs`: startup wiring.
- Create `src/HyperPet.App/MainWindow.xaml`: transparent pet window and bubble UI.
- Create `src/HyperPet.App/MainWindow.xaml.cs`: drag, menu, animation binding, shutdown.
- Create `src/HyperPet.App/Assets/Pets/Default/idle_0.png`: bundled fallback pet frame.
- Create `src/HyperPet.App/Assets/Pets/Default/alert_0.png`: bundled alert pet frame.
- Create `src/HyperPet.App/ViewModels/MainWindowViewModel.cs`: binds pet state to WPF.
- Create `src/HyperPet.App/Views/SetupWindow.xaml`: permission setup wizard.
- Create `src/HyperPet.App/Views/SetupWindow.xaml.cs`: setup window behavior.
- Create `src/HyperPet.App/Views/SettingsWindow.xaml`: simple settings UI.
- Create `src/HyperPet.App/Views/SettingsWindow.xaml.cs`: settings window behavior.
- Create `src/HyperPet.Core/HyperPet.Core.csproj`: platform-neutral domain logic.
- Create `src/HyperPet.Core/Notifications/HyperNotification.cs`: normalized notification model.
- Create `src/HyperPet.Core/Notifications/NotificationDedupe.cs`: in-session dedupe.
- Create `src/HyperPet.Core/Pet/PetState.cs`: pet state enum.
- Create `src/HyperPet.Core/Pet/PetAlert.cs`: current alert model.
- Create `src/HyperPet.Core/Pet/PetController.cs`: maps notifications to pet state.
- Create `src/HyperPet.Core/Settings/HyperPetSettings.cs`: settings model.
- Create `src/HyperPet.Core/Settings/SettingsStore.cs`: JSON settings persistence.
- Create `src/HyperPet.Windows/HyperPet.Windows.csproj`: Windows-specific services.
- Create `src/HyperPet.Windows/Notifications/INotificationListener.cs`: notification listener interface.
- Create `src/HyperPet.Windows/Notifications/WindowsNotificationListener.cs`: Windows notification listener implementation.
- Create `src/HyperPet.Windows/Startup/StartupService.cs`: Windows startup toggle.
- Create `tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj`: xUnit test project.
- Create `tests/HyperPet.Core.Tests/Notifications/NotificationDedupeTests.cs`: dedupe tests.
- Create `tests/HyperPet.Core.Tests/Pet/PetControllerTests.cs`: notification-to-pet tests.
- Create `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`: settings tests.
- Create `README.md`: build, run, and permission notes.

---

### Task 1: Solution Scaffold

**Files:**
- Create: `HyperPet.sln`
- Create: `src/HyperPet.Core/HyperPet.Core.csproj`
- Create: `src/HyperPet.Windows/HyperPet.Windows.csproj`
- Create: `src/HyperPet.App/HyperPet.App.csproj`
- Create: `tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

Run:

```powershell
dotnet new sln -n HyperPet
dotnet new classlib -n HyperPet.Core -o src/HyperPet.Core -f net8.0
dotnet new classlib -n HyperPet.Windows -o src/HyperPet.Windows -f net8.0-windows10.0.19041.0
dotnet new wpf -n HyperPet.App -o src/HyperPet.App -f net8.0-windows
dotnet new xunit -n HyperPet.Core.Tests -o tests/HyperPet.Core.Tests -f net8.0
dotnet sln HyperPet.sln add src/HyperPet.Core/HyperPet.Core.csproj
dotnet sln HyperPet.sln add src/HyperPet.Windows/HyperPet.Windows.csproj
dotnet sln HyperPet.sln add src/HyperPet.App/HyperPet.App.csproj
dotnet sln HyperPet.sln add tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj
```

Expected: each command exits `0`, and `HyperPet.sln` lists four projects.

- [ ] **Step 2: Replace `src/HyperPet.Core/HyperPet.Core.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Replace `src/HyperPet.Windows/HyperPet.Windows.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWPF>false</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\HyperPet.Core\HyperPet.Core.csproj" />
    <PackageReference Include="Microsoft.Windows.SDK.NET.Ref" Version="10.0.22621.38" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Replace `src/HyperPet.App/HyperPet.App.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\HyperPet.Core\HyperPet.Core.csproj" />
    <ProjectReference Include="..\HyperPet.Windows\HyperPet.Windows.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Resource Include="Assets\Pets\Default\*.png" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Replace `tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="2.8.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HyperPet.Core\HyperPet.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Remove template files**

Delete:

```text
src/HyperPet.Core/Class1.cs
src/HyperPet.Windows/Class1.cs
tests/HyperPet.Core.Tests/UnitTest1.cs
```

- [ ] **Step 7: Build**

Run:

```powershell
dotnet build HyperPet.sln
```

Expected: `Build succeeded.`

- [ ] **Step 8: Commit**

```powershell
git add HyperPet.sln src tests
git commit -m "chore: scaffold HyperPet solution"
```

---

### Task 2: Settings Store

**Files:**
- Create: `src/HyperPet.Core/Settings/HyperPetSettings.cs`
- Create: `src/HyperPet.Core/Settings/SettingsStore.cs`
- Create: `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`:

```csharp
using HyperPet.Core.Settings;

namespace HyperPet.Core.Tests.Settings;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenFileMissing_ReturnsDefaultsAndCreatesFile()
    {
        var directory = CreateTempDirectory();
        var store = new SettingsStore(directory);

        var settings = await store.LoadAsync();

        Assert.Equal("Default", settings.SelectedPet);
        Assert.Equal(8, settings.AlertDurationSeconds);
        Assert.False(settings.AlertsPaused);
        Assert.True(settings.ShowFullNotificationContent);
        Assert.False(settings.StartWithWindows);
        Assert.True(File.Exists(Path.Combine(directory, "settings.json")));
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsValues()
    {
        var directory = CreateTempDirectory();
        var store = new SettingsStore(directory);
        var expected = new HyperPetSettings
        {
            SelectedPet = "PixelCat",
            AlertDurationSeconds = 12,
            AlertsPaused = true,
            ShowFullNotificationContent = false,
            StartWithWindows = true,
            PetLeft = 140,
            PetTop = 220
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected.SelectedPet, actual.SelectedPet);
        Assert.Equal(expected.AlertDurationSeconds, actual.AlertDurationSeconds);
        Assert.Equal(expected.AlertsPaused, actual.AlertsPaused);
        Assert.Equal(expected.ShowFullNotificationContent, actual.ShowFullNotificationContent);
        Assert.Equal(expected.StartWithWindows, actual.StartWithWindows);
        Assert.Equal(expected.PetLeft, actual.PetLeft);
        Assert.Equal(expected.PetTop, actual.PetTop);
    }

    [Fact]
    public async Task LoadAsync_WhenJsonCorrupt_BacksUpFileAndReturnsDefaults()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "{ broken json");
        var store = new SettingsStore(directory);

        var settings = await store.LoadAsync();

        Assert.Equal("Default", settings.SelectedPet);
        Assert.True(Directory.GetFiles(directory, "settings.json.corrupt-*").Length == 1);
    }

    private static string CreateTempDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "HyperPet.Tests", Guid.NewGuid().ToString("N"));
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj --filter SettingsStoreTests
```

Expected: FAIL because `HyperPet.Core.Settings` types do not exist.

- [ ] **Step 3: Create settings model**

Create `src/HyperPet.Core/Settings/HyperPetSettings.cs`:

```csharp
namespace HyperPet.Core.Settings;

public sealed class HyperPetSettings
{
    public string SelectedPet { get; set; } = "Default";
    public int AlertDurationSeconds { get; set; } = 8;
    public bool AlertsPaused { get; set; }
    public bool ShowFullNotificationContent { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public double PetLeft { get; set; } = 80;
    public double PetTop { get; set; } = 80;

    public static HyperPetSettings CreateDefault() => new();
}
```

- [ ] **Step 4: Create settings store**

Create `src/HyperPet.Core/Settings/SettingsStore.cs`:

```csharp
using System.Text.Json;

namespace HyperPet.Core.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public SettingsStore(string settingsDirectory)
    {
        Directory.CreateDirectory(settingsDirectory);
        _settingsPath = Path.Combine(settingsDirectory, "settings.json");
    }

    public async Task<HyperPetSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            var defaults = HyperPetSettings.CreateDefault();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<HyperPetSettings>(stream, JsonOptions, cancellationToken);
            return settings ?? HyperPetSettings.CreateDefault();
        }
        catch (JsonException)
        {
            var backupPath = Path.Combine(
                Path.GetDirectoryName(_settingsPath)!,
                $"settings.json.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
            File.Move(_settingsPath, backupPath);
            var defaults = HyperPetSettings.CreateDefault();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }
    }

    public async Task SaveAsync(HyperPetSettings settings, CancellationToken cancellationToken = default)
    {
        var clamped = new HyperPetSettings
        {
            SelectedPet = string.IsNullOrWhiteSpace(settings.SelectedPet) ? "Default" : settings.SelectedPet,
            AlertDurationSeconds = Math.Clamp(settings.AlertDurationSeconds, 3, 30),
            AlertsPaused = settings.AlertsPaused,
            ShowFullNotificationContent = settings.ShowFullNotificationContent,
            StartWithWindows = settings.StartWithWindows,
            PetLeft = settings.PetLeft,
            PetTop = settings.PetTop
        };

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, clamped, JsonOptions, cancellationToken);
    }
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj --filter SettingsStoreTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/HyperPet.Core/Settings tests/HyperPet.Core.Tests/Settings
git commit -m "feat: add settings store"
```

---

### Task 3: Notification Model And Dedupe

**Files:**
- Create: `src/HyperPet.Core/Notifications/HyperNotification.cs`
- Create: `src/HyperPet.Core/Notifications/NotificationDedupe.cs`
- Create: `tests/HyperPet.Core.Tests/Notifications/NotificationDedupeTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/HyperPet.Core.Tests/Notifications/NotificationDedupeTests.cs`:

```csharp
using HyperPet.Core.Notifications;

namespace HyperPet.Core.Tests.Notifications;

public sealed class NotificationDedupeTests
{
    [Fact]
    public void ShouldAlert_FirstTime_ReturnsTrue()
    {
        var dedupe = new NotificationDedupe();
        var notification = CreateNotification("discord-1");

        Assert.True(dedupe.ShouldAlert(notification));
    }

    [Fact]
    public void ShouldAlert_SameNotificationTwice_ReturnsFalseSecondTime()
    {
        var dedupe = new NotificationDedupe();
        var notification = CreateNotification("discord-1");

        Assert.True(dedupe.ShouldAlert(notification));
        Assert.False(dedupe.ShouldAlert(notification));
    }

    [Fact]
    public void ShouldAlert_SameContentDifferentSourceId_ReturnsTrue()
    {
        var dedupe = new NotificationDedupe();

        Assert.True(dedupe.ShouldAlert(CreateNotification("discord-1")));
        Assert.True(dedupe.ShouldAlert(CreateNotification("discord-2")));
    }

    private static HyperNotification CreateNotification(string id)
    {
        return new HyperNotification(
            id,
            "Discord",
            "Friend",
            "hello",
            DateTimeOffset.Parse("2026-05-14T10:00:00+07:00"),
            canActivate: true);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj --filter NotificationDedupeTests
```

Expected: FAIL because notification types do not exist.

- [ ] **Step 3: Create notification model**

Create `src/HyperPet.Core/Notifications/HyperNotification.cs`:

```csharp
namespace HyperPet.Core.Notifications;

public sealed record HyperNotification(
    string SourceId,
    string AppName,
    string Title,
    string Body,
    DateTimeOffset Timestamp,
    bool CanActivate);
```

- [ ] **Step 4: Create dedupe service**

Create `src/HyperPet.Core/Notifications/NotificationDedupe.cs`:

```csharp
namespace HyperPet.Core.Notifications;

public sealed class NotificationDedupe
{
    private readonly HashSet<string> _seenSourceIds = new(StringComparer.Ordinal);

    public bool ShouldAlert(HyperNotification notification)
    {
        return _seenSourceIds.Add(notification.SourceId);
    }
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj --filter NotificationDedupeTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/HyperPet.Core/Notifications tests/HyperPet.Core.Tests/Notifications
git commit -m "feat: add notification dedupe"
```

---

### Task 4: Pet Controller

**Files:**
- Create: `src/HyperPet.Core/Pet/PetState.cs`
- Create: `src/HyperPet.Core/Pet/PetAlert.cs`
- Create: `src/HyperPet.Core/Pet/PetController.cs`
- Create: `tests/HyperPet.Core.Tests/Pet/PetControllerTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/HyperPet.Core.Tests/Pet/PetControllerTests.cs`:

```csharp
using HyperPet.Core.Notifications;
using HyperPet.Core.Pet;
using HyperPet.Core.Settings;

namespace HyperPet.Core.Tests.Pet;

public sealed class PetControllerTests
{
    [Fact]
    public void HandleNotification_WhenAlertsEnabled_ShowsAlert()
    {
        var controller = new PetController();
        var settings = HyperPetSettings.CreateDefault();
        var notification = CreateNotification();

        var alert = controller.HandleNotification(notification, settings);

        Assert.Equal(PetState.Alerting, controller.State);
        Assert.NotNull(alert);
        Assert.Equal("Discord", alert.AppName);
        Assert.Equal("Friend", alert.Title);
        Assert.Equal("hello from discord", alert.Body);
        Assert.True(alert.CanActivate);
    }

    [Fact]
    public void HandleNotification_WhenAlertsPaused_ReturnsNullAndKeepsIdle()
    {
        var controller = new PetController();
        var settings = HyperPetSettings.CreateDefault();
        settings.AlertsPaused = true;

        var alert = controller.HandleNotification(CreateNotification(), settings);

        Assert.Null(alert);
        Assert.Equal(PetState.Idle, controller.State);
    }

    [Fact]
    public void HandleNotification_WhenFullContentDisabled_HidesTitleAndBody()
    {
        var controller = new PetController();
        var settings = HyperPetSettings.CreateDefault();
        settings.ShowFullNotificationContent = false;

        var alert = controller.HandleNotification(CreateNotification(), settings);

        Assert.NotNull(alert);
        Assert.Equal("Discord", alert.AppName);
        Assert.Equal("Notification", alert.Title);
        Assert.Equal(string.Empty, alert.Body);
    }

    [Fact]
    public void DismissAlert_ReturnsToIdle()
    {
        var controller = new PetController();
        controller.HandleNotification(CreateNotification(), HyperPetSettings.CreateDefault());

        controller.DismissAlert();

        Assert.Equal(PetState.Idle, controller.State);
        Assert.Null(controller.CurrentAlert);
    }

    private static HyperNotification CreateNotification()
    {
        return new HyperNotification(
            "discord-1",
            "Discord",
            "Friend",
            "hello from discord",
            DateTimeOffset.Parse("2026-05-14T10:00:00+07:00"),
            canActivate: true);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj --filter PetControllerTests
```

Expected: FAIL because pet types do not exist.

- [ ] **Step 3: Create pet state enum**

Create `src/HyperPet.Core/Pet/PetState.cs`:

```csharp
namespace HyperPet.Core.Pet;

public enum PetState
{
    Idle,
    Alerting
}
```

- [ ] **Step 4: Create pet alert model**

Create `src/HyperPet.Core/Pet/PetAlert.cs`:

```csharp
namespace HyperPet.Core.Pet;

public sealed record PetAlert(
    string AppName,
    string Title,
    string Body,
    DateTimeOffset Timestamp,
    bool CanActivate);
```

- [ ] **Step 5: Create pet controller**

Create `src/HyperPet.Core/Pet/PetController.cs`:

```csharp
using HyperPet.Core.Notifications;
using HyperPet.Core.Settings;

namespace HyperPet.Core.Pet;

public sealed class PetController
{
    public PetState State { get; private set; } = PetState.Idle;
    public PetAlert? CurrentAlert { get; private set; }

    public PetAlert? HandleNotification(HyperNotification notification, HyperPetSettings settings)
    {
        if (settings.AlertsPaused)
        {
            return null;
        }

        var title = settings.ShowFullNotificationContent ? notification.Title : "Notification";
        var body = settings.ShowFullNotificationContent ? notification.Body : string.Empty;

        CurrentAlert = new PetAlert(
            notification.AppName,
            title,
            body,
            notification.Timestamp,
            notification.CanActivate);
        State = PetState.Alerting;
        return CurrentAlert;
    }

    public void DismissAlert()
    {
        CurrentAlert = null;
        State = PetState.Idle;
    }
}
```

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj --filter PetControllerTests
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add src/HyperPet.Core/Pet tests/HyperPet.Core.Tests/Pet
git commit -m "feat: add pet controller"
```

---

### Task 5: Windows Notification Listener Wrapper

**Files:**
- Create: `src/HyperPet.Windows/Notifications/NotificationAccessStatus.cs`
- Create: `src/HyperPet.Windows/Notifications/INotificationListener.cs`
- Create: `src/HyperPet.Windows/Notifications/WindowsNotificationListener.cs`

- [ ] **Step 1: Create listener interface and status enum**

Create `src/HyperPet.Windows/Notifications/NotificationAccessStatus.cs`:

```csharp
namespace HyperPet.Windows.Notifications;

public enum NotificationAccessStatus
{
    Unspecified,
    Allowed,
    Denied
}
```

Create `src/HyperPet.Windows/Notifications/INotificationListener.cs`:

```csharp
using HyperPet.Core.Notifications;

namespace HyperPet.Windows.Notifications;

public interface INotificationListener
{
    Task<NotificationAccessStatus> RequestAccessAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HyperNotification>> GetActiveNotificationsAsync(CancellationToken cancellationToken = default);
    Task<bool> TryActivateAsync(HyperNotification notification, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create Windows implementation**

Create `src/HyperPet.Windows/Notifications/WindowsNotificationListener.cs`:

```csharp
using HyperPet.Core.Notifications;
using Windows.UI.Notifications.Management;

namespace HyperPet.Windows.Notifications;

public sealed class WindowsNotificationListener : INotificationListener
{
    private readonly UserNotificationListener _listener = UserNotificationListener.Current;

    public async Task<NotificationAccessStatus> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        var accessStatus = await _listener.RequestAccessAsync().AsTask(cancellationToken);
        return accessStatus switch
        {
            UserNotificationListenerAccessStatus.Allowed => NotificationAccessStatus.Allowed,
            UserNotificationListenerAccessStatus.Denied => NotificationAccessStatus.Denied,
            _ => NotificationAccessStatus.Unspecified
        };
    }

    public async Task<IReadOnlyList<HyperNotification>> GetActiveNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var notifications = await _listener.GetNotificationsAsync(NotificationKinds.Toast).AsTask(cancellationToken);
        return notifications.Select(notification =>
        {
            var appName = notification.AppInfo.DisplayInfo.DisplayName;
            var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            var text = binding?.GetTextElements().Select(element => element.Text).ToArray() ?? Array.Empty<string>();
            var title = text.Length > 0 ? text[0] : "Notification";
            var body = text.Length > 1 ? string.Join(Environment.NewLine, text.Skip(1)) : string.Empty;
            var sourceId = $"{notification.AppInfo.AppUserModelId}:{notification.Id}:{notification.CreationTime.ToUnixTimeMilliseconds()}";

            return new HyperNotification(
                sourceId,
                string.IsNullOrWhiteSpace(appName) ? notification.AppInfo.AppUserModelId : appName,
                title,
                body,
                notification.CreationTime,
                canActivate: true);
        }).ToArray();
    }

    public Task<bool> TryActivateAsync(HyperNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
```

- [ ] **Step 3: Build Windows project**

Run:

```powershell
dotnet build src/HyperPet.Windows/HyperPet.Windows.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```powershell
git add src/HyperPet.Windows/Notifications
git commit -m "feat: add Windows notification listener"
```

---

### Task 6: Startup Service

**Files:**
- Create: `src/HyperPet.Windows/Startup/StartupService.cs`

- [ ] **Step 1: Create startup service**

Create `src/HyperPet.Windows/Startup/StartupService.cs`:

```csharp
using Microsoft.Win32;

namespace HyperPet.Windows.Startup;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HyperPet";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{executablePath}\"");
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
```

- [ ] **Step 2: Build Windows project**

Run:

```powershell
dotnet build src/HyperPet.Windows/HyperPet.Windows.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```powershell
git add src/HyperPet.Windows/Startup
git commit -m "feat: add startup service"
```

---

### Task 7: WPF View Model

**Files:**
- Create: `src/HyperPet.App/ViewModels/MainWindowViewModel.cs`

- [ ] **Step 1: Create main window view model**

Create `src/HyperPet.App/ViewModels/MainWindowViewModel.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HyperPet.Core.Pet;

namespace HyperPet.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private PetState _petState = PetState.Idle;
    private PetAlert? _currentAlert;
    private bool _isBubbleVisible;
    private bool _alertsPaused;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PetState PetState
    {
        get => _petState;
        set => SetField(ref _petState, value);
    }

    public PetAlert? CurrentAlert
    {
        get => _currentAlert;
        set
        {
            if (SetField(ref _currentAlert, value))
            {
                IsBubbleVisible = value is not null;
            }
        }
    }

    public bool IsBubbleVisible
    {
        get => _isBubbleVisible;
        set => SetField(ref _isBubbleVisible, value);
    }

    public bool AlertsPaused
    {
        get => _alertsPaused;
        set => SetField(ref _alertsPaused, value);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
```

- [ ] **Step 2: Build app project**

Run:

```powershell
dotnet build src/HyperPet.App/HyperPet.App.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```powershell
git add src/HyperPet.App/ViewModels
git commit -m "feat: add pet window view model"
```

---

### Task 8: Pet Window UI

**Files:**
- Modify: `src/HyperPet.App/MainWindow.xaml`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Replace main window XAML**

Replace `src/HyperPet.App/MainWindow.xaml`:

```xml
<Window x:Class="HyperPet.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="HyperPet"
        Width="320"
        Height="220"
        AllowsTransparency="True"
        Background="Transparent"
        WindowStyle="None"
        ResizeMode="NoResize"
        Topmost="True"
        ShowInTaskbar="False">
    <Grid MouseLeftButtonDown="OnMouseLeftButtonDown">
        <Grid.ContextMenu>
            <ContextMenu>
                <MenuItem x:Name="PauseAlertsMenuItem"
                          Header="Pause alerts"
                          IsCheckable="True"
                          Click="OnPauseAlertsClick" />
                <MenuItem Header="Settings" Click="OnSettingsClick" />
                <Separator />
                <MenuItem Header="Quit" Click="OnQuitClick" />
            </ContextMenu>
        </Grid.ContextMenu>

        <Border x:Name="Bubble"
                HorizontalAlignment="Right"
                VerticalAlignment="Top"
                MaxWidth="210"
                Padding="10"
                CornerRadius="8"
                Background="#F7FFFFFF"
                BorderBrush="#33000000"
                BorderThickness="1"
                Visibility="Collapsed">
            <StackPanel>
                <TextBlock x:Name="BubbleAppName"
                           FontWeight="SemiBold"
                           Foreground="#202124"
                           TextWrapping="Wrap" />
                <TextBlock x:Name="BubbleTitle"
                           Margin="0,4,0,0"
                           Foreground="#202124"
                           TextWrapping="Wrap" />
                <TextBlock x:Name="BubbleBody"
                           Margin="0,4,0,0"
                           Foreground="#4A4D52"
                           TextWrapping="Wrap" />
            </StackPanel>
        </Border>

        <Border Width="96"
                Height="96"
                HorizontalAlignment="Left"
                VerticalAlignment="Bottom"
                CornerRadius="48"
                Background="#FFFFD166"
                BorderBrush="#FF1F2937"
                BorderThickness="3">
            <TextBlock x:Name="PetFace"
                       Text="^_^"
                       FontSize="28"
                       FontWeight="Bold"
                       Foreground="#FF1F2937"
                       HorizontalAlignment="Center"
                       VerticalAlignment="Center" />
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Replace code-behind with drag/menu behavior**

Replace `src/HyperPet.App/MainWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using HyperPet.App.ViewModels;
using HyperPet.Core.Pet;

namespace HyperPet.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    public void ShowAlert(PetAlert alert)
    {
        _viewModel.CurrentAlert = alert;
        _viewModel.PetState = PetState.Alerting;
        BubbleAppName.Text = alert.AppName;
        BubbleTitle.Text = alert.Title;
        BubbleBody.Text = alert.Body;
        Bubble.Visibility = Visibility.Visible;
        PetFace.Text = "!";
    }

    public void DismissAlert()
    {
        _viewModel.CurrentAlert = null;
        _viewModel.PetState = PetState.Idle;
        Bubble.Visibility = Visibility.Collapsed;
        PetFace.Text = "^_^";
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            DismissAlert();
            return;
        }

        DragMove();
    }

    private void OnPauseAlertsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.AlertsPaused = PauseAlertsMenuItem.IsChecked;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new Views.SettingsWindow { Owner = this };
        settingsWindow.ShowDialog();
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
```

- [ ] **Step 3: Build app project**

Run:

```powershell
dotnet build src/HyperPet.App/HyperPet.App.csproj
```

Expected: build fails because `Views.SettingsWindow` does not exist.

- [ ] **Step 4: Commit after Task 9, not now**

No commit in this task. Task 9 completes missing settings window.

---

### Task 9: Settings And Setup Windows

**Files:**
- Create: `src/HyperPet.App/Views/SettingsWindow.xaml`
- Create: `src/HyperPet.App/Views/SettingsWindow.xaml.cs`
- Create: `src/HyperPet.App/Views/SetupWindow.xaml`
- Create: `src/HyperPet.App/Views/SetupWindow.xaml.cs`

- [ ] **Step 1: Create settings window XAML**

Create `src/HyperPet.App/Views/SettingsWindow.xaml`:

```xml
<Window x:Class="HyperPet.App.Views.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="HyperPet Settings"
        Width="360"
        Height="260"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize">
    <StackPanel Margin="18">
        <TextBlock Text="Settings" FontSize="18" FontWeight="SemiBold" />
        <CheckBox x:Name="ShowFullContentCheckBox"
                  Margin="0,16,0,0"
                  Content="Show full notification content"
                  IsChecked="True" />
        <CheckBox x:Name="StartWithWindowsCheckBox"
                  Margin="0,10,0,0"
                  Content="Start HyperPet with Windows" />
        <TextBlock Margin="0,16,0,4" Text="Alert duration seconds" />
        <Slider x:Name="AlertDurationSlider"
                Minimum="3"
                Maximum="30"
                Value="8"
                TickFrequency="1"
                IsSnapToTickEnabled="True" />
        <Button Margin="0,20,0,0"
                Width="90"
                HorizontalAlignment="Right"
                Content="Close"
                Click="OnCloseClick" />
    </StackPanel>
</Window>
```

- [ ] **Step 2: Create settings code-behind**

Create `src/HyperPet.App/Views/SettingsWindow.xaml.cs`:

```csharp
using System.Windows;

namespace HyperPet.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
```

- [ ] **Step 3: Create setup window XAML**

Create `src/HyperPet.App/Views/SetupWindow.xaml`:

```xml
<Window x:Class="HyperPet.App.Views.SetupWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="HyperPet Setup"
        Width="420"
        Height="260"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize">
    <StackPanel Margin="20">
        <TextBlock Text="Enable notifications"
                   FontSize="20"
                   FontWeight="SemiBold" />
        <TextBlock Margin="0,12,0,0"
                   TextWrapping="Wrap"
                   Text="HyperPet needs Windows notification access so the pet can react to app messages. Full notification content may appear on screen." />
        <Button x:Name="RequestAccessButton"
                Margin="0,20,0,0"
                Width="170"
                HorizontalAlignment="Left"
                Content="Request access"
                Click="OnRequestAccessClick" />
        <TextBlock x:Name="StatusText"
                   Margin="0,14,0,0"
                   Text="Permission not checked." />
        <Button Margin="0,20,0,0"
                Width="90"
                HorizontalAlignment="Right"
                Content="Close"
                Click="OnCloseClick" />
    </StackPanel>
</Window>
```

- [ ] **Step 4: Create setup code-behind**

Create `src/HyperPet.App/Views/SetupWindow.xaml.cs`:

```csharp
using System.Windows;
using HyperPet.Windows.Notifications;

namespace HyperPet.App.Views;

public partial class SetupWindow : Window
{
    private readonly Func<Task<NotificationAccessStatus>> _requestAccessAsync;

    public SetupWindow(Func<Task<NotificationAccessStatus>>? requestAccessAsync = null)
    {
        InitializeComponent();
        _requestAccessAsync = requestAccessAsync ?? (() => Task.FromResult(NotificationAccessStatus.Unspecified));
    }

    private async void OnRequestAccessClick(object sender, RoutedEventArgs e)
    {
        RequestAccessButton.IsEnabled = false;
        try
        {
            var status = await _requestAccessAsync();
            StatusText.Text = status == NotificationAccessStatus.Allowed
                ? "Permission enabled. HyperPet can watch notifications."
                : "Permission is not enabled. Check Windows notification access settings.";
        }
        finally
        {
            RequestAccessButton.IsEnabled = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
```

- [ ] **Step 5: Build app project**

Run:

```powershell
dotnet build src/HyperPet.App/HyperPet.App.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```powershell
git add src/HyperPet.App/MainWindow.xaml src/HyperPet.App/MainWindow.xaml.cs src/HyperPet.App/Views
git commit -m "feat: add pet window and setup views"
```

---

### Task 10: App Startup Wiring

**Files:**
- Modify: `src/HyperPet.App/App.xaml`
- Modify: `src/HyperPet.App/App.xaml.cs`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Replace `App.xaml`**

```xml
<Application x:Class="HyperPet.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources />
</Application>
```

- [ ] **Step 2: Replace `App.xaml.cs`**

```csharp
using System.Windows;
using HyperPet.App.Views;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pet;
using HyperPet.Core.Settings;
using HyperPet.Windows.Notifications;

namespace HyperPet.App;

public partial class App : Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private SettingsStore? _settingsStore;
    private HyperPetSettings? _settings;
    private PetController? _petController;
    private NotificationDedupe? _dedupe;
    private INotificationListener? _notificationListener;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HyperPet");
        _settingsStore = new SettingsStore(settingsDirectory);
        _settings = await _settingsStore.LoadAsync(_shutdown.Token);
        _petController = new PetController();
        _dedupe = new NotificationDedupe();
        _notificationListener = new WindowsNotificationListener();

        _mainWindow = new MainWindow();
        _mainWindow.Left = _settings.PetLeft;
        _mainWindow.Top = _settings.PetTop;
        _mainWindow.Show();

        var access = await _notificationListener.RequestAccessAsync(_shutdown.Token);
        if (access != NotificationAccessStatus.Allowed)
        {
            new SetupWindow(() => _notificationListener.RequestAccessAsync(_shutdown.Token))
            {
                Owner = _mainWindow
            }.Show();
            return;
        }

        _ = MonitorNotificationsAsync(_shutdown.Token);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        if (_settingsStore is not null && _settings is not null && _mainWindow is not null)
        {
            _settings.PetLeft = _mainWindow.Left;
            _settings.PetTop = _mainWindow.Top;
            await _settingsStore.SaveAsync(_settings);
        }

        _shutdown.Dispose();
        base.OnExit(e);
    }

    private async Task MonitorNotificationsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var notifications = await _notificationListener!.GetActiveNotificationsAsync(cancellationToken);
            foreach (var notification in notifications)
            {
                if (!_dedupe!.ShouldAlert(notification))
                {
                    continue;
                }

                var alert = _petController!.HandleNotification(notification, _settings!);
                if (alert is not null)
                {
                    _mainWindow!.ShowAlert(alert);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}
```

- [ ] **Step 3: Add window close-position save hook**

Modify `src/HyperPet.App/MainWindow.xaml.cs` constructor to include:

```csharp
public MainWindow()
{
    InitializeComponent();
    DataContext = _viewModel;
}
```

No code change is needed if constructor already matches.

- [ ] **Step 4: Build solution**

Run:

```powershell
dotnet build HyperPet.sln
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```powershell
git add src/HyperPet.App/App.xaml src/HyperPet.App/App.xaml.cs src/HyperPet.App/MainWindow.xaml.cs
git commit -m "feat: wire notification monitoring"
```

---

### Task 11: Save Settings From UI And Startup Toggle

**Files:**
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`
- Modify: `src/HyperPet.App/App.xaml.cs`

- [ ] **Step 1: Replace settings window code-behind**

Replace `src/HyperPet.App/Views/SettingsWindow.xaml.cs`:

```csharp
using System.Windows;
using HyperPet.Core.Settings;

namespace HyperPet.App.Views;

public partial class SettingsWindow : Window
{
    private readonly HyperPetSettings _settings;
    private readonly Action<bool> _applyStartupSetting;

    public SettingsWindow(HyperPetSettings settings, Action<bool> applyStartupSetting)
    {
        InitializeComponent();
        _settings = settings;
        _applyStartupSetting = applyStartupSetting;
        ShowFullContentCheckBox.IsChecked = settings.ShowFullNotificationContent;
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        AlertDurationSlider.Value = settings.AlertDurationSeconds;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _settings.ShowFullNotificationContent = ShowFullContentCheckBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _settings.AlertDurationSeconds = (int)AlertDurationSlider.Value;
        _applyStartupSetting(_settings.StartWithWindows);
        DialogResult = true;
        Close();
    }
}
```

- [ ] **Step 2: Replace main window constructor and settings opening**

Modify `src/HyperPet.App/MainWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using HyperPet.App.ViewModels;
using HyperPet.Core.Pet;
using HyperPet.Core.Settings;

namespace HyperPet.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly HyperPetSettings _settings;
    private readonly Action<bool> _applyStartupSetting;

    public MainWindow(HyperPetSettings settings, Action<bool> applyStartupSetting)
    {
        InitializeComponent();
        _settings = settings;
        _applyStartupSetting = applyStartupSetting;
        DataContext = _viewModel;
        PauseAlertsMenuItem.IsChecked = settings.AlertsPaused;
    }

    public void ShowAlert(PetAlert alert)
    {
        _viewModel.CurrentAlert = alert;
        _viewModel.PetState = PetState.Alerting;
        BubbleAppName.Text = alert.AppName;
        BubbleTitle.Text = alert.Title;
        BubbleBody.Text = alert.Body;
        Bubble.Visibility = Visibility.Visible;
        PetFace.Text = "!";
    }

    public void DismissAlert()
    {
        _viewModel.CurrentAlert = null;
        _viewModel.PetState = PetState.Idle;
        Bubble.Visibility = Visibility.Collapsed;
        PetFace.Text = "^_^";
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            DismissAlert();
            return;
        }

        DragMove();
    }

    private void OnPauseAlertsClick(object sender, RoutedEventArgs e)
    {
        _settings.AlertsPaused = PauseAlertsMenuItem.IsChecked;
        _viewModel.AlertsPaused = _settings.AlertsPaused;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new Views.SettingsWindow(_settings, _applyStartupSetting) { Owner = this };
        settingsWindow.ShowDialog();
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
```

- [ ] **Step 3: Update app startup constructor call**

In `src/HyperPet.App/App.xaml.cs`, add this field:

```csharp
private HyperPet.Windows.Startup.StartupService? _startupService;
```

Then after `_notificationListener = new WindowsNotificationListener();`, add:

```csharp
_startupService = new HyperPet.Windows.Startup.StartupService();
```

Then change:

```csharp
_mainWindow = new MainWindow();
```

to:

```csharp
_mainWindow = new MainWindow(_settings, enabled =>
{
    var executablePath = Environment.ProcessPath ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(executablePath))
    {
        _startupService.SetEnabled(enabled, executablePath);
    }
});
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build HyperPet.sln
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```powershell
git add src/HyperPet.App
git commit -m "feat: persist user settings from UI"
```

---

### Task 12: Default Pet Assets

**Files:**
- Create: `src/HyperPet.App/Assets/Pets/Default/idle_0.png`
- Create: `src/HyperPet.App/Assets/Pets/Default/alert_0.png`

- [ ] **Step 1: Generate simple PNG assets**

Run this PowerShell script:

```powershell
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path "src\HyperPet.App\Assets\Pets\Default" | Out-Null

function New-PetImage($path, $face) {
  $bmp = New-Object System.Drawing.Bitmap 96,96
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)
  $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,255,209,102))
  $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255,31,41,55)), 4
  $g.FillEllipse($brush, 6, 6, 84, 84)
  $g.DrawEllipse($pen, 6, 6, 84, 84)
  $font = New-Object System.Drawing.Font "Arial", 22, ([System.Drawing.FontStyle]::Bold)
  $textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,31,41,55))
  $format = New-Object System.Drawing.StringFormat
  $format.Alignment = [System.Drawing.StringAlignment]::Center
  $format.LineAlignment = [System.Drawing.StringAlignment]::Center
  $rect = New-Object System.Drawing.RectangleF 0,0,96,96
  $g.DrawString($face, $font, $textBrush, $rect, $format)
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose()
  $bmp.Dispose()
}

New-PetImage "src\HyperPet.App\Assets\Pets\Default\idle_0.png" "^_^"
New-PetImage "src\HyperPet.App\Assets\Pets\Default\alert_0.png" "!"
```

Expected: two PNG files exist.

- [ ] **Step 2: Build app**

Run:

```powershell
dotnet build src/HyperPet.App/HyperPet.App.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```powershell
git add src/HyperPet.App/Assets
git commit -m "feat: add default pet assets"
```

---

### Task 13: README And Manual Test Checklist

**Files:**
- Create: `README.md`

- [ ] **Step 1: Create README**

Create `README.md`:

```markdown
# HyperPet

HyperPet is a Windows desktop pet app. The pet sits on the desktop, watches Windows notifications, and shows a bubble with the notification content Windows exposes.

## Requirements

- Windows 10 build 19041 or newer
- .NET 8 SDK

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

## Notification Permission

HyperPet uses Windows notification listener APIs. Windows may ask for permission before HyperPet can read notifications. Some apps may hide or trim notification content, so HyperPet displays the full content available through Windows, not guaranteed full original message text.

## Manual MVP Checks

- Pet appears as a transparent always-on-top desktop window.
- Pet can be dragged.
- Right-click menu opens.
- Settings window opens.
- Permission setup appears when access is missing.
- Discord or another app notification triggers alert bubble.
- Bubble shows app name, title, body when available.
- Duplicate active notification does not repeatedly alert.
- App restart preserves pet position and settings.
```

- [ ] **Step 2: Run full verification**

Run:

```powershell
dotnet test HyperPet.sln
dotnet build HyperPet.sln
```

Expected: both commands complete successfully.

- [ ] **Step 3: Commit**

```powershell
git add README.md
git commit -m "docs: add HyperPet README"
```

---

### Task 14: MVP Smoke Run

**Files:**
- Modify only files needed to fix compile/runtime issues found during smoke test.

- [ ] **Step 1: Run app**

Run:

```powershell
dotnet run --project src/HyperPet.App/HyperPet.App.csproj
```

Expected:

- Pet window appears.
- Setup window appears if notification access is missing.
- App does not crash.

- [ ] **Step 2: Manual interaction checks**

Perform:

- Drag pet window.
- Right-click pet and open menu.
- Open settings.
- Toggle full content display.
- Close app.
- Restart app.
- Confirm pet position and settings persist.

- [ ] **Step 3: Notification check**

Trigger a Windows notification from any app. If Discord is available, use Discord. Expected:

- Pet face changes to alert state.
- Bubble appears.
- Bubble contains app name and available notification title/body.
- Duplicate polling does not repeatedly re-alert for the same notification.

- [ ] **Step 4: Commit smoke fixes**

If fixes were needed:

```powershell
git add src tests README.md
git commit -m "fix: stabilize MVP smoke run"
```

If no fixes were needed:

```powershell
git status --short
```

Expected: no tracked changes from smoke run.

---

## Self-Review

- Spec coverage: plan covers native WPF app, transparent pet window, sprite assets, Windows notification listener, full available content display, in-session dedupe, drag/right-click menu, setup wizard, JSON settings, startup service, no notification history, privacy note, tests, and manual Discord-style checks.
- Red-flag scan: no open implementation blanks remain in required tasks.
- Type consistency: `HyperNotification`, `HyperPetSettings`, `PetController`, `PetAlert`, and `MainWindow` signatures match across tasks.

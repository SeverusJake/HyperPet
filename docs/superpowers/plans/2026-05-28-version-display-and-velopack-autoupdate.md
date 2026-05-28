# Version Display + Velopack Auto-Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the app version in-app (About tab + context menu) and add a confirm-gated Velopack auto-update with an optional check-on-launch toggle.

**Architecture:** A static `AppVersion` reads the assembly's informational version. An `UpdateService` wraps Velopack's `UpdateManager`/`GithubSource`, guarding against non-installed dev runs. `VelopackApp.Build().Run()` runs first in `App.OnStartup`. MainWindow owns the `UpdateService`, hosts the single confirm-download-restart flow, runs the launch auto-check when enabled, and hands both the service and the confirm callback to the Settings dialog's About tab. Releases switch from raw-zip to `vpk pack` output.

**Tech Stack:** C# / .NET 8 WPF (net8.0-windows10.0.19041.0), Velopack NuGet, GitHub Releases as the update source, xUnit.

**Spec:** `docs/superpowers/specs/2026-05-28-version-display-and-velopack-autoupdate-design.md`

**Verified Velopack API (from velopack/velopack source):**
- `new GithubSource(string repoUrl, string? accessToken, bool prerelease, IFileDownloader? downloader = null)`
- `new UpdateManager(IUpdateSource source, UpdateOptions? options = null, ILogger? logger = null)`
- `UpdateManager.IsInstalled` → `bool`
- `UpdateManager.CurrentVersion` → `SemanticVersion?`
- `Task<UpdateInfo?> CheckForUpdatesAsync()` (null = up to date)
- `Task DownloadUpdatesAsync(UpdateInfo updates, Action<int>? progress = null, CancellationToken = default)`
- `void ApplyUpdatesAndRestart(VelopackAsset? toApply, string[]? restartArgs = null)` — `UpdateInfo` implicitly converts to `VelopackAsset`, so pass the `UpdateInfo` directly
- `UpdateInfo.TargetFullRelease.Version` → `SemanticVersion`
- `VelopackApp.Build().Run()` — call first at process start

---

## File Structure

**Create:**
- `src/HyperPet.App/AppVersion.cs` — static version accessor.
- `src/HyperPet.App/Update/UpdateService.cs` — Velopack wrapper.
- `tests/HyperPet.App.Tests/AppVersionTests.cs` — version-parse tests.

**Modify:**
- `src/HyperPet.App/HyperPet.App.csproj` — add Velopack PackageReference.
- `src/HyperPet.App/App.xaml.cs` — `VelopackApp.Build().Run()`, build `UpdateService`, pass to MainWindow.
- `src/HyperPet.App/MainWindow.xaml` — context-menu version header.
- `src/HyperPet.App/MainWindow.xaml.cs` — own `UpdateService`, launch auto-check, shared `PromptAndApplyUpdateAsync`, pass to SettingsWindow.
- `src/HyperPet.App/Views/SettingsWindow.xaml` — General Auto-update checkbox; new About tab; title binding.
- `src/HyperPet.App/Views/SettingsWindow.xaml.cs` — About tab logic, AutoUpdate load/commit, title.
- `src/HyperPet.Core/Settings/HyperPetSettings.cs` — `AutoUpdate` bool.
- `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs` — AutoUpdate default + round-trip asserts.

---

### Task 1: `AppVersion` helper + tests

**Files:**
- Create: `src/HyperPet.App/AppVersion.cs`
- Create: `tests/HyperPet.App.Tests/AppVersionTests.cs`

- [ ] **Step 1: Create the helper**

`src/HyperPet.App/AppVersion.cs`:

```csharp
using System.Reflection;

namespace HyperPet.App;

/// <summary>
/// Exposes the running assembly's version for display in the UI.
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// The informational version (e.g. "0.3.8"), with any "+&lt;gitsha&gt;"
    /// build-metadata suffix stripped. Falls back to the assembly version,
    /// then "0.0.0".
    /// </summary>
    public static string Current { get; } = Resolve();

    /// <summary>"HyperPet v0.3.8" — for the context menu header and About tab.</summary>
    public static string DisplayString => $"HyperPet v{Current}";

    public static string Normalize(string? informational, string? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informational))
        {
            int plus = informational.IndexOf('+');
            string trimmed = plus >= 0 ? informational[..plus] : informational;
            trimmed = trimmed.Trim();
            if (trimmed.Length > 0)
            {
                return trimmed;
            }
        }

        if (!string.IsNullOrWhiteSpace(assemblyVersion))
        {
            return assemblyVersion!.Trim();
        }

        return "0.0.0";
    }

    private static string Resolve()
    {
        Assembly asm = typeof(AppVersion).Assembly;
        string? informational = asm
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        string? assemblyVersion = asm.GetName().Version?.ToString();
        return Normalize(informational, assemblyVersion);
    }
}
```

- [ ] **Step 2: Write the failing test**

`tests/HyperPet.App.Tests/AppVersionTests.cs`:

```csharp
using HyperPet.App;
using Xunit;

namespace HyperPet.App.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("0.3.8", null, "0.3.8")]
    [InlineData("0.3.8+abc123", null, "0.3.8")]
    [InlineData("  0.3.8  ", null, "0.3.8")]
    [InlineData(null, "0.3.8.0", "0.3.8.0")]
    [InlineData("", "1.2.3.0", "1.2.3.0")]
    [InlineData(null, null, "0.0.0")]
    public void Normalize_StripsBuildMetadataAndFallsBack(string? informational, string? assemblyVersion, string expected)
    {
        Assert.Equal(expected, AppVersion.Normalize(informational, assemblyVersion));
    }

    [Fact]
    public void DisplayString_HasHyperPetPrefix()
    {
        Assert.StartsWith("HyperPet v", AppVersion.DisplayString);
    }
}
```

- [ ] **Step 3: Run to verify pass**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj -c Release -nologo`
Expected: PASS — new AppVersion tests green, existing tests still green.

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/AppVersion.cs tests/HyperPet.App.Tests/AppVersionTests.cs
git commit -m "feat: AppVersion helper for in-app version display"
```

---

### Task 2: Add Velopack package + bootstrap

**Files:**
- Modify: `src/HyperPet.App/HyperPet.App.csproj`
- Modify: `src/HyperPet.App/App.xaml.cs`

- [ ] **Step 1: Add the Velopack PackageReference**

In `src/HyperPet.App/HyperPet.App.csproj`, inside the `ItemGroup` that holds `<PackageReference Include="SixLabors.ImageSharp" ... />`, add:

```xml
    <PackageReference Include="Velopack" Version="0.0.1053" />
```

So the group reads:

```xml
  <ItemGroup>
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.12" />
    <PackageReference Include="Velopack" Version="0.0.1053" />
  </ItemGroup>
```

- [ ] **Step 2: Restore to confirm the package resolves**

Run: `dotnet restore src/HyperPet.App/HyperPet.App.csproj`
Expected: restore succeeds, no NU1101/NU1102 errors. (If 0.0.1053 is unavailable, run `dotnet add src/HyperPet.App/HyperPet.App.csproj package Velopack` to pin the latest and keep that version.)

- [ ] **Step 3: Bootstrap Velopack as the first action in OnStartup**

In `src/HyperPet.App/App.xaml.cs`, add the using near the other usings:

```csharp
using Velopack;
```

Then make `VelopackApp.Build().Run()` the very first statement of `OnStartup`, before `base.OnStartup(e)`:

```csharp
    protected override async void OnStartup(StartupEventArgs e)
    {
        // MUST be the first thing the process does — wires Velopack's
        // install / uninstall / first-run / restart hooks. Late placement
        // breaks the update lifecycle.
        VelopackApp.Build().Run();

        base.OnStartup(e);
```

- [ ] **Step 4: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/HyperPet.App/HyperPet.App.csproj src/HyperPet.App/App.xaml.cs
git commit -m "feat: add Velopack package and bootstrap VelopackApp at startup"
```

---

### Task 3: `UpdateService` wrapper

**Files:**
- Create: `src/HyperPet.App/Update/UpdateService.cs`

- [ ] **Step 1: Create the service**

`src/HyperPet.App/Update/UpdateService.cs`:

```csharp
using HyperPet.Core.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace HyperPet.App.Update;

/// <summary>
/// Thin wrapper over Velopack's <see cref="UpdateManager"/> pointed at the
/// HyperPet GitHub releases. Guards against dev / non-installed runs where
/// updates are not possible.
/// </summary>
public sealed class UpdateService
{
    private const string RepoUrl = "https://github.com/SeverusJake/HyperPet";

    private readonly HyperPetLogger? _logger;
    private readonly UpdateManager _manager;

    public UpdateService(HyperPetLogger? logger = null)
    {
        _logger = logger;
        // Public repo: no access token, stable releases only (prerelease=false).
        var source = new GithubSource(RepoUrl, null, false);
        _manager = new UpdateManager(source);
    }

    /// <summary>
    /// False when running from a raw build / dotnet run (not installed via
    /// the Velopack Setup.exe). Callers skip update checks when false.
    /// </summary>
    public bool IsSupported => _manager.IsInstalled;

    /// <summary>The currently installed version, or null on dev builds.</summary>
    public string? CurrentVersion => _manager.CurrentVersion?.ToString();

    /// <summary>
    /// Returns the available update, or null when already up to date.
    /// Throws on network / source errors — the caller decides whether to
    /// surface or swallow.
    /// </summary>
    public Task<UpdateInfo?> CheckAsync() => _manager.CheckForUpdatesAsync();

    /// <summary>
    /// Downloads the update then applies it and restarts the app. Does not
    /// return on success (the process is replaced).
    /// </summary>
    public async Task DownloadAndApplyAsync(UpdateInfo info, Action<int>? progress = null)
    {
        _logger?.Info($"UpdateService: downloading update to v{info.TargetFullRelease.Version}");
        await _manager.DownloadUpdatesAsync(info, progress);
        _logger?.Info("UpdateService: applying update and restarting");
        _manager.ApplyUpdatesAndRestart(info);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors. (If `Velopack.Sources` namespace is wrong for the pinned version, fix the using — `GithubSource` lives in `Velopack.Sources` in current releases.)

- [ ] **Step 3: Commit**

```bash
git add src/HyperPet.App/Update/UpdateService.cs
git commit -m "feat: UpdateService wrapping Velopack UpdateManager for GitHub releases"
```

---

### Task 4: `AutoUpdate` setting + persistence tests

**Files:**
- Modify: `src/HyperPet.Core/Settings/HyperPetSettings.cs`
- Modify: `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`

- [ ] **Step 1: Add the setting**

In `src/HyperPet.Core/Settings/HyperPetSettings.cs`, add after the `DebugMode` property (before `MessagingApps`):

```csharp
    /// <summary>
    /// When true, HyperPet checks GitHub for a newer release once at launch
    /// and prompts to install it. Manual checks via the About tab work
    /// regardless of this flag. Off by default.
    /// </summary>
    public bool AutoUpdate { get; set; } = false;
```

- [ ] **Step 2: Add the default assertion to the load test**

In `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`, in the first test (the one asserting defaults, right after `Assert.True(settings.OpenAppOnBubbleClick);`), add:

```csharp
        Assert.False(settings.AutoUpdate);
```

- [ ] **Step 3: Add AutoUpdate to the round-trip test**

In the same file, in `SaveAsync_ThenLoadAsync_RoundTripsValues`, set `AutoUpdate = true,` in the object initializer (next to `OpenAppOnBubbleClick = true,`):

```csharp
            OpenAppOnBubbleClick = true,
            AutoUpdate = true,
```

and after `Assert.True(actual.OpenAppOnBubbleClick);` add:

```csharp
        Assert.True(actual.AutoUpdate);
```

- [ ] **Step 4: Run the Core tests**

Run: `dotnet test tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj -c Release -nologo`
Expected: PASS — defaults test sees `AutoUpdate == false`, round-trip sees `true`.

- [ ] **Step 5: Commit**

```bash
git add src/HyperPet.Core/Settings/HyperPetSettings.cs tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs
git commit -m "feat: AutoUpdate setting with persistence tests"
```

---

### Task 5: Context-menu version header

**Files:**
- Modify: `src/HyperPet.App/MainWindow.xaml`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Add a named, disabled header item to the context menu**

In `src/HyperPet.App/MainWindow.xaml`, change the `ContextMenu` block so it starts with a disabled version item and a separator:

```xml
            <ContextMenu>
                <MenuItem x:Name="VersionMenuItem"
                          IsEnabled="False" />
                <Separator />
                <MenuItem x:Name="PauseAlertsMenuItem"
                          Header="Pause alerts"
                          IsCheckable="True"
                          Click="OnPauseAlertsClick" />
                <MenuItem Header="Settings"
                          Click="OnSettingsClick" />
                <Separator />
                <MenuItem Header="Quit"
                          Click="OnQuitClick" />
            </ContextMenu>
```

- [ ] **Step 2: Set the header text from AppVersion in the constructor**

In `src/HyperPet.App/MainWindow.xaml.cs`, right after `PauseAlertsMenuItem.IsChecked = settings.AlertsPaused;`, add:

```csharp
        VersionMenuItem.Header = AppVersion.DisplayString;
```

(`AppVersion` is in the `HyperPet.App` namespace, same as `MainWindow`, so no using is needed.)

- [ ] **Step 3: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/MainWindow.xaml src/HyperPet.App/MainWindow.xaml.cs
git commit -m "feat: show app version in pet context menu header"
```

---

### Task 6: MainWindow owns UpdateService + shared update flow + launch auto-check

**Files:**
- Modify: `src/HyperPet.App/App.xaml.cs`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Build the UpdateService in App and pass it to MainWindow**

In `src/HyperPet.App/App.xaml.cs`, add the using:

```csharp
using HyperPet.App.Update;
```

Just before the `_mainWindow = new MainWindow(` line, create the service:

```csharp
        var updateService = new UpdateService(_logger);
```

Add `updateService` as the final constructor argument:

```csharp
        _mainWindow = new MainWindow(
            _settings,
            ApplyStartupSetting,
            SaveSettings,
            spritePet,
            originalStateFps,
            originalStatePlayMode,
            appLauncher,
            SetPollInterval,
            PollSoon,
            debugSimulator,
            ApplyMonitoringSettings,
            updateService)
```

- [ ] **Step 2: Accept the service in MainWindow and add the field**

In `src/HyperPet.App/MainWindow.xaml.cs`, add the using:

```csharp
using HyperPet.App.Update;
using Velopack;
```

Add a field next to the other readonly fields (after `_applyMonitoringSettings`):

```csharp
    private readonly UpdateService? _updateService;
```

Add the parameter to the constructor signature (after `applyMonitoringSettings`):

```csharp
        Action? applyMonitoringSettings = null,
        UpdateService? updateService = null)
```

Assign it in the constructor body (after `_applyMonitoringSettings = applyMonitoringSettings;`):

```csharp
        _updateService = updateService;
```

- [ ] **Step 3: Run the launch auto-check from the Loaded handler**

In `src/HyperPet.App/MainWindow.xaml.cs`, the constructor has:

```csharp
        Loaded += (_, _) =>
        {
            ClampToWorkArea();
            ApplyDebugOverlayVisibility();
        };
```

Change it to also kick off the auto-check:

```csharp
        Loaded += (_, _) =>
        {
            ClampToWorkArea();
            ApplyDebugOverlayVisibility();
            _ = MaybeAutoCheckForUpdateAsync();
        };
```

- [ ] **Step 4: Add the auto-check + shared confirm/apply methods**

Add these methods to `MainWindow` (place them just before `OnQuitClick`):

```csharp
    private async Task MaybeAutoCheckForUpdateAsync()
    {
        // Decision: auto-update default OFF; when ON, check once at launch.
        // Startup failures are silent (logged) — only manual checks show errors.
        if (!_settings.AutoUpdate || _updateService is null || !_updateService.IsSupported)
        {
            return;
        }

        try
        {
            UpdateInfo? info = await _updateService.CheckAsync();
            if (info is not null)
            {
                await PromptAndApplyUpdateAsync(info);
            }
        }
        catch (Exception)
        {
            // Silent on launch (offline, GitHub hiccup, etc.).
        }
    }

    /// <summary>
    /// Shared confirm → download → apply → restart flow used by both the
    /// launch auto-check and the About tab's manual check. Returns only if
    /// the user declines or the update fails (success restarts the process).
    /// </summary>
    public async Task PromptAndApplyUpdateAsync(UpdateInfo info)
    {
        if (_updateService is null)
        {
            return;
        }

        string newVersion = info.TargetFullRelease.Version.ToString();
        MessageBoxResult choice = MessageBox.Show(
            this,
            $"HyperPet v{newVersion} is available. Download and update now?\n\nThe app will restart to finish.",
            "HyperPet Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (choice != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _updateService.DownloadAndApplyAsync(info);
            // ApplyUpdatesAndRestart replaces the process; code below won't run.
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"The update could not be installed.\n\n{exception.Message}",
                "HyperPet Update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
```

- [ ] **Step 5: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/HyperPet.App/App.xaml.cs src/HyperPet.App/MainWindow.xaml.cs
git commit -m "feat: launch auto-check and shared confirm/apply update flow"
```

---

### Task 7: Settings — General Auto-update toggle, About tab, title

**Files:**
- Modify: `src/HyperPet.App/MainWindow.xaml.cs` (pass service + callback to SettingsWindow)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml`
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs`

- [ ] **Step 1: Pass the service + confirm callback into SettingsWindow**

In `src/HyperPet.App/MainWindow.xaml.cs`, `OnSettingsClick` currently constructs:

```csharp
        var settingsWindow = new SettingsWindow(
            _settings,
            _applyStartupSetting,
            RefreshFromSettings,
            _spritePet,
            _originalStateFps,
            _originalStatePlayMode)
        {
            Owner = this
        };
```

Change to add the update service + prompt callback:

```csharp
        var settingsWindow = new SettingsWindow(
            _settings,
            _applyStartupSetting,
            RefreshFromSettings,
            _spritePet,
            _originalStateFps,
            _originalStatePlayMode,
            _updateService,
            PromptAndApplyUpdateAsync)
        {
            Owner = this
        };
```

- [ ] **Step 2: Add the General-tab Auto-update checkbox**

In `src/HyperPet.App/Views/SettingsWindow.xaml`, inside the General tab's `StackPanel`, add after the `ReactToInAppNotificationsCheckBox`:

```xml
                        <CheckBox x:Name="AutoUpdateCheckBox"
                                  Margin="0,8,0,0"
                                  Content="Auto-update (check on launch)" />
```

- [ ] **Step 3: Add the About tab**

In the same XAML, add a new `TabItem` as the LAST tab inside the `TabControl` (after the Advanced `TabItem`):

```xml
            <!-- ============================== ABOUT ============================= -->
            <TabItem Header="About">
                <StackPanel Margin="12">
                    <TextBlock x:Name="AboutVersionText"
                               FontSize="14"
                               FontWeight="SemiBold" />
                    <TextBlock Margin="0,8,0,0"
                               Foreground="#666"
                               Text="A desktop pet that reacts to your notifications." />

                    <Button x:Name="CheckUpdateButton"
                            Margin="0,16,0,0"
                            Padding="12,4"
                            HorizontalAlignment="Left"
                            Content="Check for update"
                            Click="OnCheckForUpdateClick" />

                    <TextBlock x:Name="UpdateStatusText"
                               Margin="0,10,0,0"
                               TextWrapping="Wrap"
                               Foreground="#444" />
                </StackPanel>
            </TabItem>
```

- [ ] **Step 4: Add fields, constructor params, version/title, and AutoUpdate load**

In `src/HyperPet.App/Views/SettingsWindow.xaml.cs`, add the using:

```csharp
using HyperPet.App.Update;
using Velopack;
```

Add fields next to `_originalStatePlayMode`:

```csharp
    private readonly UpdateService? _updateService;
    private readonly Func<UpdateInfo, Task>? _promptAndApply;
```

Extend the constructor signature (after `originalStatePlayMode`):

```csharp
        IReadOnlyDictionary<string, PlayMode>? originalStatePlayMode = null,
        UpdateService? updateService = null,
        Func<UpdateInfo, Task>? promptAndApply = null)
```

Assign them right after `_originalStatePlayMode = originalStatePlayMode;`:

```csharp
        _updateService = updateService;
        _promptAndApply = promptAndApply;
```

After `InitializeComponent();` and the existing checkbox initializers, add the AutoUpdate load and the version/title:

```csharp
        AutoUpdateCheckBox.IsChecked = settings.AutoUpdate;
```

and (anywhere after `InitializeComponent()`, e.g. right after the AutoUpdate line):

```csharp
        Title = $"Settings - {AppVersion.DisplayString}";
        AboutVersionText.Text = AppVersion.DisplayString;
```

- [ ] **Step 5: Wire AutoUpdate into dirty tracking**

In `WireDirtyTracking()`, next to the other `*.Click += OnAnyChange;` lines, add:

```csharp
        AutoUpdateCheckBox.Click += OnAnyChange;
```

- [ ] **Step 6: Persist AutoUpdate on commit**

In `CommitChanges()`, after the `if (!applied) { return false; }` block and before `ApplyStateSpeedChanges();`, add:

```csharp
        _settings.AutoUpdate = AutoUpdateCheckBox.IsChecked == true;
```

- [ ] **Step 7: Reset AutoUpdate in the Default button**

In `OnDefaultClick`, next to the other checkbox resets, add:

```csharp
        AutoUpdateCheckBox.IsChecked = false;
```

- [ ] **Step 8: Implement the About-tab manual check**

Add this handler to `SettingsWindow` (place it after `OnUnselectAllAppsClick`):

```csharp
    private async void OnCheckForUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_updateService is null || !_updateService.IsSupported)
        {
            UpdateStatusText.Text = "Updates are disabled for development builds.";
            return;
        }

        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking…";

        try
        {
            UpdateInfo? info = await _updateService.CheckAsync();
            if (info is null)
            {
                UpdateStatusText.Text = "You're on the latest version.";
            }
            else
            {
                string v = info.TargetFullRelease.Version.ToString();
                UpdateStatusText.Text = $"Update available: v{v}";
                if (_promptAndApply is not null)
                {
                    await _promptAndApply(info);
                }
            }
        }
        catch (Exception exception)
        {
            UpdateStatusText.Text = $"Could not check for updates: {exception.Message}";
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }
```

- [ ] **Step 9: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 10: Run all tests**

Run: `dotnet test -c Release -nologo`
Expected: all pass (30 prior + AppVersion theory cases).

- [ ] **Step 11: Commit**

```bash
git add src/HyperPet.App/MainWindow.xaml.cs src/HyperPet.App/Views/SettingsWindow.xaml src/HyperPet.App/Views/SettingsWindow.xaml.cs
git commit -m "feat: About tab with manual update check + General auto-update toggle"
```

---

### Task 8: Packaging migration to Velopack + release v0.4.0

**Files:** none (build / release process). This task documents and executes the new release pipeline. Run after Tasks 1-7 are merged and a manual smoke test passes.

- [ ] **Step 1: Bump version to 0.4.0**

In `src/HyperPet.App/HyperPet.App.csproj`, replace every `0.3.7` with `0.4.0` (Version, AssemblyVersion `0.4.0.0`, FileVersion `0.4.0.0`, InformationalVersion `0.4.0`).

- [ ] **Step 2: Install the Velopack CLI**

Run: `dotnet tool install -g vpk`
Expected: "Tool 'vpk' ... was successfully installed." (If already installed: `dotnet tool update -g vpk`.)

- [ ] **Step 3: Publish to a plain folder (NOT single-file)**

Run:
```bash
dotnet publish src/HyperPet.App/HyperPet.App.csproj -c Release -r win-x64 --self-contained true -o publish/velopack/0.4.0 -nologo
```
Expected: publish output in `publish/velopack/0.4.0`, including `HyperPet.exe`.

- [ ] **Step 4: Pack with Velopack**

Run:
```bash
vpk pack --packId HyperPet --packVersion 0.4.0 --packDir publish/velopack/0.4.0 --mainExe HyperPet.exe
```
Expected: a `Releases/` folder containing `HyperPet-win-Setup.exe`, `HyperPet-0.4.0-full.nupkg`, and `releases.win.json`.

- [ ] **Step 5: Commit the version bump + push**

```bash
git add src/HyperPet.App/HyperPet.App.csproj
git commit -m "$(cat <<'EOF'
feat: version display + Velopack auto-update (v0.4.0)

Adds an About tab (version + Check for update), a context-menu version
header, and a General-tab Auto-update toggle (off by default) that checks
GitHub once at launch and prompts before installing. Update delivery moves
to Velopack: releases now ship Setup.exe + nupkg + releases.win.json.
EOF
)"
git push origin main
```

- [ ] **Step 6: Tag + push tag**

```bash
git tag -a v0.4.0 -m "HyperPet v0.4.0"
git push origin v0.4.0
```

- [ ] **Step 7: Create the GitHub release with Velopack assets**

```bash
gh release create v0.4.0 \
  "Releases/HyperPet-win-Setup.exe" \
  "Releases/HyperPet-0.4.0-full.nupkg" \
  "Releases/releases.win.json" \
  --title "HyperPet v0.4.0" \
  --notes "$(cat <<'EOF'
## Version display + auto-update

- New **About** tab in Settings shows the version and a **Check for update** button.
- App version now appears in the pet's right-click menu.
- New **Auto-update (check on launch)** toggle on the General tab (off by default). When on, HyperPet checks GitHub once at startup and asks before downloading. Every update is confirmed by you.

## Installing this release

This release switches to the Velopack updater. **Download and run `HyperPet-win-Setup.exe`** once. It installs HyperPet for the current user (no admin prompt). After this, future updates download as small deltas and apply with one click.

(The old zip layout is replaced — install via Setup.exe, not by unzipping.)
EOF
)"
```
Expected: release URL printed; the three Velopack assets attached.

- [ ] **Step 8: Smoke-test the update channel**

After v0.4.0 is published, later publish a v0.4.1 the same way (Steps 1-7 with 0.4.1). Then: install v0.4.0 via Setup.exe, launch with Auto-update ON → confirm the prompt appears, accept, and the app restarts on v0.4.1. Repeat with Auto-update OFF using the About tab's **Check for update** button. Verify offline launch is silent and an offline manual check shows an error.

---

## Self-Review

**1. Spec coverage:**
- Version in About tab + context menu → Tasks 1, 5, 7.
- Check for update button (manual) → Task 7 (Step 8).
- General Auto-update checkbox, default OFF, check at launch → Tasks 4, 6, 7.
- Confirm before download/apply, both paths → Task 6 (`PromptAndApplyUpdateAsync`), reused by Task 7.
- Velopack engine, GithubSource, bootstrap → Tasks 2, 3.
- Dev-build guard (`IsSupported`) → Task 3, used in Tasks 6 & 7.
- Startup failure silent, manual shows error → Task 6 (catch swallow) and Task 7 (catch → status text).
- Settings persistence of AutoUpdate → Task 4 (model + tests), Task 7 (load/commit).
- Packaging migration + release → Task 8.
- Settings title shows version → Task 7 (Step 4).
- Tests: AppVersion parse (Task 1), AutoUpdate round-trip (Task 4). Velopack itself not unit-tested (matches spec). No gaps.

**2. Placeholder scan:** No TODO/TBD/"similar to"/"add error handling" placeholders. Every code step shows full code. Velopack package version `0.0.1053` has an explicit fallback instruction (Task 2 Step 2) if unavailable.

**3. Type consistency:**
- `UpdateService` ctor `(HyperPetLogger? logger = null)` — called as `new UpdateService(_logger)` (Task 6).
- `UpdateService.IsSupported` / `CheckAsync()` / `DownloadAndApplyAsync(UpdateInfo, Action<int>?)` — used consistently in Tasks 6 & 7.
- `MainWindow.PromptAndApplyUpdateAsync(UpdateInfo)` (public) — passed as `Func<UpdateInfo, Task>` to SettingsWindow (Task 7 Step 1 & 4); signatures match.
- `AppVersion.DisplayString` / `AppVersion.Normalize(string?, string?)` — defined Task 1, used Tasks 5 & 7.
- `AutoUpdateCheckBox` — created Task 7 Step 2, referenced Steps 4-8 and in MainWindow none (correct; only SettingsWindow touches it).
- MainWindow ctor gains `UpdateService? updateService = null` as the last optional param (Task 6); App passes it positionally last (Task 6 Step 1). Consistent.

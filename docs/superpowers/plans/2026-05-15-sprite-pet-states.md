# Sprite Pet States Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Load the Miku Kimono pet from its current `pet.json + spritesheet.webp` folder and animate its states in the WPF pet window.

**Architecture:** Add platform-neutral metadata parsing in `HyperPet.Core`, WPF sprite loading/rendering in `HyperPet.App`, and settings for Calm/Desktop behavior. The current notification flow remains, but alert display switches to the `waving` animation and returns to the selected behavior mode.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit, `SixLabors.ImageSharp` for WebP decode/cropping.

---

## File Structure

- Modify `src/HyperPet.App/Assets/Pets/miku-kimono.codex-pet/pet.json`: add frame/state metadata.
- Modify `src/HyperPet.App/HyperPet.App.csproj`: include pet folders recursively and add ImageSharp package references.
- Create `src/HyperPet.Core/Pets/PetBehaviorMode.cs`: `Calm`, `Desktop`.
- Create `src/HyperPet.Core/Pets/PetAnimationState.cs`: state metadata model.
- Create `src/HyperPet.Core/Pets/PetDefinition.cs`: pet metadata model.
- Create `src/HyperPet.Core/Pets/PetDefinitionLoader.cs`: parse pet JSON and fallback state lookup.
- Modify `src/HyperPet.Core/Settings/HyperPetSettings.cs`: add `PetBehaviorMode`.
- Modify `src/HyperPet.Core/Settings/SettingsStore.cs`: persist/sanitize `PetBehaviorMode`.
- Create `tests/HyperPet.Core.Tests/Pets/PetDefinitionLoaderTests.cs`: metadata parsing/fallback tests.
- Modify `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`: settings mode default/persistence tests.
- Create `src/HyperPet.App/Pets/SpritePet.cs`: WPF frame container.
- Create `src/HyperPet.App/Pets/SpritePetLoader.cs`: decode WebP and crop frames to `BitmapSource`.
- Create `src/HyperPet.App/Pets/PetAnimator.cs`: animation timer and movement state.
- Modify `src/HyperPet.App/MainWindow.xaml`: replace text pet with `Image`.
- Modify `src/HyperPet.App/MainWindow.xaml.cs`: drive sprite animation and behavior modes.
- Modify `src/HyperPet.App/Views/SettingsWindow.xaml`: add behavior mode selector.
- Modify `src/HyperPet.App/Views/SettingsWindow.xaml.cs`: read/write behavior mode.

---

### Task 1: Pet Metadata And Settings

**Files:**
- Modify: `src/HyperPet.App/Assets/Pets/miku-kimono.codex-pet/pet.json`
- Create: `src/HyperPet.Core/Pets/PetBehaviorMode.cs`
- Create: `src/HyperPet.Core/Pets/PetAnimationState.cs`
- Create: `src/HyperPet.Core/Pets/PetDefinition.cs`
- Create: `src/HyperPet.Core/Pets/PetDefinitionLoader.cs`
- Modify: `src/HyperPet.Core/Settings/HyperPetSettings.cs`
- Modify: `src/HyperPet.Core/Settings/SettingsStore.cs`
- Test: `tests/HyperPet.Core.Tests/Pets/PetDefinitionLoaderTests.cs`
- Test: `tests/HyperPet.Core.Tests/Settings/SettingsStoreTests.cs`

- [ ] **Step 1: Add pet metadata**

Update `src/HyperPet.App/Assets/Pets/miku-kimono.codex-pet/pet.json` to:

```json
{
  "id": "miku-kimono",
  "displayName": "Miku Kimono",
  "description": "Miku-inspired kimono chibi with lively hand, foot, blink, face, and hair animations.",
  "spritesheetPath": "spritesheet.webp",
  "kind": "person",
  "frameWidth": 128,
  "frameHeight": 208,
  "states": {
    "idle": { "row": 0, "frames": 12, "fps": 8, "loop": true },
    "runRight": { "row": 1, "frames": 12, "fps": 12, "loop": true },
    "runLeft": { "row": 2, "frames": 12, "fps": 12, "loop": true },
    "waving": { "row": 3, "frames": 12, "fps": 8, "loop": false },
    "jumping": { "row": 4, "frames": 12, "fps": 10, "loop": false },
    "failed": { "row": 5, "frames": 12, "fps": 8, "loop": false },
    "waiting": { "row": 6, "frames": 12, "fps": 6, "loop": true },
    "running": { "row": 7, "frames": 12, "fps": 12, "loop": true },
    "review": { "row": 8, "frames": 12, "fps": 8, "loop": false }
  }
}
```

- [ ] **Step 2: Write failing pet metadata tests**

Create `tests/HyperPet.Core.Tests/Pets/PetDefinitionLoaderTests.cs`:

```csharp
using HyperPet.Core.Pets;

namespace HyperPet.Core.Tests.Pets;

public sealed class PetDefinitionLoaderTests
{
    [Fact]
    public async Task LoadAsync_ReadsSpriteSheetMetadataAndStates()
    {
        string directory = CreatePetDirectory("""
        {
          "id": "miku-kimono",
          "displayName": "Miku Kimono",
          "spritesheetPath": "spritesheet.webp",
          "frameWidth": 128,
          "frameHeight": 208,
          "states": {
            "idle": { "row": 0, "frames": 12, "fps": 8, "loop": true },
            "waving": { "row": 3, "frames": 12, "fps": 8, "loop": false }
          }
        }
        """);

        PetDefinition pet = await PetDefinitionLoader.LoadAsync(directory);

        Assert.Equal("miku-kimono", pet.Id);
        Assert.Equal("Miku Kimono", pet.DisplayName);
        Assert.Equal("spritesheet.webp", pet.SpritesheetPath);
        Assert.Equal(128, pet.FrameWidth);
        Assert.Equal(208, pet.FrameHeight);
        Assert.Equal(3, pet.GetState("waving").Row);
        Assert.False(pet.GetState("waving").Loop);
    }

    [Fact]
    public async Task GetState_WhenRequestedStateMissing_FallsBackToIdle()
    {
        string directory = CreatePetDirectory("""
        {
          "id": "miku-kimono",
          "displayName": "Miku Kimono",
          "spritesheetPath": "spritesheet.webp",
          "frameWidth": 128,
          "frameHeight": 208,
          "states": {
            "idle": { "row": 0, "frames": 12, "fps": 8, "loop": true }
          }
        }
        """);

        PetDefinition pet = await PetDefinitionLoader.LoadAsync(directory);

        Assert.Equal(0, pet.GetState("missing").Row);
    }

    private static string CreatePetDirectory(string json)
    {
        string directory = Path.Combine(Path.GetTempPath(), "HyperPet.Pets", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "pet.json"), json);
        return directory;
    }
}
```

- [ ] **Step 3: Run failing tests**

Run:

```powershell
dotnet test tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj --filter PetDefinitionLoaderTests
```

Expected: fail because `HyperPet.Core.Pets` does not exist.

- [ ] **Step 4: Add core pet models**

Create `src/HyperPet.Core/Pets/PetBehaviorMode.cs`:

```csharp
namespace HyperPet.Core.Pets;

public enum PetBehaviorMode
{
    Calm,
    Desktop
}
```

Create `src/HyperPet.Core/Pets/PetAnimationState.cs`:

```csharp
namespace HyperPet.Core.Pets;

public sealed class PetAnimationState
{
    public int Row { get; set; }
    public int Frames { get; set; }
    public int Fps { get; set; }
    public bool Loop { get; set; } = true;
}
```

Create `src/HyperPet.Core/Pets/PetDefinition.cs`:

```csharp
namespace HyperPet.Core.Pets;

public sealed class PetDefinition
{
    public string Id { get; init; } = "default";
    public string DisplayName { get; init; } = "Default";
    public string Description { get; init; } = string.Empty;
    public string SpritesheetPath { get; init; } = string.Empty;
    public string Kind { get; init; } = "person";
    public int FrameWidth { get; init; }
    public int FrameHeight { get; init; }
    public IReadOnlyDictionary<string, PetAnimationState> States { get; init; } =
        new Dictionary<string, PetAnimationState>(StringComparer.OrdinalIgnoreCase);

    public PetAnimationState GetState(string stateName)
    {
        if (States.TryGetValue(stateName, out PetAnimationState? state))
        {
            return state;
        }

        if (States.TryGetValue("idle", out PetAnimationState? idle))
        {
            return idle;
        }

        return States.Values.First();
    }
}
```

Create `src/HyperPet.Core/Pets/PetDefinitionLoader.cs`:

```csharp
using System.Text.Json;

namespace HyperPet.Core.Pets;

public static class PetDefinitionLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<PetDefinition> LoadAsync(string petDirectory)
    {
        string jsonPath = Path.Combine(petDirectory, "pet.json");
        await using FileStream stream = File.OpenRead(jsonPath);
        PetDefinitionFile file = await JsonSerializer.DeserializeAsync<PetDefinitionFile>(stream, JsonOptions)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Pet metadata is empty.");

        var states = new Dictionary<string, PetAnimationState>(file.States, StringComparer.OrdinalIgnoreCase);
        if (states.Count == 0)
        {
            throw new InvalidOperationException("Pet metadata must define at least one animation state.");
        }

        return new PetDefinition
        {
            Id = string.IsNullOrWhiteSpace(file.Id) ? Path.GetFileName(petDirectory) : file.Id,
            DisplayName = string.IsNullOrWhiteSpace(file.DisplayName) ? file.Id : file.DisplayName,
            Description = file.Description ?? string.Empty,
            SpritesheetPath = file.SpritesheetPath,
            Kind = string.IsNullOrWhiteSpace(file.Kind) ? "person" : file.Kind,
            FrameWidth = file.FrameWidth,
            FrameHeight = file.FrameHeight,
            States = states
        };
    }

    private sealed class PetDefinitionFile
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string SpritesheetPath { get; set; } = string.Empty;
        public string Kind { get; set; } = "person";
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public Dictionary<string, PetAnimationState> States { get; set; } = new();
    }
}
```

- [ ] **Step 5: Add behavior mode to settings**

Modify `src/HyperPet.Core/Settings/HyperPetSettings.cs`:

```csharp
using HyperPet.Core.Pets;

namespace HyperPet.Core.Settings;

public sealed class HyperPetSettings
{
    public string SelectedPet { get; set; } = "miku-kimono";
    public PetBehaviorMode PetBehaviorMode { get; set; } = PetBehaviorMode.Calm;
    public int AlertDurationSeconds { get; set; } = 8;
    public bool AlertsPaused { get; set; }
    public bool ShowFullNotificationContent { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public double PetLeft { get; set; } = 80;
    public double PetTop { get; set; } = 80;

    public static HyperPetSettings CreateDefault() => new();
}
```

In `SettingsStore.Sanitize`, copy `PetBehaviorMode` and normalize unknown enum values:

```csharp
PetBehaviorMode = Enum.IsDefined(settings.PetBehaviorMode)
    ? settings.PetBehaviorMode
    : HyperPet.Core.Pets.PetBehaviorMode.Calm,
```

- [ ] **Step 6: Add settings tests**

Extend `SettingsStoreTests`:

```csharp
Assert.Equal(HyperPet.Core.Pets.PetBehaviorMode.Calm, settings.PetBehaviorMode);
```

Set `PetBehaviorMode = HyperPet.Core.Pets.PetBehaviorMode.Desktop` in the round-trip test and assert it round-trips.

- [ ] **Step 7: Run tests and commit**

Run:

```powershell
dotnet test tests/HyperPet.Core.Tests/HyperPet.Core.Tests.csproj
dotnet build HyperPet.sln
```

Commit:

```powershell
git add src/HyperPet.Core tests/HyperPet.Core.Tests src/HyperPet.App/Assets/Pets/miku-kimono.codex-pet/pet.json
git commit -m "feat: add sprite pet metadata"
```

---

### Task 2: WPF Sprite Loading

**Files:**
- Modify: `src/HyperPet.App/HyperPet.App.csproj`
- Create: `src/HyperPet.App/Pets/SpritePet.cs`
- Create: `src/HyperPet.App/Pets/SpritePetLoader.cs`

- [ ] **Step 1: Add packages and resource include**

Modify `src/HyperPet.App/HyperPet.App.csproj`:

```xml
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.7" />
```

Add recursive pet resources:

```xml
<Resource Include="Assets\Pets\**\*.*" />
```

Remove or avoid duplicate `Assets\Pets\Default\*.png` include if build warns about duplicate resources.

- [ ] **Step 2: Create sprite container**

Create `src/HyperPet.App/Pets/SpritePet.cs`:

```csharp
using System.Windows.Media.Imaging;
using HyperPet.Core.Pets;

namespace HyperPet.App.Pets;

public sealed class SpritePet
{
    public SpritePet(PetDefinition definition, IReadOnlyDictionary<string, IReadOnlyList<BitmapSource>> frames)
    {
        Definition = definition;
        Frames = frames;
    }

    public PetDefinition Definition { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<BitmapSource>> Frames { get; }

    public IReadOnlyList<BitmapSource> GetFrames(string stateName)
    {
        if (Frames.TryGetValue(stateName, out IReadOnlyList<BitmapSource>? frames))
        {
            return frames;
        }

        if (Frames.TryGetValue("idle", out IReadOnlyList<BitmapSource>? idleFrames))
        {
            return idleFrames;
        }

        return Frames.Values.First();
    }
}
```

- [ ] **Step 3: Create WebP sprite loader**

Create `src/HyperPet.App/Pets/SpritePetLoader.cs`:

```csharp
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HyperPet.Core.Pets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace HyperPet.App.Pets;

public static class SpritePetLoader
{
    public static async Task<SpritePet> LoadAsync(string petDirectory)
    {
        PetDefinition definition = await PetDefinitionLoader.LoadAsync(petDirectory).ConfigureAwait(false);
        string spritesheetPath = Path.Combine(petDirectory, definition.SpritesheetPath);

        using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(spritesheetPath).ConfigureAwait(false);
        var frames = new Dictionary<string, IReadOnlyList<BitmapSource>>(StringComparer.OrdinalIgnoreCase);

        foreach ((string stateName, PetAnimationState state) in definition.States)
        {
            var stateFrames = new List<BitmapSource>();
            for (int frame = 0; frame < state.Frames; frame++)
            {
                int x = frame * definition.FrameWidth;
                int y = state.Row * definition.FrameHeight;
                stateFrames.Add(CreateFrame(image, x, y, definition.FrameWidth, definition.FrameHeight));
            }

            frames[stateName] = stateFrames;
        }

        return new SpritePet(definition, frames);
    }

    private static BitmapSource CreateFrame(Image<Rgba32> image, int x, int y, int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        image.ProcessPixelRows(accessor =>
        {
            for (int row = 0; row < height; row++)
            {
                Span<Rgba32> sourceRow = accessor.GetRowSpan(y + row).Slice(x, width);
                int destinationOffset = row * width * 4;
                for (int column = 0; column < width; column++)
                {
                    Rgba32 pixel = sourceRow[column];
                    pixels[destinationOffset + column * 4 + 0] = pixel.B;
                    pixels[destinationOffset + column * 4 + 1] = pixel.G;
                    pixels[destinationOffset + column * 4 + 2] = pixel.R;
                    pixels[destinationOffset + column * 4 + 3] = pixel.A;
                }
            }
        });

        BitmapSource bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        bitmap.Freeze();
        return bitmap;
    }
}
```

- [ ] **Step 4: Build and commit**

Run:

```powershell
dotnet build src/HyperPet.App/HyperPet.App.csproj
dotnet build HyperPet.sln
```

Commit:

```powershell
git add src/HyperPet.App
git commit -m "feat: load sprite pet assets"
```

---

### Task 3: Sprite Animation In Main Window

**Files:**
- Create: `src/HyperPet.App/Pets/PetAnimator.cs`
- Modify: `src/HyperPet.App/MainWindow.xaml`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`
- Modify: `src/HyperPet.App/App.xaml.cs`

- [ ] **Step 1: Create animator**

Create `src/HyperPet.App/Pets/PetAnimator.cs`:

```csharp
using System.Windows.Threading;
using HyperPet.Core.Pets;

namespace HyperPet.App.Pets;

public sealed class PetAnimator
{
    private readonly SpritePet _pet;
    private readonly Action<System.Windows.Media.Imaging.BitmapSource> _setFrame;
    private readonly DispatcherTimer _frameTimer = new();
    private string _stateName = "idle";
    private int _frameIndex;

    public PetAnimator(SpritePet pet, Action<System.Windows.Media.Imaging.BitmapSource> setFrame)
    {
        _pet = pet;
        _setFrame = setFrame;
        _frameTimer.Tick += (_, _) => AdvanceFrame();
    }

    public string StateName => _stateName;

    public void Play(string stateName)
    {
        _stateName = stateName;
        _frameIndex = 0;
        PetAnimationState state = _pet.Definition.GetState(stateName);
        _frameTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, state.Fps));
        SetCurrentFrame();
        _frameTimer.Start();
    }

    public void Stop()
    {
        _frameTimer.Stop();
    }

    private void AdvanceFrame()
    {
        IReadOnlyList<System.Windows.Media.Imaging.BitmapSource> frames = _pet.GetFrames(_stateName);
        PetAnimationState state = _pet.Definition.GetState(_stateName);
        _frameIndex++;
        if (_frameIndex >= frames.Count)
        {
            _frameIndex = state.Loop ? 0 : frames.Count - 1;
        }

        SetCurrentFrame();
    }

    private void SetCurrentFrame()
    {
        IReadOnlyList<System.Windows.Media.Imaging.BitmapSource> frames = _pet.GetFrames(_stateName);
        _setFrame(frames[Math.Clamp(_frameIndex, 0, frames.Count - 1)]);
    }
}
```

- [ ] **Step 2: Replace pet visual with image**

In `MainWindow.xaml`, replace the `Border` containing `PetFace` with:

```xml
<Image x:Name="PetImage"
       Width="128"
       Height="208"
       HorizontalAlignment="Right"
       VerticalAlignment="Bottom"
       Stretch="None"
       SnapsToDevicePixels="True"
       RenderOptions.BitmapScalingMode="NearestNeighbor" />
```

Set window size to enough for sprite and bubble, for example `Width="390"` and `Height="260"`.

- [ ] **Step 3: Wire sprite pet into main window**

Change `MainWindow` constructor to accept `SpritePet? spritePet`. If null, keep fallback text/circle removed by setting a generated fallback frame is optional; for this implementation, if null, hide `PetImage`.

Add fields:

```csharp
private readonly SpritePet? _spritePet;
private readonly PetAnimator? _petAnimator;
private readonly DispatcherTimer _calmTimer = new();
private readonly DispatcherTimer _desktopTimer = new();
private readonly Random _random = new();
private string _resumeState = "idle";
private double _desktopVelocityX = 3;
```

In constructor:

```csharp
_spritePet = spritePet;
if (_spritePet is not null)
{
    _petAnimator = new PetAnimator(_spritePet, frame => PetImage.Source = frame);
    StartBehaviorMode();
}
else
{
    PetImage.Visibility = Visibility.Collapsed;
}
_calmTimer.Tick += (_, _) => PlayCalmIdleOrWaiting();
_desktopTimer.Tick += (_, _) => MoveDesktopPet();
```

Implement:

```csharp
private void StartBehaviorMode()
{
    _calmTimer.Stop();
    _desktopTimer.Stop();
    if (_settings.PetBehaviorMode == PetBehaviorMode.Desktop)
    {
        _petAnimator?.Play(_desktopVelocityX >= 0 ? "runRight" : "runLeft");
        _desktopTimer.Interval = TimeSpan.FromMilliseconds(33);
        _desktopTimer.Start();
        return;
    }

    _petAnimator?.Play("idle");
    _calmTimer.Interval = TimeSpan.FromSeconds(6);
    _calmTimer.Start();
}

private void PlayCalmIdleOrWaiting()
{
    _petAnimator?.Play(_random.NextDouble() < 0.25 ? "waiting" : "idle");
}

private void MoveDesktopPet()
{
    Left += _desktopVelocityX;
    Rect workArea = SystemParameters.WorkArea;
    if (Left <= workArea.Left || Left + Width >= workArea.Right)
    {
        _desktopVelocityX *= -1;
        _petAnimator?.Play(_desktopVelocityX >= 0 ? "runRight" : "runLeft");
    }
}
```

In `ShowAlert`, before bubble:

```csharp
_resumeState = _petAnimator?.StateName ?? "idle";
_calmTimer.Stop();
_desktopTimer.Stop();
_petAnimator?.Play("waving");
```

In `DismissAlert`, after clearing bubble:

```csharp
StartBehaviorMode();
```

- [ ] **Step 4: Load pet from App**

In `App.xaml.cs`, before creating `MainWindow`:

```csharp
SpritePet? spritePet = null;
try
{
    string petDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "Pets",
        "miku-kimono.codex-pet");
    spritePet = await SpritePetLoader.LoadAsync(petDirectory);
}
catch (Exception exception)
{
    Debug.WriteLine($"Could not load sprite pet: {exception}");
}
```

Pass `spritePet` to `MainWindow`.

- [ ] **Step 5: Build and commit**

Run:

```powershell
dotnet build src/HyperPet.App/HyperPet.App.csproj
dotnet build HyperPet.sln
dotnet test HyperPet.sln
```

Commit:

```powershell
git add src/HyperPet.App
git commit -m "feat: animate sprite pet states"
```

---

### Task 4: Behavior Mode Settings

**Files:**
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml`
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs`
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Add behavior selector**

In `SettingsWindow.xaml`, add:

```xml
<StackPanel Grid.Row="3"
            Margin="0,18,0,0">
    <TextBlock Text="Pet behavior" />
    <ComboBox x:Name="PetBehaviorModeComboBox"
              Margin="0,8,0,0">
        <ComboBoxItem Content="Calm" />
        <ComboBoxItem Content="Desktop" />
    </ComboBox>
</StackPanel>
```

Move alert duration to the next row and increase window height to `320`.

- [ ] **Step 2: Read/write setting**

In `SettingsWindow.xaml.cs`, initialize:

```csharp
PetBehaviorModeComboBox.SelectedIndex = settings.PetBehaviorMode == HyperPet.Core.Pets.PetBehaviorMode.Desktop ? 1 : 0;
```

On close:

```csharp
_settings.PetBehaviorMode = PetBehaviorModeComboBox.SelectedIndex == 1
    ? HyperPet.Core.Pets.PetBehaviorMode.Desktop
    : HyperPet.Core.Pets.PetBehaviorMode.Calm;
```

- [ ] **Step 3: Restart behavior after settings close**

In `MainWindow.OnSettingsClick`, after saving settings:

```csharp
StartBehaviorMode();
```

- [ ] **Step 4: Build and commit**

Run:

```powershell
dotnet build HyperPet.sln
dotnet test HyperPet.sln
```

Commit:

```powershell
git add src/HyperPet.App
git commit -m "feat: add pet behavior setting"
```

---

### Task 5: Verification

**Files:**
- Modify only files needed for fixes found during verification.

- [ ] **Step 1: Run full verification**

Run:

```powershell
dotnet build HyperPet.sln
dotnet test HyperPet.sln
dotnet run --project src\HyperPet.App\HyperPet.App.csproj
```

Expected:

- Build succeeds.
- Tests pass.
- App launches.
- Miku sprite appears instead of text-circle pet.
- Calm mode animates idle/waiting.
- Desktop mode moves pet around screen.
- Notification alert plays waving.

- [ ] **Step 2: Commit fixes if needed**

If fixes are needed:

```powershell
git add src tests
git commit -m "fix: stabilize sprite pet states"
```

---

## Self-Review

- Spec coverage: plan covers current pet folder, metadata, WebP decode, sprite rendering, Calm/Desktop behavior, notification waving, settings, fallbacks, tests, and manual checks.
- Placeholder scan: no open implementation blanks remain.
- Type consistency: `PetBehaviorMode`, `PetDefinition`, `PetAnimationState`, `SpritePet`, `SpritePetLoader`, and `PetAnimator` names match across tasks.

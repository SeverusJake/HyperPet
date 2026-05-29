# Live Pet Hot-Swap Implementation Plan

**Goal:** Switching the pet in Settings applies immediately (rebuild the sprite + animator at runtime) instead of requiring a restart.

**Architecture:** Extract the per-pet override logic (snapshot originals + apply fps/play-mode overrides) into a testable `PetOverrides` helper used by both App startup and the new runtime reload. MainWindow gains a `ReloadPetAsync(petId)` that tears down the current animator, loads the new pet via an injected loader, re-snapshots originals, applies overrides, rebuilds the animator, and restarts behavior. Settings calls a reload callback on a pet change (instead of showing the restart note).

**Tech Stack:** C# .NET 8 WPF, xUnit.

**Risk controls:** reload is wrapped in try/catch and keeps the current pet on failure. The testable core (`PetOverrides`) is unit-tested; the WPF reload path is manual-verify.

---

## Files

- Create: `src/HyperPet.App/Pets/PetOverrides.cs`
- Create: `tests/HyperPet.App.Tests/Pets/PetOverridesTests.cs`
- Modify: `src/HyperPet.App/App.xaml.cs` (use PetOverrides; pass a loader to MainWindow)
- Modify: `src/HyperPet.App/MainWindow.xaml.cs` (mutable sprite/animator; ReloadPetAsync; pass reload callback to Settings)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs` (call reload callback on pet change instead of restart note)

---

### Task 1: Extract `PetOverrides` (+ tests)

`src/HyperPet.App/Pets/PetOverrides.cs`:
```csharp
using HyperPet.App.Pets;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Pets;

/// <summary>
/// Snapshots a pet's original animation fps / play-mode and applies the
/// user's per-pet overrides from settings onto the live PetDefinition.
/// Shared by startup and runtime pet reloads.
/// </summary>
public static class PetOverrides
{
    public static Dictionary<string, int> SnapshotFps(SpritePet pet) =>
        pet.Definition.States.ToDictionary(kv => kv.Key, kv => kv.Value.Fps, StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, PlayMode> SnapshotPlayMode(SpritePet pet) =>
        pet.Definition.States.ToDictionary(kv => kv.Key, kv => kv.Value.PlayMode, StringComparer.OrdinalIgnoreCase);

    /// <summary>Applies fps + play-mode overrides for the pet's id onto its definition.</summary>
    public static void Apply(SpritePet pet, HyperPetSettings settings)
    {
        string id = pet.Definition.Id;

        if (settings.StateSpeedOverrides.TryGetValue(id, out var fpsMap))
        {
            foreach (var (state, fps) in fpsMap)
            {
                if (pet.Definition.States.TryGetValue(state, out var s) && fps >= 1 && fps <= 60)
                {
                    s.Fps = fps;
                }
            }
        }

        if (settings.StatePlayModeOverrides.TryGetValue(id, out var modeMap))
        {
            foreach (var (state, mode) in modeMap)
            {
                if (pet.Definition.States.TryGetValue(state, out var s))
                {
                    s.PlayMode = mode;
                }
            }
        }
    }
}
```

`tests/HyperPet.App.Tests/Pets/PetOverridesTests.cs`: build a `SpritePet` via a tiny in-memory `PetDefinition` + empty frames, assert SnapshotFps captures originals, and Apply mutates fps/playmode from settings and ignores out-of-range fps.

```csharp
using System.Windows.Media.Imaging;
using HyperPet.App.Pets;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;
using Xunit;

namespace HyperPet.App.Tests.Pets;

public class PetOverridesTests
{
    private static SpritePet MakePet()
    {
        var def = new PetDefinition
        {
            Id = "test",
            DisplayName = "Test",
            SpritesheetPath = "x.webp",
            FrameWidth = 1,
            FrameHeight = 1,
            States = new Dictionary<string, PetAnimationState>(StringComparer.OrdinalIgnoreCase)
            {
                ["idle"] = new PetAnimationState { Row = 0, Frames = 1, Fps = 4, Loop = true, PlayMode = PlayMode.Forward },
                ["runRight"] = new PetAnimationState { Row = 1, Frames = 1, Fps = 12, Loop = true, PlayMode = PlayMode.Reverse },
            },
        };
        var frames = new Dictionary<string, IReadOnlyList<BitmapSource>>(StringComparer.OrdinalIgnoreCase)
        {
            ["idle"] = new List<BitmapSource>(),
            ["runRight"] = new List<BitmapSource>(),
        };
        return new SpritePet(def, frames);
    }

    [Fact]
    public void SnapshotFps_CapturesOriginals()
    {
        var pet = MakePet();
        var snap = PetOverrides.SnapshotFps(pet);
        Assert.Equal(4, snap["idle"]);
        Assert.Equal(12, snap["runRight"]);
    }

    [Fact]
    public void Apply_OverridesFpsAndPlayMode_FromSettings()
    {
        var pet = MakePet();
        var settings = HyperPetSettings.CreateDefault();
        settings.StateSpeedOverrides["test"] = new Dictionary<string, int> { ["idle"] = 9 };
        settings.StatePlayModeOverrides["test"] = new Dictionary<string, PlayMode> { ["idle"] = PlayMode.PingPong };

        PetOverrides.Apply(pet, settings);

        Assert.Equal(9, pet.Definition.States["idle"].Fps);
        Assert.Equal(PlayMode.PingPong, pet.Definition.States["idle"].PlayMode);
        Assert.Equal(12, pet.Definition.States["runRight"].Fps); // untouched
    }

    [Fact]
    public void Apply_IgnoresOutOfRangeFps()
    {
        var pet = MakePet();
        var settings = HyperPetSettings.CreateDefault();
        settings.StateSpeedOverrides["test"] = new Dictionary<string, int> { ["idle"] = 999 };

        PetOverrides.Apply(pet, settings);

        Assert.Equal(4, pet.Definition.States["idle"].Fps); // unchanged
    }
}
```

Then refactor `App.xaml.cs` to use `PetOverrides` (replace the inline `ApplyStateSpeedOverrides`/`ApplyStatePlayModeOverrides` + the `ToDictionary` snapshots):
```csharp
        Dictionary<string, int>? originalStateFps = null;
        Dictionary<string, PlayMode>? originalStatePlayMode = null;
        if (spritePet is not null)
        {
            originalStateFps = PetOverrides.SnapshotFps(spritePet);
            originalStatePlayMode = PetOverrides.SnapshotPlayMode(spritePet);
            PetOverrides.Apply(spritePet, _settings);
        }
```
Delete the now-unused private `ApplyStateSpeedOverrides` and `ApplyStatePlayModeOverrides` methods in App.

Build + test + commit `refactor: extract PetOverrides helper (snapshot + apply), with tests`.

---

### Task 2: MainWindow runtime reload

In `src/HyperPet.App/MainWindow.xaml.cs`:

Make these fields mutable (drop `readonly`):
```csharp
    private PetAnimator? _petAnimator;
    private SpritePet? _spritePet;
    private IReadOnlyDictionary<string, int>? _originalStateFps;
    private IReadOnlyDictionary<string, PlayMode>? _originalStatePlayMode;
```

Add a loader field + parameter. New field after `_petCatalog`:
```csharp
    private readonly Func<string, Task<SpritePet?>>? _petLoader;
```
Add constructor parameter after `petCatalog`:
```csharp
        IReadOnlyList<PetCatalogEntry>? petCatalog = null,
        Func<string, Task<SpritePet?>>? petLoader = null)
```
Assign after `_petCatalog = ...`:
```csharp
        _petLoader = petLoader;
```

Add the reload method (place after `RefreshFromSettings`):
```csharp
    /// <summary>
    /// Swaps the live pet to the given id without restarting. Keeps the
    /// current pet on any failure. Safe to call from the UI thread.
    /// </summary>
    public async Task ReloadPetAsync(string petId)
    {
        if (_petLoader is null)
        {
            return;
        }

        try
        {
            SpritePet? pet = await _petLoader(petId);
            if (pet is null)
            {
                return; // keep current pet
            }

            _originalStateFps = PetOverrides.SnapshotFps(pet);
            _originalStatePlayMode = PetOverrides.SnapshotPlayMode(pet);
            PetOverrides.Apply(pet, _settings);

            StopBehaviorTimers();
            _petAnimator?.Stop();

            _spritePet = pet;
            _petAnimator = new PetAnimator(pet, PetImage);
            PetImage.Visibility = Visibility.Visible;

            ApplyPetSize();
            _roamController = null;          // rebuild against the new pet next perch
            _lastRoamAnimation = string.Empty;
            StartBehaviorMode();
        }
        catch (Exception)
        {
            // Keep the current pet on failure.
        }
    }
```

Pass the reload callback to SettingsWindow in `OnSettingsClick` (final arg):
```csharp
            _petCatalog,
            ReloadPetAsync)
```

Build + commit `feat: MainWindow.ReloadPetAsync for live pet swap`.

---

### Task 3: App provides the loader; Settings triggers reload

In `src/HyperPet.App/App.xaml.cs`, add a loader that resolves a pet id to a SpritePet through the catalog, and pass it as the final MainWindow argument:
```csharp
            updateService,
            _petCatalog,
            LoadPetByIdAsync)
```
Add the method (near TryLoadSpritePetAsync):
```csharp
    private async Task<SpritePet?> LoadPetByIdAsync(string petId)
    {
        PetCatalogEntry? entry = PetCatalog.Resolve(_petCatalog, petId);
        return await TryLoadSpritePetAsync(_logger!, entry);
    }
```

In `src/HyperPet.App/Views/SettingsWindow.xaml.cs`:

Add a reload-callback field after `_originalSelectedPet`:
```csharp
    private readonly Func<string, Task>? _reloadPet;
```
Add constructor parameter after `petCatalog`:
```csharp
        IReadOnlyList<PetCatalogEntry>? petCatalog = null,
        Func<string, Task>? reloadPet = null)
```
Assign after `_originalSelectedPet = settings.SelectedPet;`:
```csharp
        _reloadPet = reloadPet;
```

Replace the restart-note block at the end of `CommitChanges()` with a live reload:
```csharp
        if (!string.Equals(_settings.SelectedPet, _originalSelectedPet, StringComparison.OrdinalIgnoreCase)
            && _reloadPet is not null)
        {
            _ = _reloadPet(_settings.SelectedPet);
        }
```
(`_originalSelectedPet` stays the dialog-open value, so re-applying the same pet won't reload twice within reason; reload is idempotent and guarded.)

Build + full test run + commit `feat: live pet swap on Apply/Save (no restart)`.

---

### Task 4: Manual verification
- Add a second pet folder (or duplicate miku under a new id) under Assets/Pets.
- Settings > General > Pet → switch → Apply: pet swaps live, no restart, correct size + behavior. State-tab overrides for the new pet apply.
- Switch back: works. Trigger alert during/after: waving + resume roam. Desktop mode still perches after a swap.

---

## Self-Review
- PetOverrides snapshot/apply extracted + reused (startup + reload) → Task 1 (tested).
- Mutable animator/sprite + ReloadPetAsync with failure-safety → Task 2.
- Loader wired App→MainWindow; reload callback MainWindow→Settings; restart note replaced → Tasks 2-3.
- Types: `PetOverrides.SnapshotFps/SnapshotPlayMode/Apply`; `Func<string, Task<SpritePet?>>` loader (App `LoadPetByIdAsync`); `Func<string, Task>` reload (MainWindow `ReloadPetAsync`). MainWindow ctor gains `petLoader` last; SettingsWindow ctor gains `reloadPet` last. Consistent.
- `PetAnimator.Stop()` exists (used in teardown). `_roamController` reset so Desktop mode rebuilds against the new pet.

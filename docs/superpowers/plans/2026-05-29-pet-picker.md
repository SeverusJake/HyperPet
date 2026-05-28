# Pet Picker Implementation Plan

**Goal:** Let the user choose which pet to display from the pets installed under Assets/Pets, and make the app actually load the selected pet.

**Architecture:** A `PetCatalog` scans `Assets/Pets/*/pet.json` and returns id+displayName+directory entries. App resolves the startup directory from `settings.SelectedPet` (fallback: first entry / miku). Settings General tab gets a pet ComboBox; changing it persists `SelectedPet` and prompts that the change applies after restart (live hot-swap is out of scope for v1 to avoid rebuilding the animator mid-run).

**Tech Stack:** C# .NET 8 WPF, xUnit.

**Scope decisions:**
- v1 = restart-to-apply. No live sprite hot-swap (keeps `_petAnimator`/`_spritePet` immutable, avoids re-snapshotting per-pet overrides at runtime).
- Catalog reads only `pet.json` (cheap; no spritesheet decode) via the existing `PetDefinitionLoader`.
- Invalid/incomplete pet folders (no `pet.json`, or malformed) are skipped, not fatal.

---

## Files

- Create: `src/HyperPet.App/Pets/PetCatalogEntry.cs`
- Create: `src/HyperPet.App/Pets/PetCatalog.cs`
- Create: `tests/HyperPet.App.Tests/Pets/PetCatalogTests.cs`
- Modify: `src/HyperPet.App/App.xaml.cs` (discover catalog, load SelectedPet's dir, pass catalog to MainWindow)
- Modify: `src/HyperPet.App/MainWindow.xaml.cs` (accept catalog, forward to SettingsWindow)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml` (pet ComboBox in General)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs` (populate, select, commit SelectedPet, restart note, dirty)

---

### Task 1: PetCatalog + tests

`PetCatalogEntry.cs`:
```csharp
namespace HyperPet.App.Pets;

public sealed record PetCatalogEntry(string Id, string DisplayName, string Directory);
```

`PetCatalog.cs`:
```csharp
using System.IO;
using HyperPet.Core.Pets;

namespace HyperPet.App.Pets;

/// <summary>
/// Discovers installed pets by scanning subdirectories of a pets root for a
/// valid pet.json. Each discoverable pet yields one catalog entry.
/// </summary>
public static class PetCatalog
{
    /// <summary>
    /// Returns one entry per subdirectory of <paramref name="petsRootDirectory"/>
    /// that contains a parseable pet.json, ordered by display name. Returns an
    /// empty list when the root is missing. Folders that fail to parse are
    /// skipped.
    /// </summary>
    public static async Task<IReadOnlyList<PetCatalogEntry>> DiscoverAsync(string petsRootDirectory)
    {
        var entries = new List<PetCatalogEntry>();
        if (!Directory.Exists(petsRootDirectory))
        {
            return entries;
        }

        foreach (string dir in Directory.EnumerateDirectories(petsRootDirectory))
        {
            string json = Path.Combine(dir, "pet.json");
            if (!File.Exists(json))
            {
                continue;
            }

            try
            {
                PetDefinition def = await PetDefinitionLoader.LoadAsync(dir).ConfigureAwait(false);
                entries.Add(new PetCatalogEntry(def.Id, def.DisplayName, dir));
            }
            catch
            {
                // Skip malformed pet folders.
            }
        }

        entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    /// <summary>
    /// Resolves the directory for the selected pet id; falls back to the first
    /// entry when not found. Returns null when no pets are available.
    /// </summary>
    public static PetCatalogEntry? Resolve(IReadOnlyList<PetCatalogEntry> entries, string? selectedId)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        foreach (var entry in entries)
        {
            if (string.Equals(entry.Id, selectedId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return entries[0];
    }
}
```

`tests/HyperPet.App.Tests/Pets/PetCatalogTests.cs`:
```csharp
using System.IO;
using HyperPet.App.Pets;
using Xunit;

namespace HyperPet.App.Tests.Pets;

public class PetCatalogTests : IDisposable
{
    private readonly List<string> _temp = new();

    private string MakeRoot() 
    {
        string root = Path.Combine(Path.GetTempPath(), "HyperPet.PetCatalog", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _temp.Add(root);
        return root;
    }

    private static void WritePet(string root, string folder, string id, string displayName)
    {
        string dir = Path.Combine(root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "pet.json"),
            $$"""
            {
              "id": "{{id}}",
              "displayName": "{{displayName}}",
              "spritesheetPath": "spritesheet.webp",
              "frameWidth": 192,
              "frameHeight": 208,
              "states": { "idle": { "row": 0, "frames": 1, "fps": 1, "loop": true } }
            }
            """);
    }

    [Fact]
    public async Task DiscoverAsync_FindsValidPets_SkipsFoldersWithoutJson()
    {
        string root = MakeRoot();
        WritePet(root, "alpha", "alpha", "Alpha Pet");
        WritePet(root, "beta", "beta", "Beta Pet");
        Directory.CreateDirectory(Path.Combine(root, "NotAPet")); // no pet.json

        var entries = await PetCatalog.DiscoverAsync(root);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Alpha Pet", entries[0].DisplayName); // sorted
        Assert.Contains(entries, e => e.Id == "beta");
    }

    [Fact]
    public async Task DiscoverAsync_MissingRoot_ReturnsEmpty()
    {
        var entries = await PetCatalog.DiscoverAsync(Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid().ToString("N")));
        Assert.Empty(entries);
    }

    [Fact]
    public async Task DiscoverAsync_SkipsMalformedJson()
    {
        string root = MakeRoot();
        WritePet(root, "good", "good", "Good");
        string bad = Path.Combine(root, "bad");
        Directory.CreateDirectory(bad);
        File.WriteAllText(Path.Combine(bad, "pet.json"), "{ broken");

        var entries = await PetCatalog.DiscoverAsync(root);

        Assert.Single(entries);
        Assert.Equal("good", entries[0].Id);
    }

    [Fact]
    public void Resolve_PrefersSelected_FallsBackToFirst()
    {
        var entries = new List<PetCatalogEntry>
        {
            new("a", "A", "dirA"),
            new("b", "B", "dirB"),
        };

        Assert.Equal("b", PetCatalog.Resolve(entries, "b")!.Id);
        Assert.Equal("a", PetCatalog.Resolve(entries, "missing")!.Id);
        Assert.Null(PetCatalog.Resolve(new List<PetCatalogEntry>(), "a"));
    }

    public void Dispose()
    {
        foreach (var d in _temp)
        {
            if (Directory.Exists(d)) Directory.Delete(d, true);
        }
    }
}
```

Steps: write files → `dotnet test tests/HyperPet.App.Tests/...` → commit
`feat: PetCatalog discovers installed pets`.

---

### Task 2: App loads the selected pet

In `src/HyperPet.App/App.xaml.cs`, replace the hardcoded `TryLoadSpritePetAsync` flow.

Current:
```csharp
        SpritePet? spritePet = await TryLoadSpritePetAsync(_logger);
```
and the helper:
```csharp
    private static async Task<SpritePet?> TryLoadSpritePetAsync(HyperPetLogger logger)
    {
        string petDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Pets",
            "miku-kimono.codex-pet");

        try
        {
            return await SpritePetLoader.LoadAsync(petDirectory);
        }
        catch (Exception exception)
        {
            logger.Error($"Could not load sprite pet from '{petDirectory}'", exception);
            return null;
        }
    }
```

Add a field near the other fields:
```csharp
    private IReadOnlyList<PetCatalogEntry> _petCatalog = Array.Empty<PetCatalogEntry>();
```

Replace the load call site:
```csharp
        string petsRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Pets");
        _petCatalog = await PetCatalog.DiscoverAsync(petsRoot);
        PetCatalogEntry? selectedEntry = PetCatalog.Resolve(_petCatalog, _settings.SelectedPet);
        SpritePet? spritePet = await TryLoadSpritePetAsync(_logger, selectedEntry);
```

Replace the helper with one that takes the resolved entry:
```csharp
    private static async Task<SpritePet?> TryLoadSpritePetAsync(HyperPetLogger logger, PetCatalogEntry? entry)
    {
        if (entry is null)
        {
            logger.Error("No installed pets found under Assets/Pets.");
            return null;
        }

        try
        {
            return await SpritePetLoader.LoadAsync(entry.Directory);
        }
        catch (Exception exception)
        {
            logger.Error($"Could not load sprite pet '{entry.Id}' from '{entry.Directory}'", exception);
            return null;
        }
    }
```

Pass the catalog to MainWindow — add `_petCatalog` as the final constructor argument (after `updateService`):
```csharp
            ApplyMonitoringSettings,
            updateService,
            _petCatalog)
```

Build. Commit `feat: load the selected pet from the catalog at startup`.

---

### Task 3: MainWindow forwards the catalog to Settings

In `src/HyperPet.App/MainWindow.xaml.cs`:

Add using (already present): `using HyperPet.App.Pets;`.

Add field after `_updateService`:
```csharp
    private readonly IReadOnlyList<PetCatalogEntry> _petCatalog;
```

Add constructor parameter after `UpdateService? updateService = null`:
```csharp
        UpdateService? updateService = null,
        IReadOnlyList<PetCatalogEntry>? petCatalog = null)
```

Assign in ctor body after `_updateService = updateService;`:
```csharp
        _petCatalog = petCatalog ?? Array.Empty<PetCatalogEntry>();
```

In `OnSettingsClick`, pass the catalog as the final argument to `new SettingsWindow(...)`:
```csharp
            _updateService,
            PromptAndApplyUpdateAsync,
            _petCatalog)
```

Build. Commit `feat: pass pet catalog into the settings dialog`.

---

### Task 4: Settings pet ComboBox + persistence + restart note

In `src/HyperPet.App/Views/SettingsWindow.xaml`, add a pet picker at the TOP of the General StackPanel (before `ShowFullContentCheckBox`):
```xml
                        <StackPanel Margin="0,0,0,4">
                            <TextBlock Text="Pet" />
                            <ComboBox x:Name="PetPickerComboBox"
                                      Margin="0,6,0,10"
                                      DisplayMemberPath="DisplayName"
                                      SelectedValuePath="Id" />
                        </StackPanel>
```

In `src/HyperPet.App/Views/SettingsWindow.xaml.cs`:

Add using: `using HyperPet.App.Pets;`.

Add fields next to `_promptAndApply`:
```csharp
    private readonly IReadOnlyList<PetCatalogEntry> _petCatalog;
    private readonly string _originalSelectedPet;
```

Add constructor parameter after `Func<UpdateInfo, Task>? promptAndApply = null`:
```csharp
        Func<UpdateInfo, Task>? promptAndApply = null,
        IReadOnlyList<PetCatalogEntry>? petCatalog = null)
```

Assign + populate after `_promptAndApply = promptAndApply;`:
```csharp
        _petCatalog = petCatalog ?? Array.Empty<PetCatalogEntry>();
        _originalSelectedPet = settings.SelectedPet;
```

After `InitializeComponent();` and the other initializers, populate the combo:
```csharp
        PetPickerComboBox.ItemsSource = _petCatalog;
        PetPickerComboBox.SelectedValue = settings.SelectedPet;
        if (PetPickerComboBox.SelectedItem is null && _petCatalog.Count > 0)
        {
            PetPickerComboBox.SelectedIndex = 0;
        }
```

Dirty tracking — in `WireDirtyTracking`, near the other `SelectionChanged`:
```csharp
        PetPickerComboBox.SelectionChanged += OnAnyChange;
```

Persist in `CommitChanges()` — after the applier `TryApply` succeeds and before `ApplyStateSpeedChanges();` (next to `_settings.AutoUpdate = ...`):
```csharp
        if (PetPickerComboBox.SelectedValue is string selectedPetId && !string.IsNullOrWhiteSpace(selectedPetId))
        {
            _settings.SelectedPet = selectedPetId;
        }
```

Restart note — at the END of `CommitChanges()`, just before `return true;`, after `_dirty = false; UpdateButtonState();`:
```csharp
        if (!string.Equals(_settings.SelectedPet, _originalSelectedPet, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                this,
                "The pet change will take effect the next time you start HyperPet.",
                "HyperPet",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
```

Note: `_originalSelectedPet` is captured once at construction, so the note shows once per dialog session after a real change (Apply or Save). Acceptable for v1.

Build + full test run. Commit `feat: pet picker in settings (restart to apply)`.

---

### Task 5: Manual verification
- Launch app: pet loads (miku). Open Settings > General: Pet combo shows "Miku Kimono" selected.
- (If a second pet folder is added under Assets/Pets, it appears in the combo; selecting it + Apply shows the restart note; after restart the new pet loads.)
- Confirm no behavior regressions (alerts, modes, quiet hours).

---

## Self-Review
- Discovery + resolve → Task 1 (tested).
- App honors SelectedPet → Task 2.
- Catalog reaches the dialog → Tasks 2-3.
- Picker UI + persistence + restart note → Task 4.
- Types: `PetCatalogEntry(Id, DisplayName, Directory)`, `PetCatalog.DiscoverAsync`/`Resolve` consistent across tasks. MainWindow ctor gains `IReadOnlyList<PetCatalogEntry>?` last; App passes `_petCatalog`; SettingsWindow ctor gains it last; MainWindow passes it. `SelectedPet` already exists on settings + persists via Sanitize (string, default "miku-kimono").
- No placeholders; every step shows code.

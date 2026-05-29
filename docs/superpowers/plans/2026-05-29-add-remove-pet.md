# Add / Remove Pet Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import pets from a zip or folder into a persistent user-pets directory, and remove user-added pets — all from Settings.

**Architecture:** A testable `PetImporter` (validate + copy a staging dir into `%AppData%/HyperPet/Pets`, zip extracts to temp first) and `PetRemover` (delete user pets only). `PetCatalog` scans built-in + user roots and dedups by id. App wires both roots + a re-discover delegate through MainWindow into the Settings dialog, which gets Add-from-zip / Add-from-folder / Remove buttons. New pets hot-swap via the existing reload path.

**Tech Stack:** C# .NET 8 WPF, `System.IO.Compression` (zip), `Microsoft.Win32` dialogs, xUnit.

**Spec:** `docs/superpowers/specs/2026-05-29-add-remove-pet-design.md`

**Current call shapes (to extend, append new params at the end):**
- `MainWindow(... , UpdateService? updateService = null, IReadOnlyList<PetCatalogEntry>? petCatalog = null, Func<string, Task<SpritePet?>>? petLoader = null)`
- `SettingsWindow(... , IReadOnlyList<PetCatalogEntry>? petCatalog = null, Func<string, Task>? reloadPet = null)`
- `PetCatalog.DiscoverAsync(string petsRootDirectory)` and `Resolve(entries, id)`
- `PetCatalogEntry(string Id, string DisplayName, string Directory)`

---

## File Structure

- Create: `src/HyperPet.App/Pets/PetImporter.cs` — validate + install a pet.
- Create: `src/HyperPet.App/Pets/PetRemover.cs` — removability check + delete.
- Create: `tests/HyperPet.App.Tests/Pets/PetImporterTests.cs`
- Create: `tests/HyperPet.App.Tests/Pets/PetRemoverTests.cs`
- Modify: `src/HyperPet.App/Pets/PetCatalog.cs` — multi-root + dedup.
- Modify: `tests/HyperPet.App.Tests/Pets/PetCatalogTests.cs` — dedup test.
- Modify: `src/HyperPet.App/App.xaml.cs` — roots, merged discovery, rediscover delegate, loader re-discovers.
- Modify: `src/HyperPet.App/MainWindow.xaml.cs` — pass userPetsRoot + rediscover through.
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml` — Add/Remove buttons.
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs` — handlers, rebind, enable.

---

### Task 1: PetCatalog multi-root + dedup

**Files:**
- Modify: `src/HyperPet.App/Pets/PetCatalog.cs`
- Modify: `tests/HyperPet.App.Tests/Pets/PetCatalogTests.cs`

- [ ] **Step 1: Change DiscoverAsync to accept multiple roots**

Replace the `DiscoverAsync` method body in `src/HyperPet.App/Pets/PetCatalog.cs` with a `params` version that dedups by id (first root wins):

```csharp
    /// <summary>
    /// Returns one entry per subdirectory (across all <paramref name="roots"/>)
    /// that contains a parseable pet.json, deduped by id (earlier roots win),
    /// ordered by display name. Missing roots are skipped. Malformed pets are
    /// skipped.
    /// </summary>
    public static async Task<IReadOnlyList<PetCatalogEntry>> DiscoverAsync(params string[] roots)
    {
        var entries = new List<PetCatalogEntry>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            foreach (string dir in Directory.EnumerateDirectories(root))
            {
                string json = Path.Combine(dir, "pet.json");
                if (!File.Exists(json))
                {
                    continue;
                }

                try
                {
                    PetDefinition def = await PetDefinitionLoader.LoadAsync(dir).ConfigureAwait(false);
                    if (seenIds.Add(def.Id))
                    {
                        entries.Add(new PetCatalogEntry(def.Id, def.DisplayName, dir));
                    }
                }
                catch
                {
                    // Skip malformed pet folders.
                }
            }
        }

        entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return entries;
    }
```

`Resolve` is unchanged. Existing single-root callers/tests keep compiling (one arg binds to `params`).

- [ ] **Step 2: Add a dedup test**

In `tests/HyperPet.App.Tests/Pets/PetCatalogTests.cs`, add:

```csharp
    [Fact]
    public async Task DiscoverAsync_DedupsById_FirstRootWins()
    {
        string builtin = MakeRoot();
        string user = MakeRoot();
        WritePet(builtin, "miku", "miku", "Miku Builtin");
        WritePet(user, "miku-copy", "miku", "Miku User");   // same id
        WritePet(user, "extra", "extra", "Extra");

        var entries = await PetCatalog.DiscoverAsync(builtin, user);

        Assert.Equal(2, entries.Count);                                  // id "miku" deduped
        Assert.Contains(entries, e => e.Id == "miku" && e.DisplayName == "Miku Builtin");
        Assert.Contains(entries, e => e.Id == "extra");
    }
```

- [ ] **Step 3: Test**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj -c Release -nologo`
Expected: green (existing PetCatalog tests + the new dedup test).

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/Pets/PetCatalog.cs tests/HyperPet.App.Tests/Pets/PetCatalogTests.cs
git commit -m "feat: PetCatalog scans multiple roots and dedups by id"
```

---

### Task 2: PetRemover + tests

**Files:**
- Create: `src/HyperPet.App/Pets/PetRemover.cs`
- Create: `tests/HyperPet.App.Tests/Pets/PetRemoverTests.cs`

- [ ] **Step 1: Implement PetRemover**

`src/HyperPet.App/Pets/PetRemover.cs`:

```csharp
using System.IO;

namespace HyperPet.App.Pets;

/// <summary>
/// Removal rules for pets. Only pets that live under the user-pets root may be
/// removed; built-in (shipped) pets are protected.
/// </summary>
public static class PetRemover
{
    /// <summary>True only when <paramref name="directory"/> is inside <paramref name="userPetsRoot"/>.</summary>
    public static bool IsRemovable(string? directory, string? userPetsRoot)
    {
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(userPetsRoot))
        {
            return false;
        }

        string dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(userPetsRoot));

        return dir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deletes the pet directory when it is removable and exists. Returns true
    /// if a deletion happened, false otherwise (not removable / missing).
    /// </summary>
    public static bool TryRemove(string directory, string userPetsRoot)
    {
        if (!IsRemovable(directory, userPetsRoot) || !Directory.Exists(directory))
        {
            return false;
        }

        Directory.Delete(directory, recursive: true);
        return true;
    }
}
```

- [ ] **Step 2: Tests**

`tests/HyperPet.App.Tests/Pets/PetRemoverTests.cs`:

```csharp
using System.IO;
using HyperPet.App.Pets;
using Xunit;

namespace HyperPet.App.Tests.Pets;

public class PetRemoverTests : IDisposable
{
    private readonly List<string> _temp = new();

    private string MakeDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "HyperPet.PetRemover", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        _temp.Add(d);
        return d;
    }

    [Fact]
    public void IsRemovable_TrueUnderUserRoot_FalseOutside()
    {
        string userRoot = MakeDir();
        string petDir = Path.Combine(userRoot, "mypet");
        Directory.CreateDirectory(petDir);

        Assert.True(PetRemover.IsRemovable(petDir, userRoot));
        Assert.False(PetRemover.IsRemovable(MakeDir(), userRoot));   // unrelated path
        Assert.False(PetRemover.IsRemovable(userRoot, userRoot));    // the root itself
        Assert.False(PetRemover.IsRemovable(null, userRoot));
    }

    [Fact]
    public void TryRemove_DeletesUserPet()
    {
        string userRoot = MakeDir();
        string petDir = Path.Combine(userRoot, "mypet");
        Directory.CreateDirectory(petDir);
        File.WriteAllText(Path.Combine(petDir, "pet.json"), "{}");

        bool removed = PetRemover.TryRemove(petDir, userRoot);

        Assert.True(removed);
        Assert.False(Directory.Exists(petDir));
    }

    [Fact]
    public void TryRemove_NoOpForOutsidePath()
    {
        string userRoot = MakeDir();
        string outside = MakeDir();

        Assert.False(PetRemover.TryRemove(outside, userRoot));
        Assert.True(Directory.Exists(outside));
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

- [ ] **Step 3: Build + test**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj -c Release -nologo`
Expected: green.

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/Pets/PetRemover.cs tests/HyperPet.App.Tests/Pets/PetRemoverTests.cs
git commit -m "feat: PetRemover (delete user pets only)"
```

---

### Task 3: PetImporter + tests

**Files:**
- Create: `src/HyperPet.App/Pets/PetImporter.cs`
- Create: `tests/HyperPet.App.Tests/Pets/PetImporterTests.cs`

- [ ] **Step 1: Implement PetImporter**

`src/HyperPet.App/Pets/PetImporter.cs`:

```csharp
using System.IO;
using System.IO.Compression;
using HyperPet.Core.Pets;

namespace HyperPet.App.Pets;

public sealed record PetImportResult(bool Success, string? Message, PetCatalogEntry? Entry);

/// <summary>
/// Validates and installs a pet from a folder or zip into the user-pets root.
/// Never throws to callers — all failures are returned as a result with a
/// user-facing message.
/// </summary>
public static class PetImporter
{
    /// <summary>
    /// Installs the pet whose pet.json sits at the top of
    /// <paramref name="sourceDirectory"/> into userPetsRoot/&lt;id&gt;.
    /// </summary>
    public static async Task<PetImportResult> ImportFromFolderAsync(
        string sourceDirectory, string userPetsRoot, Func<string, bool> confirmOverwrite)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                return new PetImportResult(false, "The selected folder does not exist.", null);
            }

            if (!File.Exists(Path.Combine(sourceDirectory, "pet.json")))
            {
                return new PetImportResult(false, "That folder has no pet.json at its top level.", null);
            }

            PetDefinition def;
            try
            {
                def = await PetDefinitionLoader.LoadAsync(sourceDirectory).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new PetImportResult(false, $"pet.json is invalid: {ex.Message}", null);
            }

            if (string.IsNullOrWhiteSpace(def.Id))
            {
                return new PetImportResult(false, "pet.json has no id.", null);
            }

            string sheet = Path.Combine(sourceDirectory, def.SpritesheetPath);
            if (!File.Exists(sheet))
            {
                return new PetImportResult(false, $"The spritesheet '{def.SpritesheetPath}' referenced by pet.json is missing.", null);
            }

            Directory.CreateDirectory(userPetsRoot);
            string folderName = SanitizeFolderName(def.Id);
            string target = Path.Combine(userPetsRoot, folderName);

            if (Directory.Exists(target))
            {
                if (!confirmOverwrite(def.Id))
                {
                    return new PetImportResult(false, "Import cancelled.", null);
                }

                Directory.Delete(target, recursive: true);
            }

            // Copy to a temp sibling, then move into place so a mid-copy
            // failure never leaves a half-written pet.
            string staging = target + ".importing-" + Guid.NewGuid().ToString("N");
            try
            {
                CopyDirectory(sourceDirectory, staging);
                Directory.Move(staging, target);
            }
            catch
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                throw;
            }

            return new PetImportResult(true, null, new PetCatalogEntry(def.Id, def.DisplayName, target));
        }
        catch (Exception ex)
        {
            return new PetImportResult(false, $"Could not import the pet: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Extracts the zip to a temp directory, finds pet.json anywhere inside
    /// (nested-tolerant), and installs from its containing folder.
    /// </summary>
    public static async Task<PetImportResult> ImportFromZipAsync(
        string zipPath, string userPetsRoot, Func<string, bool> confirmOverwrite)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            return new PetImportResult(false, "The selected zip does not exist.", null);
        }

        string tempExtract = Path.Combine(Path.GetTempPath(), "HyperPet.import-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempExtract);
            try
            {
                ZipFile.ExtractToDirectory(zipPath, tempExtract);
            }
            catch (Exception ex)
            {
                return new PetImportResult(false, $"Could not read the zip: {ex.Message}", null);
            }

            string? petJson = Directory
                .EnumerateFiles(tempExtract, "pet.json", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (petJson is null)
            {
                return new PetImportResult(false, "The zip has no pet.json.", null);
            }

            string staging = Path.GetDirectoryName(petJson)!;
            return await ImportFromFolderAsync(staging, userPetsRoot, confirmOverwrite).ConfigureAwait(false);
        }
        finally
        {
            try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true); }
            catch { /* best-effort temp cleanup */ }
        }
    }

    private static string SanitizeFolderName(string id)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var chars = id.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        string name = new string(chars).Trim();
        return name.Length == 0 ? "pet" : name;
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }
        foreach (string sub in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(sub, Path.Combine(dest, Path.GetFileName(sub)));
        }
    }
}
```

- [ ] **Step 2: Tests**

`tests/HyperPet.App.Tests/Pets/PetImporterTests.cs`:

```csharp
using System.IO;
using System.IO.Compression;
using HyperPet.App.Pets;
using Xunit;

namespace HyperPet.App.Tests.Pets;

public class PetImporterTests : IDisposable
{
    private readonly List<string> _temp = new();

    private string MakeDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "HyperPet.PetImporter", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        _temp.Add(d);
        return d;
    }

    // Writes a valid pet folder (pet.json + dummy spritesheet file).
    private static void WritePetFolder(string dir, string id, string displayName, string sheet = "sheet.webp")
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "pet.json"),
            $$"""
            {
              "id": "{{id}}",
              "displayName": "{{displayName}}",
              "spritesheetPath": "{{sheet}}",
              "frameWidth": 192,
              "frameHeight": 208,
              "states": { "idle": { "row": 0, "frames": 1, "fps": 1, "loop": true } }
            }
            """);
        File.WriteAllBytes(Path.Combine(dir, sheet), new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task ImportFromFolder_Valid_CopiesIntoUserRoot()
    {
        string src = MakeDir();
        WritePetFolder(src, "newpet", "New Pet");
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromFolderAsync(src, userRoot, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Entry);
        Assert.Equal("newpet", result.Entry!.Id);
        Assert.True(File.Exists(Path.Combine(userRoot, "newpet", "pet.json")));
        Assert.True(File.Exists(Path.Combine(userRoot, "newpet", "sheet.webp")));
    }

    [Fact]
    public async Task ImportFromFolder_NoPetJson_Fails()
    {
        string src = MakeDir();
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromFolderAsync(src, userRoot, _ => true);

        Assert.False(result.Success);
        Assert.Contains("pet.json", result.Message);
    }

    [Fact]
    public async Task ImportFromFolder_MissingSpritesheet_Fails()
    {
        string src = MakeDir();
        WritePetFolder(src, "p", "P");
        File.Delete(Path.Combine(src, "sheet.webp"));
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromFolderAsync(src, userRoot, _ => true);

        Assert.False(result.Success);
        Assert.Contains("spritesheet", result.Message);
    }

    [Fact]
    public async Task ImportFromFolder_MalformedJson_Fails()
    {
        string src = MakeDir();
        File.WriteAllText(Path.Combine(src, "pet.json"), "{ broken");
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromFolderAsync(src, userRoot, _ => true);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ImportFromFolder_DuplicateConfirmTrue_Overwrites()
    {
        string userRoot = MakeDir();
        string src1 = MakeDir();
        WritePetFolder(src1, "dup", "First");
        await PetImporter.ImportFromFolderAsync(src1, userRoot, _ => true);

        string src2 = MakeDir();
        WritePetFolder(src2, "dup", "Second");
        var result = await PetImporter.ImportFromFolderAsync(src2, userRoot, _ => true);

        Assert.True(result.Success);
        Assert.Equal("Second", result.Entry!.DisplayName);
    }

    [Fact]
    public async Task ImportFromFolder_DuplicateConfirmFalse_Cancels()
    {
        string userRoot = MakeDir();
        string src1 = MakeDir();
        WritePetFolder(src1, "dup", "First");
        await PetImporter.ImportFromFolderAsync(src1, userRoot, _ => true);

        string src2 = MakeDir();
        WritePetFolder(src2, "dup", "Second");
        var result = await PetImporter.ImportFromFolderAsync(src2, userRoot, _ => false);

        Assert.False(result.Success);
        // Original kept.
        string json = File.ReadAllText(Path.Combine(userRoot, "dup", "pet.json"));
        Assert.Contains("First", json);
    }

    [Fact]
    public async Task ImportFromZip_Flat_Installs()
    {
        string src = MakeDir();
        WritePetFolder(src, "zippet", "Zip Pet");
        string zipPath = Path.Combine(MakeDir(), "pet.zip");
        ZipFile.CreateFromDirectory(src, zipPath);   // pet.json at zip root
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromZipAsync(zipPath, userRoot, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(Path.Combine(userRoot, "zippet", "pet.json")));
    }

    [Fact]
    public async Task ImportFromZip_Nested_Installs()
    {
        string root = MakeDir();
        string nested = Path.Combine(root, "inner");
        WritePetFolder(nested, "nestpet", "Nested Pet"); // pet.json under inner/
        string zipPath = Path.Combine(MakeDir(), "nested.zip");
        ZipFile.CreateFromDirectory(root, zipPath);
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromZipAsync(zipPath, userRoot, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(Path.Combine(userRoot, "nestpet", "pet.json")));
    }

    [Fact]
    public async Task ImportFromZip_NoPetJson_Fails()
    {
        string root = MakeDir();
        File.WriteAllText(Path.Combine(root, "readme.txt"), "hi");
        string zipPath = Path.Combine(MakeDir(), "empty.zip");
        ZipFile.CreateFromDirectory(root, zipPath);
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromZipAsync(zipPath, userRoot, _ => true);

        Assert.False(result.Success);
        Assert.Contains("pet.json", result.Message);
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

- [ ] **Step 3: Build + test**

Run: `dotnet test tests/HyperPet.App.Tests/HyperPet.App.Tests.csproj -c Release -nologo`
Expected: green (10 new importer tests + prior).

- [ ] **Step 4: Commit**

```bash
git add src/HyperPet.App/Pets/PetImporter.cs tests/HyperPet.App.Tests/Pets/PetImporterTests.cs
git commit -m "feat: PetImporter (zip + folder, validate, confirm-overwrite, atomic install)"
```

---

### Task 4: App wiring (roots, discovery, rediscover, loader)

**Files:**
- Modify: `src/HyperPet.App/App.xaml.cs`

- [ ] **Step 1: Add root fields**

Next to `_petCatalog`:
```csharp
    private string _builtinPetsRoot = string.Empty;
    private string _userPetsRoot = string.Empty;
```

- [ ] **Step 2: Resolve roots + merged discovery**

Replace the existing discovery block:
```csharp
        string petsRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Pets");
        _petCatalog = await PetCatalog.DiscoverAsync(petsRoot);
        PetCatalogEntry? selectedEntry = PetCatalog.Resolve(_petCatalog, _settings.SelectedPet);
        SpritePet? spritePet = await TryLoadSpritePetAsync(_logger, selectedEntry);
```
with:
```csharp
        _builtinPetsRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Pets");
        _userPetsRoot = Path.Combine(settingsDirectory, "Pets");
        Directory.CreateDirectory(_userPetsRoot);
        _petCatalog = await PetCatalog.DiscoverAsync(_builtinPetsRoot, _userPetsRoot);
        PetCatalogEntry? selectedEntry = PetCatalog.Resolve(_petCatalog, _settings.SelectedPet);
        SpritePet? spritePet = await TryLoadSpritePetAsync(_logger, selectedEntry);
```
(`settingsDirectory` is already a local in `OnStartup`.)

- [ ] **Step 3: Make the loader re-discover (so imported pets resolve)**

Replace `LoadPetByIdAsync`:
```csharp
    private async Task<SpritePet?> LoadPetByIdAsync(string petId)
    {
        var catalog = await PetCatalog.DiscoverAsync(_builtinPetsRoot, _userPetsRoot);
        return await TryLoadSpritePetAsync(_logger!, PetCatalog.Resolve(catalog, petId));
    }
```

- [ ] **Step 4: Pass userPetsRoot + a rediscover delegate to MainWindow**

Change the MainWindow construction's trailing args from:
```csharp
            updateService,
            _petCatalog,
            LoadPetByIdAsync)
```
to:
```csharp
            updateService,
            _petCatalog,
            LoadPetByIdAsync,
            _userPetsRoot,
            () => PetCatalog.DiscoverAsync(_builtinPetsRoot, _userPetsRoot))
```

- [ ] **Step 5: Build**

Run: `dotnet build src/HyperPet.App/HyperPet.App.csproj -c Release -nologo`
Expected: fails until MainWindow gains the two new params (next task) — that's OK to do together. If you prefer a green checkpoint, do Task 5 before building. Otherwise proceed to Task 5 then build.

- [ ] **Step 6: Commit (after Task 5 builds green)**

Defer the commit to the end of Task 5 so the tree builds.

---

### Task 5: MainWindow passes userPetsRoot + rediscover into Settings

**Files:**
- Modify: `src/HyperPet.App/MainWindow.xaml.cs`

- [ ] **Step 1: Fields**

After `_petLoader`:
```csharp
    private readonly string _userPetsRoot;
    private readonly Func<Task<IReadOnlyList<PetCatalogEntry>>>? _rediscoverPets;
```

- [ ] **Step 2: Constructor params**

After `Func<string, Task<SpritePet?>>? petLoader = null`:
```csharp
        Func<string, Task<SpritePet?>>? petLoader = null,
        string? userPetsRoot = null,
        Func<Task<IReadOnlyList<PetCatalogEntry>>>? rediscoverPets = null)
```

- [ ] **Step 3: Assign**

After `_petLoader = petLoader;`:
```csharp
        _userPetsRoot = userPetsRoot ?? string.Empty;
        _rediscoverPets = rediscoverPets;
```

- [ ] **Step 4: Pass to SettingsWindow**

In `OnSettingsClick`, change the trailing args from:
```csharp
            _petCatalog,
            ReloadPetAsync)
```
to:
```csharp
            _petCatalog,
            ReloadPetAsync,
            _userPetsRoot,
            _rediscoverPets)
```

- [ ] **Step 5: Build (whole solution)**

Run: `dotnet build -c Release -nologo`
Expected: fails until SettingsWindow gains the two new params (next task). Do Task 6, then build green.

- [ ] **Step 6: Commit (after Task 6 builds green)**

Defer commit to end of Task 6.

---

### Task 6: Settings dialog — Add/Remove buttons + handlers

**Files:**
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml`
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs`

- [ ] **Step 1: XAML — buttons under the Pet combo**

In `src/HyperPet.App/Views/SettingsWindow.xaml`, the General tab has:
```xml
                        <StackPanel Margin="0,0,0,4">
                            <TextBlock Text="Pet" />
                            <ComboBox x:Name="PetPickerComboBox"
                                      Margin="0,6,0,10"
                                      DisplayMemberPath="DisplayName"
                                      SelectedValuePath="Id" />
                        </StackPanel>
```
Replace that `<StackPanel>` with one that adds a button row:
```xml
                        <StackPanel Margin="0,0,0,4">
                            <TextBlock Text="Pet" />
                            <ComboBox x:Name="PetPickerComboBox"
                                      Margin="0,6,0,6"
                                      DisplayMemberPath="DisplayName"
                                      SelectedValuePath="Id" />
                            <StackPanel Orientation="Horizontal"
                                        Margin="0,0,0,10">
                                <Button Content="Add from zip…"
                                        Padding="10,2"
                                        Click="OnAddPetFromZipClick" />
                                <Button Content="Add from folder…"
                                        Margin="6,0,0,0"
                                        Padding="10,2"
                                        Click="OnAddPetFromFolderClick" />
                                <Button x:Name="RemovePetButton"
                                        Content="Remove pet"
                                        Margin="6,0,0,0"
                                        Padding="10,2"
                                        IsEnabled="False"
                                        Click="OnRemovePetClick" />
                            </StackPanel>
                        </StackPanel>
```

- [ ] **Step 2: Code-behind — usings + fields**

In `src/HyperPet.App/Views/SettingsWindow.xaml.cs` add usings:
```csharp
using System.IO;
using Microsoft.Win32;
```
Make `_petCatalog` mutable (it is currently `private readonly IReadOnlyList<PetCatalogEntry> _petCatalog;`) — change to:
```csharp
    private IReadOnlyList<PetCatalogEntry> _petCatalog;
```
Add fields after `_reloadPet`:
```csharp
    private readonly string _userPetsRoot;
    private readonly Func<Task<IReadOnlyList<PetCatalogEntry>>>? _rediscoverPets;
```

- [ ] **Step 3: Constructor params + assignment**

After `Func<string, Task>? reloadPet = null`:
```csharp
        Func<string, Task>? reloadPet = null,
        string? userPetsRoot = null,
        Func<Task<IReadOnlyList<PetCatalogEntry>>>? rediscoverPets = null)
```
After `_reloadPet = reloadPet;`:
```csharp
        _userPetsRoot = userPetsRoot ?? string.Empty;
        _rediscoverPets = rediscoverPets;
```

- [ ] **Step 4: Enable Remove based on selection**

The combo populate block currently sets `PetPickerComboBox.SelectedValue`. Right after it, add an initial enable update, and wire selection changes. Find where `PetPickerComboBox.SelectionChanged += OnAnyChange;` is added in `WireDirtyTracking` and add a second handler call:
```csharp
        PetPickerComboBox.SelectionChanged += OnAnyChange;
        PetPickerComboBox.SelectionChanged += (_, _) => UpdateRemoveButtonState();
```
Add the method (near the other small UI helpers):
```csharp
    private void UpdateRemoveButtonState()
    {
        RemovePetButton.IsEnabled =
            PetPickerComboBox.SelectedItem is PetCatalogEntry entry
            && PetRemover.IsRemovable(entry.Directory, _userPetsRoot);
    }
```
Call it once at the end of the constructor (after the combo is populated), just before `Loaded += OnLoadedClearInitializing;`:
```csharp
        UpdateRemoveButtonState();
```

- [ ] **Step 5: Add handlers**

Add these methods (place after `OnUnselectAllAppsClick` or near the other Click handlers):
```csharp
    private async void OnAddPetFromZipClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a pet zip",
            Filter = "Pet zip (*.zip)|*.zip",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var result = await PetImporter.ImportFromZipAsync(dialog.FileName, _userPetsRoot, ConfirmOverwrite);
        await HandleImportResultAsync(result);
    }

    private async void OnAddPetFromFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a pet folder",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var result = await PetImporter.ImportFromFolderAsync(dialog.FolderName, _userPetsRoot, ConfirmOverwrite);
        await HandleImportResultAsync(result);
    }

    private bool ConfirmOverwrite(string id)
    {
        return MessageBox.Show(
            this,
            $"A pet with id '{id}' already exists. Replace it?",
            "HyperPet",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private async Task HandleImportResultAsync(PetImportResult result)
    {
        if (!result.Success)
        {
            if (result.Message is not null && result.Message != "Import cancelled.")
            {
                MessageBox.Show(this, result.Message, "HyperPet", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        await RefreshCatalogAsync();
        MessageBox.Show(
            this,
            $"Added '{result.Entry!.DisplayName}'. Select it from the Pet list and click Apply to use it.",
            "HyperPet",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void OnRemovePetClick(object sender, RoutedEventArgs e)
    {
        if (PetPickerComboBox.SelectedItem is not PetCatalogEntry entry
            || !PetRemover.IsRemovable(entry.Directory, _userPetsRoot))
        {
            return;
        }

        if (MessageBox.Show(
                this,
                $"Remove the pet '{entry.DisplayName}'? This deletes its files.",
                "HyperPet",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        PetRemover.TryRemove(entry.Directory, _userPetsRoot);

        bool removedActive = string.Equals(entry.Id, _settings.SelectedPet, StringComparison.OrdinalIgnoreCase);
        await RefreshCatalogAsync();

        if (removedActive && _petCatalog.Count > 0)
        {
            string fallbackId = _petCatalog[0].Id;
            _settings.SelectedPet = fallbackId;
            PetPickerComboBox.SelectedValue = fallbackId;
            _applySettings?.Invoke();           // persist
            if (_reloadPet is not null)
            {
                _ = _reloadPet(fallbackId);     // live-swap off the removed pet
            }
        }
    }

    private async Task RefreshCatalogAsync()
    {
        if (_rediscoverPets is null)
        {
            return;
        }

        string? selected = PetPickerComboBox.SelectedValue as string;
        _petCatalog = await _rediscoverPets();
        PetPickerComboBox.ItemsSource = _petCatalog;
        PetPickerComboBox.SelectedValue = selected;
        if (PetPickerComboBox.SelectedItem is null && _petCatalog.Count > 0)
        {
            PetPickerComboBox.SelectedIndex = 0;
        }
        UpdateRemoveButtonState();
    }
```

- [ ] **Step 6: Build whole solution + full tests**

Run: `dotnet build -c Release -nologo`
Expected: `Build succeeded.` 0 errors.
Run: `dotnet test -c Release -nologo`
Expected: all pass.

- [ ] **Step 7: Commit Tasks 4-6 together**

```bash
git add src/HyperPet.App/App.xaml.cs src/HyperPet.App/MainWindow.xaml.cs src/HyperPet.App/Views/SettingsWindow.xaml src/HyperPet.App/Views/SettingsWindow.xaml.cs
git commit -m "feat: Add/Remove pet UI wired through App and MainWindow"
```

---

### Task 7: Manual verification
- Settings > General: Add from folder → pick a valid pet folder → success message → appears in combo → select + Apply → live swap.
- Add from zip (flat + nested) → works. Invalid zip (no pet.json) → clear error.
- Add same id again → overwrite prompt (Yes replaces, No cancels).
- Select a user pet → Remove enabled → confirm → removed; if it was active, swaps to miku. Select miku → Remove disabled.
- Restart → imported pets persist (under %AppData%\HyperPet\Pets).

---

## Self-Review

**Spec coverage:**
- Persistent user-pets dir + ensure-created → Task 4.
- Multi-root discovery + dedup → Task 1.
- PetImporter (zip+folder, validate, confirm-overwrite, atomic) → Task 3.
- PetRemover (user-only) → Task 2.
- Loader re-discovers for fresh imports → Task 4 step 3.
- Add/Remove UI + rebind + remove-enable + active-pet fallback → Task 6.
- Wiring App→MainWindow→Settings → Tasks 4-6.
- Tests: importer (10), remover (3), catalog dedup (1) → Tasks 1-3. UI manual → Task 7. Matches spec.

**Placeholder scan:** none. Every code step is complete. Build-order caveat (Tasks 4-6 build green together) is explicit, with a single combined commit.

**Type consistency:**
- `PetImporter.ImportFromFolderAsync/ImportFromZipAsync(string, string, Func<string,bool>)` → `PetImportResult(bool, string?, PetCatalogEntry?)` — used in Task 6 handlers.
- `PetRemover.IsRemovable(string?, string?)` / `TryRemove(string, string)` — used in Task 6.
- `PetCatalog.DiscoverAsync(params string[])` / `Resolve` — Tasks 1,4.
- MainWindow ctor adds `petLoader, userPetsRoot, rediscoverPets` (App passes all three, Task 4 step 4). SettingsWindow ctor adds `reloadPet, userPetsRoot, rediscoverPets` (MainWindow passes, Task 5 step 4). Field `_petCatalog` made mutable in Settings (Task 6 step 2) for rebind.
- `OpenFolderDialog.FolderName` / `OpenFileDialog.FileName` — .NET 8 `Microsoft.Win32`.

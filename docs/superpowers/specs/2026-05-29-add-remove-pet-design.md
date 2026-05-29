# Add / Remove Pet

Date: 2026-05-29
Status: Approved (pending spec review)
Author: claude (with user)

## Problem

Pets are only discoverable if their folder is dropped manually into the
install directory. There is no in-app way to add a pet, and anything placed
in the install dir is wiped by Velopack updates. Users need to import pets
(and remove ones they added) from the UI, persistently.

## Goals

- Import a pet from a **.zip** (shareable) or a **folder** (local authoring).
- Store imported pets in a **persistent** location that survives updates.
- Validate imports; reject malformed ones with a clear message.
- Confirm-overwrite when an imported pet's id already exists.
- **Remove** a user-added pet (never a built-in one).

## Decisions (from brainstorming)

- Formats: **zip + folder**.
- Duplicate id on import: **confirm overwrite** (Yes replaces; No cancels).
- **Remove** user pets: yes (built-in pets cannot be removed).
- **No auto-select** after import: the pet is added to the list; the user
  selects it and clicks Apply to swap (live hot-swap already exists).

## Non-goals

- Editing pet.json in-app.
- Authoring/cropping spritesheets in-app.
- Per-source UI badges beyond enabling/disabling Remove.
- Downloading pets from a URL/gallery.

## Storage

- **Built-in pets:** `AppContext.BaseDirectory\Assets\Pets` (shipped; e.g. miku). Replaced on update — never written to by import.
- **User pets:** `%AppData%\HyperPet\Pets` (same parent as settings.json). Created on demand. Survives updates.
- A pet is **removable** iff its directory is under the user-pets root.

## Architecture

### `PetCatalog` (modify)

`DiscoverAsync(params string[] roots)` — scan each existing root for
`*/pet.json`, parse via `PetDefinitionLoader`, build entries. Dedup by id:
the **first** root wins (callers pass built-in root first, so a built-in id
shadows a user folder with the same id). Order result by display name.
Single-root callers (existing tests) keep working.

`PetCatalogEntry(string Id, string DisplayName, string Directory)` unchanged.

### `PetImporter` (new, `HyperPet.App.Pets`) — testable

```csharp
public sealed record PetImportResult(bool Success, string? Message, PetCatalogEntry? Entry);

public static class PetImporter
{
    // Validate + copy a staging folder (already on disk) into userPetsRoot/<id>.
    public static Task<PetImportResult> ImportFromFolderAsync(
        string sourceDirectory, string userPetsRoot, Func<string, bool> confirmOverwrite);

    // Extract zip to a temp dir, locate pet.json anywhere inside (nested-
    // tolerant), then run the folder import on that staging dir. Temp cleaned up.
    public static Task<PetImportResult> ImportFromZipAsync(
        string zipPath, string userPetsRoot, Func<string, bool> confirmOverwrite);
}
```

Shared install path (used by both):
1. Locate `pet.json` in the staging dir (folder import: must be at the root;
   zip import: searched recursively, its containing folder becomes staging).
2. Validate: `PetDefinitionLoader.LoadAsync(stagingDir)` (throws on bad json /
   frame bounds → caught → failure result). Then require the referenced
   spritesheet file to exist under staging.
3. Resolve `id` from the definition. Target = `userPetsRoot/<id>`.
4. If target exists: `confirmOverwrite(id)`; false → cancelled result; true →
   delete the existing target first.
5. Copy the staging directory's files into the target (all files, recursive),
   so multi-file pets work. Copy into a temp sibling then move into place so a
   mid-copy failure leaves no half-written pet.
6. Return success with a `PetCatalogEntry` for the new target.

All failure modes return `PetImportResult(false, message, null)` — never throw
to the UI. Messages are user-facing ("That folder has no pet.json.", "The
spritesheet 'x.webp' referenced by pet.json is missing.", etc.).

### `PetRemover` (new, small, `HyperPet.App.Pets`) — testable

```csharp
public static class PetRemover
{
    // True only when 'directory' is inside userPetsRoot (case-insensitive,
    // full-path normalized). Built-in pets return false.
    public static bool IsRemovable(string directory, string userPetsRoot);

    // Deletes the pet directory if removable. Returns false (no-op) when not
    // removable or missing.
    public static bool TryRemove(string directory, string userPetsRoot);
}
```

### Settings UI (General tab, by the Pet combo)

Three buttons under the existing Pet ComboBox:
- **Add from zip…** → `Microsoft.Win32.OpenFileDialog` (filter `*.zip`).
- **Add from folder…** → `Microsoft.Win32.OpenFolderDialog` (.NET 8 WPF built-in).
- **Remove pet** → enabled only when the selected entry `IsRemovable`.

Handlers (in `SettingsWindow`):
- Import: call the importer with `userPetsRoot` and a `confirmOverwrite` that
  shows a Yes/No MessageBox. On success → `await RefreshCatalogAsync()` →
  rebind combo → info message (no auto-select). On failure → warning message.
- Remove: confirm Yes/No → `PetRemover.TryRemove`. If the removed id equals
  `_settings.SelectedPet`, set `SelectedPet` to the first remaining entry,
  persist it, and live hot-swap via the existing reload callback. Then refresh
  + rebind. Selection-changed updates the Remove button's enabled state.

`SettingsWindow` gains: `string userPetsRoot`, a
`Func<Task<IReadOnlyList<PetCatalogEntry>>> rediscoverPets`, and reuses the
existing `Func<string, Task>? reloadPet`. `_petCatalog` becomes mutable so the
combo can rebind after import/remove.

### Wiring (App → MainWindow → SettingsWindow)

- App computes `_builtinPetsRoot = BaseDirectory/Assets/Pets` and
  `_userPetsRoot = %AppData%/HyperPet/Pets` (ensure-created).
- Discovery: `PetCatalog.DiscoverAsync(_builtinPetsRoot, _userPetsRoot)`.
- App passes `_userPetsRoot` and a `rediscoverPets` delegate
  (`() => PetCatalog.DiscoverAsync(_builtinPetsRoot, _userPetsRoot)`) through
  MainWindow into SettingsWindow, alongside the existing catalog + reload.
- `LoadPetByIdAsync` already resolves through the current catalog; after an
  import the live list is what the user selects from, so reload works for new
  pets too (it re-resolves via `PetCatalog.Resolve` over the rediscovered set —
  MainWindow refreshes its `_petCatalog` reference when Settings reports a
  change, or App's loader re-discovers on demand).

Loader note: to keep `LoadPetByIdAsync` correct for freshly imported pets,
it re-discovers both roots on each call (cheap — reads pet.json only):
```csharp
private async Task<SpritePet?> LoadPetByIdAsync(string petId)
{
    var catalog = await PetCatalog.DiscoverAsync(_builtinPetsRoot, _userPetsRoot);
    return await TryLoadSpritePetAsync(_logger!, PetCatalog.Resolve(catalog, petId));
}
```

## Error handling

- Missing/invalid zip or folder → failure message; nothing copied.
- No `pet.json` / bad json / missing spritesheet → failure message.
- Overwrite declined → cancelled, no change.
- Remove of a built-in or non-existent dir → no-op (button disabled anyway).
- Removing the active pet → swap to first remaining (miku always present).
- Copy failure → temp-then-move leaves the prior state intact.

## Testing

- `PetImporterTests`: valid folder import; missing pet.json; missing
  spritesheet; malformed pet.json; duplicate id with confirm=true (overwrites)
  and confirm=false (cancels); zip import (flat) and nested-folder zip. Uses
  temp dirs, a real generated zip, and a fake `confirmOverwrite`.
- `PetRemoverTests`: `IsRemovable` true under user root / false for built-in /
  false for outside paths; `TryRemove` deletes a user pet, no-ops a built-in.
- `PetCatalogTests`: extend with a multi-root dedup case (same id in both roots
  → built-in wins; distinct ids → both appear).
- UI (file/folder dialogs, button enable state) → manual verification.

## Manual verification

- Add a pet from a folder → appears in the combo → select + Apply → swaps live.
- Add the same pet again → overwrite prompt → Yes replaces, No cancels.
- Add from a zip (and a nested zip) → works.
- Add an invalid zip (no pet.json) → clear error, nothing added.
- Remove a user pet → confirm → gone from combo; if it was active, pet swaps to
  miku. Built-in miku selected → Remove disabled.
- Restart → imported pets persist (stored under %AppData%).

## Files

- Create: `src/HyperPet.App/Pets/PetImporter.cs`
- Create: `src/HyperPet.App/Pets/PetRemover.cs`
- Create: `tests/HyperPet.App.Tests/Pets/PetImporterTests.cs`
- Create: `tests/HyperPet.App.Tests/Pets/PetRemoverTests.cs`
- Modify: `src/HyperPet.App/Pets/PetCatalog.cs` (multi-root + dedup)
- Modify: `tests/HyperPet.App.Tests/Pets/PetCatalogTests.cs` (dedup case)
- Modify: `src/HyperPet.App/App.xaml.cs` (user-pets root, merged discovery, rediscover delegate, loader re-discovers)
- Modify: `src/HyperPet.App/MainWindow.xaml.cs` (pass userPetsRoot + rediscover through)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml` (Add/Remove buttons)
- Modify: `src/HyperPet.App/Views/SettingsWindow.xaml.cs` (handlers, rebind, remove-enable)

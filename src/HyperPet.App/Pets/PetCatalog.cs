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

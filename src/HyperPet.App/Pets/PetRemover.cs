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
        try { PetPathPolicy.RejectReparseAncestors(dir); }
        catch { return false; }
        return string.Equals(Path.GetDirectoryName(dir), root, StringComparison.OrdinalIgnoreCase);
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

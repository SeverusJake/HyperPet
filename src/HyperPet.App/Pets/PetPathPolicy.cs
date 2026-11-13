using System.IO;

namespace HyperPet.App.Pets;

internal static class PetPathPolicy
{
    public static string ResolveInstallTarget(string root, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || id.EndsWith('.') ||
            id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id is "." or ".." ||
            Path.IsPathRooted(id)) throw new InvalidOperationException("Pet id is not a safe folder name.");
        string stem = id.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
             "123456789¹²³".Contains(stem[3])))
            throw new InvalidOperationException("Pet id is a reserved Windows name.");
        string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string target = Path.GetFullPath(Path.Combine(parent, id));
        if (!string.Equals(Path.GetDirectoryName(target), parent, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Pet target must be a direct child of the pets folder.");
        RejectReparseAncestors(parent);
        RejectReparseAncestors(target);
        return target;
    }

    public static void ValidateSource(string source, string target)
    {
        source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source and installed pet folders must not overlap.");
        RejectReparseAncestors(source);
    }

    public static string ResolveContainedFile(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidOperationException("Spritesheet path must be relative.");
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Spritesheet must be inside the pet folder.");
        RejectReparseAncestors(path);
        return path;
    }

    public static void RejectReparseAncestors(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if (IsLink(current))
                throw new InvalidOperationException("Pet paths must not contain symbolic links or junctions.");
    }

    // Only name-surrogate reparse points redirect a path. Cloud placeholders
    // (OneDrive Files On-Demand) also carry the ReparsePoint attribute but stay
    // in place, so they have no link target and must remain importable.
    internal static bool IsLink(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        return info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0 && info.LinkTarget is not null;
    }
}

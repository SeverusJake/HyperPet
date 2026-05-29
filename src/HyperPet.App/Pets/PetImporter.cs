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

using System.IO;
using System.IO.Compression;
using HyperPet.Core.Pets;

namespace HyperPet.App.Pets;

public sealed record PetImportResult(bool Success, string? Message, PetCatalogEntry? Entry);

public static class PetImporter
{
    private const long MaxTotalBytes = 256L * 1024 * 1024;
    private const long MaxFileBytes = 64L * 1024 * 1024;
    private const int MaxFiles = 1024;
    private const int MaxDepth = 8;

    public static Task<PetImportResult> ImportFromFolderAsync(string sourceDirectory, string userPetsRoot,
        Func<string, bool> confirmOverwrite, IReadOnlySet<string>? protectedIds = null,
        CancellationToken cancellationToken = default)
        => ImportFromFolderCoreAsync(sourceDirectory, userPetsRoot, confirmOverwrite, new PetImportFileOperations(), protectedIds, cancellationToken);

    internal static async Task<PetImportResult> ImportFromFolderCoreAsync(string sourceDirectory, string userPetsRoot,
        Func<string, bool> confirmOverwrite, PetImportFileOperations operations, IReadOnlySet<string>? protectedIds = null,
        CancellationToken cancellationToken = default)
    {
        string? staging = null;
        string? backup = null;
        try
        {
            if (!Directory.Exists(sourceDirectory)) return new(false, "The selected folder does not exist.", null);
            var definition = await PetDefinitionLoader.LoadAsync(sourceDirectory);
            string target = PetPathPolicy.ResolveInstallTarget(userPetsRoot, definition.Id);
            PetPathPolicy.ValidateSource(sourceDirectory, target);
            if (protectedIds?.Contains(definition.Id) == true)
                return new(false, "A built-in pet already uses this id. Choose a different pet id.", null);
            if (!File.Exists(PetPathPolicy.ResolveContainedFile(sourceDirectory, definition.SpritesheetPath)))
                return new(false, "The spritesheet referenced by pet.json is missing.", null);
            Directory.CreateDirectory(userPetsRoot);
            // Siblings of the pets root stay on the same volume and cannot enter its catalog.
            string parent = Path.GetDirectoryName(Path.GetFullPath(userPetsRoot))!;
            staging = Path.Combine(parent, ".hyperpet-stage-" + Guid.NewGuid().ToString("N"));
            await Task.Run(() => CopyBounded(sourceDirectory, staging, cancellationToken), cancellationToken);
            await SpritePetLoader.LoadAsync(staging);
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(target))
            {
                // This continuation retains the caller's UI context. All I/O above is separate.
                if (!confirmOverwrite(definition.Id)) return new(false, "Import cancelled.", null);
                PetPathPolicy.RejectReparseAncestors(target);
                backup = Path.Combine(parent, ".hyperpet-backup-" + Guid.NewGuid().ToString("N"));
                operations.MoveDirectory(target, backup);
            }
            try { operations.MoveDirectory(staging, target); staging = null; }
            catch
            {
                if (backup is not null && !Directory.Exists(target))
                {
                    try { operations.MoveDirectory(backup, target); backup = null; }
                    catch (Exception rollback) { throw new IOException($"Install failed. Original pet is preserved at '{backup}'. {rollback.Message}"); }
                }
                throw;
            }
            string? warning = null;
            if (backup is not null)
            {
                try { Directory.Delete(backup, true); backup = null; }
                catch { warning = $"Pet installed. Previous copy is preserved at '{backup}'."; }
            }
            return new(true, warning, new(definition.Id, definition.DisplayName, target));
        }
        catch (Exception exception) { return new(false, $"Could not import the pet: {exception.Message}", null); }
        finally
        {
            // Never remove the backup here: a failed rollback must leave it recoverable.
            try { if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, true); }
            catch { }
        }
    }

    public static async Task<PetImportResult> ImportFromZipAsync(string zipPath, string userPetsRoot,
        Func<string, bool> confirmOverwrite, IReadOnlySet<string>? protectedIds = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(zipPath)) return new(false, "The selected zip does not exist.", null);
        string temporary = Path.Combine(Path.GetTempPath(), "HyperPet.import-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Task.Run(() => ExtractBounded(zipPath, temporary, cancellationToken), cancellationToken);
            string[] metadata = Directory.GetFiles(temporary, "pet.json", SearchOption.AllDirectories);
            if (metadata.Length != 1) return new(false, "The zip must contain exactly one pet.json.", null);
            return await ImportFromFolderAsync(Path.GetDirectoryName(metadata[0])!, userPetsRoot, confirmOverwrite, protectedIds, cancellationToken);
        }
        catch (Exception exception) { return new(false, $"Could not read the zip: {exception.Message}", null); }
        finally { try { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); } catch { } }
    }

    private static void CopyBounded(string source, string destination, CancellationToken cancellationToken)
    {
        long total = 0; int count = 0;
        void Copy(string from, string to, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth > MaxDepth) throw new InvalidOperationException("Pet folder exceeds maximum depth (8).");
            PetPathPolicy.RejectReparseAncestors(from);
            Directory.CreateDirectory(to);
            foreach (string file in Directory.EnumerateFiles(from))
            {
                cancellationToken.ThrowIfCancellationRequested();
                PetPathPolicy.RejectReparseAncestors(file);
                long size = new FileInfo(file).Length;
                if (++count > MaxFiles || size > MaxFileBytes || (total += size) > MaxTotalBytes)
                    throw new InvalidOperationException("Pet exceeds file count or size limits.");
                using var input = File.OpenRead(file);
                using var output = File.Create(Path.Combine(to, Path.GetFileName(file)));
                CopyStreamBounded(input, output, size, cancellationToken);
            }
            foreach (string directory in Directory.EnumerateDirectories(from)) Copy(directory, Path.Combine(to, Path.GetFileName(directory)), depth + 1);
        }
        Copy(source, destination, 0);
    }

    private static void ExtractBounded(string zipPath, string destination, CancellationToken cancellationToken)
    {
        if (new FileInfo(zipPath).Length > 100L * 1024 * 1024) throw new InvalidOperationException("Zip exceeds 100 MiB.");
        Directory.CreateDirectory(destination);
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count > MaxFiles) throw new InvalidOperationException("Zip exceeds 1024 entries.");
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000) throw new InvalidOperationException("Zip links are not allowed.");
            string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length > MaxDepth + 1)
                throw new InvalidOperationException("Zip exceeds maximum folder depth.");
            string target = PetPathPolicy.ResolveContainedFile(destination, relative);
            if (entry.Length > MaxFileBytes || (total += entry.Length) > MaxTotalBytes) throw new InvalidOperationException("Zip exceeds extraction size limits.");
            if (entry.Name.Length == 0) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write);
            CopyStreamBounded(input, output, entry.Length, cancellationToken);
        }
    }

    private static void CopyStreamBounded(Stream input, Stream output, long expected, CancellationToken token)
    {
        var buffer = new byte[81920]; long copied = 0; int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
        {
            token.ThrowIfCancellationRequested();
            copied += read;
            if (copied > expected || copied > MaxFileBytes) throw new InvalidOperationException("File grew beyond the validated size.");
            output.Write(buffer, 0, read);
        }
        if (copied != expected) throw new InvalidOperationException("File size changed during import.");
    }
}

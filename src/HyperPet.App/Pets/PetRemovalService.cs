using System.IO;

namespace HyperPet.App.Pets;

internal sealed record PetRemovalResult(bool Success, string? Error, string? RecoveryDirectory);

internal class PetRemovalFileOperations
{
    public virtual void Move(string source, string target) => Directory.Move(source, target);
    public virtual void Delete(string path) => Directory.Delete(path, recursive: true);
}

internal sealed class PetRemovalService(PetRemovalFileOperations? operations = null)
{
    private readonly PetRemovalFileOperations _files = operations ?? new();

    public async Task<PetRemovalResult> RemoveAsync(string userRoot, PetCatalogEntry entry, string activeId,
        IReadOnlyList<PetCatalogEntry> catalog,
        Func<string, CancellationToken, Task<PreparedPetReload?>> prepareFallback,
        Func<PreparedPetReload, bool> commitFallback, CancellationToken token)
    {
        string? quarantine = null;
        string source = entry.Directory;
        bool moved = false;
        try
        {
            if (!PetRemover.IsRemovable(source, userRoot) || !Directory.Exists(source))
                return new(false, "Pet folder is missing or protected.", null);
            bool active = string.Equals(entry.Id, activeId, StringComparison.OrdinalIgnoreCase);
            PreparedPetReload? prepared = null;
            if (active)
            {
                foreach (var candidate in catalog.Where(item => !string.Equals(item.Id, entry.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    token.ThrowIfCancellationRequested();
                    try { prepared = await prepareFallback(candidate.Id, token); }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    { /* Try the next installed fallback without deleting the active pet. */ }
                    if (prepared is not null &&
                        string.Equals(prepared.Pet.Definition.Id, candidate.Id, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(prepared.Pet.Definition.Id, entry.Id, StringComparison.OrdinalIgnoreCase)) break;
                    prepared = null;
                }
                if (prepared is null) return new(false, "No playable fallback pet is available. The current pet was kept.", null);
            }
            token.ThrowIfCancellationRequested();
            if (!PetRemover.IsRemovable(source, userRoot)) return new(false, "Pet path changed or is protected.", null);
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(userRoot));
            quarantine = Path.Combine(Path.GetDirectoryName(root)!, ".hyperpet-removed-" + Guid.NewGuid().ToString("N"));
            PetPathPolicy.RejectReparseAncestors(quarantine);
            _files.Move(source, quarantine); moved = true;
            if (active && !commitFallback(prepared!)) throw new InvalidOperationException("Fallback was superseded; removal cancelled.");
        }
        catch (Exception exception)
        {
            if (moved)
            {
                try { _files.Move(quarantine!, source); }
                catch (Exception rollback)
                { return new(false, exception.Message + "\nRestore failed: " + rollback.Message, quarantine); }
            }
            return new(false, exception is OperationCanceledException ? "Removal cancelled." : exception.Message, null);
        }

        // Logical removal succeeded. Recursive cleanup can partially fail: never
        // restore a partially deleted tree as though it were the original pet.
        try { await Task.Run(() => _files.Delete(quarantine!)); return new(true, null, null); }
        catch (Exception exception) { return new(true, "Pet removed; leftover files could not be cleaned: " + exception.Message, quarantine); }
    }
}

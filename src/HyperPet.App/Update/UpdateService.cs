using HyperPet.Core.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace HyperPet.App.Update;

/// <summary>
/// Thin wrapper over Velopack's <see cref="UpdateManager"/> pointed at the
/// HyperPet GitHub releases. Guards against dev / non-installed runs where
/// updates are not possible.
/// </summary>
public sealed class UpdateService
{
    private const string RepoUrl = "https://github.com/SeverusJake/HyperPet";

    private readonly HyperPetLogger? _logger;
    private readonly UpdateManager _manager;

    public UpdateService(HyperPetLogger? logger = null)
    {
        _logger = logger;
        // Public repo: no access token, stable releases only (prerelease=false).
        var source = new GithubSource(RepoUrl, null, false);
        _manager = new UpdateManager(source);
    }

    /// <summary>
    /// False when running from a raw build / dotnet run (not installed via
    /// the Velopack Setup.exe). Callers skip update checks when false.
    /// </summary>
    public bool IsSupported => _manager.IsInstalled;

    /// <summary>The currently installed version, or null on dev builds.</summary>
    public string? CurrentVersion => _manager.CurrentVersion?.ToString();

    /// <summary>
    /// Returns the available update, or null when already up to date.
    /// Throws on network / source errors — the caller decides whether to
    /// surface or swallow.
    /// </summary>
    public Task<UpdateInfo?> CheckAsync() => _manager.CheckForUpdatesAsync();

    /// <summary>
    /// Downloads the update then applies it and restarts the app. Does not
    /// return on success (the process is replaced).
    /// </summary>
    public async Task DownloadAndApplyAsync(UpdateInfo info, Action<int>? progress = null)
    {
        _logger?.Info($"UpdateService: downloading update to v{info.TargetFullRelease.Version}");
        await _manager.DownloadUpdatesAsync(info, progress, false);
        _logger?.Info("UpdateService: applying update and restarting");
        _manager.ApplyUpdatesAndRestart(info);
    }
}

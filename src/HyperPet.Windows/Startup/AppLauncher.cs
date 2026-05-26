using System.Diagnostics;

namespace HyperPet.Windows.Startup;

/// <summary>
/// Launches a Windows app by its AppUserModelId. Works for packaged apps
/// (Microsoft Store / UWP / WinRT) by using the <c>shell:AppsFolder\</c> path
/// that Explorer recognizes. Falls back gracefully when the AUMID is unknown.
/// </summary>
public sealed class AppLauncher : IAppLauncher
{
    public bool TryLaunch(string appUserModelId)
    {
        if (string.IsNullOrWhiteSpace(appUserModelId))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{appUserModelId}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"AppLauncher could not launch '{appUserModelId}': {exception}");
            return false;
        }
    }
}

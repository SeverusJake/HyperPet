using System.IO;

namespace HyperPet.App.Lifecycle;

internal sealed record AppLaunchContext(string DataDirectory, bool CanManageStartup, string ProfileKey)
{
    public static AppLaunchContext Resolve(bool development, bool installed, string roaming, string local)
        => development
            ? new(Path.Combine(local, "HyperPet", "Dev"), false, "dev")
            : new(Path.Combine(roaming, "HyperPet"), installed, "production");
}

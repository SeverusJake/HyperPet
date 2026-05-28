using System.Reflection;

namespace HyperPet.App;

/// <summary>
/// Exposes the running assembly's version for display in the UI.
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// The informational version (e.g. "0.3.8"), with any "+&lt;gitsha&gt;"
    /// build-metadata suffix stripped. Falls back to the assembly version,
    /// then "0.0.0".
    /// </summary>
    public static string Current { get; } = Resolve();

    /// <summary>"HyperPet v0.3.8" — for the context menu header and About tab.</summary>
    public static string DisplayString => $"HyperPet v{Current}";

    public static string Normalize(string? informational, string? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informational))
        {
            int plus = informational.IndexOf('+');
            string trimmed = plus >= 0 ? informational[..plus] : informational;
            trimmed = trimmed.Trim();
            if (trimmed.Length > 0)
            {
                return trimmed;
            }
        }

        if (!string.IsNullOrWhiteSpace(assemblyVersion))
        {
            return assemblyVersion!.Trim();
        }

        return "0.0.0";
    }

    private static string Resolve()
    {
        Assembly asm = typeof(AppVersion).Assembly;
        string? informational = asm
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        string? assemblyVersion = asm.GetName().Version?.ToString();
        return Normalize(informational, assemblyVersion);
    }
}

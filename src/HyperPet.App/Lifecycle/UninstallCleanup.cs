namespace HyperPet.App.Lifecycle;

internal static class UninstallCleanup
{
    public static void Run(Action removeStartupRegistration, Action removeUserData)
    {
        // Each cleanup is best-effort and independent: a locked data file must
        // not leave an autorun command behind, and registry failure must not
        // prevent the remaining uninstall cleanup.
        try { removeStartupRegistration(); }
        catch { }

        try { removeUserData(); }
        catch { }
    }
}

namespace HyperPet.Windows.Startup;

public interface IAppLauncher
{
    bool TryLaunch(string appUserModelId);
}

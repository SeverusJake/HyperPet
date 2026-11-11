using HyperPet.Core.Settings;

namespace HyperPet.App.Views;

public static class PetPlacement
{
    public static Placement Compute(
        HyperPetSettings settings, double width, double height, WorkArea workArea)
    {
        var defaults = HyperPetSettings.CreateDefault();
        double left = settings.RememberPetPosition && double.IsFinite(settings.PetLeft)
            ? settings.PetLeft : defaults.PetLeft;
        double top = settings.RememberPetPosition && double.IsFinite(settings.PetTop)
            ? settings.PetTop : defaults.PetTop;

        return new Placement(
            Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width)),
            Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height)));
    }
}

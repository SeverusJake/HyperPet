namespace HyperPet.App.Views;

public readonly record struct Placement(double Left, double Top);

/// <summary>
/// Computes where to open the Settings window relative to the pet: to the pet's left, falling back to its right when the left would overflow the work area. Both axes are clamped into the work area.
/// </summary>
public static class SettingsPlacement
{
    private const double Gap = 8;

    public static Placement Compute(
        double petLeft, double petTop, double petWidth, double petHeight,
        double settingsWidth, double settingsHeight,
        double waLeft, double waTop, double waRight, double waBottom)
    {
        double leftX = petLeft - Gap - settingsWidth;
        double left = leftX >= waLeft
            ? leftX                               // left of pet
            : petLeft + petWidth + Gap;           // fallback: right of pet

        double maxLeft = Math.Max(waLeft, waRight - settingsWidth);
        left = Math.Clamp(left, waLeft, maxLeft);

        double top = petTop;
        double maxTop = Math.Max(waTop, waBottom - settingsHeight);
        top = Math.Clamp(top, waTop, maxTop);

        return new Placement(left, top);
    }
}

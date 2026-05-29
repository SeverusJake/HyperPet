namespace HyperPet.App.Views;

public readonly record struct Placement(double Left, double Top);

/// <summary>
/// Computes where to open the Settings window relative to the pet: to the
/// pet's right, falling back to its left when the right would overflow the
/// work area. Both axes are clamped into the work area.
/// </summary>
public static class SettingsPlacement
{
    private const double Gap = 8;

    public static Placement Compute(
        double petLeft, double petTop, double petWidth, double petHeight,
        double settingsWidth, double settingsHeight,
        double waLeft, double waTop, double waRight, double waBottom)
    {
        double rightX = petLeft + petWidth + Gap;
        double left = rightX + settingsWidth <= waRight
            ? rightX
            : petLeft - Gap - settingsWidth;

        double maxLeft = Math.Max(waLeft, waRight - settingsWidth);
        left = Math.Clamp(left, waLeft, maxLeft);

        double top = petTop;
        double maxTop = Math.Max(waTop, waBottom - settingsHeight);
        top = Math.Clamp(top, waTop, maxTop);

        return new Placement(left, top);
    }
}

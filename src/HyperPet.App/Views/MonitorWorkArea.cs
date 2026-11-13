namespace HyperPet.App.Views;

/// <summary>A monitor work area. Units (physical pixels or DIPs) are the caller's.</summary>
public readonly record struct WorkArea(double Left, double Top, double Right, double Bottom);

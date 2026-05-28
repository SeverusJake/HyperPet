namespace HyperPet.App.Pets.Roaming;

/// <summary>
/// Supplies the set of surfaces the pet can perch on, and refreshes a single
/// ledge's current geometry.
/// </summary>
public interface ILedgeProvider
{
    /// <summary>All current perch surfaces (windows + taskbar + screen-top).</summary>
    IReadOnlyList<Ledge> GetLedges();

    /// <summary>
    /// Re-reads a window-backed ledge's current rectangle. Returns null when
    /// the window is gone, minimized, or hidden. Synthetic ledges (null Hwnd)
    /// return themselves unchanged.
    /// </summary>
    Ledge? TryRefresh(Ledge ledge);
}

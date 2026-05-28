namespace HyperPet.App.Pets.Roaming;

/// <summary>
/// A horizontal surface the pet can stand and walk on. Backed by a window
/// (Hwnd set) or synthetic (taskbar / screen-top, Hwnd null).
/// </summary>
public sealed record Ledge(IntPtr? Hwnd, double Left, double Right, double TopY)
{
    public double Width => Right - Left;

    /// <summary>
    /// Identity for "is this the same surface" comparisons. Window ledges are
    /// identified by handle; synthetic ledges by their geometry.
    /// </summary>
    public bool IsSameSurface(Ledge other)
    {
        if (Hwnd is not null || other.Hwnd is not null)
        {
            return Hwnd == other.Hwnd;
        }

        return TopY == other.TopY && Left == other.Left;
    }
}

using System.Windows;

namespace HyperPet.App.Views;

/// <summary>Finds a viewport beside the pet without moving the pet or covering it.</summary>
public static class NotificationPlacement
{
    private const double Gap = 12;

    public static Rect Compute(Rect petBounds, Size desiredSize, WorkArea workArea)
    {
        double width = Math.Max(0, workArea.Right - workArea.Left);
        double height = Math.Max(0, workArea.Bottom - workArea.Top);
        var regions = new[]
        {
            new Rect(workArea.Left, workArea.Top, width,
                Math.Clamp(petBounds.Top - Gap - workArea.Top, 0, height)),
            new Rect(Math.Clamp(petBounds.Right + Gap, workArea.Left, workArea.Right), workArea.Top,
                Math.Clamp(workArea.Right - petBounds.Right - Gap, 0, width), height),
            new Rect(workArea.Left, workArea.Top,
                Math.Clamp(petBounds.Left - Gap - workArea.Left, 0, width), height),
            new Rect(workArea.Left, Math.Clamp(petBounds.Bottom + Gap, workArea.Top, workArea.Bottom), width,
                Math.Clamp(workArea.Bottom - petBounds.Bottom - Gap, 0, height))
        };

        int best = -1;
        double bestArea = 0;
        for (int i = 0; i < regions.Length; i++)
        {
            Rect region = regions[i];
            if (region.Width >= desiredSize.Width && region.Height >= desiredSize.Height)
            {
                return Place(i, region, desiredSize, petBounds);
            }
            double area = Math.Min(desiredSize.Width, region.Width) * Math.Min(desiredSize.Height, region.Height);
            if (area > bestArea) { best = i; bestArea = area; }
        }

        if (best < 0) { return Rect.Empty; }
        return Place(best, regions[best], new Size(Math.Min(desiredSize.Width, regions[best].Width),
            Math.Min(desiredSize.Height, regions[best].Height)), petBounds);
    }

    private static Rect Place(int side, Rect region, Size size, Rect pet)
    {
        double x = Math.Clamp(pet.Left + pet.Width / 2 - size.Width / 2, region.Left, region.Right - size.Width);
        double y = Math.Clamp(pet.Top + pet.Height / 2 - size.Height / 2, region.Top, region.Bottom - size.Height);
        if (side == 0) { y = region.Bottom - size.Height; }
        if (side == 1) { x = region.Left; }
        if (side == 2) { x = region.Right - size.Width; }
        if (side == 3) { y = region.Top; }
        return new Rect(x, y, size.Width, size.Height);
    }
}

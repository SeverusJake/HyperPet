using System.Windows;
namespace HyperPet.App.Pets;
internal static class PetGeometry
{
    public static Rect SpriteBounds(double windowWidth, double windowHeight, double spriteWidth, double spriteHeight,
        double sourceWidth = 0, double sourceHeight = 0)
    {
        if (sourceWidth > 0 && sourceHeight > 0)
        {
            double scale = Math.Min(spriteWidth / sourceWidth, spriteHeight / sourceHeight);
            spriteWidth = sourceWidth * scale; spriteHeight = sourceHeight * scale;
        }
        return new((windowWidth - spriteWidth) / 2, (windowHeight - spriteHeight) / 2, spriteWidth, spriteHeight);
    }
    public static Point WindowOriginForSprite(Point spriteTopLeft, Rect bounds)
        => new(spriteTopLeft.X - bounds.Left, spriteTopLeft.Y - bounds.Top);
}

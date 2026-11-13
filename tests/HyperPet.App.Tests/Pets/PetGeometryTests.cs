using System.Windows;
using HyperPet.App.Pets;

namespace HyperPet.App.Tests.Pets;
public sealed class PetGeometryTests
{
    [Fact]
    public void UniformStretch_UsesRenderedSourceBounds()
    {
        Assert.Equal(new Rect(99, 34, 192, 192), PetGeometry.SpriteBounds(390, 260, 192, 208, 100, 100));
        Assert.Equal(new Rect(143, 26, 104, 208), PetGeometry.SpriteBounds(390, 260, 192, 208, 100, 200));
    }
    [Theory]
    [InlineData(1)] [InlineData(8)] [InlineData(10)]
    public void SpriteFoot_LandsOnLedgeRegardlessOfWindowPadding(int size)
    {
        var bounds = PetGeometry.SpriteBounds(390, 260, 192 * size / 8.0, 208 * size / 8.0);
        var origin = PetGeometry.WindowOriginForSprite(new Point(300, 900 - bounds.Height), bounds);
        Assert.Equal(900, origin.Y + bounds.Bottom, 8);
        Assert.Equal(300, origin.X + bounds.Left, 8);
    }
}

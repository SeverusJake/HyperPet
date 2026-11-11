using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HyperPet.App.Pets;

namespace HyperPet.App.Tests.Pets;

public sealed class SpritePetLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "HyperPet.SpriteLoader", Guid.NewGuid().ToString("N"));

    public SpritePetLoaderTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task LoadAsync_CropsFramesByRowAndColumn_PreservingColorAndAlpha()
    {
        WriteFixture();

        var pet = await SpritePetLoader.LoadAsync(_directory);

        Assert.Equal(2, pet.GetFrames("idle").Count);
        Assert.Equal(2, pet.GetFrames("runRight").Count);
        AssertPixels(pet.GetFrames("idle")[0], new byte[] { 0, 0, 255, 255, 0, 0, 255, 255 });
        AssertPixels(pet.GetFrames("idle")[1], new byte[] { 0, 255, 0, 128, 0, 255, 0, 128 });
        AssertPixels(pet.GetFrames("runRight")[0], new byte[] { 255, 0, 0, 255, 255, 0, 0, 255 });
        AssertPixels(pet.GetFrames("runRight")[1], new byte[8]);
    }

    [Fact]
    public async Task LoadAsync_LoadsBundledWebpPet_WithAllAnimationFrames()
    {
        string builtin = Path.Combine(AppContext.BaseDirectory, "Assets", "Pets", "miku-kimono.codex-pet");

        var pet = await SpritePetLoader.LoadAsync(builtin);

        Assert.Equal(9, pet.Frames.Count);
        foreach (var frames in pet.Frames.Values)
        {
            Assert.Equal(8, frames.Count);
            Assert.All(frames, frame =>
            {
                Assert.Equal(192, frame.PixelWidth);
                Assert.Equal(208, frame.PixelHeight);
                Assert.True(frame.IsFrozen);
            });
        }
    }

    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(2, 1, 1)]
    [InlineData(int.MaxValue, 1, 2)]
    public async Task LoadAsync_RejectsFramesOutsideSpritesheet(int row, int frameCount, int frameHeight)
    {
        WriteFixture(row, frameCount, frameHeight);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SpritePetLoader.LoadAsync(_directory));

        Assert.Contains("exceeds spritesheet bounds", error.Message);
    }

    [Fact]
    public async Task LoadAsync_RejectsInvalidImage_WithReadableError()
    {
        WriteFixture();
        await File.WriteAllBytesAsync(Path.Combine(_directory, "sheet.png"), new byte[] { 1, 2, 3 });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SpritePetLoader.LoadAsync(_directory));

        Assert.Contains("spritesheet", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private void WriteFixture(int row = 0, int frameCount = 2, int frameHeight = 1)
    {
        File.WriteAllText(Path.Combine(_directory, "pet.json"), JsonSerializer.Serialize(new
        {
            id = "fixture", spritesheetPath = "sheet.png", frameWidth = 2, frameHeight,
            states = new
            {
                idle = new { row, frames = frameCount, fps = 4 },
                runRight = new { row = 1, frames = 2, fps = 4 }
            }
        }));
        byte[] pixels =
        {
            0, 0, 255, 255, 0, 0, 255, 255, 0, 255, 0, 128, 0, 255, 0, 128,
            255, 0, 0, 255, 255, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0
        };
        var bitmap = BitmapSource.Create(4, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 16);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(_directory, "sheet.png"));
        encoder.Save(output);
    }

    private static void AssertPixels(BitmapSource frame, byte[] expected)
    {
        Assert.True(frame.IsFrozen);
        Assert.Equal(2, frame.PixelWidth);
        Assert.Equal(1, frame.PixelHeight);
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        byte[] actual = new byte[8];
        converted.CopyPixels(actual, 8, 0);
        Assert.Equal(expected, actual);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

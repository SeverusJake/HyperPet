using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HyperPet.Core.Pets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HyperPet.App.Pets;

public static class SpritePetLoader
{
    public static async Task<SpritePet> LoadAsync(string petDirectory)
    {
        PetDefinition definition = await PetDefinitionLoader.LoadAsync(petDirectory)
            .ConfigureAwait(false);
        string spritesheetPath = Path.Combine(petDirectory, definition.SpritesheetPath);

        using Image<Rgba32> spritesheet = await Image.LoadAsync<Rgba32>(spritesheetPath)
            .ConfigureAwait(false);

        var frames = new Dictionary<string, IReadOnlyList<BitmapSource>>(StringComparer.OrdinalIgnoreCase);
        foreach ((string stateName, PetAnimationState state) in definition.States)
        {
            var stateFrames = new List<BitmapSource>(state.Frames);
            for (int frameIndex = 0; frameIndex < state.Frames; frameIndex++)
            {
                var cropRectangle = new Rectangle(
                    frameIndex * definition.FrameWidth,
                    state.Row * definition.FrameHeight,
                    definition.FrameWidth,
                    definition.FrameHeight);

                ValidateCropRectangle(definition, stateName, frameIndex, cropRectangle, spritesheet);

                using Image<Rgba32> frame = spritesheet.Clone(context => context.Crop(cropRectangle));
                stateFrames.Add(CreateBitmapSource(frame));
            }

            frames[stateName] = stateFrames;
        }

        return new SpritePet(definition, frames);
    }

    private static void ValidateCropRectangle(
        PetDefinition definition,
        string stateName,
        int frameIndex,
        Rectangle cropRectangle,
        Image<Rgba32> spritesheet)
    {
        if (cropRectangle.Right <= spritesheet.Width && cropRectangle.Bottom <= spritesheet.Height)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Pet '{definition.Id}' state '{stateName}' frame {frameIndex} exceeds spritesheet bounds. " +
            $"Crop rectangle x={cropRectangle.X}, y={cropRectangle.Y}, width={cropRectangle.Width}, height={cropRectangle.Height}; " +
            $"spritesheet width={spritesheet.Width}, height={spritesheet.Height}.");
    }

    private static BitmapSource CreateBitmapSource(Image<Rgba32> frame)
    {
        int stride = frame.Width * 4;
        byte[] pixels = new byte[stride * frame.Height];

        frame.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> sourceRow = accessor.GetRowSpan(y);
                int rowOffset = y * stride;
                for (int x = 0; x < sourceRow.Length; x++)
                {
                    Rgba32 pixel = sourceRow[x];
                    int pixelOffset = rowOffset + (x * 4);
                    pixels[pixelOffset] = pixel.B;
                    pixels[pixelOffset + 1] = pixel.G;
                    pixels[pixelOffset + 2] = pixel.R;
                    pixels[pixelOffset + 3] = pixel.A;
                }
            }
        });

        BitmapSource bitmap = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();
        return bitmap;
    }
}

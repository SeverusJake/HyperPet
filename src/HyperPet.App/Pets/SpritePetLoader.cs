using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HyperPet.Core.Pets;
using SkiaSharp;

namespace HyperPet.App.Pets;

public static class SpritePetLoader
{
    public static async Task<SpritePet> LoadAsync(string petDirectory)
    {
        PetDefinition definition = await PetDefinitionLoader.LoadAsync(petDirectory)
            .ConfigureAwait(false);
        string path = PetPathPolicy.ResolveContainedFile(petDirectory, definition.SpritesheetPath);

        // Decode and crop off the UI thread.
        return await Task.Run(() => LoadFrames(definition, path)).ConfigureAwait(false);
    }

    private static SpritePet LoadFrames(PetDefinition definition, string path)
    {
        using var input = File.OpenRead(path);
        using var codec = SKCodec.Create(input)
            ?? throw new InvalidOperationException($"Could not decode spritesheet '{definition.SpritesheetPath}'.");
        if (codec.Info.Width > 8192 || codec.Info.Height > 8192 ||
            (long)codec.Info.Width * codec.Info.Height > 16_777_216 ||
            definition.States.Count > 32 || definition.States.Values.Sum(state => (long)state.Frames) > 256 ||
            definition.States.Values.Sum(state => (long)state.Frames) * definition.FrameWidth * definition.FrameHeight * 4 > 256L * 1024 * 1024)
            throw new InvalidOperationException("Pet exceeds decoded image/frame limits.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var spritesheet = SKBitmap.Decode(codec, info)
            ?? throw new InvalidOperationException($"Could not decode spritesheet '{definition.SpritesheetPath}'.");

        var frames = new Dictionary<string, IReadOnlyList<BitmapSource>>(StringComparer.OrdinalIgnoreCase);
        foreach ((string stateName, PetAnimationState state) in definition.States)
        {
            // Validate before allocating frames or casting crop coordinates.
            // Wide arithmetic prevents malformed metadata from wrapping.
            long right = (long)state.Frames * definition.FrameWidth;
            long bottom = ((long)state.Row + 1) * definition.FrameHeight;
            if (right > spritesheet.Width || bottom > spritesheet.Height)
            {
                throw new InvalidOperationException(
                    $"Pet '{definition.Id}' state '{stateName}' exceeds spritesheet bounds. " +
                    $"Required width={right}, height={bottom}; " +
                    $"spritesheet width={spritesheet.Width}, height={spritesheet.Height}.");
            }

            var stateFrames = new List<BitmapSource>(state.Frames);
            for (int frameIndex = 0; frameIndex < state.Frames; frameIndex++)
            {
                var cropRectangle = SKRectI.Create(
                    frameIndex * definition.FrameWidth,
                    state.Row * definition.FrameHeight,
                    definition.FrameWidth,
                    definition.FrameHeight);

                using var frame = new SKBitmap();
                if (!spritesheet.ExtractSubset(frame, cropRectangle))
                {
                    throw new InvalidOperationException($"Could not crop spritesheet state '{stateName}', frame {frameIndex}.");
                }

                // A subset may share parent rows. The final row includes only
                // this crop's pixels, not a full stride past its right edge.
                int bufferSize = checked((frame.Height - 1) * frame.RowBytes + frame.Width * 4);
                var bitmap = BitmapSource.Create(
                    frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null,
                    frame.GetPixels(), bufferSize, frame.RowBytes);
                bitmap.Freeze();
                stateFrames.Add(bitmap);
            }

            frames[stateName] = stateFrames;
        }

        return new SpritePet(definition, frames);
    }
}

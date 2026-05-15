using System.Windows.Media.Imaging;
using HyperPet.Core.Pets;

namespace HyperPet.App.Pets;

public sealed class SpritePet
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<BitmapSource>> _frames;

    public SpritePet(
        PetDefinition definition,
        IReadOnlyDictionary<string, IReadOnlyList<BitmapSource>> frames)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _frames = new Dictionary<string, IReadOnlyList<BitmapSource>>(
            frames ?? throw new ArgumentNullException(nameof(frames)),
            StringComparer.OrdinalIgnoreCase);
    }

    public PetDefinition Definition { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<BitmapSource>> Frames => _frames;

    public IReadOnlyList<BitmapSource> GetFrames(string stateName)
    {
        if (_frames.TryGetValue(stateName, out IReadOnlyList<BitmapSource>? requestedFrames))
        {
            return requestedFrames;
        }

        if (_frames.TryGetValue("idle", out IReadOnlyList<BitmapSource>? idleFrames))
        {
            return idleFrames;
        }

        return _frames.Values.First();
    }
}

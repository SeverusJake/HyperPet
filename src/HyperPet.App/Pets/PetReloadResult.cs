using HyperPet.Core.Pets;
namespace HyperPet.App.Pets;
public sealed record PetReloadResult(SpritePet? Pet, IReadOnlyDictionary<string, int>? OriginalFps,
    IReadOnlyDictionary<string, PlayMode>? OriginalModes, string? Error)
{
    public bool Success => Pet is not null;
}

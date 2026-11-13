using HyperPet.Core.Pets;
namespace HyperPet.App.Pets;
public sealed record PreparedPetReload(SpritePet Pet, IReadOnlyDictionary<string, int> OriginalFps,
    IReadOnlyDictionary<string, PlayMode> OriginalModes, int Generation);

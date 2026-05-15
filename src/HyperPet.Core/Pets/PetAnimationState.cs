namespace HyperPet.Core.Pets;

public sealed class PetAnimationState
{
    public int Row { get; set; }
    public int Frames { get; set; }
    public int Fps { get; set; }
    public bool Loop { get; set; } = true;
}

namespace HyperPet.Core.Pets;

public sealed class PetDefinition
{
    public string Id { get; init; } = "default";
    public string DisplayName { get; init; } = "Default";
    public string Description { get; init; } = string.Empty;
    public string SpritesheetPath { get; init; } = string.Empty;
    public string Kind { get; init; } = "person";
    public int FrameWidth { get; init; }
    public int FrameHeight { get; init; }
    public IReadOnlyDictionary<string, PetAnimationState> States { get; init; } =
        new Dictionary<string, PetAnimationState>(StringComparer.OrdinalIgnoreCase);

    public PetAnimationState GetState(string stateName)
    {
        if (States.TryGetValue(stateName, out PetAnimationState? state))
        {
            return state;
        }

        if (States.TryGetValue("idle", out PetAnimationState? idle))
        {
            return idle;
        }

        return States.Values.First();
    }
}

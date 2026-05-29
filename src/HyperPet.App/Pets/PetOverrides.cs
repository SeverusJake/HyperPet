using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Pets;

/// <summary>
/// Snapshots a pet's original animation fps / play-mode and applies the
/// user's per-pet overrides from settings onto the live PetDefinition.
/// Shared by startup and runtime pet reloads.
/// </summary>
public static class PetOverrides
{
    public static Dictionary<string, int> SnapshotFps(SpritePet pet) =>
        pet.Definition.States.ToDictionary(kv => kv.Key, kv => kv.Value.Fps, StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, PlayMode> SnapshotPlayMode(SpritePet pet) =>
        pet.Definition.States.ToDictionary(kv => kv.Key, kv => kv.Value.PlayMode, StringComparer.OrdinalIgnoreCase);

    /// <summary>Applies fps + play-mode overrides for the pet's id onto its definition.</summary>
    public static void Apply(SpritePet pet, HyperPetSettings settings)
    {
        string id = pet.Definition.Id;

        if (settings.StateSpeedOverrides.TryGetValue(id, out var fpsMap))
        {
            foreach (var (state, fps) in fpsMap)
            {
                if (pet.Definition.States.TryGetValue(state, out var s) && fps >= 1 && fps <= 60)
                {
                    s.Fps = fps;
                }
            }
        }

        if (settings.StatePlayModeOverrides.TryGetValue(id, out var modeMap))
        {
            foreach (var (state, mode) in modeMap)
            {
                if (pet.Definition.States.TryGetValue(state, out var s))
                {
                    s.PlayMode = mode;
                }
            }
        }
    }
}

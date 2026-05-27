using System.Text.Json.Serialization;

namespace HyperPet.Core.Pets;

/// <summary>
/// Frame iteration mode for a sprite animation state.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlayMode
{
    /// <summary>Play frames 1, 2, ..., N, 1, 2, ...</summary>
    Forward = 0,

    /// <summary>Play frames N, N-1, ..., 1, N, N-1, ...</summary>
    Reverse = 1,

    /// <summary>Play 1..N then N-1..1 then 2..N then N-1..1 (back-and-forth).</summary>
    PingPong = 2,
}

public sealed class PetAnimationState
{
    public int Row { get; set; }
    public int Frames { get; set; }
    public int Fps { get; set; }
    public bool Loop { get; set; } = true;
    public PlayMode PlayMode { get; set; } = PlayMode.Forward;
}

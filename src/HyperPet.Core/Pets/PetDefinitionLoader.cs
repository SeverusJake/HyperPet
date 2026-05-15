using System.Text.Json;

namespace HyperPet.Core.Pets;

public static class PetDefinitionLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<PetDefinition> LoadAsync(string petDirectory)
    {
        string jsonPath = Path.Combine(petDirectory, "pet.json");
        await using FileStream stream = File.OpenRead(jsonPath);
        PetDefinitionFile file = await JsonSerializer.DeserializeAsync<PetDefinitionFile>(stream, JsonOptions)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Pet metadata is empty.");

        var states = new Dictionary<string, PetAnimationState>(file.States, StringComparer.OrdinalIgnoreCase);
        if (states.Count == 0)
        {
            throw new InvalidOperationException("Pet metadata must define at least one animation state.");
        }

        string id = string.IsNullOrWhiteSpace(file.Id) ? Path.GetFileName(petDirectory) : file.Id;
        string displayName = string.IsNullOrWhiteSpace(file.DisplayName) ? id : file.DisplayName;

        return new PetDefinition
        {
            Id = id,
            DisplayName = displayName,
            Description = file.Description ?? string.Empty,
            SpritesheetPath = file.SpritesheetPath,
            Kind = string.IsNullOrWhiteSpace(file.Kind) ? "person" : file.Kind,
            FrameWidth = file.FrameWidth,
            FrameHeight = file.FrameHeight,
            States = states
        };
    }

    private sealed class PetDefinitionFile
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string SpritesheetPath { get; set; } = string.Empty;
        public string Kind { get; set; } = "person";
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public Dictionary<string, PetAnimationState> States { get; set; } = [];
    }
}

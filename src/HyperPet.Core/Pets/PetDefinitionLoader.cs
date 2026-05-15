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

        Validate(file);

        var states = file.States.ToDictionary(
            state => state.Key,
            state => state.Value!,
            StringComparer.OrdinalIgnoreCase);
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

    private static void Validate(PetDefinitionFile file)
    {
        if (file.FrameWidth <= 0)
        {
            throw new InvalidOperationException("Pet metadata frameWidth must be greater than zero.");
        }

        if (file.FrameHeight <= 0)
        {
            throw new InvalidOperationException("Pet metadata frameHeight must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(file.SpritesheetPath))
        {
            throw new InvalidOperationException("Pet metadata spritesheetPath must not be empty.");
        }

        if (file.States is null || file.States.Count == 0)
        {
            throw new InvalidOperationException("Pet metadata must define at least one animation state.");
        }

        foreach ((string stateName, PetAnimationState? state) in file.States)
        {
            if (state is null)
            {
                throw new InvalidOperationException($"Pet metadata state '{stateName}' must not be null.");
            }

            if (state.Row < 0)
            {
                throw new InvalidOperationException($"Pet metadata state '{stateName}' row must be greater than or equal to zero.");
            }

            if (state.Frames <= 0)
            {
                throw new InvalidOperationException($"Pet metadata state '{stateName}' frames must be greater than zero.");
            }

            if (state.Fps <= 0)
            {
                throw new InvalidOperationException($"Pet metadata state '{stateName}' fps must be greater than zero.");
            }
        }
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
        public Dictionary<string, PetAnimationState?> States { get; set; } = [];
    }
}

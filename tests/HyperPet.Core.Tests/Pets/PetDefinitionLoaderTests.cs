using HyperPet.Core.Pets;

namespace HyperPet.Core.Tests.Pets;

public sealed class PetDefinitionLoaderTests : IDisposable
{
    private readonly List<string> _tempDirectories = [];

    [Fact]
    public async Task LoadAsync_ReadsSpriteSheetMetadataAndStates()
    {
        string directory = CreatePetDirectory("""
        {
          "id": "miku-kimono",
          "displayName": "Miku Kimono",
          "spritesheetPath": "spritesheet.webp",
          "frameWidth": 128,
          "frameHeight": 208,
          "states": {
            "idle": { "row": 0, "frames": 12, "fps": 8, "loop": true },
            "waving": { "row": 3, "frames": 12, "fps": 8, "loop": false }
          }
        }
        """);

        PetDefinition pet = await PetDefinitionLoader.LoadAsync(directory);

        Assert.Equal("miku-kimono", pet.Id);
        Assert.Equal("Miku Kimono", pet.DisplayName);
        Assert.Equal("spritesheet.webp", pet.SpritesheetPath);
        Assert.Equal(128, pet.FrameWidth);
        Assert.Equal(208, pet.FrameHeight);
        Assert.Equal(3, pet.GetState("waving").Row);
        Assert.False(pet.GetState("waving").Loop);
    }

    [Fact]
    public async Task GetState_WhenRequestedStateMissing_FallsBackToIdle()
    {
        string directory = CreatePetDirectory("""
        {
          "id": "miku-kimono",
          "displayName": "Miku Kimono",
          "spritesheetPath": "spritesheet.webp",
          "frameWidth": 128,
          "frameHeight": 208,
          "states": {
            "idle": { "row": 0, "frames": 12, "fps": 8, "loop": true }
          }
        }
        """);

        PetDefinition pet = await PetDefinitionLoader.LoadAsync(directory);

        Assert.Equal(0, pet.GetState("missing").Row);
    }

    [Theory]
    [InlineData(0, 208, "frameWidth")]
    [InlineData(128, 0, "frameHeight")]
    public async Task LoadAsync_WhenFrameDimensionsAreInvalid_ThrowsInvalidOperationException(
        int frameWidth,
        int frameHeight,
        string expectedMessage)
    {
        string directory = CreatePetDirectory($$"""
        {
          "id": "miku-kimono",
          "displayName": "Miku Kimono",
          "spritesheetPath": "spritesheet.webp",
          "frameWidth": {{frameWidth}},
          "frameHeight": {{frameHeight}},
          "states": {
            "idle": { "row": 0, "frames": 12, "fps": 8, "loop": true }
          }
        }
        """);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => PetDefinitionLoader.LoadAsync(directory));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(0, 8, "frames")]
    [InlineData(12, 0, "fps")]
    public async Task LoadAsync_WhenStateFramesOrFpsAreInvalid_ThrowsInvalidOperationException(
        int frames,
        int fps,
        string expectedMessage)
    {
        string directory = CreatePetDirectory($$"""
        {
          "id": "miku-kimono",
          "displayName": "Miku Kimono",
          "spritesheetPath": "spritesheet.webp",
          "frameWidth": 128,
          "frameHeight": 208,
          "states": {
            "idle": { "row": 0, "frames": {{frames}}, "fps": {{fps}}, "loop": true }
          }
        }
        """);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => PetDefinitionLoader.LoadAsync(directory));

        Assert.Contains("idle", exception.Message);
        Assert.Contains(expectedMessage, exception.Message);
    }

    public void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private string CreatePetDirectory(string json)
    {
        string directory = Path.Combine(Path.GetTempPath(), "HyperPet.Pets", Guid.NewGuid().ToString("N"));
        _tempDirectories.Add(directory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "pet.json"), json);
        return directory;
    }
}

using System.IO;
using HyperPet.App.Pets;
using Xunit;

namespace HyperPet.App.Tests.Pets;

public class PetCatalogTests : IDisposable
{
    private readonly List<string> _temp = new();

    private string MakeRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "HyperPet.PetCatalog", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _temp.Add(root);
        return root;
    }

    private static void WritePet(string root, string folder, string id, string displayName)
    {
        string dir = Path.Combine(root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "pet.json"),
            $$"""
            {
              "id": "{{id}}",
              "displayName": "{{displayName}}",
              "spritesheetPath": "spritesheet.webp",
              "frameWidth": 192,
              "frameHeight": 208,
              "states": { "idle": { "row": 0, "frames": 1, "fps": 1, "loop": true } }
            }
            """);
    }

    [Fact]
    public async Task DiscoverAsync_FindsValidPets_SkipsFoldersWithoutJson()
    {
        string root = MakeRoot();
        WritePet(root, "alpha", "alpha", "Alpha Pet");
        WritePet(root, "beta", "beta", "Beta Pet");
        Directory.CreateDirectory(Path.Combine(root, "NotAPet"));

        var entries = await PetCatalog.DiscoverAsync(root);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Alpha Pet", entries[0].DisplayName);
        Assert.Contains(entries, e => e.Id == "beta");
    }

    [Fact]
    public async Task DiscoverAsync_MissingRoot_ReturnsEmpty()
    {
        var entries = await PetCatalog.DiscoverAsync(Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid().ToString("N")));
        Assert.Empty(entries);
    }

    [Fact]
    public async Task DiscoverAsync_SkipsMalformedJson()
    {
        string root = MakeRoot();
        WritePet(root, "good", "good", "Good");
        string bad = Path.Combine(root, "bad");
        Directory.CreateDirectory(bad);
        File.WriteAllText(Path.Combine(bad, "pet.json"), "{ broken");

        var entries = await PetCatalog.DiscoverAsync(root);

        Assert.Single(entries);
        Assert.Equal("good", entries[0].Id);
    }

    [Fact]
    public void Resolve_PrefersSelected_FallsBackToFirst()
    {
        var entries = new List<PetCatalogEntry>
        {
            new("a", "A", "dirA"),
            new("b", "B", "dirB"),
        };

        Assert.Equal("b", PetCatalog.Resolve(entries, "b")!.Id);
        Assert.Equal("a", PetCatalog.Resolve(entries, "missing")!.Id);
        Assert.Null(PetCatalog.Resolve(new List<PetCatalogEntry>(), "a"));
    }

    public void Dispose()
    {
        foreach (var d in _temp)
        {
            if (Directory.Exists(d))
            {
                Directory.Delete(d, true);
            }
        }
    }
}

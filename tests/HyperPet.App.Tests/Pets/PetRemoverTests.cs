using System.IO;
using HyperPet.App.Pets;
using Xunit;

namespace HyperPet.App.Tests.Pets;

public class PetRemoverTests : IDisposable
{
    private readonly List<string> _temp = new();

    private string MakeDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "HyperPet.PetRemover", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        _temp.Add(d);
        return d;
    }

    [Fact]
    public void IsRemovable_TrueUnderUserRoot_FalseOutside()
    {
        string userRoot = MakeDir();
        string petDir = Path.Combine(userRoot, "mypet");
        Directory.CreateDirectory(petDir);

        Assert.True(PetRemover.IsRemovable(petDir, userRoot));
        Assert.False(PetRemover.IsRemovable(MakeDir(), userRoot));
        Assert.False(PetRemover.IsRemovable(userRoot, userRoot));
        Assert.False(PetRemover.IsRemovable(null, userRoot));
    }

    [Fact]
    public void TryRemove_DeletesUserPet()
    {
        string userRoot = MakeDir();
        string petDir = Path.Combine(userRoot, "mypet");
        Directory.CreateDirectory(petDir);
        File.WriteAllText(Path.Combine(petDir, "pet.json"), "{}");

        bool removed = PetRemover.TryRemove(petDir, userRoot);

        Assert.True(removed);
        Assert.False(Directory.Exists(petDir));
    }

    [Fact]
    public void TryRemove_NoOpForOutsidePath()
    {
        string userRoot = MakeDir();
        string outside = MakeDir();

        Assert.False(PetRemover.TryRemove(outside, userRoot));
        Assert.True(Directory.Exists(outside));
    }

    public void Dispose()
    {
        foreach (var d in _temp)
        {
            if (Directory.Exists(d)) Directory.Delete(d, true);
        }
    }
}

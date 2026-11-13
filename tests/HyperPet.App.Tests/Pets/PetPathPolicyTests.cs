using System.Diagnostics;
using System.IO;
using HyperPet.App.Pets;

namespace HyperPet.App.Tests.Pets;

public sealed class PetPathPolicyTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "HyperPet.PathPolicy", Guid.NewGuid().ToString("N"));

    public PetPathPolicyTests() => Directory.CreateDirectory(_sandbox);

    [Fact]
    public void Junction_IsRejectedAsLink()
    {
        string target = Directory.CreateDirectory(Path.Combine(_sandbox, "target")).FullName;
        string junction = Path.Combine(_sandbox, "junction");
        // Junctions need no elevation, unlike symbolic links.
        using (var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{target}\"")
            { CreateNoWindow = true, UseShellExecute = false })!)
        {
            mklink.WaitForExit();
            Assert.Equal(0, mklink.ExitCode);
        }

        Assert.True(PetPathPolicy.IsLink(junction));
        Assert.Throws<InvalidOperationException>(() => PetPathPolicy.RejectReparseAncestors(Path.Combine(junction, "pet")));
    }

    [Fact]
    public void OrdinaryDirectoryAndMissingPath_AreNotLinks()
    {
        string plain = Directory.CreateDirectory(Path.Combine(_sandbox, "plain")).FullName;

        Assert.False(PetPathPolicy.IsLink(plain));
        Assert.False(PetPathPolicy.IsLink(Path.Combine(_sandbox, "missing")));
    }

    public void Dispose()
    {
        // Remove the junction itself first so recursion never follows it.
        string junction = Path.Combine(_sandbox, "junction");
        if (Directory.Exists(junction)) Directory.Delete(junction);
        if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, recursive: true);
    }
}

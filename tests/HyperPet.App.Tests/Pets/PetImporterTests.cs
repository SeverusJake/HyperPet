using System.IO;
using System.IO.Compression;
using HyperPet.App.Pets;
using Xunit;

namespace HyperPet.App.Tests.Pets;

public class PetImporterTests : IDisposable
{
    private readonly List<string> _temp = new();

    private string MakeDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "HyperPet.PetImporter", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        _temp.Add(d);
        return d;
    }

    private static void WritePetFolder(string dir, string id, string displayName, string sheet = "sheet.webp")
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "pet.json"),
            $$"""
            {
              "id": "{{id}}",
              "displayName": "{{displayName}}",
              "spritesheetPath": "{{sheet}}",
              "frameWidth": 192,
              "frameHeight": 208,
              "states": { "idle": { "row": 0, "frames": 1, "fps": 1, "loop": true } }
            }
            """);
        File.WriteAllBytes(Path.Combine(dir, sheet), new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task ImportFromFolder_Valid_CopiesIntoUserRoot()
    {
        string src = MakeDir();
        WritePetFolder(src, "newpet", "New Pet");
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromFolderAsync(src, userRoot, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Entry);
        Assert.Equal("newpet", result.Entry!.Id);
        Assert.True(File.Exists(Path.Combine(userRoot, "newpet", "pet.json")));
        Assert.True(File.Exists(Path.Combine(userRoot, "newpet", "sheet.webp")));
    }

    [Fact]
    public async Task ImportFromFolder_NoPetJson_Fails()
    {
        string src = MakeDir();
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromFolderAsync(src, userRoot, _ => true);

        Assert.False(result.Success);
        Assert.Contains("pet.json", result.Message);
    }

    [Fact]
    public async Task ImportFromFolder_MissingSpritesheet_Fails()
    {
        string src = MakeDir();
        WritePetFolder(src, "p", "P");
        File.Delete(Path.Combine(src, "sheet.webp"));
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromFolderAsync(src, userRoot, _ => true);

        Assert.False(result.Success);
        Assert.Contains("spritesheet", result.Message);
    }

    [Fact]
    public async Task ImportFromFolder_MalformedJson_Fails()
    {
        string src = MakeDir();
        File.WriteAllText(Path.Combine(src, "pet.json"), "{ broken");
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromFolderAsync(src, userRoot, _ => true);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ImportFromFolder_DuplicateConfirmTrue_Overwrites()
    {
        string userRoot = MakeDir();
        string src1 = MakeDir();
        WritePetFolder(src1, "dup", "First");
        await PetImporter.ImportFromFolderAsync(src1, userRoot, _ => true);

        string src2 = MakeDir();
        WritePetFolder(src2, "dup", "Second");
        var result = await PetImporter.ImportFromFolderAsync(src2, userRoot, _ => true);

        Assert.True(result.Success);
        Assert.Equal("Second", result.Entry!.DisplayName);
    }

    [Fact]
    public async Task ImportFromFolder_DuplicateConfirmFalse_Cancels()
    {
        string userRoot = MakeDir();
        string src1 = MakeDir();
        WritePetFolder(src1, "dup", "First");
        await PetImporter.ImportFromFolderAsync(src1, userRoot, _ => true);

        string src2 = MakeDir();
        WritePetFolder(src2, "dup", "Second");
        var result = await PetImporter.ImportFromFolderAsync(src2, userRoot, _ => false);

        Assert.False(result.Success);
        string json = File.ReadAllText(Path.Combine(userRoot, "dup", "pet.json"));
        Assert.Contains("First", json);
    }

    [Fact]
    public async Task ImportFromZip_Flat_Installs()
    {
        string src = MakeDir();
        WritePetFolder(src, "zippet", "Zip Pet");
        string zipPath = Path.Combine(MakeDir(), "pet.zip");
        ZipFile.CreateFromDirectory(src, zipPath);
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromZipAsync(zipPath, userRoot, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(Path.Combine(userRoot, "zippet", "pet.json")));
    }

    [Fact]
    public async Task ImportFromZip_Nested_Installs()
    {
        string root = MakeDir();
        string nested = Path.Combine(root, "inner");
        WritePetFolder(nested, "nestpet", "Nested Pet");
        string zipPath = Path.Combine(MakeDir(), "nested.zip");
        ZipFile.CreateFromDirectory(root, zipPath);
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromZipAsync(zipPath, userRoot, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(Path.Combine(userRoot, "nestpet", "pet.json")));
    }

    [Fact]
    public async Task ImportFromZip_NoPetJson_Fails()
    {
        string root = MakeDir();
        File.WriteAllText(Path.Combine(root, "readme.txt"), "hi");
        string zipPath = Path.Combine(MakeDir(), "empty.zip");
        ZipFile.CreateFromDirectory(root, zipPath);
        string userRoot = MakeDir();

        var result = await PetImporter.ImportFromZipAsync(zipPath, userRoot, _ => true);

        Assert.False(result.Success);
        Assert.Contains("pet.json", result.Message);
    }

    public void Dispose()
    {
        foreach (var d in _temp)
        {
            if (Directory.Exists(d)) Directory.Delete(d, true);
        }
    }
}

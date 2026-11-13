using System.IO;
using System.Text.Json;
using HyperPet.App.Pets;
using HyperPet.App.Tests.TestSupport;

namespace HyperPet.App.Tests.Pets;

[Collection("WPF UI")]
public sealed class PetImportSafetyTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "HyperPet.ImportSafety", Guid.NewGuid().ToString("N"));
    private string Folder(string name) { var p = Path.Combine(_sandbox, name); Directory.CreateDirectory(p); return p; }
    private static void WritePet(string directory, string id, string sheet = "sheet.png")
    {
        File.WriteAllText(Path.Combine(directory, "pet.json"), JsonSerializer.Serialize(new
        {
            id, displayName = id, spritesheetPath = sheet, frameWidth = 1, frameHeight = 1,
            states = new { idle = new { row = 0, frames = 1, fps = 1, loop = true } }
        }));
        using var bitmap = new SkiaSharp.SKBitmap(1, 1);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var output = File.Create(Path.Combine(directory, "sheet.png"));
        data.SaveTo(output);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("CON")]
    [InlineData("pet.")]
    public async Task UnsafeId_DoesNotDeleteMarkers(string id)
    {
        var source = Folder("source"); var root = Folder("parent/Pets");
        WritePet(source, id);
        var marker = Path.Combine(root, "keep.txt"); File.WriteAllText(marker, "keep");
        var result = await PetImporter.ImportFromFolderAsync(source, root, _ => true);
        Assert.False(result.Success);
        Assert.True(File.Exists(marker));
    }

    [Fact]
    public async Task ImportingOwnInstalledFolder_PreservesOriginal()
    {
        var root = Folder("Pets"); var source = Folder("Pets/self"); WritePet(source, "self");
        var result = await PetImporter.ImportFromFolderAsync(source, root, _ => true);
        Assert.False(result.Success);
        Assert.True(File.Exists(Path.Combine(source, "pet.json")));
    }

    [Fact]
    public async Task InvalidImage_DoesNotReplaceExistingPet()
    {
        var root = Folder("Pets"); var old = Folder("Pets/pet"); WritePet(old, "pet");
        var marker = Path.Combine(old, "keep.txt"); File.WriteAllText(marker, "keep");
        var source = Folder("source"); WritePet(source, "pet");
        File.WriteAllBytes(Path.Combine(source, "sheet.png"), new byte[] { 1, 2, 3 });
        var result = await PetImporter.ImportFromFolderAsync(source, root, _ => true);
        Assert.False(result.Success); Assert.True(File.Exists(marker));
    }

    [Fact]
    public async Task ExternalSpritesheet_IsRejected()
    {
        var source = Folder("source"); WritePet(source, "pet", "../outside.png");
        File.Copy(Path.Combine(source, "sheet.png"), Path.Combine(_sandbox, "outside.png"));
        Assert.False((await PetImporter.ImportFromFolderAsync(source, Folder("Pets"), _ => true)).Success);
    }

    [Fact]
    public async Task BuiltinIdCollision_IsRejected()
    {
        var source = Folder("source"); WritePet(source, "built-in");
        var result = await PetImporter.ImportFromFolderAsync(source, Folder("Pets"), _ => true, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "built-in" });
        Assert.False(result.Success);
        Assert.Contains("built-in", result.Message);
    }

    [Fact]
    public async Task CopyFailure_PreservesInstalledPet()
    {
        var source = Folder("source"); WritePet(source, "pet");
        var old = Folder("Pets/pet"); WritePet(old, "pet");
        string marker = Path.Combine(old, "keep.txt"); File.WriteAllText(marker, "keep");
        string locked = Path.Combine(source, "locked.txt"); File.WriteAllText(locked, "locked");
        using var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = await PetImporter.ImportFromFolderAsync(source, Folder("Pets"), _ => true);
        Assert.False(result.Success); Assert.True(File.Exists(marker));
    }

    [Fact]
    public Task OverwriteConfirmation_UsesCallerDispatcher() => StaThread.RunAsync(async () =>
    {
        var source = Folder("source"); WritePet(source, "pet");
        var root = Folder("Pets"); WritePet(Folder("Pets/pet"), "pet");
        var owner = new System.Windows.Window(); int called = 0;
        try
        {
            var result = await PetImporter.ImportFromFolderAsync(source, root, _ => { owner.VerifyAccess(); ++called; return false; });
            Assert.False(result.Success); Assert.Equal(1, called); Assert.Equal("Import cancelled.", result.Message);
        }
        finally { owner.Close(); }
    });

    [Fact]
    public async Task ExcessiveDepth_IsRejectedBeforeReplacingOldPet()
    {
        var source = Folder("source"); WritePet(source, "pet");
        string nested = source;
        for (int depth = 0; depth < 9; depth++) nested = Path.Combine(nested, "sub");
        Directory.CreateDirectory(nested);
        string old = Folder("Pets/pet"); WritePet(old, "pet");
        string marker = Path.Combine(old, "keep.txt"); File.WriteAllText(marker, "keep");
        var result = await PetImporter.ImportFromFolderAsync(source, Folder("Pets"), _ => true);
        Assert.False(result.Success); Assert.True(File.Exists(marker));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FailedSwap_PreservesOriginalOrRecoverableBackup(bool failRollback)
    {
        var source = Folder("source"); WritePet(source, "pet");
        var old = Folder("Pets/pet"); WritePet(old, "pet");
        File.WriteAllText(Path.Combine(old, "keep.txt"), "keep");
        var result = await PetImporter.ImportFromFolderCoreAsync(source, Folder("Pets"), _ => true, new FailedSwap(failRollback));
        Assert.False(result.Success);
        if (!failRollback) Assert.True(File.Exists(Path.Combine(old, "keep.txt")));
        else
        {
            var backup = Assert.Single(Directory.GetDirectories(_sandbox, ".hyperpet-backup-*"));
            Assert.True(File.Exists(Path.Combine(backup, "keep.txt")));
            Assert.Contains(backup, result.Message);
        }
    }

    private sealed class FailedSwap(bool rollbackAlsoFails) : PetImportFileOperations
    {
        private int _moves;
        public override void MoveDirectory(string source, string destination)
        {
            ++_moves;
            if (_moves == 2 || (_moves == 3 && rollbackAlsoFails)) throw new IOException("Injected directory move failure.");
            base.MoveDirectory(source, destination);
        }
    }

    public void Dispose() { if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, true); }
}

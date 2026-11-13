using HyperPet.Core.Diagnostics;

namespace HyperPet.Core.Tests.Diagnostics;
public sealed class HyperPetLoggerTests
{
    [Fact]
    public void InaccessibleLog_DoesNotEscapeOrRecurse()
    {
        string root = Path.Combine(Path.GetTempPath(), "HyperPet.LogTest", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string blocked = Path.Combine(root, "not-a-directory"); File.WriteAllText(blocked, "data");
            var logger = new HyperPetLogger(blocked);
            logger.Info("test"); logger.Error("test", new IOException("expected"));
            Assert.Equal("data", File.ReadAllText(blocked));
        }
        finally { Directory.Delete(root, true); }
    }
}

using HyperPet.App;
using Xunit;

namespace HyperPet.App.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("0.3.8", null, "0.3.8")]
    [InlineData("0.3.8+abc123", null, "0.3.8")]
    [InlineData("  0.3.8  ", null, "0.3.8")]
    [InlineData(null, "0.3.8.0", "0.3.8.0")]
    [InlineData("", "1.2.3.0", "1.2.3.0")]
    [InlineData(null, null, "0.0.0")]
    public void Normalize_StripsBuildMetadataAndFallsBack(string? informational, string? assemblyVersion, string expected)
    {
        Assert.Equal(expected, AppVersion.Normalize(informational, assemblyVersion));
    }

    [Fact]
    public void DisplayString_HasHyperPetPrefix()
    {
        Assert.StartsWith("HyperPet v", AppVersion.DisplayString);
    }
}

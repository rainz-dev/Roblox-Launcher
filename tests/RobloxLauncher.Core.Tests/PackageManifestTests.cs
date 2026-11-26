using RobloxLauncher.Core.Deployment;

namespace RobloxLauncher.Core.Tests;

public class PackageManifestTests
{
    private static string RealManifest => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rbxPkgManifest.txt"));

    [Fact]
    public void Parses_real_manifest()
    {
        var packages = PackageManifest.Parse(RealManifest);

        Assert.Equal(22, packages.Count);
        Assert.Equal(new Package("RobloxApp.zip", "60e244567d170679c8c497bc5ee888de", 138433382, 175470635), packages[0]);
        Assert.Equal("RobloxPlayerInstaller.exe", packages[^1].Name);
    }

    [Fact]
    public void Every_package_in_real_manifest_is_mapped_or_ignored()
    {
        // If this fails after refreshing the fixture, Roblox added a package: add it to PackageMap.
        foreach (var package in PackageManifest.Parse(RealManifest))
            Assert.True(PackageMap.IsIgnored(package.Name) || PackageMap.TryGetDestination(package.Name, out _), package.Name);
    }

    [Fact]
    public void Handles_windows_line_endings()
    {
        var packages = PackageManifest.Parse("v0\r\na.zip\r\n0123456789abcdef0123456789ABCDEF\r\n1\r\n2\r\n");
        Assert.Equal("0123456789abcdef0123456789abcdef", Assert.Single(packages).Md5);
    }

    [Theory]
    [InlineData("")]
    [InlineData("v1\na.zip\n0123456789abcdef0123456789abcdef\n1\n2")]
    [InlineData("v0\na.zip\n0123456789abcdef0123456789abcdef\n1")]
    [InlineData("v0\na.zip\nnot-a-hash\n1\n2")]
    [InlineData("v0\na.zip\n0123456789abcdef0123456789abcdef\n-1\n2")]
    [InlineData("v0\n..\\evil.zip\n0123456789abcdef0123456789abcdef\n1\n2")]
    public void Rejects_malformed_manifests(string text) =>
        Assert.Throws<FormatException>(() => PackageManifest.Parse(text));
}

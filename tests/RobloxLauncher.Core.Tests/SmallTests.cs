using RobloxLauncher.Core.Deployment;
using RobloxLauncher.Core.Install;
using RobloxLauncher.Core.Launch;

namespace RobloxLauncher.Core.Tests;

public class LaunchRequestTests
{
    [Fact]
    public void No_args_opens_the_app() =>
        Assert.Equal(new LaunchRequest(LauncherCommand.Launch, null), LaunchRequest.Parse([]));

    [Theory]
    [InlineData("roblox-player:1+launchmode:play+gameinfo:abc+placelauncherurl:https%3A%2F%2Fassetgame.roblox.com")]
    [InlineData("roblox://placeId=1818")]
    public void Passes_roblox_links_through_unchanged(string uri) =>
        Assert.Equal(new LaunchRequest(LauncherCommand.Launch, uri), LaunchRequest.Parse(["-player", uri]));

    [Fact]
    public void Ignores_non_roblox_links() =>
        Assert.Null(LaunchRequest.Parse(["https://example.com"]).Uri);

    [Fact]
    public void Redacts_login_ticket_from_logs()
    {
        string redacted = LaunchRequest.RedactForLog("roblox-player:1+launchmode:play+gameinfo:SECRET_TICKET-abc+launchtime:1");
        Assert.Equal("roblox-player:1+launchmode:play+gameinfo:<redacted>+launchtime:1", redacted);
    }

    [Theory]
    [InlineData("--register", LauncherCommand.Register)]
    [InlineData("--UNREGISTER", LauncherCommand.Unregister)]
    [InlineData("--settings", LauncherCommand.Settings)]
    public void Parses_commands(string arg, LauncherCommand expected) =>
        Assert.Equal(expected, LaunchRequest.Parse([arg]).Command);
}

public class VersionGuidTests
{
    [Theory]
    [InlineData("version-2c6fd5e09aaf4cfa", true)]
    [InlineData("version-", false)]
    [InlineData("version-..\\..\\Windows", false)]
    [InlineData("2c6fd5e09aaf4cfa", false)]
    [InlineData(null, false)]
    public void Only_accepts_expected_shape(string? value, bool valid) =>
        Assert.Equal(valid, DeploymentClient.IsValidVersionGuid(value));
}

public class SafeZipTests
{
    [Fact]
    public void Extracts_backslash_paths_into_subfolders()
    {
        using var temp = new TempDir();
        string zip = Path.Combine(temp.Path, "a.zip");
        File.WriteAllBytes(zip, TestZip.Create(("fonts\\Arial.ttf", "font")));

        SafeZip.ExtractToDirectory(zip, Path.Combine(temp.Path, "out"));

        Assert.Equal("font", File.ReadAllText(Path.Combine(temp.Path, "out", "fonts", "Arial.ttf")));
    }

    [Fact]
    public void Treats_leading_separators_and_root_entries_as_zip_relative()
    {
        // Real Roblox zips contain a "\\" directory entry and entries like "\\fonts\\a.ttf".
        using var temp = new TempDir();
        string zip = Path.Combine(temp.Path, "a.zip");
        File.WriteAllBytes(zip, TestZip.CreateWithDirectories(["\\", "\\fonts\\"], ("\\fonts\\a.ttf", "font"), ("/b.txt", "b")));

        SafeZip.ExtractToDirectory(zip, Path.Combine(temp.Path, "out"));

        Assert.Equal("font", File.ReadAllText(Path.Combine(temp.Path, "out", "fonts", "a.ttf")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(temp.Path, "out", "b.txt")));
    }

    [Theory]
    [InlineData("..\\..\\evil.txt")]
    [InlineData("\\..\\evil.txt")]
    [InlineData("../evil.txt")]
    public void Refuses_entries_outside_the_target(string entryName)
    {
        using var temp = new TempDir();
        string zip = Path.Combine(temp.Path, "a.zip");
        File.WriteAllBytes(zip, TestZip.Create((entryName, "x")));

        Assert.Throws<InvalidDataException>(() => SafeZip.ExtractToDirectory(zip, Path.Combine(temp.Path, "out", "inner")));
        Assert.False(File.Exists(Path.Combine(temp.Path, "evil.txt")));
        Assert.False(File.Exists(Path.Combine(temp.Path, "out", "evil.txt")));
    }
}

public class InstallStateTests
{
    [Fact]
    public void Round_trips()
    {
        using var temp = new TempDir();
        string file = Path.Combine(temp.Path, "state.json");
        new InstallState("version-abc123", "0.1.2").Save(file);
        Assert.Equal(new InstallState("version-abc123", "0.1.2"), InstallState.Load(file));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"VersionGuid":"..\\..\\Windows","Version":"1"}""")]
    public void Ignores_bad_or_unsafe_state(string content)
    {
        using var temp = new TempDir();
        string file = Path.Combine(temp.Path, "state.json");
        File.WriteAllText(file, content);
        Assert.Null(InstallState.Load(file));
    }
}

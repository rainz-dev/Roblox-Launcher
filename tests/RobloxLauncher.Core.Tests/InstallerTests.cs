using System.Net;
using RobloxLauncher.Core.Deployment;
using RobloxLauncher.Core.Install;

namespace RobloxLauncher.Core.Tests;

/// <summary>End-to-end install against a fake CDN.</summary>
public sealed class InstallerTests : IDisposable
{
    private const string Cdn1 = "https://cdn1.test";
    private const string Cdn2 = "https://cdn2.test";
    private const string Guid1 = "version-aaaa1111";
    private const string Guid2 = "version-bbbb2222";

    private readonly TempDir _temp = new();
    private readonly FakeHttpHandler _http = new();
    private readonly LauncherPaths _paths;
    private readonly DeploymentClient _client;
    private readonly Installer _installer;

    private readonly byte[] _app = TestZip.Create(("RobloxPlayerBeta.exe", "exe"), ("RobloxPlayerBeta.dll", "dll"));
    private readonly byte[] _fonts = TestZip.Create(("fonts\\Arial.ttf", "font"));
    private readonly byte[] _platformFonts = TestZip.Create(("Emoji.ttf", "emoji"));

    public InstallerTests()
    {
        _paths = new LauncherPaths(Path.Combine(_temp.Path, "launcher"));
        _client = new DeploymentClient(new HttpClient(_http), [Cdn1, Cdn2]);
        _installer = new Installer(_client, _paths);
    }

    public void Dispose() => _temp.Dispose();

    private static string Manifest(params (string Name, byte[] Data)[] packages) =>
        "v0\n" + string.Concat(packages.Select(p => $"{p.Name}\n{TestZip.Md5(p.Data)}\n{p.Data.Length}\n{p.Data.Length * 2}\n"));

    private void Publish(string host, string guid, params (string Name, byte[] Data)[] packages)
    {
        _http.Serve($"{host}/{guid}-rbxPkgManifest.txt", Manifest(packages));
        foreach (var (name, data) in packages) _http.Serve($"{host}/{guid}-{name}", data);
    }

    private (string, byte[])[] StandardPackages =>
    [
        ("RobloxApp.zip", _app),
        ("content-fonts.zip", _fonts),
        ("content-platform-fonts.zip", _platformFonts),
        ("RobloxPlayerInstaller.exe", "installer"u8.ToArray()),
    ];

    [Fact]
    public async Task Installs_packages_into_the_right_folders()
    {
        Publish(Cdn1, Guid1, StandardPackages);
        var progress = new List<InstallProgress>();

        string dir = await _installer.EnsureInstalledAsync(new ClientVersion("1.0", Guid1), new SyncProgress(progress), default);

        Assert.Equal(Path.Combine(_paths.Versions, Guid1), dir);
        Assert.True(File.Exists(Path.Combine(dir, "RobloxPlayerBeta.exe")));
        Assert.Equal("font", File.ReadAllText(Path.Combine(dir, "content", "fonts", "fonts", "Arial.ttf")));
        Assert.Equal("emoji", File.ReadAllText(Path.Combine(dir, "PlatformContent", "pc", "fonts", "Emoji.ttf")));
        Assert.Contains("<ContentFolder>content</ContentFolder>", File.ReadAllText(Path.Combine(dir, "AppSettings.xml")));
        Assert.Equal(new InstallState(Guid1, "1.0"), InstallState.Load(_paths.StateFile));
        Assert.True(_installer.IsInstalled(Guid1));

        // The installer .exe is never downloaded, and nothing staging-related is left behind.
        Assert.DoesNotContain(_http.Requests, r => r.EndsWith("RobloxPlayerInstaller.exe"));
        Assert.False(Directory.Exists(dir + ".staging"));
        Assert.False(File.Exists(_paths.InstallLockFile));

        var lastDownload = progress.Last(p => p.Stage == InstallStage.Downloading);
        Assert.Equal(lastDownload.Total, lastDownload.Done);
    }

    [Fact]
    public async Task Does_nothing_when_already_up_to_date()
    {
        Publish(Cdn1, Guid1, StandardPackages);
        await _installer.EnsureInstalledAsync(new ClientVersion("1.0", Guid1), null, default);
        int requests = _http.Requests.Count;

        await _installer.EnsureInstalledAsync(new ClientVersion("1.0", Guid1), null, default);

        Assert.Equal(requests, _http.Requests.Count);
    }

    [Fact]
    public async Task Update_reuses_unchanged_packages_and_removes_the_old_build()
    {
        Publish(Cdn1, Guid1, StandardPackages);
        await _installer.EnsureInstalledAsync(new ClientVersion("1.0", Guid1), null, default);

        byte[] newApp = TestZip.Create(("RobloxPlayerBeta.exe", "exe v2"));
        Publish(Cdn1, Guid2, ("RobloxApp.zip", newApp), ("content-fonts.zip", _fonts));
        _http.Requests.Clear();

        string dir = await _installer.EnsureInstalledAsync(new ClientVersion("2.0", Guid2), null, default);

        Assert.Equal("exe v2", File.ReadAllText(Path.Combine(dir, "RobloxPlayerBeta.exe")));
        Assert.Contains($"{Cdn1}/{Guid2}-RobloxApp.zip", _http.Requests);
        Assert.DoesNotContain($"{Cdn1}/{Guid2}-content-fonts.zip", _http.Requests); // cached by MD5
        Assert.False(Directory.Exists(Path.Combine(_paths.Versions, Guid1)));
        Assert.False(File.Exists(Path.Combine(_paths.Downloads, TestZip.Md5(_platformFonts)))); // no longer used
    }

    [Fact]
    public async Task Falls_back_to_the_next_cdn_on_errors_and_corrupt_downloads()
    {
        Publish(Cdn2, Guid1, StandardPackages);
        _http.Fail($"{Cdn1}/{Guid1}-rbxPkgManifest.txt");
        _http.Serve($"{Cdn1}/{Guid1}-RobloxApp.zip", "corrupted"u8.ToArray());

        string dir = await _installer.EnsureInstalledAsync(new ClientVersion("1.0", Guid1), null, default);

        Assert.Equal("exe", File.ReadAllText(Path.Combine(dir, "RobloxPlayerBeta.exe")));
        Assert.Contains($"{Cdn2}/{Guid1}-RobloxApp.zip", _http.Requests);
    }

    [Fact]
    public async Task Fails_cleanly_when_every_cdn_fails()
    {
        Publish(Cdn1, Guid1, StandardPackages);
        _http.Fail($"{Cdn1}/{Guid1}-content-fonts.zip", HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _installer.EnsureInstalledAsync(new ClientVersion("1.0", Guid1), null, default));

        Assert.False(_installer.IsInstalled(Guid1));
        Assert.False(Directory.Exists(Path.Combine(_paths.Versions, Guid1)));
        Assert.False(File.Exists(_paths.InstallLockFile));
    }

    [Fact]
    public async Task Stops_on_unknown_packages()
    {
        Publish(Cdn1, Guid1, ("RobloxApp.zip", _app), ("content-brand-new.zip", _fonts));

        var ex = await Assert.ThrowsAsync<UnknownPackageException>(() =>
            _installer.EnsureInstalledAsync(new ClientVersion("1.0", Guid1), null, default));
        Assert.Equal("content-brand-new.zip", ex.PackageName);
    }

    [Fact]
    public async Task Rejects_a_build_without_the_player_exe()
    {
        Publish(Cdn1, Guid1, ("RobloxApp.zip", TestZip.Create(("readme.txt", "no exe here"))));

        await Assert.ThrowsAsync<BootstrapException>(() =>
            _installer.EnsureInstalledAsync(new ClientVersion("1.0", Guid1), null, default));
        Assert.False(Directory.Exists(Path.Combine(_paths.Versions, Guid1)));
    }

    [Fact]
    public async Task Bootstrapper_installs_latest_version()
    {
        _http.Serve(DeploymentClient.VersionApiUrl, $$"""{"version":"1.0","clientVersionUpload":"{{Guid1}}","bootstrapperVersion":""}""");
        Publish(Cdn1, Guid1, StandardPackages);

        string dir = await new Bootstrapper(_client, _installer, _paths).PrepareAsync(null, default);

        Assert.Equal(Path.Combine(_paths.Versions, Guid1), dir);
    }

    [Fact]
    public async Task Bootstrapper_launches_installed_build_when_offline()
    {
        Publish(Cdn1, Guid1, StandardPackages);
        await _installer.EnsureInstalledAsync(new ClientVersion("1.0", Guid1), null, default);
        _http.Fail(DeploymentClient.VersionApiUrl);

        string dir = await new Bootstrapper(_client, _installer, _paths).PrepareAsync(null, default);

        Assert.Equal(Path.Combine(_paths.Versions, Guid1), dir);
    }

    [Fact]
    public async Task Bootstrapper_explains_when_offline_and_not_installed()
    {
        _http.Fail(DeploymentClient.VersionApiUrl);

        var ex = await Assert.ThrowsAsync<BootstrapException>(() =>
            new Bootstrapper(_client, _installer, _paths).PrepareAsync(null, default));
        Assert.Contains("internet", ex.Message);
    }

    [Fact]
    public async Task Bootstrapper_rejects_a_malicious_version_id()
    {
        _http.Serve(DeploymentClient.VersionApiUrl, """{"version":"1.0","clientVersionUpload":"..\\..\\Windows"}""");

        await Assert.ThrowsAsync<BootstrapException>(() =>
            new Bootstrapper(_client, _installer, _paths).PrepareAsync(null, default));
    }

    /// <summary>Progress&lt;T&gt; posts asynchronously; this records reports immediately.</summary>
    private sealed class SyncProgress(List<InstallProgress> sink) : IProgress<InstallProgress>
    {
        public void Report(InstallProgress value) { lock (sink) sink.Add(value); }
    }
}

using System.Diagnostics;
using RobloxLauncher.Core.Install;
using RobloxLauncher.Core.Settings;
using RobloxLauncher.Core.Tweaks;

namespace RobloxLauncher.Core.Tests;

internal sealed class FakeRegistry : IRegistryValues
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<string> GetValueNames() => Values.Keys;
    public string? GetValue(string name) => Values.GetValueOrDefault(name);
    public void SetValue(string name, string value) => Values[name] = value;
    public void DeleteValue(string name) => Values.Remove(name);
}

public sealed class PerformanceTweaksTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly FakeRegistry _gpu = new();
    private readonly FakeRegistry _compat = new();
    private readonly string _versions;
    private readonly PerformanceTweaks _tweaks;

    public PerformanceTweaksTests()
    {
        _versions = Path.Combine(_temp.Path, "Versions");
        _tweaks = new PerformanceTweaks(_gpu, _compat, _versions);
    }

    public void Dispose() => _temp.Dispose();

    private string Exe(string guid) => Path.Combine(_versions, guid, "RobloxPlayerBeta.exe");

    [Fact]
    public void Sets_switches_for_the_current_build()
    {
        _tweaks.ApplyBeforeLaunch(Exe("version-a"), new LauncherSettings { UseDedicatedGpu = true, DisableFullscreenOptimizations = true });

        Assert.Equal(PerformanceTweaks.HighPerformanceGpu, _gpu.Values[Exe("version-a")]);
        Assert.Equal(PerformanceTweaks.DisableFullscreenOptimizationsFlag, _compat.Values[Exe("version-a")]);
    }

    [Fact]
    public void Turning_off_removes_the_switch()
    {
        _tweaks.ApplyBeforeLaunch(Exe("version-a"), new LauncherSettings { UseDedicatedGpu = true });
        _tweaks.ApplyBeforeLaunch(Exe("version-a"), new LauncherSettings { UseDedicatedGpu = false });

        Assert.Empty(_gpu.Values);
    }

    [Fact]
    public void Update_moves_switches_to_the_new_build_and_leaves_other_apps_alone()
    {
        _gpu.Values[@"C:\Games\Other.exe"] = "GpuPreference=1;";
        _compat.Values[@"C:\Games\Other.exe"] = "~ RUNASADMIN";
        _gpu.Values["DirectXUserGlobalSettings"] = "VRROptimizeEnable=0;";
        var on = new LauncherSettings { UseDedicatedGpu = true, DisableFullscreenOptimizations = true };

        _tweaks.ApplyBeforeLaunch(Exe("version-a"), on);
        _tweaks.ApplyBeforeLaunch(Exe("version-b"), on);

        Assert.False(_gpu.Values.ContainsKey(Exe("version-a")));
        Assert.False(_compat.Values.ContainsKey(Exe("version-a")));
        Assert.True(_gpu.Values.ContainsKey(Exe("version-b")));
        Assert.Equal("GpuPreference=1;", _gpu.Values[@"C:\Games\Other.exe"]);
        Assert.Equal("~ RUNASADMIN", _compat.Values[@"C:\Games\Other.exe"]);
        Assert.Equal("VRROptimizeEnable=0;", _gpu.Values["DirectXUserGlobalSettings"]);
    }

    [Fact]
    public void Refuses_exes_outside_the_launcher_folder() =>
        Assert.Throws<ArgumentException>(() =>
            _tweaks.ApplyBeforeLaunch(Path.Combine(_temp.Path, "Elsewhere", "RobloxPlayerBeta.exe"), new LauncherSettings()));

    [Fact]
    public void Folder_with_a_similar_name_is_not_ours() =>
        Assert.Throws<ArgumentException>(() =>
            _tweaks.ApplyBeforeLaunch(Path.Combine(_temp.Path, "VersionsOld", "x", "RobloxPlayerBeta.exe"), new LauncherSettings()));

    [Fact]
    public void Priority_is_left_alone_when_off()
    {
        using var self = Process.GetCurrentProcess();
        var before = self.PriorityClass;
        PerformanceTweaks.ApplyAfterLaunch(self, new LauncherSettings { HighPriority = false });
        Assert.Equal(before, self.PriorityClass);
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void Missing_file_gives_defaults()
    {
        using var temp = new TempDir();
        var settings = new SettingsStore(Path.Combine(temp.Path, "settings.json")).Load();

        Assert.False(settings.HighPriority);
        Assert.False(settings.UseDedicatedGpu);
        Assert.False(settings.DisableFullscreenOptimizations);
    }

    [Fact]
    public void Ignores_settings_from_older_versions()
    {
        using var temp = new TempDir();
        string file = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(file, """{"HighPriority":true,"CloseAfterLaunch":false}""");
        Assert.True(new SettingsStore(file).Load().HighPriority);
    }

    [Fact]
    public void Round_trips()
    {
        using var temp = new TempDir();
        var store = new SettingsStore(Path.Combine(temp.Path, "sub", "settings.json"));
        store.Save(new LauncherSettings { HighPriority = true, UseDedicatedGpu = true });

        var loaded = store.Load();
        Assert.True(loaded.HighPriority);
        Assert.True(loaded.UseDedicatedGpu);
        Assert.False(loaded.DisableFullscreenOptimizations);
    }

    [Fact]
    public void Broken_file_gives_defaults()
    {
        using var temp = new TempDir();
        string file = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(file, "{ nope");
        Assert.False(new SettingsStore(file).Load().HighPriority);
    }
}

public class RemoveInstalledBuildsTests
{
    [Fact]
    public void Removes_builds_and_state_but_keeps_the_download_cache()
    {
        using var temp = new TempDir();
        var paths = new LauncherPaths(temp.Path);
        paths.EnsureCreated();
        Directory.CreateDirectory(Path.Combine(paths.Versions, "version-a"));
        File.WriteAllText(Path.Combine(paths.Versions, "version-a", "RobloxPlayerBeta.exe"), "exe");
        new InstallState("version-a", "1.0").Save(paths.StateFile);
        File.WriteAllText(Path.Combine(paths.Downloads, "abc"), "cached");

        Assert.True(Installer.RemoveInstalledBuilds(paths));

        Assert.Empty(Directory.GetDirectories(paths.Versions));
        Assert.False(File.Exists(paths.StateFile));
        Assert.True(File.Exists(Path.Combine(paths.Downloads, "abc")));
    }
}

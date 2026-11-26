namespace RobloxLauncher.Core;

/// <summary>Where the launcher keeps its files. Default root: %LocalAppData%\RobloxLauncher.</summary>
public sealed class LauncherPaths(string root)
{
    public static LauncherPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name));

    public string Root { get; } = root;

    /// <summary>One subfolder per installed Roblox build, named after its version id.</summary>
    public string Versions => Path.Combine(Root, "Versions");

    /// <summary>Downloaded packages, named by their MD5 so unchanged packages are reused across updates.</summary>
    public string Downloads => Path.Combine(Root, "Downloads");

    public string Logs => Path.Combine(Root, "Logs");

    public string StateFile => Path.Combine(Root, "state.json");

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string InstallLockFile => Path.Combine(Root, "install.lock");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Versions);
        Directory.CreateDirectory(Downloads);
        Directory.CreateDirectory(Logs);
    }
}

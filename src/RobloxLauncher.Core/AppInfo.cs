namespace RobloxLauncher.Core;

/// <summary>
/// The launcher's name and version. The name is used for the settings folder,
/// window titles and registry keys, so renaming the launcher is a one-line change here
/// (plus the AssemblyName in src/RobloxLauncher/RobloxLauncher.csproj).
/// </summary>
public static class AppInfo
{
    public const string Name = "RobloxLauncher";
    public const string Version = "0.1.0";
}

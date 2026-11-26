using System.IO;
using RobloxLauncher.Core;

namespace RobloxLauncher;

/// <summary>
/// Start menu shortcuts: "RobloxLauncher" (launch Roblox) and "RobloxLauncher Settings".
/// Created through Windows' own WScript.Shell COM object, so no extra libraries are needed.
/// </summary>
internal static class Shortcuts
{
    private static string StartMenuFolder => Environment.GetFolderPath(Environment.SpecialFolder.Programs);

    private static string LaunchShortcut => Path.Combine(StartMenuFolder, $"{AppInfo.Name}.lnk");

    private static string SettingsShortcut => Path.Combine(StartMenuFolder, $"{AppInfo.Name} Settings.lnk");

    public static void Create(string exePath)
    {
        Write(LaunchShortcut, exePath, "", "Launch Roblox");
        Write(SettingsShortcut, exePath, "--settings", $"{AppInfo.Name} settings");
    }

    public static void Remove()
    {
        File.Delete(LaunchShortcut);
        File.Delete(SettingsShortcut);
    }

    private static void Write(string shortcutPath, string exePath, string arguments, string description)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell isn't available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic link = shell.CreateShortcut(shortcutPath);
            link.TargetPath = exePath;
            link.Arguments = arguments;
            link.WorkingDirectory = Path.GetDirectoryName(exePath);
            link.IconLocation = $"{exePath},0";
            link.Description = description;
            link.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }
}

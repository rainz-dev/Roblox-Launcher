using Microsoft.Win32;
using RobloxLauncher.Core;

namespace RobloxLauncher;

/// <summary>
/// Makes Windows open roblox-player: and roblox: links with this launcher.
/// Writes only to HKEY_CURRENT_USER, so it needs no admin rights and only affects your account.
/// The previous handler (normally Roblox's own launcher) is backed up so --unregister can restore it.
/// </summary>
internal static class ProtocolRegistrar
{
    private static readonly string[] Schemes = ["roblox-player", "roblox"];

    private static string BackupKeyPath => $@"Software\{AppInfo.Name}\ProtocolBackup";

    private static string SchemeKeyPath(string scheme) => $@"Software\Classes\{scheme}";

    public static void Register(string exePath)
    {
        string command = $"\"{exePath}\" \"%1\"";
        string icon = $"\"{exePath}\",0";

        foreach (string scheme in Schemes)
        {
            using var key = Registry.CurrentUser.CreateSubKey(SchemeKeyPath(scheme));
            using var commandKey = key.CreateSubKey(@"shell\open\command");
            using var iconKey = key.CreateSubKey("DefaultIcon");

            string? existingCommand = commandKey.GetValue("") as string;
            if (existingCommand == command) continue;

            if (existingCommand is not null && !IsOurs(existingCommand))
                BackUp(scheme, existingCommand, iconKey.GetValue("") as string);

            key.SetValue("", $"URL: {scheme} Protocol");
            key.SetValue("URL Protocol", "");
            iconKey.SetValue("", icon);
            commandKey.SetValue("", command);
            Log.Info($"Registered {scheme}: links -> {exePath}");
        }
    }

    public static void Unregister()
    {
        var backups = new Dictionary<string, (string? Command, string? Icon)>();
        using (var backup = Registry.CurrentUser.OpenSubKey(BackupKeyPath))
        {
            foreach (string scheme in Schemes)
                backups[scheme] = (backup?.GetValue($"{scheme}.command") as string, backup?.GetValue($"{scheme}.icon") as string);
        }

        foreach (string scheme in Schemes)
        {
            var (command, icon) = backups[scheme];
            if (command is null)
            {
                Registry.CurrentUser.DeleteSubKeyTree(SchemeKeyPath(scheme), throwOnMissingSubKey: false);
                Log.Info($"Removed {scheme}: handler (nothing to restore).");
                continue;
            }

            using var key = Registry.CurrentUser.CreateSubKey(SchemeKeyPath(scheme));
            using (var commandKey = key.CreateSubKey(@"shell\open\command")) commandKey.SetValue("", command);
            if (icon is not null)
            {
                using var iconKey = key.CreateSubKey("DefaultIcon");
                iconKey.SetValue("", icon);
            }
            Log.Info($"Restored {scheme}: handler -> {command}");
        }

        Registry.CurrentUser.DeleteSubKeyTree(BackupKeyPath, throwOnMissingSubKey: false);
    }

    private static bool IsOurs(string command) =>
        command.Contains($"{AppInfo.Name}.exe", StringComparison.OrdinalIgnoreCase);

    private static void BackUp(string scheme, string command, string? icon)
    {
        using var backup = Registry.CurrentUser.CreateSubKey(BackupKeyPath);
        if (backup.GetValue($"{scheme}.command") is not null) return; // keep the original, not a later one
        backup.SetValue($"{scheme}.command", command);
        if (icon is not null) backup.SetValue($"{scheme}.icon", icon);
    }
}

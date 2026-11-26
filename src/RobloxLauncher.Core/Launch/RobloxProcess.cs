using System.Diagnostics;
using RobloxLauncher.Core.Install;

namespace RobloxLauncher.Core.Launch;

public static class RobloxProcess
{
    /// <summary>
    /// Starts RobloxPlayerBeta.exe. A game link is passed through unchanged as a single argument
    /// (never spliced into a command string); with no link, "--app" opens the Roblox home screen.
    /// </summary>
    public static Process Start(string installDir, string? launchUri)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(installDir, Installer.PlayerExeName))
        {
            WorkingDirectory = installDir,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(launchUri ?? "--app");

        return Process.Start(startInfo) ?? throw new BootstrapException("Roblox didn't start.");
    }
}

using System.Diagnostics;
using System.IO;
using System.Security;
using RobloxLauncher.Core;
using RobloxLauncher.Core.Deployment;
using RobloxLauncher.Core.Install;
using RobloxLauncher.Core.Launch;
using RobloxLauncher.Core.Settings;
using RobloxLauncher.Core.Tweaks;

namespace RobloxLauncher;

/// <summary>Update if needed, apply performance settings, start Roblox. Shared by the main window and link launches.</summary>
internal static class LaunchFlow
{
    public static async Task<Process> RunAsync(
        string? launchUri, LauncherSettings settings, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var paths = LauncherPaths.Default;
        using var http = DeploymentClient.CreateHttpClient();
        var client = new DeploymentClient(http);
        var installer = new Installer(client, paths);

        string installDir = await new Bootstrapper(client, installer, paths).PrepareAsync(progress, ct);
        progress.Report(new InstallProgress(InstallStage.Launching));

        string exe = Path.Combine(installDir, Installer.PlayerExeName);
        try
        {
            new PerformanceTweaks(RegistryValues.GpuPreferences, RegistryValues.CompatibilityLayers, paths.Versions)
                .ApplyBeforeLaunch(exe, settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // A tweak failing should never stop you from playing.
            Log.Warn($"Couldn't apply performance settings: {ex.Message}");
        }

        var roblox = RobloxProcess.Start(installDir, launchUri);
        PerformanceTweaks.ApplyAfterLaunch(roblox, settings);
        Log.Info("Roblox started.");
        return roblox;
    }
}

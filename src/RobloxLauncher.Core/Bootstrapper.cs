using RobloxLauncher.Core.Deployment;
using RobloxLauncher.Core.Install;

namespace RobloxLauncher.Core;

/// <summary>Checks for a Roblox update, installs it if needed, and returns the folder to launch from.</summary>
public sealed class Bootstrapper(DeploymentClient client, Installer installer, LauncherPaths paths)
{
    public async Task<string> PrepareAsync(IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        progress?.Report(new InstallProgress(InstallStage.CheckingForUpdates));

        ClientVersion latest;
        try
        {
            latest = await client.GetLatestVersionAsync(ct);
            Log.Info($"Latest Roblox: {latest.Version} ({latest.VersionGuid}).");
        }
        catch (Exception ex) when (DeploymentClient.IsTransient(ex, ct))
        {
            // Offline or the API is down: launch whatever is installed rather than failing.
            Log.Warn($"Version check failed: {ex.Message}");
            var state = InstallState.Load(paths.StateFile);
            if (state is not null && installer.IsInstalled(state.VersionGuid))
            {
                Log.Warn($"Launching installed build {state.Version} without updating.");
                return installer.GetInstallDirectory(state.VersionGuid);
            }
            throw new BootstrapException(
                "Couldn't reach Roblox to check for updates, and Roblox isn't installed yet. " +
                "Check your internet connection and try again.", ex);
        }

        return await installer.EnsureInstalledAsync(latest, progress, ct);
    }
}

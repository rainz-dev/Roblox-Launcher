using System.Windows.Controls;
using RobloxLauncher.Core.Install;

namespace RobloxLauncher;

/// <summary>Turns install progress into the status line, detail line and bar used by both windows.</summary>
internal static class ProgressText
{
    public static void Apply(InstallProgress progress, TextBlock status, TextBlock detail, ProgressBar bar)
    {
        status.Text = progress.Stage switch
        {
            InstallStage.CheckingForUpdates => "Checking for updates…",
            InstallStage.FetchingManifest => "Getting the package list…",
            InstallStage.Downloading => "Downloading Roblox…",
            InstallStage.Extracting => "Installing…",
            InstallStage.Launching => "Starting Roblox…",
            _ => status.Text,
        };

        if (progress.Fraction is double fraction)
        {
            bar.IsIndeterminate = false;
            bar.Value = fraction * 100;
        }
        else
        {
            bar.IsIndeterminate = true;
        }

        detail.Text = progress.Stage == InstallStage.Downloading && progress.Total > 0
            ? $"{ToMegabytes(progress.Done):0.0} / {ToMegabytes(progress.Total):0.0} MB"
            : progress.Detail ?? "";
    }

    private static double ToMegabytes(long bytes) => bytes / 1024d / 1024d;
}

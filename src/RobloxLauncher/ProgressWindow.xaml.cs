using System.Windows;
using RobloxLauncher.Core;
using RobloxLauncher.Core.Install;

namespace RobloxLauncher;

/// <summary>Small window shown while checking for updates, downloading and launching.</summary>
public partial class ProgressWindow : Window
{
    private readonly CancellationTokenSource _cancel = new();

    public ProgressWindow()
    {
        InitializeComponent();
        Title = AppInfo.Name;
    }

    /// <summary>Cancelled by the Cancel button or by closing the window.</summary>
    public CancellationToken CancellationToken => _cancel.Token;

    public void Report(InstallProgress progress)
    {
        if (_cancel.IsCancellationRequested) return;

        StatusText.Text = progress.Stage switch
        {
            InstallStage.CheckingForUpdates => "Checking for updates…",
            InstallStage.FetchingManifest => "Getting the package list…",
            InstallStage.Downloading => "Downloading Roblox…",
            InstallStage.Extracting => "Installing…",
            InstallStage.Launching => "Starting Roblox…",
            _ => StatusText.Text,
        };

        if (progress.Fraction is double fraction)
        {
            Bar.IsIndeterminate = false;
            Bar.Value = fraction * 100;
        }
        else
        {
            Bar.IsIndeterminate = true;
        }

        DetailText.Text = progress.Stage == InstallStage.Downloading && progress.Total > 0
            ? $"{ToMegabytes(progress.Done):0.0} / {ToMegabytes(progress.Total):0.0} MB"
            : progress.Detail ?? "";
    }

    private static double ToMegabytes(long bytes) => bytes / 1024d / 1024d;

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        StatusText.Text = "Cancelling…";
        _cancel.Cancel();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancel.Cancel();
        base.OnClosed(e);
    }
}

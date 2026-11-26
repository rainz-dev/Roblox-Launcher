using System.Windows;
using RobloxLauncher.Core;
using RobloxLauncher.Core.Deployment;
using RobloxLauncher.Core.Install;
using RobloxLauncher.Core.Launch;

namespace RobloxLauncher;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = LauncherPaths.Default;
        Log.Init(paths.Logs);
        Log.Info($"{AppInfo.Name} {AppInfo.Version} started. Args: {LaunchRequest.RedactForLog(string.Join(' ', e.Args))}");

        var request = LaunchRequest.Parse(e.Args);
        string exePath = Environment.ProcessPath!;
        ProgressWindow? window = null;

        try
        {
            switch (request.Command)
            {
                case LauncherCommand.Register:
                    ProtocolRegistrar.Register(exePath);
                    ShowInfo("Roblox links will now open with this launcher.");
                    Shutdown(0);
                    return;

                case LauncherCommand.Unregister:
                    ProtocolRegistrar.Unregister();
                    ShowInfo("Roblox links were given back to the official Roblox launcher.");
                    Shutdown(0);
                    return;
            }

            // Re-register every launch so the links follow the .exe if you move it.
            ProtocolRegistrar.Register(exePath);

            window = new ProgressWindow();
            window.Show();

            using var http = DeploymentClient.CreateHttpClient();
            var client = new DeploymentClient(http);
            var bootstrapper = new Bootstrapper(client, new Installer(client, paths), paths);

            string installDir = await bootstrapper.PrepareAsync(
                new Progress<InstallProgress>(window.Report), window.CancellationToken);

            window.Report(new InstallProgress(InstallStage.Launching));
            RobloxProcess.Start(installDir, request.Uri);
            Log.Info("Roblox started.");
            Shutdown(0);
        }
        catch (OperationCanceledException) when (window?.CancellationToken.IsCancellationRequested == true)
        {
            Log.Info("Cancelled by user.");
            Shutdown(1);
        }
        catch (Exception ex)
        {
            Log.Error("Launch failed.", ex);
            string message = ex is BootstrapException ? ex.Message : $"Something went wrong: {ex.Message}";
            MessageBox.Show($"{message}\n\nLog file: {Log.FilePath}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void ShowInfo(string message) =>
        MessageBox.Show(message, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
}

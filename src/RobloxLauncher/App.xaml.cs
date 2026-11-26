using System.Windows;
using RobloxLauncher.Core;
using RobloxLauncher.Core.Launch;
using RobloxLauncher.Core.Settings;

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

        switch (request.Command)
        {
            case LauncherCommand.Register:
                RunAndExit(() => ProtocolRegistrar.Register(exePath), "Roblox links will now open with this launcher.");
                return;
            case LauncherCommand.Unregister:
                RunAndExit(ProtocolRegistrar.Unregister, "Roblox links were given back to the official Roblox launcher.");
                return;
        }

        // Re-register every launch so the links follow the .exe if you move it.
        TryRegisterLinks(exePath);

        if (request.Uri is null)
        {
            // Opened by you: show the main window.
            new MainWindow().Show();
            return;
        }

        // Opened by a game link from the website: update, start the game, close.
        var window = new ProgressWindow();
        window.Show();
        try
        {
            var settings = new SettingsStore(paths.SettingsFile).Load();
            await LaunchFlow.RunAsync(request.Uri, settings, new Progress<Core.Install.InstallProgress>(window.Report), window.CancellationToken);
            Shutdown(0);
        }
        catch (OperationCanceledException) when (window.CancellationToken.IsCancellationRequested)
        {
            Log.Info("Cancelled by user.");
            Shutdown(1);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            Shutdown(1);
        }
    }

    internal static void ShowError(Exception ex)
    {
        Log.Error("Launch failed.", ex);
        string message = ex is BootstrapException ? ex.Message : $"Something went wrong: {ex.Message}";
        MessageBox.Show($"{message}\n\nLog file: {Log.FilePath}", AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void TryRegisterLinks(string exePath)
    {
        try
        {
            ProtocolRegistrar.Register(exePath);
        }
        catch (Exception ex)
        {
            Log.Warn($"Couldn't register Roblox links: {ex.Message}");
        }
    }

    private void RunAndExit(Action action, string successMessage)
    {
        try
        {
            action();
            MessageBox.Show(successMessage, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            Shutdown(1);
        }
    }
}

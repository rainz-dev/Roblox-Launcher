using System.Windows;
using RobloxLauncher.Core;
using RobloxLauncher.Core.Install;
using RobloxLauncher.Core.Launch;
using RobloxLauncher.Core.Settings;

namespace RobloxLauncher;

/// <summary>
/// Opening the launcher (or a game link) shows the small launch window, updates if needed and starts Roblox,
/// like the official launcher. Settings live in a separate window: --settings, the Start menu shortcut,
/// or the gear on the launch window.
/// </summary>
public partial class App : Application
{
    private SettingsWindow? _settingsWindow;
    private bool _launching;

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
                RunAndExit(() => { ProtocolRegistrar.Register(exePath); Shortcuts.Create(exePath); },
                    "Roblox links will now open with this launcher.");
                return;
            case LauncherCommand.Unregister:
                RunAndExit(() => { ProtocolRegistrar.Unregister(); Shortcuts.Remove(); },
                    "Roblox links were given back to the official Roblox launcher.");
                return;
        }

        // Every run: make sure links and Start menu shortcuts point at this .exe (it may have moved).
        Integrate(exePath);

        if (request.Command == LauncherCommand.Settings)
        {
            OpenSettings();
            return;
        }

        await LaunchAsync(request.Uri, paths);
    }

    private async Task LaunchAsync(string? uri, LauncherPaths paths)
    {
        var window = new ProgressWindow();
        window.Show();
        _launching = true;
        try
        {
            var settings = new SettingsStore(paths.SettingsFile).Load();
            await LaunchFlow.RunAsync(uri, settings, new Progress<InstallProgress>(window.Report), window.CancellationToken);
        }
        catch (OperationCanceledException) when (window.CancellationToken.IsCancellationRequested)
        {
            Log.Info("Cancelled by user.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _launching = false;
        }

        window.Close();
        // If the settings window was opened from the gear, keep running until it's closed.
        if (_settingsWindow is null) Shutdown(0);
    }

    /// <summary>Opens the settings window, or brings it to the front if it's already open.</summary>
    internal void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow();
        _settingsWindow.Show();
    }

    internal void OnSettingsClosed()
    {
        _settingsWindow = null;
        if (!_launching) Shutdown(0); // otherwise the launch finishes first, then exits
    }

    internal static void ShowError(Exception ex)
    {
        Log.Error("Launch failed.", ex);
        string message = ex is BootstrapException ? ex.Message : $"Something went wrong: {ex.Message}";
        MessageBox.Show($"{message}\n\nLog file: {Log.FilePath}", AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void Integrate(string exePath)
    {
        try
        {
            ProtocolRegistrar.Register(exePath);
        }
        catch (Exception ex)
        {
            Log.Warn($"Couldn't register Roblox links: {ex.Message}");
        }

        try
        {
            Shortcuts.Create(exePath);
        }
        catch (Exception ex)
        {
            Log.Warn($"Couldn't create Start menu shortcuts: {ex.Message}");
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

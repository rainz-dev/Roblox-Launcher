using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RobloxLauncher.Core;
using RobloxLauncher.Core.Install;
using RobloxLauncher.Core.Settings;

namespace RobloxLauncher;

/// <summary>The launcher's main window: Home (PLAY), Performance and Settings.</summary>
public partial class MainWindow : Window
{
    private readonly LauncherPaths _paths = LauncherPaths.Default;
    private readonly SettingsStore _store;
    private readonly LauncherSettings _settings;
    private CancellationTokenSource? _launchCancel;

    public MainWindow()
    {
        _store = new SettingsStore(_paths.SettingsFile);
        _settings = _store.Load();

        InitializeComponent();

        Title = AppInfo.Name;
        TitleText.Text = AppInfo.Name;
        VersionFooter.Text = $"Launcher {AppInfo.Version}";
        DataContext = _settings; // the Performance switches bind to these properties
        RefreshInstalledVersion();
        ShowPage("home");
    }

    // ---- Title bar ----

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnMinimizeClicked(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _launchCancel?.Cancel();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    // ---- Navigation ----

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (HomePage is null) return; // fires once during InitializeComponent, before the pages exist
        ShowPage((string)((FrameworkElement)sender).Tag);
    }

    private void ShowPage(string page)
    {
        HomePage.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
        PerformancePage.Visibility = page == "performance" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;

        bool soon = page is not ("home" or "performance" or "settings");
        SoonPage.Visibility = soon ? Visibility.Visible : Visibility.Collapsed;
        if (soon) SoonTitle.Text = page;
    }

    // ---- Home ----

    private void RefreshInstalledVersion()
    {
        var state = InstallState.Load(_paths.StateFile);
        InstalledVersionText.Text = state is null
            ? "Roblox isn't installed yet. PLAY will download it."
            : $"Version {state.Version}";
    }

    private async void OnPlayClicked(object sender, RoutedEventArgs e)
    {
        _launchCancel = new CancellationTokenSource();
        SetLaunching(true);
        try
        {
            await LaunchFlow.RunAsync(null, _settings, new Progress<InstallProgress>(ReportProgress), _launchCancel.Token);

            if (_settings.CloseAfterLaunch)
            {
                Close();
                return;
            }
        }
        catch (OperationCanceledException) when (_launchCancel.IsCancellationRequested)
        {
            Log.Info("Cancelled by user.");
        }
        catch (Exception ex)
        {
            App.ShowError(ex);
        }

        SetLaunching(false);
        RefreshInstalledVersion();
    }

    private void OnCancelLaunchClicked(object sender, RoutedEventArgs e)
    {
        CancelLaunchButton.IsEnabled = false;
        LaunchStatusText.Text = "Cancelling…";
        _launchCancel?.Cancel();
    }

    private void SetLaunching(bool launching)
    {
        PlayButton.IsEnabled = !launching;
        PlayHintText.Visibility = launching ? Visibility.Collapsed : Visibility.Visible;
        CancelLaunchButton.Visibility = launching ? Visibility.Visible : Visibility.Collapsed;
        CancelLaunchButton.IsEnabled = true;
        LaunchProgressPanel.Visibility = launching ? Visibility.Visible : Visibility.Collapsed;
        LaunchProgressBar.IsIndeterminate = true;
        LaunchStatusText.Text = "Starting…";
        LaunchDetailText.Text = "";
    }

    private void ReportProgress(InstallProgress progress)
    {
        if (_launchCancel?.IsCancellationRequested == true) return;
        ProgressText.Apply(progress, LaunchStatusText, LaunchDetailText, LaunchProgressBar);
    }

    // ---- Performance ----

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Couldn't save settings.", ex);
            MessageBox.Show($"Couldn't save your settings: {ex.Message}", AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---- Settings ----

    private void OnRegisterClicked(object sender, RoutedEventArgs e) => RunSettingsAction(
        () => ProtocolRegistrar.Register(Environment.ProcessPath!), "Roblox links now open with this launcher.");

    private void OnUnregisterClicked(object sender, RoutedEventArgs e) => RunSettingsAction(
        ProtocolRegistrar.Unregister,
        "Roblox links were given back to the official launcher. Opening this launcher again takes them back.");

    private void OnOpenLogsClicked(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_paths.Logs);
        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { _paths.Logs } });
    }

    private void OnReinstallClicked(object sender, RoutedEventArgs e)
    {
        bool done = Installer.RemoveInstalledBuilds(_paths);
        SettingsStatusText.Text = done
            ? "Roblox was removed. Press PLAY to install it again."
            : "Some files are in use. Close Roblox and try again.";
        RefreshInstalledVersion();
    }

    private void RunSettingsAction(Action action, string success)
    {
        try
        {
            action();
            SettingsStatusText.Text = success;
        }
        catch (Exception ex)
        {
            Log.Error("Settings action failed.", ex);
            SettingsStatusText.Text = $"That didn't work: {ex.Message}";
        }
    }
}

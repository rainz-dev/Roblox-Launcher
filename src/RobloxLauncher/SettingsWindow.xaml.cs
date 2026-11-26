using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using RobloxLauncher.Core;
using RobloxLauncher.Core.Install;
using RobloxLauncher.Core.Settings;

namespace RobloxLauncher;

/// <summary>
/// The settings app: opened with --settings, the "RobloxLauncher Settings" shortcut,
/// or the gear on the launch window. Changes are saved immediately and apply on the next launch.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly LauncherPaths _paths = LauncherPaths.Default;
    private readonly SettingsStore _store;
    private readonly LauncherSettings _settings;

    public SettingsWindow()
    {
        _store = new SettingsStore(_paths.SettingsFile);
        _settings = _store.Load();

        InitializeComponent();

        Title = $"{AppInfo.Name} Settings";
        TitleText.Text = Title;
        VersionFooter.Text = $"Launcher {AppInfo.Version}";
        DataContext = _settings; // the Performance switches bind to these properties
        BuildFastFlagControls();
        ShowPage("performance");
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
        base.OnClosed(e);
        ((App)Application.Current).OnSettingsClosed();
    }

    // ---- Navigation ----

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (PerformancePage is null) return; // fires once during InitializeComponent, before the pages exist
        ShowPage((string)((FrameworkElement)sender).Tag);
    }

    private void ShowPage(string page)
    {
        PerformancePage.Visibility = page == "performance" ? Visibility.Visible : Visibility.Collapsed;
        FastFlagsPage.Visibility = page == "fastflags" ? Visibility.Visible : Visibility.Collapsed;
        GeneralPage.Visibility = page == "general" ? Visibility.Visible : Visibility.Collapsed;

        bool soon = page is not ("performance" or "fastflags" or "general");
        SoonPage.Visibility = soon ? Visibility.Visible : Visibility.Collapsed;
        if (soon) SoonTitle.Text = page;
    }

    // ---- Performance ----

    private void OnSettingChanged(object sender, RoutedEventArgs e) => SaveSettings();

    private void SaveSettings()
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

    // ---- General ----

    private void OnRegisterClicked(object sender, RoutedEventArgs e) => RunAction(
        () => ProtocolRegistrar.Register(Environment.ProcessPath!), "Roblox links now open with this launcher.");

    private void OnUnregisterClicked(object sender, RoutedEventArgs e) => RunAction(
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
        GeneralStatusText.Text = done
            ? "Roblox was removed. It reinstalls the next time you launch."
            : "Some files are in use. Close Roblox and try again.";
    }

    private void RunAction(Action action, string success)
    {
        try
        {
            action();
            GeneralStatusText.Text = success;
        }
        catch (Exception ex)
        {
            Log.Error("Settings action failed.", ex);
            GeneralStatusText.Text = $"That didn't work: {ex.Message}";
        }
    }
}

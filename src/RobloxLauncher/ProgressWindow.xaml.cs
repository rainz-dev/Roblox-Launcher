using System.Windows;
using System.Windows.Input;
using RobloxLauncher.Core;
using RobloxLauncher.Core.Install;

namespace RobloxLauncher;

/// <summary>Small window shown while a game link updates and starts Roblox.</summary>
public partial class ProgressWindow : Window
{
    private readonly CancellationTokenSource _cancel = new();

    public ProgressWindow()
    {
        InitializeComponent();
        Title = AppInfo.Name;
        NameText.Text = AppInfo.Name;
    }

    /// <summary>Cancelled by the Cancel button or by closing the window.</summary>
    public CancellationToken CancellationToken => _cancel.Token;

    public void Report(InstallProgress progress)
    {
        if (_cancel.IsCancellationRequested) return;
        ProgressText.Apply(progress, StatusText, DetailText, Bar);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

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

namespace RobloxLauncher.Core.Settings;

/// <summary>Everything the settings window can change. Saved as settings.json.</summary>
public sealed class LauncherSettings
{
    /// <summary>Run Roblox at High CPU priority.</summary>
    public bool HighPriority { get; set; }

    /// <summary>Ask Windows to always run Roblox on the high-performance (dedicated) GPU.</summary>
    public bool UseDedicatedGpu { get; set; }

    /// <summary>Turn off Windows "fullscreen optimizations" for Roblox.</summary>
    public bool DisableFullscreenOptimizations { get; set; }

    /// <summary>Close the launcher window after pressing Play once Roblox has started.</summary>
    public bool CloseAfterLaunch { get; set; } = true;
}

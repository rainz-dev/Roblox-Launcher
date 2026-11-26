using System.ComponentModel;
using System.Diagnostics;
using RobloxLauncher.Core.Settings;

namespace RobloxLauncher.Core.Tweaks;

/// <summary>
/// Windows performance settings for Roblox. These are the same per-app switches Windows has in
/// Settings › Display › Graphics and in an .exe's Properties › Compatibility tab; nothing touches the game itself.
/// </summary>
/// <param name="gpuPreferences">HKCU\Software\Microsoft\DirectX\UserGpuPreferences</param>
/// <param name="compatibilityLayers">HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers</param>
/// <param name="versionsRoot">The launcher's Versions folder. Only values for .exe files inside it are ever changed.</param>
public sealed class PerformanceTweaks(IRegistryValues gpuPreferences, IRegistryValues compatibilityLayers, string versionsRoot)
{
    /// <summary>Windows' value for "High performance" in Graphics settings.</summary>
    public const string HighPerformanceGpu = "GpuPreference=2;";

    /// <summary>The compatibility flag behind "Disable fullscreen optimizations".</summary>
    public const string DisableFullscreenOptimizationsFlag = "~ DISABLEDXMAXIMIZEDWINDOWEDMODE";

    /// <summary>
    /// Sets or clears the per-exe registry switches for <paramref name="robloxExe"/>. Windows keys them by
    /// full path, and every Roblox update has a new path, so entries for older builds are removed too.
    /// </summary>
    public void ApplyBeforeLaunch(string robloxExe, LauncherSettings settings)
    {
        string exe = Path.GetFullPath(robloxExe);
        if (!IsOurs(exe))
            throw new ArgumentException("Tweaks only apply to Roblox builds installed by this launcher.", nameof(robloxExe));

        Sync(gpuPreferences, exe, settings.UseDedicatedGpu ? HighPerformanceGpu : null);
        Sync(compatibilityLayers, exe, settings.DisableFullscreenOptimizations ? DisableFullscreenOptimizationsFlag : null);
    }

    /// <summary>Raises the running Roblox process to High priority if enabled. Never fails the launch.</summary>
    public static void ApplyAfterLaunch(Process roblox, LauncherSettings settings)
    {
        if (!settings.HighPriority) return;
        try
        {
            roblox.PriorityClass = ProcessPriorityClass.High;
            Log.Info("Set Roblox to High priority.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            Log.Warn($"Couldn't set Roblox priority: {ex.Message}");
        }
    }

    private void Sync(IRegistryValues key, string exe, string? desired)
    {
        foreach (string name in key.GetValueNames().ToList())
        {
            if (IsOurs(name) && !string.Equals(name, exe, StringComparison.OrdinalIgnoreCase))
                key.DeleteValue(name); // left over from an older Roblox build
        }

        if (desired is null)
        {
            if (key.GetValue(exe) is not null) key.DeleteValue(exe);
        }
        else if (key.GetValue(exe) != desired)
        {
            key.SetValue(exe, desired);
        }
    }

    private bool IsOurs(string path)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionsRoot)) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}

using Microsoft.Win32;
using RobloxLauncher.Core.Tweaks;

namespace RobloxLauncher;

/// <summary>The string values of one key under HKEY_CURRENT_USER.</summary>
internal sealed class RegistryValues(string subKeyPath) : IRegistryValues
{
    public static RegistryValues GpuPreferences { get; } = new(@"Software\Microsoft\DirectX\UserGpuPreferences");

    public static RegistryValues CompatibilityLayers { get; } = new(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");

    public IEnumerable<string> GetValueNames()
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        return key?.GetValueNames() ?? [];
    }

    public string? GetValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        return key?.GetValue(name) as string;
    }

    public void SetValue(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void DeleteValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

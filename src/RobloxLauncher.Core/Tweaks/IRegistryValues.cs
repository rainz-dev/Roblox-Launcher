namespace RobloxLauncher.Core.Tweaks;

/// <summary>
/// The string values of one registry key. The Windows app passes real HKCU keys;
/// tests pass a dictionary, so the tweak logic is testable anywhere.
/// </summary>
public interface IRegistryValues
{
    IEnumerable<string> GetValueNames();
    string? GetValue(string name);
    void SetValue(string name, string value);
    void DeleteValue(string name);
}

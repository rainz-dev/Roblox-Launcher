using System.Text.Json;
using RobloxLauncher.Core.Deployment;

namespace RobloxLauncher.Core.Install;

/// <summary>Which Roblox build is installed. Saved as state.json in the launcher folder.</summary>
public sealed record InstallState(string VersionGuid, string Version)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static InstallState? Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var state = JsonSerializer.Deserialize<InstallState>(File.ReadAllText(path));
            return DeploymentClient.IsValidVersionGuid(state?.VersionGuid) ? state : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Log.Warn($"Ignoring unreadable state file: {ex.Message}");
            return null;
        }
    }

    public void Save(string path)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Options));
        File.Move(temp, path, overwrite: true);
    }
}

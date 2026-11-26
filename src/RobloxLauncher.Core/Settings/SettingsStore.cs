using System.Text.Json;

namespace RobloxLauncher.Core.Settings;

/// <summary>Loads and saves <see cref="LauncherSettings"/> as JSON. A missing or broken file gives defaults.</summary>
public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public LauncherSettings Load()
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(path), Options) ?? new LauncherSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Log.Warn($"Ignoring unreadable settings file: {ex.Message}");
        }
        return new LauncherSettings();
    }

    public void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, path, overwrite: true);
    }
}

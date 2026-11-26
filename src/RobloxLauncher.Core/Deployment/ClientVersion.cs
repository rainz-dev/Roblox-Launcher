using System.Text.Json.Serialization;

namespace RobloxLauncher.Core.Deployment;

/// <summary>
/// Response of https://clientsettings.roblox.com/v2/client-version/WindowsPlayer, e.g.
/// {"version":"0.748.0.7481526","clientVersionUpload":"version-2c6fd5e09aaf4cfa", ...}
/// </summary>
public sealed record ClientVersion(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("clientVersionUpload")] string VersionGuid);

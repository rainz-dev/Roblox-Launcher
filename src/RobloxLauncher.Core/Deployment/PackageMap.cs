namespace RobloxLauncher.Core.Deployment;

/// <summary>
/// Where each WindowsPlayer package gets unpacked, relative to the version folder.
/// Roblox's official installer has this list built in; the manifest only names the zips.
/// If Roblox adds a package, installs stop with <see cref="UnknownPackageException"/> until it's added here.
/// </summary>
public static class PackageMap
{
    private static readonly Dictionary<string, string> Destinations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RobloxApp.zip"] = "",
        ["redist.zip"] = "",
        ["shaders.zip"] = "shaders/",
        ["ssl.zip"] = "ssl/",

        ["WebView2.zip"] = "",
        ["WebView2RuntimeInstaller.zip"] = "WebView2RuntimeInstaller/",

        ["content-avatar.zip"] = "content/avatar/",
        ["content-configs.zip"] = "content/configs/",
        ["content-fonts.zip"] = "content/fonts/",
        ["content-sky.zip"] = "content/sky/",
        ["content-sounds.zip"] = "content/sounds/",
        ["content-textures2.zip"] = "content/textures/",
        ["content-models.zip"] = "content/models/",

        ["content-platform-fonts.zip"] = "PlatformContent/pc/fonts/",
        ["content-platform-dictionaries.zip"] = "PlatformContent/pc/shared_compression_dictionaries/",
        ["content-terrain.zip"] = "PlatformContent/pc/terrain/",
        ["content-textures3.zip"] = "PlatformContent/pc/textures/",

        ["extracontent-luapackages.zip"] = "ExtraContent/LuaPackages/",
        ["extracontent-translations.zip"] = "ExtraContent/translations/",
        ["extracontent-models.zip"] = "ExtraContent/models/",
        ["extracontent-textures.zip"] = "ExtraContent/textures/",
        ["extracontent-places.zip"] = "ExtraContent/places/",
    };

    /// <summary>Packages the launcher replaces, so it never downloads them.</summary>
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "RobloxPlayerInstaller.exe",
        "RobloxPlayerLauncher.exe",
    };

    public static bool IsIgnored(string packageName) => Ignored.Contains(packageName);

    public static bool TryGetDestination(string packageName, out string destination) =>
        Destinations.TryGetValue(packageName, out destination!);
}

namespace RobloxLauncher.Core.FastFlags;

/// <summary>
/// The only Fast Flags the Roblox client honors from ClientAppSettings.json. Everything else is silently ignored.
/// Source: "Allowlist for local client configuration via Fast Flags", Roblox DevForum, 29 Sep 2025
/// (https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569).
/// Roblox can change this list at any time: update it here.
/// </summary>
public static class FastFlagAllowlist
{
    public static readonly IReadOnlySet<string> Flags = new HashSet<string>(StringComparer.Ordinal)
    {
        // Geometry
        "DFIntCSGLevelOfDetailSwitchingDistance",
        "DFIntCSGLevelOfDetailSwitchingDistanceL12",
        "DFIntCSGLevelOfDetailSwitchingDistanceL23",
        "DFIntCSGLevelOfDetailSwitchingDistanceL34",

        // Rendering
        "FFlagHandleAltEnterFullscreenManually",
        "DFFlagTextureQualityOverrideEnabled",
        "DFIntTextureQualityOverride",
        "FIntDebugForceMSAASamples",
        "DFFlagDisableDPIScale",
        "FFlagDebugGraphicsPreferD3D11",
        "FFlagDebugSkyGray",
        "DFFlagDebugPauseVoxelizer",
        "DFIntDebugFRMQualityLevelOverride",
        "FIntFRMMaxGrassDistance",
        "FIntFRMMinGrassDistance",
        "FFlagDebugGraphicsPreferVulkan",
        "FFlagDebugGraphicsPreferOpenGL",

        // User interface
        "FIntGrassMovementReducedMotionFactor",
    };

    public static bool IsAllowed(string flag) => Flags.Contains(flag);
}

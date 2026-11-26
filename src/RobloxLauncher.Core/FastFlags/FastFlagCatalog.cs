namespace RobloxLauncher.Core.FastFlags;

/// <summary>
/// The FastFlags page: every control and preset, built only from allowlisted flags.
/// Values follow community testing (Roblox doesn't document them), so they stay conservative.
/// </summary>
public static class FastFlagCatalog
{
    private const string True = "True";
    private const string False = "False";

    private static FlagChoice C(string id, string label, params (string Key, string Value)[] flags) =>
        new(id, label, flags.ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal));

    public static readonly FlagGroup GraphicsApi = new("graphics-api", "Graphics API",
        "Which renderer Roblox uses. Auto lets Roblox pick; try another if you get stutters or crashes.",
        [
            C("default", "Auto"),
            C("d3d11", "DirectX 11", ("FFlagDebugGraphicsPreferD3D11", True)),
            C("vulkan", "Vulkan", ("FFlagDebugGraphicsPreferVulkan", True)),
            C("opengl", "OpenGL", ("FFlagDebugGraphicsPreferOpenGL", True)),
        ]);

    public static readonly FlagGroup RenderQuality = new("render-quality", "Render quality",
        "Locks Roblox's graphics level, overriding the in-game slider.",
        [
            FlagChoice.Default,
            C("low", "Low", ("DFIntDebugFRMQualityLevelOverride", "1")),
            C("medium", "Medium", ("DFIntDebugFRMQualityLevelOverride", "10")),
            C("high", "High", ("DFIntDebugFRMQualityLevelOverride", "16")),
            C("max", "Max", ("DFIntDebugFRMQualityLevelOverride", "21")),
        ]);

    public static readonly FlagGroup TextureQuality = new("texture-quality", "Texture quality",
        "Lower textures use less video memory.",
        [
            FlagChoice.Default,
            C("low", "Low", ("DFFlagTextureQualityOverrideEnabled", True), ("DFIntTextureQualityOverride", "0")),
            C("medium", "Medium", ("DFFlagTextureQualityOverrideEnabled", True), ("DFIntTextureQualityOverride", "1")),
            C("high", "High", ("DFFlagTextureQualityOverrideEnabled", True), ("DFIntTextureQualityOverride", "2")),
            C("max", "Max", ("DFFlagTextureQualityOverrideEnabled", True), ("DFIntTextureQualityOverride", "3")),
        ]);

    public static readonly FlagGroup AntiAliasing = new("anti-aliasing", "Anti-aliasing",
        "Smooths jagged edges. Off is fastest.",
        [
            FlagChoice.Default,
            C("off", "Off", ("FIntDebugForceMSAASamples", "1")),
            C("2x", "2×", ("FIntDebugForceMSAASamples", "2")),
            C("4x", "4×", ("FIntDebugForceMSAASamples", "4")),
            C("8x", "8×", ("FIntDebugForceMSAASamples", "8")),
        ]);

    public static readonly FlagGroup Grass = new("grass", "Grass",
        "Terrain grass. Turning it off helps on low-end PCs.",
        [
            FlagChoice.Default,
            C("off", "Off", ("FIntFRMMinGrassDistance", "0"), ("FIntFRMMaxGrassDistance", "0")),
        ]);

    public static readonly FlagGroup PartDetail = new("part-detail", "Distant detail",
        "Low switches unions and meshes to their simplest version at any distance.",
        [
            FlagChoice.Default,
            C("low", "Low",
                ("DFIntCSGLevelOfDetailSwitchingDistance", "0"),
                ("DFIntCSGLevelOfDetailSwitchingDistanceL12", "0"),
                ("DFIntCSGLevelOfDetailSwitchingDistanceL23", "0"),
                ("DFIntCSGLevelOfDetailSwitchingDistanceL34", "0")),
        ]);

    public static readonly FlagGroup ExclusiveFullscreen = new("exclusive-fullscreen", "Exclusive fullscreen with Alt+Enter",
        "Alt+Enter switches to true fullscreen instead of a borderless window.",
        [FlagChoice.Default, C("on", "On", ("FFlagHandleAltEnterFullscreenManually", False))], isSwitch: true);

    public static readonly FlagGroup DisableDpiScaling = new("disable-dpi", "Disable DPI scaling",
        "Renders at your real resolution on scaled displays (e.g. 125%). Sharper, a bit heavier.",
        [FlagChoice.Default, C("on", "On", ("DFFlagDisableDPIScale", True))], isSwitch: true);

    public static readonly FlagGroup GraySky = new("gray-sky", "Gray sky",
        "Replaces the skybox with flat gray.",
        [FlagChoice.Default, C("on", "On", ("FFlagDebugSkyGray", True))], isSwitch: true);

    public static readonly FlagGroup StillGrass = new("still-grass", "Still grass",
        "Stops grass from swaying.",
        [FlagChoice.Default, C("on", "On", ("FIntGrassMovementReducedMotionFactor", "0"))], isSwitch: true);

    public static readonly IReadOnlyList<FlagGroup> Options = [GraphicsApi, RenderQuality, TextureQuality, AntiAliasing, Grass, PartDetail];

    public static readonly IReadOnlyList<FlagGroup> Switches = [ExclusiveFullscreen, DisableDpiScaling, GraySky, StillGrass];

    public static IEnumerable<FlagGroup> AllGroups => Options.Concat(Switches);

    /// <summary>The groups presets change. Graphics API and the switches are left as you set them.</summary>
    private static readonly IReadOnlyList<FlagGroup> PresetGroups = [RenderQuality, TextureQuality, AntiAliasing, Grass, PartDetail];

    public static readonly IReadOnlyList<FlagPreset> Presets =
    [
        new("competitive", "Competitive", "Max FPS: lowest quality, low textures, no anti-aliasing, no grass.",
            new() { ["render-quality"] = "low", ["texture-quality"] = "low", ["anti-aliasing"] = "off", ["grass"] = "off", ["part-detail"] = "low" }),
        new("balanced", "Balanced", "Medium quality and textures.",
            new() { ["render-quality"] = "medium", ["texture-quality"] = "medium" }),
        new("showcase", "Showcase", "Best looks: max quality and textures, 4× anti-aliasing.",
            new() { ["render-quality"] = "max", ["texture-quality"] = "max", ["anti-aliasing"] = "4x" }),
    ];

    /// <summary>Applies a preset: its choices, and Default for every other preset-controlled group.</summary>
    public static void ApplyPreset(IDictionary<string, string> flags, FlagPreset preset)
    {
        foreach (var group in PresetGroups)
        {
            string choiceId = preset.Choices.GetValueOrDefault(group.Id, "default");
            group.Apply(flags, group.Choice(choiceId));
        }
    }

    /// <summary>The preset that matches the current flags, if any.</summary>
    public static FlagPreset? CurrentPreset(IReadOnlyDictionary<string, string> flags) =>
        Presets.FirstOrDefault(p => PresetGroups.All(g =>
            g.Current(flags)?.Id == p.Choices.GetValueOrDefault(g.Id, "default")));
}

/// <param name="Choices">Group id → choice id. Groups not listed go back to Default.</param>
public sealed record FlagPreset(string Id, string Label, string Description, Dictionary<string, string> Choices);

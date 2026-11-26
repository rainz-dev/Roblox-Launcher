using RobloxLauncher.Core.FastFlags;
using RobloxLauncher.Core.Settings;

namespace RobloxLauncher.Core.Tests;

public class FastFlagCatalogTests
{
    private static Dictionary<string, string> Empty() => new(StringComparer.Ordinal);

    [Fact]
    public void Every_control_uses_only_allowlisted_flags()
    {
        // FlagGroup's constructor enforces this; touching the catalog runs it for every group.
        foreach (var group in FastFlagCatalog.AllGroups)
            Assert.All(group.Keys, k => Assert.True(FastFlagAllowlist.IsAllowed(k), k));
    }

    [Fact]
    public void Every_preset_names_real_groups_and_choices()
    {
        var groups = FastFlagCatalog.AllGroups.ToDictionary(g => g.Id);
        foreach (var preset in FastFlagCatalog.Presets)
            foreach (var (groupId, choiceId) in preset.Choices)
                Assert.Contains(groups[groupId].Choices, c => c.Id == choiceId);
    }

    [Fact]
    public void Picking_an_option_replaces_the_groups_flags()
    {
        var flags = Empty();
        var textures = FastFlagCatalog.TextureQuality;

        textures.Apply(flags, textures.Choice("low"));
        textures.Apply(flags, textures.Choice("max"));

        Assert.Equal("3", flags["DFIntTextureQualityOverride"]);
        Assert.Equal("True", flags["DFFlagTextureQualityOverrideEnabled"]);
        Assert.Equal("max", textures.Current(flags)?.Id);

        textures.Apply(flags, FlagChoice.Default);
        Assert.Empty(flags);
        Assert.Equal("default", textures.Current(flags)?.Id);
    }

    [Fact]
    public void Graphics_api_options_are_exclusive()
    {
        var flags = Empty();
        var api = FastFlagCatalog.GraphicsApi;
        api.Apply(flags, api.Choice("vulkan"));
        api.Apply(flags, api.Choice("d3d11"));

        Assert.Equal(["FFlagDebugGraphicsPreferD3D11"], flags.Keys);
    }

    [Fact]
    public void Hand_edited_values_show_as_no_option()
    {
        var flags = Empty();
        flags["DFIntDebugFRMQualityLevelOverride"] = "5";
        Assert.Null(FastFlagCatalog.RenderQuality.Current(flags));
    }

    [Fact]
    public void Current_matches_values_case_insensitively()
    {
        var flags = Empty();
        flags["DFFlagDisableDPIScale"] = "true";
        Assert.Equal("on", FastFlagCatalog.DisableDpiScaling.Current(flags)?.Id);
    }

    [Fact]
    public void Presets_leave_graphics_api_switches_and_custom_flags_alone()
    {
        var flags = Empty();
        FastFlagCatalog.GraphicsApi.Apply(flags, FastFlagCatalog.GraphicsApi.Choice("vulkan"));
        FastFlagCatalog.GraySky.Apply(flags, FastFlagCatalog.GraySky.Choice("on"));
        flags["FFlagSomethingCustom"] = "True";

        FastFlagCatalog.ApplyPreset(flags, FastFlagCatalog.Presets.Single(p => p.Id == "competitive"));
        FastFlagCatalog.ApplyPreset(flags, FastFlagCatalog.Presets.Single(p => p.Id == "showcase"));

        Assert.Equal("True", flags["FFlagDebugGraphicsPreferVulkan"]);
        Assert.Equal("True", flags["FFlagDebugSkyGray"]);
        Assert.Equal("True", flags["FFlagSomethingCustom"]);
        // Competitive's grass/detail settings were reset by Showcase.
        Assert.False(flags.ContainsKey("FIntFRMMaxGrassDistance"));
        Assert.False(flags.ContainsKey("DFIntCSGLevelOfDetailSwitchingDistance"));
        Assert.Equal("showcase", FastFlagCatalog.CurrentPreset(flags)?.Id);
    }

    [Fact]
    public void No_preset_matches_after_a_manual_change()
    {
        var flags = Empty();
        FastFlagCatalog.ApplyPreset(flags, FastFlagCatalog.Presets.Single(p => p.Id == "competitive"));
        FastFlagCatalog.AntiAliasing.Apply(flags, FastFlagCatalog.AntiAliasing.Choice("8x"));
        Assert.Null(FastFlagCatalog.CurrentPreset(flags));
    }

    [Fact]
    public void Groups_reject_flags_that_are_not_allowlisted() =>
        Assert.Throws<ArgumentException>(() => new FlagGroup("x", "X", "",
            [FlagChoice.Default, new FlagChoice("on", "On", new Dictionary<string, string> { ["DFIntTaskSchedulerTargetFps"] = "999" })]));
}

public class FastFlagFileTests
{
    [Fact]
    public void Parses_strings_numbers_and_booleans()
    {
        var flags = FastFlagFile.Parse("""
            {
              // comments and trailing commas are fine
              "DFIntTextureQualityOverride": 2,
              "DFFlagDisableDPIScale": true,
              "FFlagDebugSkyGray": "True",
            }
            """);

        Assert.Equal("2", flags["DFIntTextureQualityOverride"]);
        Assert.Equal("True", flags["DFFlagDisableDPIScale"]);
        Assert.Equal("True", flags["FFlagDebugSkyGray"]);
    }

    [Fact]
    public void Empty_text_means_no_flags() => Assert.Empty(FastFlagFile.Parse("   "));

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"Bad Name": "1"}""")]
    [InlineData("""{"..\\evil": "1"}""")]
    [InlineData("""{"FFlagX": {"nested": 1}}""")]
    [InlineData("""{"FFlagX": null}""")]
    public void Rejects_bad_input_with_a_readable_error(string json) =>
        Assert.Throws<FormatException>(() => FastFlagFile.Parse(json));

    [Fact]
    public void Lists_flags_roblox_will_ignore() =>
        Assert.Equal(["DFIntTaskSchedulerTargetFps"],
            FastFlagFile.Ignored(["DFFlagDisableDPIScale", "DFIntTaskSchedulerTargetFps"]));

    [Fact]
    public void Writes_and_removes_ClientAppSettings()
    {
        using var temp = new TempDir();
        string file = Path.Combine(temp.Path, "ClientSettings", "ClientAppSettings.json");

        FastFlagFile.WriteTo(temp.Path, new Dictionary<string, string> { ["FFlagDebugSkyGray"] = "True" });
        Assert.Equal(new Dictionary<string, string> { ["FFlagDebugSkyGray"] = "True" }, FastFlagFile.Parse(File.ReadAllText(file)));

        FastFlagFile.WriteTo(temp.Path, new Dictionary<string, string>());
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Flags_round_trip_through_settings()
    {
        using var temp = new TempDir();
        var store = new SettingsStore(Path.Combine(temp.Path, "settings.json"));
        var settings = new LauncherSettings();
        settings.FastFlags["DFIntTextureQualityOverride"] = "1";
        store.Save(settings);

        Assert.Equal("1", store.Load().FastFlags["DFIntTextureQualityOverride"]);
    }
}

using System.Windows;
using System.Windows.Controls;
using RobloxLauncher.Core.FastFlags;

namespace RobloxLauncher;

/// <summary>The FastFlags page: presets, option pills and switches built from <see cref="FastFlagCatalog"/>.</summary>
public partial class SettingsWindow
{
    private readonly List<(FlagGroup Group, FlagChoice Choice, RadioButton Pill)> _optionPills = [];
    private readonly List<(FlagGroup Group, CheckBox Switch)> _flagSwitches = [];
    private readonly List<(FlagPreset Preset, RadioButton Pill)> _presetPills = [];
    private bool _refreshingFlags;

    private Dictionary<string, string> Flags => _settings.FastFlags;

    private void BuildFastFlagControls()
    {
        foreach (var preset in FastFlagCatalog.Presets)
        {
            var pill = new RadioButton { Style = Pill, Content = preset.Label, GroupName = "preset", Tag = preset };
            pill.Checked += OnPresetChecked;
            PresetPills.Children.Add(pill);
            _presetPills.Add((preset, pill));
        }

        foreach (var group in FastFlagCatalog.Options)
        {
            var pills = new WrapPanel { Margin = new Thickness(0, 12, 0, -8) };
            foreach (var choice in group.Choices)
            {
                var pill = new RadioButton { Style = Pill, Content = choice.Label, GroupName = group.Id, Tag = (group, choice) };
                pill.Checked += OnFlagChoiceChecked;
                pills.Children.Add(pill);
                _optionPills.Add((group, choice, pill));
            }
            FlagOptionCards.Children.Add(Card(group, pills, inline: false));
        }

        foreach (var group in FastFlagCatalog.Switches)
        {
            var toggle = new CheckBox { Style = (Style)FindResource("ToggleSwitch"), VerticalAlignment = VerticalAlignment.Center, Tag = group };
            System.Windows.Automation.AutomationProperties.SetName(toggle, group.Label);
            toggle.Click += OnFlagSwitchClicked;
            FlagSwitchCards.Children.Add(Card(group, toggle, inline: true));
            _flagSwitches.Add((group, toggle));
        }

        RefreshFastFlags();
    }

    private Style Pill => (Style)FindResource("Pill");

    /// <summary>A filled card with the group's name and description, and its control beside or below.</summary>
    private Border Card(FlagGroup group, FrameworkElement control, bool inline)
    {
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = group.Label, FontSize = 15, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = group.Description, Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 4, 0, 0) });

        var panel = new DockPanel();
        if (inline)
        {
            control.Margin = new Thickness(24, 0, 0, 0);
            DockPanel.SetDock(control, Dock.Right);
            panel.Children.Add(control);
            panel.Children.Add(text);
        }
        else
        {
            DockPanel.SetDock(text, Dock.Top);
            panel.Children.Add(text);
            panel.Children.Add(control);
        }

        return new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0, 0, 0, 8), Child = panel };
    }

    /// <summary>Makes every control show the current flags. Hand-edited values select no option.</summary>
    private void RefreshFastFlags()
    {
        _refreshingFlags = true;
        try
        {
            foreach (var (group, choice, pill) in _optionPills)
                pill.IsChecked = group.Current(Flags)?.Id == choice.Id;

            foreach (var (group, toggle) in _flagSwitches)
                toggle.IsChecked = group.Current(Flags)?.Id == "on";

            var preset = FastFlagCatalog.CurrentPreset(Flags);
            foreach (var (p, pill) in _presetPills)
                pill.IsChecked = p == preset;
            PresetDescription.Text = preset?.Description ?? "Pick a starting point, then fine-tune below.";

            RawFlagsBox.Text = Flags.Count == 0 ? "{\n}" : FastFlagFile.ToJson(Flags);
            ShowIgnoredFlags();
        }
        finally
        {
            _refreshingFlags = false;
        }
    }

    private void ShowIgnoredFlags()
    {
        var ignored = FastFlagFile.Ignored(Flags.Keys);
        RawFlagsStatus.Text = ignored.Count == 0
            ? $"{Flags.Count} flag(s) set."
            : $"Roblox ignores these because they aren't on its allowlist: {string.Join(", ", ignored)}";
    }

    private void FlagsChanged()
    {
        SaveSettings();
        RefreshFastFlags();
    }

    private void OnPresetChecked(object sender, RoutedEventArgs e)
    {
        if (_refreshingFlags) return;
        FastFlagCatalog.ApplyPreset(Flags, (FlagPreset)((FrameworkElement)sender).Tag);
        FlagsChanged();
    }

    private void OnFlagChoiceChecked(object sender, RoutedEventArgs e)
    {
        if (_refreshingFlags) return;
        var (group, choice) = ((FlagGroup, FlagChoice))((FrameworkElement)sender).Tag;
        group.Apply(Flags, choice);
        FlagsChanged();
    }

    private void OnFlagSwitchClicked(object sender, RoutedEventArgs e)
    {
        var toggle = (CheckBox)sender;
        var group = (FlagGroup)toggle.Tag;
        group.Apply(Flags, group.Choice(toggle.IsChecked == true ? "on" : "default"));
        FlagsChanged();
    }

    private void OnSaveRawFlagsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var parsed = FastFlagFile.Parse(RawFlagsBox.Text);
            Flags.Clear();
            foreach (var (key, value) in parsed) Flags[key] = value;
            FlagsChanged();
        }
        catch (FormatException ex)
        {
            RawFlagsStatus.Text = ex.Message; // keep their text so they can fix it
        }
    }

    private void OnResetFlagsClicked(object sender, RoutedEventArgs e)
    {
        Flags.Clear();
        FlagsChanged();
    }
}

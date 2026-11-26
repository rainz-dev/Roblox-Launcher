namespace RobloxLauncher.Core.FastFlags;

/// <summary>One option of a <see cref="FlagGroup"/>: the flags it sets (empty = Roblox's default).</summary>
public sealed record FlagChoice(string Id, string Label, IReadOnlyDictionary<string, string> Flags)
{
    public static FlagChoice Default { get; } = new("default", "Default", new Dictionary<string, string>());
}

/// <summary>
/// A setting made of one or more flags, shown as a row of options (or a switch).
/// Picking an option removes all of the group's flags, then adds the option's.
/// </summary>
public sealed class FlagGroup
{
    public FlagGroup(string id, string label, string description, IReadOnlyList<FlagChoice> choices, bool isSwitch = false)
    {
        Id = id;
        Label = label;
        Description = description;
        Choices = choices;
        IsSwitch = isSwitch;
        Keys = choices.SelectMany(c => c.Flags.Keys).ToHashSet(StringComparer.Ordinal);

        foreach (string key in Keys)
            if (!FastFlagAllowlist.IsAllowed(key))
                throw new ArgumentException($"{key} is not on Roblox's allowlist.");
    }

    public string Id { get; }
    public string Label { get; }
    public string Description { get; }
    public IReadOnlyList<FlagChoice> Choices { get; }

    /// <summary>A two-option group shown as an on/off switch (Choices[0] = off, Choices[1] = on).</summary>
    public bool IsSwitch { get; }

    /// <summary>Every flag this group controls.</summary>
    public IReadOnlySet<string> Keys { get; }

    public FlagChoice Choice(string id) => Choices.First(c => c.Id == id);

    /// <summary>The option that matches <paramref name="flags"/> exactly, or null if they were edited by hand.</summary>
    public FlagChoice? Current(IReadOnlyDictionary<string, string> flags)
    {
        foreach (var choice in Choices)
        {
            bool matches = Keys.All(key =>
                choice.Flags.TryGetValue(key, out string? want)
                    ? flags.TryGetValue(key, out string? have) && string.Equals(have, want, StringComparison.OrdinalIgnoreCase)
                    : !flags.ContainsKey(key));
            if (matches) return choice;
        }
        return null;
    }

    public void Apply(IDictionary<string, string> flags, FlagChoice choice)
    {
        foreach (string key in Keys) flags.Remove(key);
        foreach (var (key, value) in choice.Flags) flags[key] = value;
    }
}

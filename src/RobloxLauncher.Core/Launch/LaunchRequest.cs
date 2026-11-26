namespace RobloxLauncher.Core.Launch;

public enum LauncherCommand
{
    /// <summary>Update if needed, then start Roblox (with a game link if one was given).</summary>
    Launch,
    /// <summary>Make this launcher handle roblox-player: and roblox: links.</summary>
    Register,
    /// <summary>Give the links back to whatever handled them before.</summary>
    Unregister,
}

/// <summary>What the launcher was asked to do, parsed from its command line.</summary>
public sealed record LaunchRequest(LauncherCommand Command, string? Uri)
{
    public static LaunchRequest Parse(IReadOnlyList<string> args)
    {
        foreach (string arg in args)
        {
            if (arg.Equals("--register", StringComparison.OrdinalIgnoreCase))
                return new LaunchRequest(LauncherCommand.Register, null);
            if (arg.Equals("--unregister", StringComparison.OrdinalIgnoreCase))
                return new LaunchRequest(LauncherCommand.Unregister, null);
            if (IsRobloxUri(arg))
                return new LaunchRequest(LauncherCommand.Launch, arg);
        }
        return new LaunchRequest(LauncherCommand.Launch, null);
    }

    public static bool IsRobloxUri(string value) =>
        value.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase);
}

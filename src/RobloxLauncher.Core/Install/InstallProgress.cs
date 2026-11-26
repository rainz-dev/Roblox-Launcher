namespace RobloxLauncher.Core.Install;

public enum InstallStage
{
    CheckingForUpdates,
    FetchingManifest,
    Downloading,
    Extracting,
    Launching,
}

/// <param name="Done">Bytes downloaded (Downloading) or packages unpacked (Extracting).</param>
/// <param name="Total">Matching total, or 0 when unknown.</param>
public sealed record InstallProgress(InstallStage Stage, long Done = 0, long Total = 0, string? Detail = null)
{
    public double? Fraction => Total > 0 ? Math.Clamp((double)Done / Total, 0, 1) : null;
}

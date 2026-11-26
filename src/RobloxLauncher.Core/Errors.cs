namespace RobloxLauncher.Core;

/// <summary>An error whose message is written for the person using the launcher.</summary>
public class BootstrapException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Roblox shipped a package the launcher doesn't know where to put. See <see cref="Deployment.PackageMap"/>.</summary>
public sealed class UnknownPackageException(string packageName) : BootstrapException(
    $"Roblox added a new package ({packageName}) that this launcher doesn't know how to install yet. " +
    "It needs to be added to PackageMap.cs.")
{
    public string PackageName { get; } = packageName;
}

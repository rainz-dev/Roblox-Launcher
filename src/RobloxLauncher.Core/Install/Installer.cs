using System.Security.Cryptography;
using RobloxLauncher.Core.Deployment;

namespace RobloxLauncher.Core.Install;

/// <summary>Downloads, verifies and unpacks a Roblox build into Versions\{version-id}.</summary>
public sealed class Installer(DeploymentClient client, LauncherPaths paths)
{
    public const string PlayerExeName = "RobloxPlayerBeta.exe";

    private const int MaxParallelDownloads = 4;
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(500);

    // Roblox reads this from its install folder to find its content directory.
    internal const string AppSettingsXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <Settings>
        	<ContentFolder>content</ContentFolder>
        	<BaseUrl>http://www.roblox.com</BaseUrl>
        </Settings>
        """;

    public string GetInstallDirectory(string versionGuid) => Path.Combine(paths.Versions, versionGuid);

    public bool IsInstalled(string versionGuid) =>
        InstallState.Load(paths.StateFile)?.VersionGuid == versionGuid &&
        File.Exists(Path.Combine(GetInstallDirectory(versionGuid), PlayerExeName));

    /// <summary>
    /// Deletes every installed Roblox build so the next launch reinstalls (cached downloads are kept and re-checked).
    /// Returns false if some files were in use, e.g. Roblox is still running.
    /// </summary>
    public static bool RemoveInstalledBuilds(LauncherPaths paths)
    {
        if (File.Exists(paths.StateFile)) File.Delete(paths.StateFile);
        if (!Directory.Exists(paths.Versions)) return true;

        bool allRemoved = true;
        foreach (string dir in Directory.EnumerateDirectories(paths.Versions))
            allRemoved &= TryDeleteDirectory(dir);
        return allRemoved;
    }

    /// <summary>Makes sure <paramref name="version"/> is installed and returns its folder.</summary>
    public async Task<string> EnsureInstalledAsync(
        ClientVersion version, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        string installDir = GetInstallDirectory(version.VersionGuid);
        if (IsInstalled(version.VersionGuid))
        {
            Log.Info($"Roblox {version.Version} ({version.VersionGuid}) is up to date.");
            return installDir;
        }

        paths.EnsureCreated();
        await using var installLock = await AcquireInstallLockAsync(ct);

        // Another launcher window may have finished the same install while we waited.
        if (IsInstalled(version.VersionGuid)) return installDir;

        Log.Info($"Installing Roblox {version.Version} ({version.VersionGuid}).");
        progress?.Report(new InstallProgress(InstallStage.FetchingManifest));
        var manifest = await client.GetManifestAsync(version.VersionGuid, ct);
        var packages = ResolveDestinations(manifest);

        await DownloadAllAsync(version.VersionGuid, packages, progress, ct);
        await Task.Run(() => Unpack(installDir, packages, progress, ct), ct);

        new InstallState(version.VersionGuid, version.Version).Save(paths.StateFile);
        Log.Info($"Installed Roblox {version.Version}.");

        CleanUp(version.VersionGuid, manifest);
        return installDir;
    }

    private static List<(Package Package, string Destination)> ResolveDestinations(IReadOnlyList<Package> manifest)
    {
        var result = new List<(Package, string)>();
        foreach (var package in manifest)
        {
            if (PackageMap.IsIgnored(package.Name)) continue;
            if (!PackageMap.TryGetDestination(package.Name, out string destination))
                throw new UnknownPackageException(package.Name);
            result.Add((package, destination));
        }
        return result;
    }

    private string CachePath(Package package) => Path.Combine(paths.Downloads, package.Md5);

    private async Task DownloadAllAsync(
        string versionGuid,
        List<(Package Package, string Destination)> packages,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        long total = packages.Sum(p => p.Package.PackedSize);
        long done = 0;
        void AddBytes(long delta) =>
            progress?.Report(new InstallProgress(InstallStage.Downloading, Interlocked.Add(ref done, delta), total));

        AddBytes(0);
        using var failFast = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var gate = new SemaphoreSlim(MaxParallelDownloads);

        async Task DownloadOne(Package package)
        {
            await gate.WaitAsync(failFast.Token);
            try
            {
                string cached = CachePath(package);
                if (File.Exists(cached) && await FileMd5Async(cached, failFast.Token) == package.Md5)
                {
                    AddBytes(package.PackedSize);
                    return;
                }
                await client.DownloadPackageAsync(versionGuid, package, cached, AddBytes, failFast.Token);
            }
            catch
            {
                failFast.Cancel(); // stop the other downloads as soon as one fails for good
                throw;
            }
            finally
            {
                gate.Release();
            }
        }

        var tasks = packages.Select(p => DownloadOne(p.Package)).ToList();
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Surface the real failure rather than the cancellations it caused.
            var real = tasks.Where(t => t.IsFaulted).Select(t => t.Exception!.InnerException!).FirstOrDefault();
            if (real is not null) throw real;
            throw;
        }
    }

    private void Unpack(
        string installDir,
        List<(Package Package, string Destination)> packages,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        // Unpack into a staging folder first, so a cancelled or failed install never leaves a broken build in place.
        string staging = installDir + ".staging";
        DeleteDirectory(staging);
        Directory.CreateDirectory(staging);

        for (int i = 0; i < packages.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (package, destination) = packages[i];
            progress?.Report(new InstallProgress(InstallStage.Extracting, i, packages.Count, package.Name));
            SafeZip.ExtractToDirectory(CachePath(package), Path.Combine(staging, destination));
        }

        File.WriteAllText(Path.Combine(staging, "AppSettings.xml"), AppSettingsXml);

        if (!File.Exists(Path.Combine(staging, PlayerExeName)))
            throw new BootstrapException($"The downloaded Roblox build is missing {PlayerExeName}.");

        DeleteDirectory(installDir);
        Directory.Move(staging, installDir);
    }

    /// <summary>Deletes old Roblox builds and cached packages that the current build doesn't use.</summary>
    private void CleanUp(string currentGuid, IReadOnlyList<Package> manifest)
    {
        foreach (string dir in Directory.EnumerateDirectories(paths.Versions))
        {
            if (string.Equals(Path.GetFileName(dir), currentGuid, StringComparison.OrdinalIgnoreCase)) continue;
            // Fails harmlessly if that build is still running; we'll try again next launch.
            if (!TryDeleteDirectory(dir)) Log.Warn($"Couldn't delete old build {dir} (still in use?).");
        }

        var keep = manifest.Select(p => p.Md5).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(paths.Downloads))
        {
            if (keep.Contains(Path.GetFileName(file))) continue;
            try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Only one launcher may install at a time (e.g. you click two game links quickly).
    /// The lock is an exclusively opened file that is deleted when released.
    /// </summary>
    private async Task<FileStream> AcquireInstallLockAsync(CancellationToken ct)
    {
        while (true)
        {
            try
            {
                return new FileStream(paths.InstallLockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                    FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                await Task.Delay(LockRetryDelay, ct);
            }
        }
    }

    private static async Task<string> FileMd5Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await MD5.HashDataAsync(stream, ct));
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            DeleteDirectory(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

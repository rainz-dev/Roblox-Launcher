using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace RobloxLauncher.Core.Deployment;

/// <summary>Talks to Roblox's official version API and setup CDN.</summary>
public sealed class DeploymentClient
{
    public const string VersionApiUrl = "https://clientsettings.roblox.com/v2/client-version/WindowsPlayer";

    /// <summary>Tried in order; later hosts are mirrors used only if the first one fails.</summary>
    public static readonly IReadOnlyList<string> DefaultCdnHosts =
    [
        "https://setup.rbxcdn.com",
        "https://setup-aws.rbxcdn.com",
        "https://setup-ak.rbxcdn.com",
    ];

    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);

    private static readonly System.Buffers.SearchValues<char> HexDigits =
        System.Buffers.SearchValues.Create("0123456789abcdefABCDEF");

    private readonly HttpClient _http;
    private readonly IReadOnlyList<string> _cdnHosts;

    public DeploymentClient(HttpClient http, IReadOnlyList<string>? cdnHosts = null)
    {
        _http = http;
        _cdnHosts = cdnHosts ?? DefaultCdnHosts;
    }

    public static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        // No global timeout: big packages can take minutes. API calls get their own timeout below.
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Name}/{AppInfo.Version}");
        return http;
    }

    /// <summary>True for failures worth retrying elsewhere or falling back from (network, bad data, timeouts).</summary>
    public static bool IsTransient(Exception ex, CancellationToken userToken) => ex switch
    {
        OperationCanceledException => !userToken.IsCancellationRequested,
        HttpRequestException or IOException or FormatException or InvalidDataException or JsonException => true,
        _ => false,
    };

    /// <summary>Version ids become folder names, so only accept the exact expected shape.</summary>
    public static bool IsValidVersionGuid(string? value) =>
        value is { Length: > 8 } && value.StartsWith("version-", StringComparison.Ordinal) &&
        !value.AsSpan(8).ContainsAnyExcept(HexDigits);

    public async Task<ClientVersion> GetLatestVersionAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ApiTimeout);

        var version = await _http.GetFromJsonAsync<ClientVersion>(VersionApiUrl, cts.Token)
            ?? throw new InvalidDataException("The version API returned an empty response.");
        if (!IsValidVersionGuid(version.VersionGuid))
            throw new InvalidDataException($"The version API returned an unexpected version id '{version.VersionGuid}'.");
        return version;
    }

    public async Task<IReadOnlyList<Package>> GetManifestAsync(string versionGuid, CancellationToken ct)
    {
        Exception? last = null;
        foreach (string host in _cdnHosts)
        {
            string url = $"{host}/{versionGuid}-rbxPkgManifest.txt";
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(ApiTimeout);
                string text = await _http.GetStringAsync(url, cts.Token);
                return PackageManifest.Parse(text);
            }
            catch (Exception ex) when (IsTransient(ex, ct))
            {
                Log.Warn($"Manifest download failed from {url}: {ex.Message}");
                last = ex;
            }
        }
        throw new HttpRequestException("Couldn't download the package list from any Roblox CDN.", last);
    }

    /// <summary>
    /// Downloads one package to <paramref name="destination"/>, checking its MD5. Tries each CDN host in turn.
    /// <paramref name="onBytes"/> gets byte deltas for progress (negative if a failed attempt is rolled back).
    /// </summary>
    public async Task DownloadPackageAsync(
        string versionGuid, Package package, string destination, Action<long> onBytes, CancellationToken ct)
    {
        string partial = destination + ".part";
        Exception? last = null;

        foreach (string host in _cdnHosts)
        {
            string url = $"{host}/{versionGuid}-{package.Name}";
            long received = 0;
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
                await using (var source = await response.Content.ReadAsStreamAsync(ct))
                await using (var target = File.Create(partial))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await source.ReadAsync(buffer, ct)) > 0)
                    {
                        md5.AppendData(buffer, 0, read);
                        await target.WriteAsync(buffer.AsMemory(0, read), ct);
                        received += read;
                        onBytes(read);
                    }
                }

                string actual = Convert.ToHexStringLower(md5.GetHashAndReset());
                if (actual != package.Md5)
                    throw new InvalidDataException($"{package.Name} is corrupted (MD5 {actual}, expected {package.Md5}).");

                File.Move(partial, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (IsTransient(ex, ct))
            {
                Log.Warn($"Download failed from {url}: {ex.Message}");
                onBytes(-received);
                TryDelete(partial);
                last = ex;
            }
        }
        throw new HttpRequestException($"Couldn't download {package.Name} from any Roblox CDN.", last);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

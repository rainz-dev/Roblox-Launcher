using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace RobloxLauncher.Core.Tests;

/// <summary>Serves canned responses by URL, so tests never touch the network.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new();
    public List<string> Requests { get; } = [];

    public void Serve(string url, byte[] body) =>
        _routes[url] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    public void Serve(string url, string body) => Serve(url, Encoding.UTF8.GetBytes(body));

    public void Fail(string url, HttpStatusCode status = HttpStatusCode.ServiceUnavailable) =>
        _routes[url] = () => new HttpResponseMessage(status);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string url = request.RequestUri!.ToString();
        lock (Requests) Requests.Add(url);
        return Task.FromResult(_routes.TryGetValue(url, out var respond)
            ? respond()
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

internal static class TestZip
{
    /// <summary>Builds a zip in memory. Entry names are used verbatim (Roblox uses backslashes).</summary>
    public static byte[] Create(params (string Name, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(content);
            }
        }
        return buffer.ToArray();
    }

    public static string Md5(byte[] data) => Convert.ToHexStringLower(MD5.HashData(data));
}

/// <summary>A temp folder deleted when the test ends.</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rl-tests-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}

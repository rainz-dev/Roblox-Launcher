namespace RobloxLauncher.Core.Deployment;

/// <summary>One entry of rbxPkgManifest.txt.</summary>
/// <param name="Name">File name on the CDN, e.g. "content-fonts.zip".</param>
/// <param name="Md5">Lowercase hex MD5 of the downloaded file.</param>
/// <param name="PackedSize">Download size in bytes.</param>
/// <param name="Size">Unpacked size in bytes.</param>
public sealed record Package(string Name, string Md5, long PackedSize, long Size);

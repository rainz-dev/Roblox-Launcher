using System.Globalization;

namespace RobloxLauncher.Core.Deployment;

/// <summary>
/// Parses rbxPkgManifest.txt. Format: a "v0" header line, then 4 lines per package:
/// file name, MD5, packed size, unpacked size.
/// </summary>
public static class PackageManifest
{
    public static IReadOnlyList<Package> Parse(string text)
    {
        string[] lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        if (lines.Length == 0 || lines[0] != "v0")
            throw new FormatException($"Unsupported manifest format: expected a 'v0' header, got '{lines.FirstOrDefault()}'.");
        if ((lines.Length - 1) % 4 != 0)
            throw new FormatException("Manifest is truncated: every package needs 4 lines.");

        var packages = new List<Package>();
        for (int i = 1; i < lines.Length; i += 4)
        {
            string name = lines[i];
            string md5 = lines[i + 1].ToLowerInvariant();

            if (name.IndexOfAny(['/', '\\']) >= 0 || name.Contains(".."))
                throw new FormatException($"Invalid package name '{name}'.");
            if (md5.Length != 32 || !md5.All(char.IsAsciiHexDigit))
                throw new FormatException($"Invalid MD5 for package '{name}'.");
            if (!long.TryParse(lines[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out long packed) ||
                !long.TryParse(lines[i + 3], NumberStyles.None, CultureInfo.InvariantCulture, out long size))
                throw new FormatException($"Invalid size for package '{name}'.");

            packages.Add(new Package(name, md5, packed, size));
        }
        return packages;
    }
}

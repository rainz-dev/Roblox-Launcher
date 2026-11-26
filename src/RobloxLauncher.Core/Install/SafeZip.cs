using System.IO.Compression;

namespace RobloxLauncher.Core.Install;

public static class SafeZip
{
    /// <summary>
    /// Extracts a zip, refusing any entry that would land outside <paramref name="destination"/>
    /// ("zip slip"). Roblox's zips use backslashes in entry names, so both separators are handled.
    /// </summary>
    public static void ExtractToDirectory(string zipPath, string destination)
    {
        string root = Path.GetFullPath(destination);
        string rootWithSeparator = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            string relative = entry.FullName.Replace('\\', '/');
            string target = Path.GetFullPath(Path.Combine(root, relative));

            if (!target.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(target, root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Zip entry '{entry.FullName}' points outside the install folder.");

            if (relative.EndsWith('/'))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }
}

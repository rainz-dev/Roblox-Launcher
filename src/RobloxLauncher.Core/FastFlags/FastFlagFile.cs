using System.Text.Json;
using System.Text.RegularExpressions;

namespace RobloxLauncher.Core.FastFlags;

/// <summary>Reads the raw editor's JSON and writes Roblox's ClientSettings\ClientAppSettings.json.</summary>
public static partial class FastFlagFile
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string RelativePath => Path.Combine("ClientSettings", "ClientAppSettings.json");

    /// <summary>
    /// Writes the flags into a Roblox install folder, or removes the file when there are none.
    /// Values are written as strings, which Roblox accepts for every flag type.
    /// </summary>
    public static void WriteTo(string installDir, IReadOnlyDictionary<string, string> flags)
    {
        string path = Path.Combine(installDir, RelativePath);
        if (flags.Count == 0)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ToJson(flags));
    }

    public static string ToJson(IReadOnlyDictionary<string, string> flags) =>
        JsonSerializer.Serialize(new SortedDictionary<string, string>(flags.ToDictionary(), StringComparer.Ordinal), Indented);

    /// <summary>
    /// Parses the raw editor. Accepts a JSON object whose values are strings, numbers or booleans
    /// (booleans become "True"/"False", Roblox's spelling). Throws FormatException with a readable message.
    /// </summary>
    public static Dictionary<string, string> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>(StringComparer.Ordinal);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            throw new FormatException($"That isn't valid JSON: {ex.Message}");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException("Flags must be a JSON object, like { \"FlagName\": \"Value\" }.");

            var flags = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!ValidName().IsMatch(property.Name))
                    throw new FormatException($"\"{property.Name}\" isn't a valid flag name.");

                flags[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString()!,
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "True",
                    JsonValueKind.False => "False",
                    _ => throw new FormatException($"{property.Name}: values must be text, a number, or true/false."),
                };
            }
            return flags;
        }
    }

    /// <summary>Flags Roblox will ignore because they aren't allowlisted.</summary>
    public static IReadOnlyList<string> Ignored(IEnumerable<string> flagNames) =>
        flagNames.Where(n => !FastFlagAllowlist.IsAllowed(n)).Order(StringComparer.Ordinal).ToList();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,199}$")]
    private static partial Regex ValidName();
}

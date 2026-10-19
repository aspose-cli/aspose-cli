using System.Text.Json;

namespace Aspose.Cli.Tests;

/// <summary>
/// The rules of scripts/documentation-paths.json, which scripts/test.ps1 applies through
/// scripts/documentation-paths.ps1: documentation and repository metadata outside the projects.
/// </summary>
internal static class DocumentationPaths
{
    public static readonly string RulesFile = Path.Combine(RepositoryPaths.Root, "scripts", "documentation-paths.json");

    private static readonly Lazy<Rules> Loaded = new(static () =>
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(RulesFile));
        JsonElement root = document.RootElement;
        return new Rules(Strings(root, "extensions"), Strings(root, "directories"), Strings(root, "namePrefixes"), Strings(root, "names"));
    });

    /// <summary>Whether a repository-relative path with forward slashes is documentation, ignoring case.</summary>
    public static bool Contains(string path)
    {
        Rules rules = Loaded.Value;
        string name = path.Split('/')[^1];
        return rules.Extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            || rules.Directories.Any(directory => path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            || rules.NamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || rules.Names.Any(candidate => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] Strings(JsonElement root, string property) =>
        [.. root.GetProperty(property).EnumerateArray().Select(static item => item.GetString()!)];

    private sealed record Rules(string[] Extensions, string[] Directories, string[] NamePrefixes, string[] Names);
}

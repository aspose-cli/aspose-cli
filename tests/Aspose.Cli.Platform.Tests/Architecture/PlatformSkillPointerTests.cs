using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// The platform Skill points to the CLI's own description of the contract instead of restating
/// it: capabilities lists the codes with their exit codes, the error-payload schema the details
/// keys, and each result schema its fields. A restated catalog goes stale silently when the
/// contract changes, so the Skill holds no catalog: no run of four or more list items or table
/// rows that each start with a contract name, such as an error code, a details key, a result
/// field or an exit code. A few named examples inside prose or a short list stay allowed, so
/// the check fires on the shape of a catalog, not on mentioning a name; the threshold of four
/// lets a list of up to three examples, such as the exit codes that come without an error, stand.
/// </summary>
public sealed partial class PlatformSkillPointerTests
{
    /// <summary>The fewest consecutive entries keyed by a contract name that make a catalog.</summary>
    private const int CatalogEntries = 4;

    private static readonly string Platform = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Skills", "Platform");

    [Fact]
    public void PlatformSkill_HoldsNoCatalogOfContractNames()
    {
        string[] files = [.. Directory.EnumerateFiles(Platform, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];
        Assert.NotEmpty(files);

        var catalogs = new List<string>();
        foreach (string file in files)
        {
            string[] lines = File.ReadAllText(file).ReplaceLineEndings("\n").Split('\n');
            int run = 0;
            int first = 0;
            for (int index = 0; index <= lines.Length; index++)
            {
                string? line = index < lines.Length ? lines[index] : null;
                if (line is not null && IsContinuation(line) && run > 0)
                {
                    continue;
                }

                if (line is not null && KeyedEntry().IsMatch(line))
                {
                    first = run == 0 ? index : first;
                    run++;
                    continue;
                }

                if (run >= CatalogEntries)
                {
                    catalogs.Add($"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{first + 1}: {run} entries keyed by contract names, "
                        + $"starting '{lines[first].Trim()}'");
                }

                run = 0;
            }
        }

        Assert.True(catalogs.Count == 0,
            "The platform Skill restates no catalog of codes, details keys, result fields or exit codes; point to "
            + "'aspose-cli capabilities', 'aspose-cli schema <id>' or the error's details instead:"
            + Environment.NewLine + string.Join(Environment.NewLine, catalogs));
    }

    /// <summary>The wrapped continuation of a list item: an indented line that starts no new entry.</summary>
    private static bool IsContinuation(string line) =>
        line.Length > 0 && char.IsWhiteSpace(line[0]) && !line.TrimStart().StartsWith("- ", StringComparison.Ordinal);

    /// <summary>
    /// A list item or table row whose first cell or first word is a contract name: an
    /// <c>UPPER_SNAKE</c> code, a <c>details.*</c> key or a camelCase field in backticks, or a
    /// bare exit code number.
    /// </summary>
    [GeneratedRegex(@"^\s*(?:[-*]|\|)\s*(?:\*\*)?(?:`(?:[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+|details\.[A-Za-z.]+|[a-z][A-Za-z0-9]*(?:\.[a-z][A-Za-z0-9]*)*)`|[0-9]+(?=\s*[|:]))")]
    private static partial Regex KeyedEntry();
}

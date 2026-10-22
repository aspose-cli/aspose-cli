using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// SECURITY.md stays true: each boundary it states ends with a <c>Checked by:</c> line naming the
/// test classes or <c>Class.Method</c> tests that check it, and each name is a test in this
/// repository, so a boundary cannot be stated without a check or outlive the check it names.
/// </summary>
public sealed partial class SecurityClaimsTests
{
    private static readonly string Security = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "SECURITY.md"));

    private static readonly Lazy<Dictionary<string, string>> TestSources = new(static () =>
        Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToDictionary(static file => file, File.ReadAllText, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void EveryBoundaryNamesTheTestsThatCheckIt()
    {
        string[] boundaries = Boundaries(Security);

        Assert.NotEmpty(boundaries);
        Assert.All(boundaries, static boundary =>
        {
            string title = Title(boundary);
            string[] checks = Checks(boundary);
            Assert.True(checks.Length > 0, $"SECURITY.md boundary '{title}' has no 'Checked by:' line naming a test.");
            Assert.All(checks, check => Assert.True(
                Exists(check),
                $"SECURITY.md boundary '{title}' names '{check}', which is no test class or Class.Method test under tests/."));
        });
    }

    [Fact]
    public void ABoundaryWithoutACheckOrWithAMissingTestIsCaught()
    {
        string[] boundaries = Boundaries(
            """
            ## Boundaries

            - **Linked.** A claim.
              Checked by: `SecurityClaimsTests.EveryBoundaryNamesTheTestsThatCheckIt`, `SecurityClaimsTests`.
            - **Unlinked.** A claim
              that wraps.
            - **Missing.** A claim.
              Checked by: `SecurityClaimsTests.NoSuchTest`, `NoSuchTests`.

            ## Releases
            """);

        Assert.Equal(["Linked.", "Unlinked.", "Missing."], boundaries.Select(Title));
        Assert.All(Checks(boundaries[0]), static check => Assert.True(Exists(check), check));
        Assert.Empty(Checks(boundaries[1]));
        Assert.All(Checks(boundaries[2]), static check => Assert.False(Exists(check), check));
    }

    private static string[] Boundaries(string markdown)
    {
        Match section = BoundariesSection().Match(markdown);
        Assert.True(section.Success, "SECURITY.md has no '## Boundaries' section.");
        return [.. BoundaryItem().Matches(section.Groups["body"].Value).Select(static match => match.Value)];
    }

    private static string Title(string boundary) => BoundaryTitle().Match(boundary).Groups["title"].Value;

    private static string[] Checks(string boundary) =>
        [.. CheckedBy().Matches(boundary)
            .SelectMany(static line => CheckName().Matches(line.Groups["names"].Value))
            .Select(static name => name.Groups["name"].Value)];

    private static bool Exists(string check)
    {
        string[] parts = check.Split('.');
        string[] classFiles = [.. TestSources.Value
            .Where(source => Regex.IsMatch(source.Value, $@"\bclass {Regex.Escape(parts[0])}\b"))
            .Select(static source => source.Value)];
        return parts.Length switch
        {
            1 => classFiles.Length > 0,
            2 => classFiles.Any(source => Regex.IsMatch(source, $@"\b(?:void|Task) {Regex.Escape(parts[1])}\(")),
            _ => false,
        };
    }

    [GeneratedRegex(@"^## Boundaries\r?\n(?<body>.*?)(?=^## |\z)", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex BoundariesSection();

    [GeneratedRegex(@"^- .*?(?=^- |^\s*$|\z)", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex BoundaryItem();

    [GeneratedRegex(@"^- \*\*(?<title>[^*]+)\*\*")]
    private static partial Regex BoundaryTitle();

    [GeneratedRegex(@"Checked by:(?<names>(?:\s*`[^`]+`,?)+)")]
    private static partial Regex CheckedBy();

    [GeneratedRegex(@"`(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?)`")]
    private static partial Regex CheckName();
}

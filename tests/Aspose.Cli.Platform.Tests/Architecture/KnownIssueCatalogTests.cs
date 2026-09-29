using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// KNOWN-ISSUES.md stays true: each issue is reproduced by one test, handled by code that names
/// it, and filed under the SDK version the build pins, so an SDK update fails here until the
/// issues are checked against the new version.
/// </summary>
public sealed partial class KnownIssueCatalogTests
{
    private static readonly string KnownIssues = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "KNOWN-ISSUES.md"));

    [Fact]
    public void EveryKnownIssueIsReproducedByOneTestAndNamedByItsHandling()
    {
        string[] issues = [.. IssueHeading().Matches(KnownIssues).Select(static match => match.Groups["id"].Value)];
        string[] reproduced = [.. Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "tests"), "*KnownIssueTests.cs", SearchOption.AllDirectories)
            .SelectMany(static file => Reproduction().Matches(File.ReadAllText(file)))
            .Select(static match => match.Groups["id"].Value)];
        string source = string.Concat(Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        Assert.NotEmpty(issues);
        Assert.Equal(issues.Order(StringComparer.Ordinal), reproduced.Order(StringComparer.Ordinal));
        Assert.All(issues, id => Assert.True(source.Contains(id, StringComparison.Ordinal), $"No source code names known issue {id}."));
    }

    [Fact]
    public void KnownIssuesAreFiledUnderThePinnedSdkVersions()
    {
        Dictionary<string, string> pinned = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryPaths.Root, "eng", "products.json")))!["products"]!
            .AsArray()
            .ToDictionary(static product => (string)product!["sdkPackageId"]!, static product => (string)product!["sdkVersion"]!);

        foreach (Match section in SdkHeading().Matches(KnownIssues))
        {
            string package = section.Groups["package"].Value;
            Assert.True(pinned.TryGetValue(package, out string? version), $"KNOWN-ISSUES.md names {package}, which no product uses.");
            Assert.True(
                version == section.Groups["version"].Value,
                $"The build pins {package} {version}, but KNOWN-ISSUES.md lists issues for {section.Groups["version"].Value}. "
                + "Run the Full test scope against the new SDK, delete the issues it no longer reproduces, then update the heading.");
        }
    }

    [GeneratedRegex(@"^### (?<id>[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)+)\r?$", RegexOptions.Multiline)]
    private static partial Regex IssueHeading();

    [GeneratedRegex(@"^## (?<package>\S+) (?<version>\d+\.\d+\.\d+)\r?$", RegexOptions.Multiline)]
    private static partial Regex SdkHeading();

    [GeneratedRegex(@"KnownIssue\.Reproduces\(\s*""(?<id>[A-Z0-9-]+)""")]
    private static partial Regex Reproduction();
}

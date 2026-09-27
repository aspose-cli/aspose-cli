using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Every acceptance gate reproduces the known issue of the same id in KNOWN-ISSUES.md.</summary>
public sealed class ReleaseAcceptanceGateTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void EveryAcceptanceDirectoryIsAGateForAKnownIssue()
    {
        CliResult result = Plan(Path.Combine(RepositoryPaths.Root, "KNOWN-ISSUES.md"));

        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        JsonArray gates = JsonNode.Parse(result.StdOut)!["gates"]!.AsArray();
        Assert.Equal(
            Directory.GetDirectories(Path.Combine(RepositoryPaths.Root, "tests", "acceptance")).Length,
            gates.Count);
    }

    [Fact]
    public void AGateWithoutAKnownIssueAndAnIssueWithoutAGateAreRejected()
    {
        string knownIssues = _directory.File("KNOWN-ISSUES.md");
        File.WriteAllText(knownIssues, """
            # Known issues

            ### CELLS-SVG-EGRESS

            ### NOT-A-GATE
            """, new UTF8Encoding(false));

        CliResult result = Plan(knownIssues);

        Assert.NotEqual(0, result.ExitCode);
        string output = result.StdOut + result.StdErr;
        Assert.Contains("NOT-A-GATE", output, StringComparison.Ordinal);
        Assert.Contains("PDF-HTML-EGRESS", output, StringComparison.Ordinal);
    }

    private CliResult Plan(string knownIssues)
    {
        string shell = ToolPath.Require("pwsh");
        return new CliProcess(shell, CliEnvironment.Evaluation(_directory.File("config")), TimeSpan.FromSeconds(60))
            .Run(_directory.Path,
            [
                "-NoLogo", "-NoProfile", "-NonInteractive",
                "-File", Path.Combine(RepositoryPaths.Root, "scripts", "acceptance.ps1"),
                "-Plan", "-KnownIssuesPath", knownIssues,
            ]);
    }
}

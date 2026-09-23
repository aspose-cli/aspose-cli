using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Known-failing SDK acceptance gates block a release unless KNOWN-ISSUES.md waives them.</summary>
public sealed class ReleaseAcceptanceGateTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private static readonly string Version = XDocument.Load(Path.Combine(RepositoryPaths.Root, "Directory.Build.props"))
        .Descendants("Version").Single().Value;

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void EveryAcceptanceDirectoryIsAListedGateAndNothingIsWaivedByDefault()
    {
        CliResult result = Plan(_directory.File("missing-KNOWN-ISSUES.md"));

        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        JsonArray gates = JsonNode.Parse(result.StdOut)!["gates"]!.AsArray();
        Assert.Equal(
            Directory.GetDirectories(Path.Combine(RepositoryPaths.Root, "tests", "acceptance")).Length,
            gates.Count);
        Assert.Contains(gates, gate => gate!["id"]!.GetValue<string>() == "SLD-003");
        Assert.All(gates, gate => Assert.False(gate!["waived"]!.GetValue<bool>()));
    }

    [Fact]
    public void AWaiverAppliesOnlyToItsGateAndTheDeclaredVersion()
    {
        string knownIssues = Write($"""
            # Known issues

            ## Release gate waivers

            Waived gates still run; a waived gate that starts passing is reported.

            | Gate | Version | Tracking | Reason |
            | --- | --- | --- | --- |
            | SLD-003 | {Version} | SLIDESNET-00000 | Upstream chart title layout defect; documented for customers. |
            | PDF-MOVE-BOOKMARK | 0.0.1 | PDFNET-00000 | Waived for an older version only. |

            ## Other notes
            | not | a | waiver | row |
            """);

        CliResult result = Plan(knownIssues);

        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        JsonArray gates = JsonNode.Parse(result.StdOut)!["gates"]!.AsArray();
        JsonNode slides = gates.Single(gate => gate!["id"]!.GetValue<string>() == "SLD-003")!;
        Assert.True(slides["waived"]!.GetValue<bool>());
        Assert.Equal("SLIDESNET-00000", slides["tracking"]!.GetValue<string>());
        Assert.False(gates.Single(gate => gate!["id"]!.GetValue<string>() == "PDF-MOVE-BOOKMARK")!["waived"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("| NOT-A-GATE | 1.0.0 | T-1 | Reason |", "unknown acceptance gate")]
    [InlineData("| SLD-003 | next | T-1 | Reason |", "invalid version")]
    [InlineData("| SLD-003 | 1.0.0 |  | Reason |", "tracking reference and a reason")]
    [InlineData("| SLD-003 | 1.0.0 | T-1 |", "four cells")]
    public void InvalidWaiversAreRejected(string row, string message)
    {
        CliResult result = Plan(Write($"""
            ## Release gate waivers

            | Gate | Version | Tracking | Reason |
            | --- | --- | --- | --- |
            {row}
            """));

        Assert.NotEqual(0, result.ExitCode);
        string output = (result.StdOut + result.StdErr)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
        Assert.Contains(message, output, StringComparison.Ordinal);
    }

    private string Write(string content)
    {
        string path = _directory.File("KNOWN-ISSUES-" + Guid.NewGuid().ToString("N") + ".md");
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    private CliResult Plan(string knownIssues)
    {
        string shell = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.Combine(path.Trim('"'), OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh"))
            .First(File.Exists);
        return new CliProcess(shell, CliEnvironment.Evaluation(_directory.File("config")), TimeSpan.FromSeconds(60))
            .Run(_directory.Path,
            [
                "-NoLogo", "-NoProfile", "-NonInteractive",
                "-File", Path.Combine(RepositoryPaths.Root, "scripts", "acceptance.ps1"),
                "-Plan", "-KnownIssuesPath", knownIssues,
            ]);
    }
}

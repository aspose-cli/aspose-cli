using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// An extraction that finds no table names the commands to compare with, and what it names runs
/// as written: with its required directory, on the pages selected, at a path with a space and, on
/// an encrypted input, with the variable that opens it.
/// </summary>
public sealed partial class PdfExtractTablesHintTests : IDisposable
{
    private const string Variable = "TABLES_PDF_PASSWORD";

    private readonly TempWorkspace _workspace = new();

    public PdfExtractTablesHintTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("plain.pdf", null)]
    [InlineData("with a space.pdf", null)]
    [InlineData("plain.pdf", "1")]
    public void NoTableFound_TheExtractionItNamesRunsAsWritten(string file, string? pages)
    {
        File.WriteAllBytes(_workspace.File(file), ScenarioFixtures.Read("pdf.pdf"));
        string[] selection = pages is null ? [] : ["--pages", pages];

        string line = Named(Hint(_workspace.Run(["pdf", "extract", file, "--what", "tables", "--out-dir", "tables", .. selection, "--output-format", "json"])), "pdf extract");

        Assert.Equal(pages is not null, line.Contains("--pages 1", StringComparison.Ordinal));
        CliResult ran = _workspace.RunCommandLine(line);
        Assert.True(ran.ExitCode == 0, ran.StdErr);
        Assert.True(new FileInfo(_workspace.File("tables/document.txt")).Length > 0);
    }

    [Fact]
    public void NoTableFound_TheReviewItNamesRunsAsWritten()
    {
        File.WriteAllBytes(_workspace.File("plain.pdf"), ScenarioFixtures.Read("pdf.pdf"));

        string line = Named(Hint(_workspace.Run("pdf", "extract", "plain.pdf", "--what", "tables", "--out-dir", "tables", "--output-format", "json")), "review");

        CliResult ran = _workspace.RunCommandLine(line);
        Assert.True(ran.ExitCode is 0 or 8, ran.StdErr);
        Assert.True(File.Exists(Path.Combine(_workspace.File("plain.pdf.review"), "review.json")));
    }

    [Fact]
    public void NoTableFound_OnAnEncryptedInput_TheCommandsItNamesPassThePasswordVariableOn()
    {
        File.WriteAllBytes(_workspace.File("locked.pdf"), ScenarioFixtures.Read("pdf.encrypted"));
        var password = new Dictionary<string, string?> { [Variable] = ScenarioFixtures.Password };

        string hint = Hint(_workspace.RunWithEnv(password, "pdf", "extract", "locked.pdf", "--what", "tables", "--out-dir", "tables", "--password-env", Variable, "--output-format", "json"));

        string extraction = Named(hint, "pdf extract");
        Assert.Contains($"--password-env {Variable}", extraction, StringComparison.Ordinal);
        Assert.Contains($"--password-env {Variable}", Named(hint, "review"), StringComparison.Ordinal);
        CliResult ran = _workspace.RunCommandLine(extraction, password);
        Assert.True(ran.ExitCode == 0, ran.StdErr);
        Assert.True(new FileInfo(_workspace.File("tables/document.txt")).Length > 0);
    }

    private static string Hint(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray()
            .Single(static warning => warning!["code"]!.GetValue<string>() == "TABLES_NOT_FOUND")!["hint"]!.GetValue<string>();
    }

    private static string Named(string hint, string command)
    {
        Match match = QuotedCommand().Matches(hint).FirstOrDefault(found => found.Groups["path"].Value == command)
            ?? throw new Xunit.Sdk.XunitException($"The hint names no runnable '{command}' command: {hint}");
        return match.Groups["line"].Value;
    }

    [GeneratedRegex(@"'(?<line>aspose-cli (?<path>pdf extract|review) [^']+)'")]
    private static partial Regex QuotedCommand();
}

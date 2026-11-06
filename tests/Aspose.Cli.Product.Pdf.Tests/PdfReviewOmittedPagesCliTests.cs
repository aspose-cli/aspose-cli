using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// A review renders no more pages than <c>--max-items</c> allows, nor than the preview snapshot
/// budget has room for: it stops there instead of failing, keeps the pages it rendered as evidence,
/// and names the rest in one finding whose commands, run as written, review them on their own and
/// open the document as the review did.
/// </summary>
public sealed partial class PdfReviewOmittedPagesCliTests : IDisposable
{
    private const string Variable = "REVIEW_PDF_PASSWORD";
    private const string Password = "omitted-pages-secret-5d21";
    private const string OwnerVariable = "REVIEW_PDF_OWNER_PASSWORD";
    private const string FileBytes = "ASPOSE_CLI_PREVIEW_MAX_FILE_BYTES";
    private const string SnapshotBytes = "ASPOSE_CLI_PREVIEW_MAX_SNAPSHOT_BYTES";
    private readonly PdfEngineFixture _fixture = new();
    private readonly TempWorkspace _workspace = new();

    public PdfReviewOmittedPagesCliTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose()
    {
        _workspace.Dispose();
        _fixture.Dispose();
    }

    /// <summary>
    /// PDF-006: three pages reviewed with <c>--max-items 1</c> name pages 2-3 and say why; the
    /// split and the review the hint gives run as written and review both pages.
    /// </summary>
    [Fact]
    public void Review_BeyondMaxItems_NamesThePagesLeftAndCommandsThatReviewThem()
    {
        CreateDocument("plain.pdf", pages: 3);

        CliResult review = _workspace.Run("review", "plain.pdf", "--max-items", "1", "--out", "plain.review", "--output-format", "json");

        Assert.True(review.ExitCode == 8, review.StdOut + review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        Assert.False(result["coverage"]!["complete"]!.GetValue<bool>());
        JsonNode finding = Omitted(result);
        Assert.Equal("page/2-3", finding["location"]!.GetValue<string>());
        Assert.Contains("--max-items", finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
        (string split, string part) = Commands(finding);
        Assert.DoesNotContain("--password-env", split + part, StringComparison.Ordinal);
        RunsAsWritten(split, part, variables: null, expectedPages: 2);
    }

    /// <summary>
    /// An encrypted document of four pages read with <c>--password-env</c>, whose snapshot budget
    /// holds two of its pages and the manifest: the review stops at the page that does not fit,
    /// keeps both images, and names pages 3-4 and the snapshot variable; its commands pass the same
    /// variable on, never the password, and run as written in the same environment.
    /// </summary>
    [Fact]
    [Category(TestCategory.Slow)]
    public void Review_WhenTheSnapshotBudgetRunsOut_KeepsTheRenderedPagesAndNamesTheRest()
    {
        CreateEncryptedDocument("enc.pdf", pages: 4);
        Dictionary<string, string?> password = new() { [Variable] = Password };
        (long[] pages, long manifest) = Probe(password, "enc.pdf", "--password-env", Variable);
        Assert.True(pages[2] > manifest, $"{pages[2]} {manifest}");
        Dictionary<string, string?> budget = new(password) { [SnapshotBytes] = Text(pages[0] + pages[1] + manifest) };

        CliResult review = _workspace.RunWithEnv(budget,
            "review", "enc.pdf", "--password-env", Variable, "--out", "enc.review", "--output-format", "json");

        Assert.True(review.ExitCode == 8, review.StdOut + review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        JsonNode coverage = result["coverage"]!;
        Assert.Equal((2, 2, false), (coverage["renderedItemCount"]!.GetValue<int>(), coverage["omittedItemCount"]!.GetValue<int>(), coverage["complete"]!.GetValue<bool>()));
        Assert.Equal(
            ["artifacts/page-0001.png", "artifacts/page-0002.png"],
            result["artifacts"]!.AsArray()
                .Where(static artifact => artifact!["role"]!.GetValue<string>() == "evidence")
                .Select(static artifact => artifact!["path"]!.GetValue<string>()));
        Assert.Equal(
            ["page-0001.png", "page-0002.png", "view.json"],
            Directory.EnumerateFiles(_workspace.File(Path.Combine("enc.review", "artifacts"))).Select(static file => Path.GetFileName(file)).Order(StringComparer.Ordinal));
        JsonNode finding = Omitted(result);
        Assert.Equal("page/3-4", finding["location"]!.GetValue<string>());
        Assert.Contains(SnapshotBytes, finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(FileBytes, review.StdOut, StringComparison.Ordinal);
        (string split, string part) = Commands(finding);
        Assert.Contains($" --password-env {Variable} ", split, StringComparison.Ordinal);
        Assert.Contains($" --password-env {Variable} ", part, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, review.StdOut + review.StdErr, StringComparison.Ordinal);
        RunsAsWritten(split, part, budget, expectedPages: 2);
    }

    /// <summary>
    /// PDF-009: a snapshot budget smaller than two pages, and far smaller than one file may hold,
    /// fails the review no more: it renders the page that fits, and its finding names the snapshot
    /// variable, never the one of a single file.
    /// </summary>
    [Fact]
    public void Review_WithASmallSnapshotBudget_RendersWhatFitsAndNamesTheSnapshotVariable()
    {
        CreateDocument("plain.pdf", pages: 3);
        (long[] pages, _) = Probe(null, "plain.pdf");
        Dictionary<string, string?> budget = new() { [SnapshotBytes] = Text(pages[0] + pages[1] - 1) };

        CliResult review = _workspace.RunWithEnv(budget, "review", "plain.pdf", "--out", "small.review", "--output-format", "json");

        Assert.True(review.ExitCode == 8, review.StdOut + review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        Assert.Equal((1, false), (result["coverage"]!["renderedItemCount"]!.GetValue<int>(), result["coverage"]!["complete"]!.GetValue<bool>()));
        JsonNode finding = Omitted(result);
        Assert.Equal("page/2-3", finding["location"]!.GetValue<string>());
        Assert.Contains(SnapshotBytes, finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(FileBytes, review.StdOut + review.StdErr, StringComparison.Ordinal);
        Commands(finding);
    }

    /// <summary>
    /// A snapshot budget smaller than the first page renders no page; the finding names the
    /// snapshot variable in its message and its hint, as reviewing fewer pages would render none either.
    /// </summary>
    [Fact]
    public void Review_WhenTheFirstPageDoesNotFit_NamesTheSnapshotVariableToRaise()
    {
        CreateDocument("plain.pdf", pages: 3);
        (long[] pages, _) = Probe(null, "plain.pdf");
        Dictionary<string, string?> budget = new() { [SnapshotBytes] = Text(pages[0] - 1) };

        CliResult review = _workspace.RunWithEnv(budget, "review", "plain.pdf", "--out", "none.review", "--output-format", "json");

        Assert.True(review.ExitCode == 8, review.StdOut + review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        Assert.Equal(0, result["coverage"]!["renderedItemCount"]!.GetValue<int>());
        JsonNode finding = Omitted(result);
        Assert.Equal("page/1-3", finding["location"]!.GetValue<string>());
        Assert.Contains(SnapshotBytes, finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains(SnapshotBytes, finding["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(FileBytes, review.StdOut + review.StdErr, StringComparison.Ordinal);
    }

    /// <summary>
    /// A snapshot budget smaller than one file may hold but exactly as large as the whole evidence
    /// holds all of it: the review is complete and names no page left out.
    /// </summary>
    [Fact]
    public void Review_WhenTheEvidenceJustFitsTheSnapshotBudget_IsComplete()
    {
        CreateDocument("plain.pdf", pages: 3);
        (long[] pages, long manifest) = Probe(null, "plain.pdf");
        Dictionary<string, string?> budget = new() { [SnapshotBytes] = Text(pages.Sum() + manifest) };

        CliResult review = _workspace.RunWithEnv(budget, "review", "plain.pdf", "--out", "full.review", "--output-format", "json");

        Assert.True(review.ExitCode == 0, review.StdOut + review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        Assert.Equal((3, true), (result["coverage"]!["renderedItemCount"]!.GetValue<int>(), result["coverage"]!["complete"]!.GetValue<bool>()));
        Assert.DoesNotContain(result["findings"]!.AsArray(), static finding => finding!["code"]!.GetValue<string>() == "PDF_REVIEW_TRUNCATED");
    }

    /// <summary>
    /// Only the snapshot's bytes stop a review: a page beyond the bytes one file may hold, or a
    /// file beyond how many a view holds, fails it as before, with no evidence, and the error
    /// names the variable of that limit.
    /// </summary>
    [Theory]
    [InlineData(FileBytes, "1000")]
    [InlineData("ASPOSE_CLI_PREVIEW_MAX_FILES", "1")]
    public void Review_BeyondAnotherLimit_FailsNamingItsVariable(string variable, string value)
    {
        CreateDocument("plain.pdf", pages: 3);

        CliResult review = _workspace.RunWithEnv(
            new Dictionary<string, string?> { [variable] = value },
            "review", "plain.pdf", "--out", "failed.review", "--output-format", "json");

        Assert.True(review.ExitCode == 4, review.StdOut + review.StdErr);
        JsonNode error = JsonNode.Parse(review.StdErr)!["error"]!;
        Assert.Equal("PREVIEW_BUDGET_EXCEEDED", error["code"]!.GetValue<string>());
        Assert.Contains(variable, error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(SnapshotBytes, error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(_workspace.File("failed.review")));
    }

    private void CreateDocument(string file, int pages) =>
        File.Copy(_fixture.CreateDocument(file, pages), _workspace.File(file));

    // Encrypted by an edit, as a user would protect it.
    private void CreateEncryptedDocument(string file, int pages)
    {
        CreateDocument("source.pdf", pages);
        File.WriteAllText(_workspace.File("encrypt.json"),
            $$"""{"ops":[{"op":"encrypt_document","userPasswordEnv":"{{Variable}}","ownerPasswordEnv":"{{OwnerVariable}}"}]}""");
        CliResult encrypted = _workspace.RunWithEnv(
            new Dictionary<string, string?> { [Variable] = Password, [OwnerVariable] = Password + "-owner" },
            "pdf", "edit", "source.pdf", "--ops", "encrypt.json", "--out", file, "--output-format", "json");
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);
    }

    /// <summary>The bytes of each page image and of the view manifest of a review without a budget.</summary>
    private (long[] Pages, long Manifest) Probe(Dictionary<string, string?>? variables, params string[] input)
    {
        string directory = $"probe-{Guid.NewGuid():N}";
        CliResult probe = _workspace.RunWithEnv(variables ?? new Dictionary<string, string?>(), ["review", .. input, "--out", directory, "--output-format", "json"]);
        Assert.True(probe.ExitCode == 0, probe.StdOut + probe.StdErr);
        string artifacts = _workspace.File(Path.Combine(directory, "artifacts"));
        return (
            [.. Directory.EnumerateFiles(artifacts, "page-*.png").Order(StringComparer.Ordinal).Select(static file => new FileInfo(file).Length)],
            new FileInfo(Path.Combine(artifacts, "view.json")).Length);
    }

    private static JsonNode Omitted(JsonNode result) => Assert.Single(
        result["findings"]!.AsArray(),
        static finding => finding!["code"]!.GetValue<string>() == "PDF_REVIEW_TRUNCATED")!;

    /// <summary>The split and the review of its part that a finding's hint names, in that order.</summary>
    private static (string Split, string Part) Commands(JsonNode finding)
    {
        string hint = finding["hint"]!.GetValue<string>();
        string[] commands = [.. CommandPattern().Matches(hint).Select(static match => match.Value)];
        Assert.True(commands.Length == 2, hint);
        Assert.StartsWith("aspose-cli pdf split ", commands[0], StringComparison.Ordinal);
        Assert.StartsWith("aspose-cli review ", commands[1], StringComparison.Ordinal);
        return (commands[0], commands[1]);
    }

    // The split writes one part; its review renders every page of it.
    private void RunsAsWritten(string split, string part, IReadOnlyDictionary<string, string?>? variables, int expectedPages)
    {
        CliResult splitRun = _workspace.RunCommandLine(split, variables);
        Assert.True(splitRun.ExitCode == 0, $"{split}{Environment.NewLine}{splitRun.StdErr}");
        CliResult partRun = _workspace.RunCommandLine(part, variables);
        Assert.True(partRun.ExitCode == 0, $"{part}{Environment.NewLine}{partRun.StdOut}{partRun.StdErr}");
        JsonNode coverage = JsonNode.Parse(partRun.StdOut)!["coverage"]!;
        Assert.Equal((expectedPages, true), (coverage["renderedItemCount"]!.GetValue<int>(), coverage["complete"]!.GetValue<bool>()));
    }

    private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex("aspose-cli .*? --output-format json")]
    private static partial Regex CommandPattern();
}

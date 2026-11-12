using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// A review that <c>--max-items</c> stopped before the last part of its view reports, in every
/// product alike, one finding that says how many parts it rendered of how many and which it left
/// out, as its coverage counts them, and whose hint names the review of every part in a new
/// directory: that review, run as written, is complete and names nothing left out, and run a second
/// time finds the directory it wrote.
/// </summary>
[Category(TestCategory.Slow)]
public sealed partial class ReviewTruncationCliTests
{
    [Theory]
    [InlineData("book.xlsx", "CELLS_REVIEW_TRUNCATED", "sheets")]
    [InlineData("doc.docx", "WORDS_REVIEW_TRUNCATED", "pages")]
    [InlineData("deck.pptx", "SLIDES_REVIEW_TRUNCATED", "slides")]
    [InlineData("doc.pdf", "PDF_REVIEW_TRUNCATED", "pages")]
    public void Review_BeyondMaxItems_NamesWhatItLeftOutAndTheReviewOfEveryPart(string file, string code, string parts)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        ReviewSnapshotBudgetTests.CreateSample(workspace, file);

        CliResult review = workspace.Run("review", file, "--max-items", "1", "--out", "first", "--output-format", "json");

        Assert.True(review.ExitCode == 8, review.StdOut + review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        int rendered = result["coverage"]!["renderedItemCount"]!.GetValue<int>();
        int total = result["coverage"]!["expectedItemCount"]!.GetValue<int>();
        Assert.True(rendered == 1 && total > 2, review.StdOut);
        JsonNode finding = Assert.Single(result["findings"]!.AsArray(), static finding => finding!["code"]!.GetValue<string>().EndsWith("_REVIEW_TRUNCATED", StringComparison.Ordinal))!;
        Assert.Equal(code, finding["code"]!.GetValue<string>());
        string message = finding["message"]!.GetValue<string>();
        Assert.Contains(Text($"rendered {rendered} of {total} {parts}, the most --max-items allows"), message, StringComparison.Ordinal);
        Assert.Contains(Text($"the {total - rendered} after the first {rendered} ({rendered + 1}-{total} in the view's order)"), message, StringComparison.Ordinal);
        // review.json and the page record the result the command printed.
        Assert.Equal(result["findings"]!.ToJsonString(), JsonNode.Parse(File.ReadAllText(workspace.File(Path.Combine("first", "review.json"))))!["findings"]!.ToJsonString());
        Assert.Contains(code, File.ReadAllText(workspace.File(Path.Combine("first", "index.html"))), StringComparison.Ordinal);

        string command = Assert.Single(CommandPattern().Matches(finding["hint"]!.GetValue<string>())).Groups[1].Value;
        Assert.Contains(Text($" --max-items {total} "), command, StringComparison.Ordinal);
        CliResult all = workspace.RunCommandLine(command);

        Assert.True(all.ExitCode == 0, command + Environment.NewLine + all.StdOut + all.StdErr);
        JsonNode complete = JsonNode.Parse(all.StdOut)!;
        Assert.Equal((total, true), (complete["coverage"]!["renderedItemCount"]!.GetValue<int>(), complete["coverage"]!["complete"]!.GetValue<bool>()));
        Assert.DoesNotContain(complete["findings"]!.AsArray(), static finding => finding!["code"]!.GetValue<string>().EndsWith("_REVIEW_TRUNCATED", StringComparison.Ordinal));
        CliResult twice = workspace.RunCommandLine(command);
        Assert.Equal("OUTPUT_EXISTS", JsonNode.Parse(twice.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex("'(aspose-cli review .*? --output-format json)'")]
    private static partial Regex CommandPattern();
}

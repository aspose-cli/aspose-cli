using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// Every product's review renders each part while the snapshot budget, its manifest included,
/// still holds it, instead of failing on the part that does not fit: the parts it rendered stay as
/// evidence, the rest count as omitted, and the evidence never holds more bytes than the budget.
/// </summary>
[Category(TestCategory.Slow)]
public sealed class ReviewSnapshotBudgetTests
{
    private const string SnapshotBytes = "ASPOSE_CLI_PREVIEW_MAX_SNAPSHOT_BYTES";

    /// <summary>
    /// Each budget is smaller than one file may hold: one exactly as large as the whole evidence
    /// holds it all; one byte less leaves the last part out so that the manifest fits; and one that
    /// holds the first part and the manifest stops at the second part.
    /// </summary>
    [Theory]
    [InlineData("book.xlsx")]
    [InlineData("doc.docx")]
    [InlineData("deck.pptx")]
    [InlineData("doc.pdf")]
    public void Review_WithinTheSnapshotBudget_RendersEachPartThatFits(string file)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        CreateSample(workspace, file);
        CliResult probe = workspace.Run("review", file, "--out", "probe", "--output-format", "json");
        Assert.True(probe.ExitCode == 0, probe.StdOut + probe.StdErr);
        string probed = workspace.File(Path.Combine("probe", "artifacts"));
        string[] images = [.. Directory.EnumerateFiles(probed, "*.png").Select(static path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];
        long[] parts = [.. images.Select(name => new FileInfo(Path.Combine(probed, name)).Length)];
        long manifest = new FileInfo(Path.Combine(probed, "view.json")).Length;
        Assert.True(parts.Length >= 3 && parts[1] > manifest, $"{string.Join(", ", parts)}; {manifest}");

        (JsonNode full, _) = Review(workspace, file, "full", parts.Sum() + manifest, expectedExit: 0);
        Assert.Equal((parts.Length, true), Rendered(full));
        Assert.DoesNotContain(full["findings"]!.AsArray(), static finding => finding!["code"]!.GetValue<string>().EndsWith("_REVIEW_TRUNCATED", StringComparison.Ordinal));

        (JsonNode tight, string[] kept) = Review(workspace, file, "tight", parts.Sum() + manifest - 1, expectedExit: 8);
        Assert.Equal((parts.Length - 1, false), Rendered(tight));
        Assert.Equal([.. images[..^1], "view.json"], kept);

        (JsonNode stopped, string[] first) = Review(workspace, file, "stopped", parts[0] + manifest, expectedExit: 8);
        Assert.Equal((1, false), Rendered(stopped));
        Assert.Equal(parts.Length - 1, stopped["coverage"]!["omittedItemCount"]!.GetValue<int>());
        Assert.Equal([images[0], "view.json"], first);
    }

    /// <summary>
    /// A review of <paramref name="file"/> under <paramref name="budget"/> snapshot bytes, which exits with
    /// <paramref name="expectedExit"/> and whose evidence holds no more than the budget: its result and
    /// the files of its view.
    /// </summary>
    private static (JsonNode Result, string[] Files) Review(TempWorkspace workspace, string file, string directory, long budget, int expectedExit)
    {
        CliResult review = workspace.RunWithEnv(
            new Dictionary<string, string?> { [SnapshotBytes] = budget.ToString(CultureInfo.InvariantCulture) },
            "review", file, "--out", directory, "--output-format", "json");
        Assert.True(review.ExitCode == expectedExit, review.StdOut + review.StdErr);
        string artifacts = workspace.File(Path.Combine(directory, "artifacts"));
        string[] files = [.. Directory.EnumerateFiles(artifacts).Order(StringComparer.Ordinal)];
        Assert.True(files.Sum(static path => new FileInfo(path).Length) <= budget);
        return (JsonNode.Parse(review.StdOut)!, [.. files.Select(static path => Path.GetFileName(path))]);
    }

    private static (int Rendered, bool Complete) Rendered(JsonNode result) =>
        (result["coverage"]!["renderedItemCount"]!.GetValue<int>(), result["coverage"]!["complete"]!.GetValue<bool>());

    /// <summary>A workbook of three sheets, a document of three or four pages (as many as evaluation mode reads of a PDF), a deck of three slides, or that document as a PDF.</summary>
    private static void CreateSample(TempWorkspace workspace, string file)
    {
        switch (Path.GetExtension(file))
        {
            case ".xlsx":
                workspace.Run("cells", "create", file, "--sheet-names", "A,B,C", "--output-format", "json").Succeeded();
                File.WriteAllText(workspace.File("values.json"), """
                    {"ops":[
                     {"op":"set_values","sheet":"A","range":"A1","values":[["Hello","World"],[1,2]]},
                     {"op":"set_values","sheet":"B","range":"A1","values":[["Hello","World"],[1,2]]},
                     {"op":"set_values","sheet":"C","range":"A1","values":[["Hello","World"],[1,2]]}
                    ]}
                    """);
                workspace.Run("cells", "edit", file, "--in-place", "--ops", "values.json", "--output-format", "json").Succeeded();
                break;
            case ".pptx":
                File.WriteAllText(workspace.File("deck.md"), string.Concat(
                    new[] { "One", "Two", "Three" }.Select(static title => $"# {title}\n\n- First point\n- Second point\n\n")));
                workspace.Run("slides", "create", file, "--from-markdown", "deck.md", "--output-format", "json").Succeeded();
                break;
            default:
                File.WriteAllText(workspace.File("doc.md"), "# Report\n\n" + string.Concat(Enumerable.Range(1, 80).Select(static number =>
                    $"Paragraph {number} of the report, long enough to fill a line or two of the page with ordinary text.\n\n")));
                workspace.Run("words", "create", "doc.docx", "--from-markdown", "doc.md", "--output-format", "json").Succeeded();
                if (file.EndsWith(".pdf", StringComparison.Ordinal))
                {
                    workspace.Run("words", "convert", "doc.docx", "--to", "pdf", "--out", file, "--output-format", "json").Succeeded();
                }
                break;
        }
    }
}

using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfCliWorkflowTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Category(TestCategory.Slow)]
    [Fact]
    public void InspectQueryEditExtractAndConvert_RoundTripsThroughTheBuiltCli()
    {
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Portable PDF workflow"));
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 650, 280, 680))
            {
                PartialName = "Customer",
                Value = "Draft",
            });
            document.Save(_workspace.File("source.pdf"));
        }

        CliResult capabilities = _workspace.Run(
            "capabilities", "pdf", "--output", "json");
        Assert.True(capabilities.ExitCode == 0, capabilities.StdErr);
        JsonNode product = JsonNode.Parse(capabilities.StdOut)!["products"]![0]!;
        Assert.Equal(
            ["pdf", "pdf convert", "pdf create", "pdf edit", "pdf extract", "pdf inspect", "pdf merge", "pdf query", "pdf query forms", "pdf query pages", "pdf query search", "pdf render", "pdf sign", "pdf split", "pdf validate"],
            product["commands"]!.AsArray()
                .Select(static command => command!["path"]!.GetValue<string>()));
        Assert.Equal(
            [
                "add_attachment", "add_bookmark", "add_footer_text", "add_header_text",
                "add_link", "add_page_numbers", "add_stamp_image", "add_watermark_image",
                "add_watermark_text", "crop_pages", "decrypt", "delete_bookmarks",
                "delete_pages", "encrypt", "flatten_forms", "insert_blank_page",
                "insert_pages_from", "move_pages", "optimize", "redact_area",
                "redact_text", "remove_attachment", "remove_metadata", "rotate_pages",
                "set_form_field", "set_metadata", "set_page_labels", "set_page_size",
            ],
            Assert.Single(product["operations"]!.AsArray())!["ops"]!.AsArray()
                .Select(static operation => operation!.GetValue<string>()));

        CliResult info = _workspace.Run(
            "pdf", "inspect", "source.pdf", "--output", "json");
        CliResult read = _workspace.Run(
            "pdf", "query", "pages", "source.pdf", "--pages", "1", "--output", "json");
        CliResult forms = _workspace.Run(
            "pdf", "query", "forms", "source.pdf", "--output", "json");
        CliResult convert = _workspace.Run(
            "pdf", "convert", "source.pdf", "--to", "txt",
            "--out", "source.txt", "--output", "json");

        Assert.True(info.ExitCode == 0, info.StdErr);
        Assert.Equal(1, JsonNode.Parse(info.StdOut)!["pdf"]!["pages"]!.GetValue<int>());
        Assert.True(read.ExitCode == 0, read.StdErr);
        Assert.Contains("Portable PDF workflow", read.StdOut, StringComparison.Ordinal);
        Assert.True(forms.ExitCode == 0, forms.StdErr);
        Assert.Contains("Customer", forms.StdOut, StringComparison.Ordinal);
        Assert.True(convert.ExitCode == 0, convert.StdErr);
        Assert.True(File.Exists(_workspace.File("source.txt")));

        const string operations =
            "{\"ops\":[{\"op\":\"set_form_field\",\"name\":\"Customer\",\"value\":\"Contoso\"}]}";
        CliResult edited = _workspace.Run(
            "pdf", "edit", "source.pdf", "--ops", operations,
            "--out", "edited.pdf", "--output", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);

        CliResult reopened = _workspace.Run(
            "pdf", "query", "forms", "edited.pdf", "--output", "json");
        Assert.True(reopened.ExitCode == 0, reopened.StdErr);
        Assert.Contains("Contoso", reopened.StdOut, StringComparison.Ordinal);

        CliResult exported = _workspace.Run(
            "pdf", "extract", "edited.pdf", "--what", "forms", "--to", "json",
            "--out", "forms.json", "--output", "json");
        Assert.True(exported.ExitCode == 0, exported.StdErr);
        Assert.True(File.Exists(_workspace.File("forms.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Edit_UnsupportedOperationPreservesTheEntireInput(bool bestEffort)
    {
        string input = _workspace.File("source.pdf");
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("Quarterly report"));
            document.Save(input);
        }
        byte[] original = File.ReadAllBytes(input);
        const string operations =
            """{"ops":[{"op":"set_metadata","title":"Changed"},{"op":"linearize"}]}""";
        List<string> arguments =
        [
            "pdf", "edit", "source.pdf", "--ops", operations,
            "--in-place", "--backup", "--output", "json",
        ];
        if (bestEffort)
        {
            arguments.Add("--best-effort");
        }

        CliResult result = _workspace.Run(arguments.ToArray());

        Assert.Equal(4, result.ExitCode);
        Assert.Empty(result.StdOut);
        Assert.Equal("OPS_INVALID", JsonNode.Parse(result.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(original, File.ReadAllBytes(input));
    }

    [Fact]
    public void EvaluationMode_DisclosesAPartialInspectAndRefusesReadingPastTheFourthPage()
    {
        using (var fixture = new PdfEngineFixture())
        {
            File.Copy(fixture.CreateRawDocument("six.pdf", pages: 6), _workspace.File("six.pdf"));
        }

        CliResult inspect = _workspace.Run("pdf", "inspect", "six.pdf", "--output", "json");
        CliResult firstPages = _workspace.Run("pdf", "query", "pages", "six.pdf", "--pages", "1-4", "--output", "json");
        CliResult allPages = _workspace.Run("pdf", "query", "pages", "six.pdf", "--output", "json");

        Assert.True(inspect.ExitCode == 0, inspect.StdErr);
        JsonNode truncated = Assert.Single(
            JsonNode.Parse(inspect.StdOut)!["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "EVAL_INPUT_TRUNCATED")!;
        Assert.StartsWith("Evaluation mode shows only the first 4 of 6 pages", truncated["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(firstPages.ExitCode == 0, firstPages.StdErr);
        Assert.Equal(7, allPages.ExitCode);
        Assert.Equal("EVALUATION_LIMIT", JsonNode.Parse(allPages.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void QueryPages_NextRereadsACutPageAndRaisesTheBudgetForAPageThatAloneExceedsIt()
    {
        using (var document = new Document())
        {
            for (int number = 1; number <= 3; number++)
            {
                document.Pages.Add().Paragraphs.Add(new TextFragment($"Portable PDF page {number}"));
            }

            document.Save(_workspace.File("pages.pdf"));
        }

        CliResult window = _workspace.Run("pdf", "query", "pages", "pages.pdf", "--max-chars", "24", "--output", "json");
        CliResult single = _workspace.Run("pdf", "query", "pages", "pages.pdf", "--pages", "2", "--max-chars", "5", "--output", "json");

        Assert.True(window.ExitCode == 0, window.StdErr);
        JsonNode result = JsonNode.Parse(window.StdOut)!;
        JsonArray pages = result["pages"]!.AsArray();
        Assert.True(pages[^1]!["truncated"]!.GetValue<bool>());
        int cut = pages[^1]!["page"]!.GetValue<int>();
        string resume = cut == 3 ? "3" : $"{cut}-3";
        int budget = pages.Count == 1 ? 48 : 24;
        string next = result["window"]!["next"]!.GetValue<string>();
        Assert.StartsWith("aspose-cli pdf query pages ", next, StringComparison.Ordinal);
        Assert.EndsWith($" --pages {resume} --mode plain --max-chars {budget} --output json", next, StringComparison.Ordinal);
        Assert.True(single.ExitCode == 0, single.StdErr);
        Assert.EndsWith(
            " --pages 2 --mode plain --max-chars 10 --output json",
            JsonNode.Parse(single.StdOut)!["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void QuerySearch_WindowNextReturnsTheFollowingHits()
    {
        using (var document = new Document())
        {
            for (int number = 1; number <= 3; number++)
            {
                document.Pages.Add().Paragraphs.Add(new TextFragment($"Marker {number}a and Marker {number}b"));
            }

            document.Save(_workspace.File("markers.pdf"));
        }

        CliResult first = _workspace.Run(
            "pdf", "query", "search", "markers.pdf", "--pages", "1-3", "--pattern", "marker", "--max-hits", "4", "--output", "json");

        Assert.True(first.ExitCode == 0, first.StdErr);
        JsonNode window = JsonNode.Parse(first.StdOut)!["window"]!;
        Assert.Equal("hit", window["unit"]!.GetValue<string>());
        Assert.Equal(4, window["returned"]!.GetValue<int>());
        Assert.True(window["truncated"]!.GetValue<bool>());
        string next = window["next"]!.GetValue<string>();
        Assert.StartsWith("aspose-cli pdf query search ", next, StringComparison.Ordinal);
        Assert.EndsWith(
            "markers.pdf\" --pages 1-3 --pattern marker --max-hits 4 --skip 4 --output json", next, StringComparison.Ordinal);

        CliResult second = _workspace.RunCommandLine(next);

        Assert.True(second.ExitCode == 0, second.StdErr);
        JsonNode rest = JsonNode.Parse(second.StdOut)!;
        JsonArray hits = rest["hits"]!.AsArray();
        Assert.Equal([3, 3], hits.Select(static hit => hit!["page"]!.GetValue<int>()));
        Assert.Equal([1, 2], hits.Select(static hit => hit!["occurrence"]!.GetValue<int>()));
        Assert.False(rest["window"]!["truncated"]!.GetValue<bool>());
        Assert.Null(rest["window"]!["next"]);
    }

    public void Dispose() => _workspace.Dispose();
}

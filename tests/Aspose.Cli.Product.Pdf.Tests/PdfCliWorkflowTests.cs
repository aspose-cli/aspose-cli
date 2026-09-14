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
            product["operations"]!.AsArray()
                .Select(static operation => operation!["id"]!.GetValue<string>()));

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

    public void Dispose() => _workspace.Dispose();
}

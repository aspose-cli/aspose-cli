using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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

        CliResult info = _workspace.Run(
            "pdf", "inspect", "source.pdf", "--output-format", "json");
        CliResult read = _workspace.Run(
            "pdf", "read", "source.pdf", "--pages", "1", "--output-format", "json");
        CliResult forms = _workspace.Run(
            "pdf", "inspect", "source.pdf", "--detail", "forms", "--output-format", "json");
        CliResult convert = _workspace.Run(
            "pdf", "convert", "source.pdf", "--to", "txt",
            "--out", "source.txt", "--output-format", "json");

        Assert.Equal(1, info.Json()["pdf"]!["pageCount"]!.GetValue<int>());
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
            "--out", "edited.pdf", "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);

        CliResult reopened = _workspace.Run(
            "pdf", "inspect", "edited.pdf", "--detail", "forms", "--output-format", "json");
        Assert.True(reopened.ExitCode == 0, reopened.StdErr);
        Assert.Contains("Contoso", reopened.StdOut, StringComparison.Ordinal);

        CliResult exported = _workspace.Run(
            "pdf", "extract", "edited.pdf", "--what", "forms", "--form-format", "json",
            "--out-dir", "form-data", "--output-format", "json");
        Assert.True(exported.ExitCode == 0, exported.StdErr);
        Assert.True(File.Exists(_workspace.File(Path.Combine("form-data", "form.json"))));
        JsonNode data = Assert.Single(exported.Json()["outputs"]!.AsArray())!;
        Assert.Equal("json", data["format"]!.GetValue<string>());
        Assert.NotNull(data["fingerprint"]);
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
            """{"ops":[{"op":"set_properties","title":"Changed"},{"op":"linearize"}]}""";
        List<string> arguments =
        [
            "pdf", "edit", "source.pdf", "--ops", operations,
            "--in-place", "--backup", "--output-format", "json",
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

    [Theory]
    [InlineData("not an image")]
    [InlineData("")]
    public void Create_RefusesAFileThatIsNotAnImageAsACorruptInputNamingIt(string content)
    {
        File.WriteAllText(_workspace.File("scan.png"), content);

        CliResult created = _workspace.Run(
            "pdf", "create", "scan.pdf", "--from-images", "scan.png", "--output-format", "json");

        Assert.Equal(3, created.ExitCode);
        JsonNode error = JsonNode.Parse(created.StdErr)!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.Equal(_workspace.File("scan.png"), error["details"]!["path"]!.GetValue<string>(), ignoreCase: true);
        Assert.False(File.Exists(_workspace.File("scan.pdf")));
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("html")]
    [InlineData("pdfa-2b")]
    public void EvaluationMode_ReportsTheEnginesPageLimitForAPageSelectionOfALongerDocument(string format)
    {
        using (var fixture = new PdfEngineFixture())
        {
            File.Copy(fixture.CreateRawDocument("six.pdf", pages: 6), _workspace.File("six.pdf"));
        }

        string output = $"six-{format}.{(format.StartsWith("pdfa", StringComparison.Ordinal) ? "pdf" : format)}";
        CliResult convert = _workspace.Run(
            "pdf", "convert", "six.pdf", "--to", format, "--pages", "1-2", "--out", output, "--output-format", "json");

        // Removing the unselected pages reads past the fourth page, which evaluation mode refuses;
        // the limit is reported as the engine raised it, not worked around with a page copy.
        Assert.Equal(7, convert.ExitCode);
        JsonNode error = JsonNode.Parse(convert.StdErr)!["error"]!;
        Assert.Equal("EVALUATION_LIMIT", error["code"]!.GetValue<string>());
        Assert.Equal(4, error["details"]!["limit"]!.GetValue<int>());
        // A selection would read the other pages all the same, so the hint offers none.
        Assert.DoesNotContain("--pages", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File(output)));
    }

    [Fact]
    public void EvaluationMode_DisclosesAPartialInspectAndRefusesReadingPastTheFourthPage()
    {
        using (var fixture = new PdfEngineFixture())
        {
            File.Copy(fixture.CreateRawDocument("six.pdf", pages: 6), _workspace.File("six.pdf"));
        }

        CliResult inspect = _workspace.Run("pdf", "inspect", "six.pdf", "--output-format", "json");
        CliResult firstPages = _workspace.Run("pdf", "read", "six.pdf", "--pages", "1-4", "--output-format", "json");
        CliResult allPages = _workspace.Run("pdf", "read", "six.pdf", "--output-format", "json");

        JsonNode truncated = Assert.Single(
            inspect.Json()["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "EVALUATION_INPUT_TRUNCATED")!;
        Assert.StartsWith("Aspose.PDF evaluation mode reads only the first 4 pages of a document", truncated["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(firstPages.ExitCode == 0, firstPages.StdErr);
        Assert.Equal(7, allPages.ExitCode);
        Assert.Equal("EVALUATION_LIMIT", JsonNode.Parse(allPages.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void EvaluationMode_RefusesAMergeOfMoreThanFourPagesWithTheEnginesPageLimit()
    {
        using (var fixture = new PdfEngineFixture())
        {
            File.Copy(fixture.CreateRawDocument("east.pdf", pages: 3), _workspace.File("east.pdf"));
            File.Copy(fixture.CreateRawDocument("south.pdf", pages: 3), _workspace.File("south.pdf"));
        }

        CliResult merge = _workspace.Run(
            "pdf", "merge", "east.pdf", "south.pdf", "--out", "merged.pdf", "--output-format", "json");

        Assert.Equal(7, merge.ExitCode);
        JsonNode error = JsonNode.Parse(merge.StdErr)!["error"]!;
        Assert.Equal("EVALUATION_LIMIT", error["code"]!.GetValue<string>());
        Assert.Contains("reads only the first 4 pages", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("pages", error["details"]!["unit"]!.GetValue<string>());
        Assert.DoesNotContain("--pages", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("merged.pdf")));
    }

    [Fact]
    public void EvaluationMode_ListsEveryBookmarkOfALongerDocument()
    {
        // Six pages and six bookmarks, one per page, written without the engine.
        const int count = 6;
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R /Outlines 3 0 R >>",
            $"<< /Type /Pages /Count {count} /Kids [{string.Join(' ', Enumerable.Range(0, count).Select(static index => $"{4 + index} 0 R"))}] >>",
            $"<< /Type /Outlines /First {4 + count} 0 R /Last {3 + (2 * count)} 0 R /Count {count} >>",
        };
        int content = 4 + (2 * count);
        for (int index = 0; index < count; index++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents {content} 0 R >>");
        }

        for (int index = 0; index < count; index++)
        {
            int self = 4 + count + index;
            string previous = index > 0 ? $" /Prev {self - 1} 0 R" : string.Empty;
            string next = index < count - 1 ? $" /Next {self + 1} 0 R" : string.Empty;
            objects.Add($"<< /Title (Chapter {index + 1}) /Parent 3 0 R{previous}{next} /Dest [{4 + index} 0 R /Fit] >>");
        }

        objects.Add("<< /Length 0 >>\nstream\n\nendstream");
        PdfEngineFixture.WriteRawPdf(_workspace.File("chapters.pdf"), objects);

        CliResult inspect = _workspace.Run("pdf", "inspect", "chapters.pdf", "--detail", "outline", "--output-format", "json");

        JsonNode result = inspect.Json();
        Assert.Equal(
            Enumerable.Range(1, count).Select(static number => $"Chapter {number}"),
            result["outline"]!.AsArray().Select(static item => item!["title"]!.GetValue<string>()));
        Assert.Single(
            result["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "EVALUATION_INPUT_TRUNCATED");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EvaluationMode_CountsEveryFieldAndListsEveryAttachmentOfALongerDocument(bool fieldsOnLaterPages)
    {
        // Six pages, six text fields and six attachments, written without the engine. The
        // fields lie one per page, or on the first four pages only.
        const int count = 6;
        int Page(int index) => 3 + index;
        int FieldPage(int index) => fieldsOnLaterPages ? index : Math.Min(index, 3);
        int Widget(int index) => 4 + count + index;
        int Specification(int index) => 4 + (2 * count) + index;
        int Embedded(int index) => 4 + (3 * count) + index;
        string References(IEnumerable<int> indexes, Func<int, int> number) =>
            string.Join(' ', indexes.Select(index => $"{number(index)} 0 R"));
        IEnumerable<int> all = Enumerable.Range(0, count);
        string names = string.Join(' ', all.Select(index => $"(file{index + 1}.txt) {Specification(index)} 0 R"));
        var objects = new List<string>
        {
            $"<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [{References(all, Widget)}] >> /Names << /EmbeddedFiles << /Names [{names}] >> >> >>",
            $"<< /Type /Pages /Count {count} /Kids [{References(all, Page)}] >>",
        };
        int content = 3 + count;
        for (int page = 0; page < count; page++)
        {
            string widgets = References(all.Where(index => FieldPage(index) == page), Widget);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents {content} 0 R /Annots [{widgets}] >>");
        }

        objects.Add("<< /Length 0 >>\nstream\n\nendstream");
        for (int index = 0; index < count; index++)
        {
            objects.Add($"<< /Type /Annot /Subtype /Widget /FT /Tx /T (Field{index + 1}) /V (Value {index + 1}) /Rect [72 {100 * (index + 1)} 300 {(100 * (index + 1)) + 20}] /P {Page(FieldPage(index))} 0 R >>");
        }

        for (int index = 0; index < count; index++)
        {
            objects.Add($"<< /Type /Filespec /F (file{index + 1}.txt) /UF (file{index + 1}.txt) /EF << /F {Embedded(index)} 0 R >> >>");
        }

        for (int index = 0; index < count; index++)
        {
            objects.Add($"<< /Type /EmbeddedFile /Length 6 >>\nstream\nfile {index + 1}\nendstream");
        }

        PdfEngineFixture.WriteRawPdf(_workspace.File("fields.pdf"), objects);

        CliResult inspect = _workspace.Run(
            "pdf", "inspect", "fields.pdf", "--detail", "forms", "--detail", "attachments", "--output-format", "json");
        JsonNode result = inspect.Json();
        Assert.Equal(count, result["forms"]!["fieldCount"]!.GetValue<int>());
        Assert.Equal(
            Enumerable.Range(1, count).Select(static number => $"file{number}.txt"),
            result["attachments"]!.AsArray().Select(static item => item!["name"]!.GetValue<string>()));
        Assert.Contains(result["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "EVALUATION_INPUT_TRUNCATED");
        JsonNode read = result;
        JsonArray fields = read["forms"]!["fields"]!.AsArray();
        Assert.Equal(
            all.Select(static index => $"Value {index + 1}"),
            fields.Select(static item => item!["value"]!.GetValue<string>()));
        // A field on a page past the fourth keeps its value but has no page.
        Assert.Equal(
            all.Select(index => FieldPage(index) < 4 ? FieldPage(index) + 1 : (int?)null),
            fields.Select(static item => item!["page"]?.GetValue<int>()));
        JsonNode[] truncated = [.. (read["warnings"]?.AsArray() ?? []).Where(
            static warning => warning!["code"]!.GetValue<string>() == "EVALUATION_INPUT_TRUNCATED")!];
        // The inspection of the longer document warns once, whether or not a field lies past the fourth page.
        Assert.Contains("reads only the first 4 pages", Assert.Single(truncated)["message"]!.GetValue<string>(), StringComparison.Ordinal);
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

        CliResult window = _workspace.Run("pdf", "read", "pages.pdf", "--max-chars", "24", "--output-format", "json");
        CliResult single = _workspace.Run("pdf", "read", "pages.pdf", "--pages", "2", "--max-chars", "5", "--output-format", "json");

        JsonNode result = window.Json();
        JsonArray pages = result["pages"]!.AsArray();
        Assert.True(pages[^1]!["truncated"]!.GetValue<bool>());
        int cut = pages[^1]!["page"]!.GetValue<int>();
        string resume = cut == 3 ? "3" : $"{cut}-3";
        int budget = pages.Count == 1 ? 48 : 24;
        string next = result["window"]!["next"]!.GetValue<string>();
        Assert.StartsWith("aspose-cli pdf read ", next, StringComparison.Ordinal);
        Assert.EndsWith($" --pages {resume} --content text --max-chars {budget} --output-format json", next, StringComparison.Ordinal);
        Assert.EndsWith(
            " --pages 2 --content text --max-chars 10 --output-format json",
            single.Json()["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Search_WindowNextReturnsTheFollowingHits()
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
            "pdf", "search", "markers.pdf", "--pages", "1-3", "--pattern", "marker", "--max-hits", "4", "--output-format", "json");

        JsonNode window = first.Json()["window"]!;
        Assert.Equal("hit", window["unit"]!.GetValue<string>());
        Assert.Equal(4, window["returned"]!.GetValue<int>());
        Assert.True(window["truncated"]!.GetValue<bool>());
        string next = window["next"]!.GetValue<string>();
        Assert.StartsWith("aspose-cli pdf search ", next, StringComparison.Ordinal);
        Assert.EndsWith(
            "markers.pdf\" --pages 1-3 --pattern marker --max-hits 4 --skip 4 --output-format json", next, StringComparison.Ordinal);

        CliResult second = _workspace.RunCommandLine(next);

        JsonNode rest = second.Json();
        JsonArray hits = rest["hits"]!.AsArray();
        Assert.Equal([3, 3], hits.Select(static hit => hit!["page"]!.GetValue<int>()));
        Assert.Equal([1, 2], hits.Select(static hit => hit!["occurrence"]!.GetValue<int>()));
        Assert.False(rest["window"]!["truncated"]!.GetValue<bool>());
        Assert.Null(rest["window"]!["next"]);
    }

    [Fact]
    public void Merge_OfAWordDocumentNamedPdf_NamesThatInputAndWhatItIs()
    {
        File.WriteAllText(_workspace.File("doc.md"), "# Title\n\nFirst paragraph.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "doc.docx", "--from-markdown", "doc.md", "--output-format", "json").ExitCode);
        File.Move(_workspace.File("doc.docx"), _workspace.File("renamed.pdf"));
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("Report"));
            document.Save(_workspace.File("report.pdf"));
        }

        CliResult merged = _workspace.Run("pdf", "merge", "report.pdf", "renamed.pdf", "--out", "merged.pdf", "--output-format", "json");

        Assert.Equal(6, merged.ExitCode);
        JsonNode error = JsonNode.Parse(merged.StdErr)!["error"]!;
        Assert.Equal("FORMAT_MISMATCH", error["code"]!.GetValue<string>());
        Assert.Equal(_workspace.File("renamed.pdf"), error["details"]!["path"]!.GetValue<string>());
        Assert.Equal("words", Assert.Single(error["details"]!["detected"]!.AsArray())!.GetValue<string>());
    }

    [Fact]
    public void CreateFromHtml_DisclosesTheFieldsTheImporterNamedItself()
    {
        File.WriteAllText(_workspace.File("form.html"), """
            <html><body><form>
            <input type="text" name="company"/>
            <input type="radio" name="kind" value="maker"/> Maker
            <input type="radio" name="kind" value="seller"/> Seller
            <input type="checkbox" name="iso9001" value="yes"/> ISO 9001
            </form></body></html>
            """);
        File.WriteAllText(_workspace.File("text.html"), """<html><body><form><input type="text" name="company"/></form></body></html>""");
        File.WriteAllText(_workspace.File("contact.html"), """
            <html><body><form>
            <input type="text" name="company"/>
            <input type="email" name="mail"/> <input type="EMAIL" name="copy"/> <input name="phone" type='tel'/>
            <input data-type="url" type="text" name="site"/>
            <!-- <input type="file" name="upload"/> -->
            </form></body></html>
            """);

        // The three documents are independent, so they are created at once.
        CliResult form = null!, fields = null!, text = null!, contact = null!;
        Parallel.Invoke(
            () =>
            {
                form = _workspace.Run("pdf", "create", "form.pdf", "--from-html", "form.html", "--output-format", "json");
                fields = _workspace.Run("pdf", "inspect", "form.pdf", "--detail", "forms", "--output-format", "json");
            },
            () => text = _workspace.Run("pdf", "create", "text.pdf", "--from-html", "text.html", "--output-format", "json"),
            () => contact = _workspace.Run("pdf", "create", "contact.pdf", "--from-html", "contact.html", "--output-format", "json"));

        JsonNode lossy = Assert.Single(
            form.Json()["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION")!;
        JsonArray read = fields.Json()["forms"]!["fields"]!.AsArray();
        string[] generated = [.. read.Select(static field => field!["name"]!.GetValue<string>()).Where(static name => name != "company").Distinct()];
        Assert.NotEmpty(generated);
        Assert.All(generated, name => Assert.Contains($"'{name}'", lossy["message"]!.GetValue<string>(), StringComparison.Ordinal));
        Assert.Contains("rect", lossy["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(text.ExitCode == 0, text.StdErr);
        Assert.DoesNotContain("LOSSY_CONVERSION", text.StdOut, StringComparison.Ordinal);
        // The importer drops some input types without a field; the HTML shows which.
        string dropped = Assert.Single(
            contact.Json()["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION")!["message"]!.GetValue<string>();
        Assert.Contains("dropped 3 input(s) of type email, tel", dropped, StringComparison.Ordinal);
        Assert.DoesNotContain("generated names", dropped, StringComparison.Ordinal);
    }

    [Fact]
    public void QuerySearch_GivesTheTextAroundEachHit()
    {
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Clause 7. The supplier shall pay a penalty of 0.5% for each day of delay."));
            page.Paragraphs.Add(new TextFragment("Clause 9. No penalty applies to force majeure."));
            document.Save(_workspace.File("contract.pdf"));
        }

        CliResult search = _workspace.Run(
            "pdf", "search", "contract.pdf", "--pattern", "penalty", "--output-format", "json");

        JsonArray hits = search.Json()["hits"]!.AsArray();
        Assert.Equal(["penalty", "penalty"], hits.Select(static hit => hit!["text"]!.GetValue<string>()));
        string[] contexts = [.. hits.Select(static hit => hit!["context"]!.GetValue<string>())];
        Assert.Contains("shall pay a penalty of 0.5%", contexts[0], StringComparison.Ordinal);
        Assert.Contains("No penalty applies", contexts[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_TakesAFormatOnlyForFormDataAndWritesEverythingIntoOutDir()
    {
        using (var document = new Document())
        {
            document.Pages.Add();
            document.Save(_workspace.File("source.pdf"));
        }

        CliResult format = _workspace.Run(
            "pdf", "extract", "source.pdf", "--what", "images", "--form-format", "json", "--out-dir", "files", "--output-format", "json");
        CliResult file = _workspace.Run(
            "pdf", "extract", "source.pdf", "--what", "forms", "--form-format", "json", "--out", "forms.json", "--output-format", "json");
        CliResult unnamed = _workspace.Run(
            "pdf", "extract", "source.pdf", "--what", "forms", "--out-dir", "files", "--output-format", "json");

        Assert.Equal(2, format.ExitCode);
        JsonNode error = JsonNode.Parse(format.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.StartsWith("Invalid use of --form-format:", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(2, file.ExitCode);
        Assert.Contains("--out", file.StdErr, StringComparison.Ordinal);
        Assert.Equal(2, unnamed.ExitCode);
        Assert.StartsWith("Invalid use of --form-format:", JsonNode.Parse(unnamed.StdErr)!["error"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(_workspace.File("files")));
    }

    [Theory]
    [InlineData("text")]
    [InlineData("forms")]
    public void Extract_RefusesAByteOrderMarkForOutputOtherThanTables(string what)
    {
        using (var document = new Document())
        {
            document.Pages.Add();
            document.Save(_workspace.File("source.pdf"));
        }

        CliResult result = _workspace.Run(
            "pdf", "extract", "source.pdf", "--what", what, "--bom", "--out-dir", "files", "--output-format", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.StartsWith("Invalid use of --bom:", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The option names how a source is read, whatever its extension: --from-markdown lays out
    /// Markdown under either Markdown extension, with a heading for each # line, and
    /// --from-text lays out the same file as plain text, # and all. Each input is reported
    /// with the declared id of the format it was read as.
    /// </summary>
    [Theory]
    [InlineData("--from-markdown", "notes.md", "md")]
    [InlineData("--from-markdown", "notes.markdown", "md")]
    [InlineData("--from-text", "notes.md", "txt")]
    [InlineData("--from-text", "notes.text", "txt")]
    public void Create_ReadsTheSourceAsItsOptionNamesWhateverItsExtension(string option, string source, string format)
    {
        File.WriteAllText(_workspace.File(source), "# Quarterly results\n\nRevenue grew in every region.\n");

        CliResult created = _workspace.Run(
            "pdf", "create", "notes.pdf", option, source, "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        JsonNode input = Assert.Single(JsonNode.Parse(created.StdOut)!["inputs"]!.AsArray())!;
        Assert.Equal(format, input["format"]!.GetValue<string>());
        Assert.Equal(_workspace.File(source), input["path"]!.GetValue<string>(), ignoreCase: true);
        using var document = new Document(_workspace.File("notes.pdf"));
        var absorber = new TextFragmentAbsorber();
        document.Pages.Accept(absorber);
        TextFragment[] fragments = [.. absorber.TextFragments];
        string text = string.Concat(fragments.Select(static fragment => fragment.Text));
        TextFragment heading = fragments.First(static fragment => fragment.Text.Contains("Quarterly", StringComparison.Ordinal));
        TextFragment body = fragments.First(static fragment => fragment.Text.Contains("Revenue", StringComparison.Ordinal));
        if (format == "md")
        {
            Assert.DoesNotContain("#", text, StringComparison.Ordinal);
            Assert.True(heading.TextState.FontSize > body.TextState.FontSize, $"{heading.TextState.FontSize} <= {body.TextState.FontSize}");
        }
        else
        {
            Assert.Contains("# Quarterly results", text, StringComparison.Ordinal);
            Assert.Equal(body.TextState.FontSize, heading.TextState.FontSize);
        }
    }

    /// <summary>
    /// A PDF is created from exactly one source, and --allow-network-resources applies to the
    /// HTML importer alone, so Markdown with it is refused before anything is read.
    /// </summary>
    [Fact]
    public void Create_RefusesTwoSourcesAndNetworkResourcesForMarkdown()
    {
        File.WriteAllText(_workspace.File("notes.md"), "# Notes\n");
        File.WriteAllText(_workspace.File("notes.txt"), "Notes\n");

        CliResult twoSources = _workspace.Run(
            "pdf", "create", "notes.pdf", "--from-markdown", "notes.md", "--from-text", "notes.txt", "--output-format", "json");
        CliResult network = _workspace.Run(
            "pdf", "create", "notes.pdf", "--from-markdown", "notes.md", "--allow-network-resources", "--output-format", "json");

        Assert.True(twoSources.ExitCode == 2, twoSources.StdErr);
        JsonNode usage = JsonNode.Parse(twoSources.StdErr)!["error"]!;
        Assert.Equal("USAGE_ERROR", usage["code"]!.GetValue<string>());
        Assert.Contains("--from-markdown", usage["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(network.ExitCode == 2, network.StdErr);
        JsonNode invalid = JsonNode.Parse(network.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", invalid["code"]!.GetValue<string>());
        Assert.Equal("--allow-network-resources", invalid["details"]!["option"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("notes.pdf")));
    }

    [Theory]
    [InlineData("a5", 419.53, 595.28)]
    [InlineData("tabloid", 792.0, 1224.0)]
    public void Create_TakesEveryPageSizePresetTheOperationsTake(string preset, double width, double height)
    {
        File.WriteAllText(_workspace.File("notes.md"), "# Notes\n");

        CliResult created = _workspace.Run(
            "pdf", "create", "notes.pdf", "--from-markdown", "notes.md", "--page-size", preset, "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        using var document = new Document(_workspace.File("notes.pdf"));
        Assert.Equal(width, document.Pages[1].Rect.Width, 0.5);
        Assert.Equal(height, document.Pages[1].Rect.Height, 0.5);
    }

    /// <summary>
    /// pdf convert does not encrypt: an --encrypt option, which it does not have, is refused with
    /// the edit that encrypts a copy, and its help does not list one.
    /// </summary>
    [Theory]
    [InlineData("--encrypt", "secret")]
    [InlineData("--encrypt-env", "PDF_PASSWORD")]
    [InlineData("--encrypt-stdin")]
    public void Convert_RefusesEncryptionWithTheEditThatEncryptsACopy(params string[] encryption)
    {
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("Encrypt me"));
            document.Save(_workspace.File("plain.pdf"));
        }

        CliResult refused = _workspace.Run(
            ["pdf", "convert", "plain.pdf", "--to", "docx", .. encryption, "--output-format", "json"]);
        CliResult help = _workspace.Run("pdf", "convert", "--help");

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Equal(encryption[0], error["details"]!["option"]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        Assert.Contains("'pdf convert' does not encrypt", hint, StringComparison.Ordinal);
        Assert.Contains("'pdf edit --out --ops'", hint, StringComparison.Ordinal);
        Assert.Contains("encrypt_document", hint, StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("plain.docx")));
        Assert.DoesNotContain("--encrypt", help.StdOut, StringComparison.Ordinal);
    }

    /// <summary>
    /// Page images come from pdf render alone: pdf convert refuses an image target, whether
    /// --to or the --out extension names it, the extension with a hint that runs pdf render,
    /// and capabilities does not list it as a convert format.
    /// </summary>
    [Theory]
    [InlineData("png", "page.png")]
    [InlineData("jpeg", "page.jpg")]
    [InlineData("svg", "page.svg")]
    public void Convert_RefusesAPageImageTargetWithTheRenderThatWritesIt(string format, string output)
    {
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("Render me"));
            document.Save(_workspace.File("plain.pdf"));
        }

        CliResult named = _workspace.Run(
            "pdf", "convert", "plain.pdf", "--to", format, "--out", output, "--output-format", "json");
        CliResult byExtension = _workspace.Run(
            "pdf", "convert", "plain.pdf", "--out", output, "--output-format", "json");
        CliResult capabilities = _workspace.Run("capabilities", "--output-format", "json");

        foreach (CliResult refused in new[] { named, byExtension })
        {
            Assert.True(refused.ExitCode == 2, refused.StdErr);
            Assert.Equal("OPTION_INVALID", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
        }

        Assert.Contains("pdf render", JsonNode.Parse(byExtension.StdErr)!["error"]!["hint"]!.GetValue<string>(), StringComparison.Ordinal);

        Assert.False(File.Exists(_workspace.File(output)));
        JsonNode pdf = JsonNode.Parse(capabilities.StdOut)!["products"]!.AsArray()
            .Single(product => product!["id"]!.GetValue<string>() == "pdf")!;
        string[] convertFormats = [.. pdf["convertFormats"]!.AsArray().Select(static id => id!.GetValue<string>())];
        Assert.DoesNotContain(format, convertFormats);
        Assert.Contains("tiff", convertFormats);
        Assert.Contains(format, pdf["renderFormats"]!.AsArray().Select(static id => id!.GetValue<string>()));
    }

    /// <summary>A multi-page TIFF is one export of the selected pages, which pdf convert keeps.</summary>
    [Fact]
    public void Convert_WritesTheSelectedPagesAsOneMultiPageTiff()
    {
        using (var fixture = new PdfEngineFixture())
        {
            File.Copy(fixture.CreateRawDocument("three.pdf", pages: 3), _workspace.File("three.pdf"));
        }

        CliResult convert = _workspace.Run(
            "pdf", "convert", "three.pdf", "--to", "tiff", "--out", "three.tiff", "--output-format", "json");

        Assert.True(convert.ExitCode == 0, convert.StdErr);
        JsonNode output = Assert.Single(JsonNode.Parse(convert.StdOut)!["outputs"]!.AsArray())!;
        Assert.Equal("tiff", output["format"]!.GetValue<string>());
        Assert.Equal(3, TiffPageCount(File.ReadAllBytes(_workspace.File("three.tiff"))));
    }

    // The number of image file directories in a TIFF, one per page.
    private static int TiffPageCount(byte[] tiff)
    {
        bool little = tiff[0] == (byte)'I';
        uint Read32(long at) => little
            ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(tiff.AsSpan((int)at))
            : System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(tiff.AsSpan((int)at));
        int Read16(long at) => little
            ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan((int)at))
            : System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(tiff.AsSpan((int)at));
        int count = 0;
        for (long offset = Read32(4); offset != 0; offset = Read32(offset + 2 + (12L * Read16(offset))))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// PDF-2: the headings of a PDF created from Markdown are not bookmarks, so a split by
    /// bookmarks is refused with a hint that names add_bookmark in an operation that runs as
    /// given, after which the split succeeds.
    /// </summary>
    [Fact]
    public void MarkdownHeadings_MakeNoBookmarks_SoSplitByBookmarksNeedsAddBookmark()
    {
        File.WriteAllText(_workspace.File("notes.md"), "# One\n\nFirst part.\n\n# Two\n\nSecond part.\n");
        Assert.Equal(0, _workspace.Run("pdf", "create", "notes.pdf", "--from-markdown", "notes.md").ExitCode);

        JsonNode inspected = _workspace.Run("pdf", "inspect", "notes.pdf", "--detail", "outline", "--output-format", "json").Json();
        CliResult refused = _workspace.Run("pdf", "split", "notes.pdf", "--by", "bookmarks", "--out-dir", "parts", "--output-format", "json");

        Assert.Empty(inspected["outline"]?.AsArray() ?? []);
        Assert.Equal(2, refused.ExitCode);
        string hint = JsonNode.Parse(refused.StdErr)!["error"]!["hint"]!.GetValue<string>();
        Assert.Contains("pdf edit", hint, StringComparison.Ordinal);
        Match operation = Regex.Match(hint, "\\{\"op\":\"add_bookmark\"[^}]*\\}");
        Assert.True(operation.Success, hint);
        CliResult edited = _workspace.Run("pdf", "edit", "notes.pdf", "--ops", $"{{\"ops\":[{operation.Value}]}}", "--out", "marked.pdf");
        Assert.True(edited.ExitCode == 0, edited.StdErr);
        CliResult split = _workspace.Run("pdf", "split", "marked.pdf", "--by", "bookmarks", "--out-dir", "parts", "--output-format", "json");
        Assert.True(split.ExitCode == 0, split.StdErr);
    }

    public void Dispose() => _workspace.Dispose();
}

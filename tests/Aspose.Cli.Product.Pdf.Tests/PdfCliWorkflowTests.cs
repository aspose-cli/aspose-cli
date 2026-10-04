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
        Assert.Equal(1, JsonNode.Parse(info.StdOut)!["pdf"]!["pageCount"]!.GetValue<int>());
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

    [Theory]
    [InlineData("docx")]
    [InlineData("html")]
    [InlineData("pdfa-2b")]
    public void EvaluationMode_ConvertsTheFirstPagesOfALongerDocument(string format)
    {
        using (var fixture = new PdfEngineFixture())
        {
            File.Copy(fixture.CreateRawDocument("six.pdf", pages: 6), _workspace.File("six.pdf"));
        }

        string output = $"six-{format}.{(format.StartsWith("pdfa", StringComparison.Ordinal) ? "pdf" : format)}";
        CliResult convert = _workspace.Run(
            "pdf", "convert", "six.pdf", "--to", format, "--pages", "1-2", "--out", output, "--output", "json");

        Assert.True(convert.ExitCode == 0, convert.StdErr);
        Assert.True(new FileInfo(_workspace.File(output)).Length > 0);
        JsonNode copied = Assert.Single(
            JsonNode.Parse(convert.StdOut)!["warnings"]!.AsArray(),
            static warning => warning!["message"]!.GetValue<string>().Contains("were copied into a new one", StringComparison.Ordinal))!;
        Assert.Equal("LOSSY_CONVERSION", copied["code"]!.GetValue<string>());
        Assert.Contains("bookmarks, attachments and document properties", copied["message"]!.GetValue<string>(), StringComparison.Ordinal);
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
    public void EvaluationMode_RefusesAMergeOfMoreThanFourPagesWithAMergeHint()
    {
        using (var fixture = new PdfEngineFixture())
        {
            File.Copy(fixture.CreateRawDocument("east.pdf", pages: 3), _workspace.File("east.pdf"));
            File.Copy(fixture.CreateRawDocument("south.pdf", pages: 3), _workspace.File("south.pdf"));
        }

        CliResult merge = _workspace.Run(
            "pdf", "merge", "east.pdf", "south.pdf", "--out", "merged.pdf", "--output", "json");

        Assert.Equal(7, merge.ExitCode);
        JsonNode error = JsonNode.Parse(merge.StdErr)!["error"]!;
        Assert.Equal("EVALUATION_LIMIT", error["code"]!.GetValue<string>());
        Assert.Contains("together", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("merge inputs that have at most 4 pages together", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
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
        WriteRawPdf("chapters.pdf", objects);

        CliResult inspect = _workspace.Run("pdf", "inspect", "chapters.pdf", "--detail", "outline", "--output", "json");

        Assert.True(inspect.ExitCode == 0, inspect.StdErr);
        JsonNode result = JsonNode.Parse(inspect.StdOut)!;
        Assert.Equal(
            Enumerable.Range(1, count).Select(static number => $"Chapter {number}"),
            result["outline"]!.AsArray().Select(static item => item!["title"]!.GetValue<string>()));
        JsonNode truncated = Assert.Single(
            result["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "EVAL_INPUT_TRUNCATED")!;
        Assert.Contains("bookmarks, attachments and the form field count are complete", truncated["message"]!.GetValue<string>(), StringComparison.Ordinal);
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

        WriteRawPdf("fields.pdf", objects);

        CliResult inspect = _workspace.Run(
            "pdf", "inspect", "fields.pdf", "--detail", "forms", "--detail", "attachments", "--output", "json");
        CliResult forms = _workspace.Run("pdf", "query", "forms", "fields.pdf", "--output", "json");

        Assert.True(inspect.ExitCode == 0, inspect.StdErr);
        JsonNode result = JsonNode.Parse(inspect.StdOut)!;
        Assert.Equal(count, result["forms"]!["fieldCount"]!.GetValue<int>());
        Assert.Equal(
            Enumerable.Range(1, count).Select(static number => $"file{number}.txt"),
            result["attachments"]!.AsArray().Select(static item => item!["name"]!.GetValue<string>()));
        Assert.Contains(result["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "EVAL_INPUT_TRUNCATED");
        Assert.True(forms.ExitCode == 0, forms.StdErr);
        JsonNode read = JsonNode.Parse(forms.StdOut)!;
        JsonArray fields = read["fields"]!.AsArray();
        Assert.Equal(
            all.Select(static index => $"Value {index + 1}"),
            fields.Select(static item => item!["value"]!.GetValue<string>()));
        // A field on a page past the fourth keeps its value but has no page.
        Assert.Equal(
            all.Select(index => FieldPage(index) < 4 ? FieldPage(index) + 1 : (int?)null),
            fields.Select(static item => item!["page"]?.GetValue<int>()));
        JsonNode[] truncated = [.. (read["warnings"]?.AsArray() ?? []).Where(
            static warning => warning!["code"]!.GetValue<string>() == "EVAL_INPUT_TRUNCATED")!];
        if (fieldsOnLaterPages)
        {
            Assert.Contains("of 6 pages, so these fields have no page: Field5, Field6.", Assert.Single(truncated)["message"]!.GetValue<string>(), StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(truncated);
        }
    }

    /// <summary>Writes numbered objects, the first being the catalog, as a PDF with a cross-reference table.</summary>
    private void WriteRawPdf(string fileName, IReadOnlyList<string> objects)
    {
        var pdf = new System.Text.StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (int index = 0; index < objects.Count; index++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        int xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets)
        {
            pdf.Append($"{offset:0000000000} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllBytes(_workspace.File(fileName), System.Text.Encoding.ASCII.GetBytes(pdf.ToString()));
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

    [Fact]
    public void Merge_OfAWordDocumentNamedPdf_NamesThatInputAndWhatItIs()
    {
        File.WriteAllText(_workspace.File("doc.md"), "# Title\n\nFirst paragraph.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "doc.docx", "--markdown", "doc.md", "--output", "json").ExitCode);
        File.Move(_workspace.File("doc.docx"), _workspace.File("renamed.pdf"));
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("Report"));
            document.Save(_workspace.File("report.pdf"));
        }

        CliResult merged = _workspace.Run("pdf", "merge", "report.pdf", "renamed.pdf", "--out", "merged.pdf", "--output", "json");

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

        CliResult form = _workspace.Run("pdf", "create", "form.pdf", "--from-html", "form.html", "--output", "json");
        CliResult text = _workspace.Run("pdf", "create", "text.pdf", "--from-html", "text.html", "--output", "json");
        CliResult contact = _workspace.Run("pdf", "create", "contact.pdf", "--from-html", "contact.html", "--output", "json");
        CliResult fields = _workspace.Run("pdf", "query", "forms", "form.pdf", "--output", "json");

        Assert.True(form.ExitCode == 0, form.StdErr);
        JsonNode lossy = Assert.Single(
            JsonNode.Parse(form.StdOut)!["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION")!;
        JsonArray read = JsonNode.Parse(fields.StdOut)!["fields"]!.AsArray();
        string[] generated = [.. read.Select(static field => field!["name"]!.GetValue<string>()).Where(static name => name != "company").Distinct()];
        Assert.NotEmpty(generated);
        Assert.All(generated, name => Assert.Contains($"'{name}'", lossy["message"]!.GetValue<string>(), StringComparison.Ordinal));
        Assert.Contains("rect", lossy["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(text.ExitCode == 0, text.StdErr);
        Assert.DoesNotContain("LOSSY_CONVERSION", text.StdOut, StringComparison.Ordinal);
        // The importer drops some input types without a field; the HTML shows which.
        Assert.True(contact.ExitCode == 0, contact.StdErr);
        string dropped = Assert.Single(
            JsonNode.Parse(contact.StdOut)!["warnings"]!.AsArray(),
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
            "pdf", "query", "search", "contract.pdf", "--pattern", "penalty", "--output", "json");

        Assert.True(search.ExitCode == 0, search.StdErr);
        JsonArray hits = JsonNode.Parse(search.StdOut)!["hits"]!.AsArray();
        Assert.Equal(["penalty", "penalty"], hits.Select(static hit => hit!["snippet"]!.GetValue<string>()));
        string[] contexts = [.. hits.Select(static hit => hit!["context"]!.GetValue<string>())];
        Assert.Contains("shall pay a penalty of 0.5%", contexts[0], StringComparison.Ordinal);
        Assert.Contains("No penalty applies", contexts[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("attachments", "--out", "files")]
    [InlineData("images", "--to", "json")]
    public void Extract_NamesTheFormOutputOptionGivenAndPointsToOutDir(string what, string option, string value)
    {
        using (var document = new Document())
        {
            document.Pages.Add();
            document.Save(_workspace.File("source.pdf"));
        }

        CliResult result = _workspace.Run(
            "pdf", "extract", "source.pdf", "--what", what, option, value, "--output", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.StartsWith($"Invalid use of {option}:", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains($"--out-dir", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(option == "--out" ? "--to" : "--out ", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
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
            "pdf", "extract", "source.pdf", "--what", what, "--bom", "--output", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.StartsWith("Invalid use of --bom:", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    public void Dispose() => _workspace.Dispose();
}

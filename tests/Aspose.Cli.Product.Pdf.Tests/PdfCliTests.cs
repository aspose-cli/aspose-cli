using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// The PDF commands through the built CLI, with or without a license: what each command states to
/// the engine, what it writes and what its result says. Each command is a process of its own, and
/// the inputs are written as raw PDF, so no engine in this process touches them.
/// </summary>
public sealed class PdfCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void ConvertHtml_WritesAPdf_WithOrWithoutTo()
    {
        File.WriteAllText(_workspace.File("page.html"), "<html><body><h1>Hello</h1><p>World</p></body></html>");

        CliResult byExtension = _workspace.Run("pdf", "convert", "page.html", "--out", "page.pdf");
        CliResult byTo = _workspace.Run("pdf", "convert", "page.html", "--to", "pdf", "--out", "other.pdf");
        CliResult read = _workspace.Run("pdf", "read", "page.pdf", "--at", "page/1/text");

        Assert.True(byExtension.ExitCode == 0, byExtension.StdErr);
        Assert.True(byTo.ExitCode == 0, byTo.StdErr);
        Assert.Equal("html", JsonNode.Parse(byExtension.StdOut)!["input"]!["format"]!.GetValue<string>());
        Assert.Contains("World", read.StdOut, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("convert")]
    [InlineData("read")]
    public void Markdown_IsAFormatNoPdfCommandReads(string verb)
    {
        File.WriteAllText(_workspace.File("notes.md"), "# Notes\n");

        CliResult refused = verb == "convert"
            ? _workspace.Run("pdf", "convert", "notes.md", "--out", "notes.pdf")
            : _workspace.Run("pdf", "read", "notes.md");

        Assert.Equal("FORMAT_UNSUPPORTED", Error(refused)["code"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("notes.pdf")));
    }

    [Theory]
    [InlineData("read")]
    [InlineData("edit")]
    public void Html_IsReadOnlyByConvert(string verb)
    {
        File.WriteAllText(_workspace.File("page.html"), "<html><body>x</body></html>");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"do":"flatten_form"}]}""");

        CliResult refused = verb == "edit"
            ? _workspace.Run("pdf", "edit", "page.html", "--ops", "ops.json", "--out", "out.pdf")
            : _workspace.Run("pdf", "read", "page.html");

        Assert.Equal("FORMAT_UNSUPPORTED", Error(refused)["code"]!.GetValue<string>());
    }

    [Fact]
    public void ConvertToPdfA_TakesTheEnginesErrorAction_AndReturnsItsLog()
    {
        WritePdf("doc.pdf", pages: 1);

        CliResult refused = _workspace.Run("pdf", "convert", "doc.pdf", "--to", "pdfa-2b", "--out", "a.pdf");
        CliResult converted = _workspace.Run("pdf", "convert", "doc.pdf", "--to", "pdfa-2b", "--convert-error-action", "Delete", "--out", "a.pdf");

        Assert.Equal("OPTION_INVALID", Error(refused)["code"]!.GetValue<string>());
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        JsonNode result = JsonNode.Parse(converted.StdOut)!;
        Assert.NotNull(result["converted"]);
        Assert.NotNull(result["conversionLog"]);
    }

    [Fact]
    public void ConvertPages_AppliesOnlyWhereTheEnginesSaveSelectsPages()
    {
        WritePdf("doc.pdf", pages: 3);

        CliResult refused = _workspace.Run("pdf", "convert", "doc.pdf", "--pages", "2", "--out", "doc.docx");
        CliResult html = _workspace.Run("pdf", "convert", "doc.pdf", "--pages", "2", "--out", "doc.html");

        Assert.Equal("OPTION_INVALID", Error(refused)["code"]!.GetValue<string>());
        Assert.True(html.ExitCode == 0, html.StdErr);
        string text = File.ReadAllText(_workspace.File("doc.html"));
        Assert.Contains("page", text, StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(_workspace.Path, "doc.*", SearchOption.TopDirectoryOnly), static file => file.EndsWith(".html", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_OfAPageTheDocumentDoesNotHave_IsPageNotFound()
    {
        WritePdf("doc.pdf", pages: 2);

        CliResult refused = _workspace.Run("pdf", "render", "doc.pdf", "--pages", "9", "--out", "page.png");

        Assert.Equal("PAGE_NOT_FOUND", Error(refused)["code"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("page.png")));
    }

    [Fact]
    public void MergeAndSplit_CallTheEngine_AndWriteWhatTheyName()
    {
        WritePdf("a.pdf", pages: 2);
        WritePdf("b.pdf", pages: 1);

        CliResult merged = _workspace.Run("pdf", "merge", "a.pdf", "b.pdf", "--out", "ab.pdf");
        CliResult split = _workspace.Run("pdf", "split", "ab.pdf", "--by", "pages", "--groups", "1-2", "--groups", "3", "--out-dir", "parts");

        Assert.True(merged.ExitCode == 0, merged.StdErr);
        Assert.True(JsonNode.Parse(merged.StdOut)!["concatenated"]!.GetValue<bool>());
        Assert.True(split.ExitCode == 0, split.StdErr);
        Assert.Equal(["1-2", "3"], JsonNode.Parse(split.StdOut)!["outputs"]!.AsArray().Select(static part => part!["pages"]!.GetValue<string>()));
    }

    [Fact]
    public void Sign_ReadsTheNewSignatureBackFromTheSavedFile()
    {
        WritePdf("doc.pdf", pages: 1);
        using (var fixture = new PdfEngineFixture())
        {
            File.Copy(fixture.CreateCertificate("pfx-pass"), _workspace.File("signer.pfx"));
        }

        CliResult signed = _workspace.RunWithEnv(new Dictionary<string, string?> { ["PFX_PASSWORD"] = "pfx-pass" },
            "pdf", "sign", "doc.pdf", "--page", "1", "--certificate", "signer.pfx", "--certificate-password-env", "PFX_PASSWORD",
            "--visible", "--rect", "36,36,180,60", "--reason", "Approved", "--out", "signed.pdf");
        CliResult invisibleWithRect = _workspace.RunWithEnv(new Dictionary<string, string?> { ["PFX_PASSWORD"] = "pfx-pass" },
            "pdf", "sign", "doc.pdf", "--page", "1", "--certificate", "signer.pfx", "--certificate-password-env", "PFX_PASSWORD",
            "--rect", "36,36,180,60", "--out", "other.pdf");

        Assert.True(signed.ExitCode == 0, signed.StdErr);
        JsonNode signature = Assert.Single(JsonNode.Parse(signed.StdOut)!["signatures"]!.AsArray())!;
        Assert.True(signature["verify"]!.GetValue<bool>());
        Assert.Equal("OPTION_INVALID", Error(invisibleWithRect)["code"]!.GetValue<string>());
    }

    [Fact]
    public void Validate_ReturnsTheEnginesVerdictAndLog()
    {
        WritePdf("doc.pdf", pages: 1);

        CliResult validated = _workspace.Run("pdf", "validate", "doc.pdf", "--profile", "pdfa-1b");

        Assert.True(validated.ExitCode == 0, validated.StdErr);
        JsonNode result = JsonNode.Parse(validated.StdOut)!;
        Assert.False(result["valid"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(result["log"]!.GetValue<string>()));
    }

    [Fact]
    public void Create_WritesTheEnginesNewDocument_OrACopyOfATemplate()
    {
        WritePdf("template.pdf", pages: 2);

        CliResult blank = _workspace.Run("pdf", "create", "blank.pdf");
        CliResult copy = _workspace.Run("pdf", "create", "copy.pdf", "--template", "template.pdf");
        CliResult blankRead = _workspace.Run("pdf", "read", "blank.pdf");
        CliResult copyRead = _workspace.Run("pdf", "read", "copy.pdf");

        Assert.True(blank.ExitCode == 0, blank.StdErr);
        Assert.True(copy.ExitCode == 0, copy.StdErr);
        Assert.Equal(0, JsonNode.Parse(blankRead.StdOut)!["inventory"]!["read"]!["pages"]!.GetValue<int>());
        Assert.Equal(2, JsonNode.Parse(copyRead.StdOut)!["inventory"]!["read"]!["pages"]!.GetValue<int>());
    }

    [Fact]
    public void EditInPlace_LeavesOnlyTheDocument_AndReadsTheChangeBack()
    {
        WritePdf("doc.pdf", pages: 2);
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"set":"document","props":{"info.title":"Report"}},{"delete":"page/2"}]}""");

        CliResult edited = _workspace.Run("pdf", "edit", "doc.pdf", "--in-place", "--ops", "ops.json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        JsonNode result = JsonNode.Parse(edited.StdOut)!;
        Assert.Equal("Report", result["ops"]![0]!["now"]!["info.title"]!.GetValue<string>());
        Assert.False(result["ops"]![1]!["exists"]!.GetValue<bool>());
        Assert.Equal(["doc.pdf", "ops.json"], Directory.EnumerateFileSystemEntries(_workspace.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EncryptedPdf_IsReadWithItsPassword()
    {
        using (var fixture = new PdfEngineFixture())
        {
            File.Copy(fixture.CreateEncryptedDocument("open-pass", "owner-pass", "secret.pdf"), _workspace.File("secret.pdf"));
        }

        CliResult refused = _workspace.Run("pdf", "read", "secret.pdf");
        CliResult read = _workspace.RunWithEnv(new Dictionary<string, string?> { ["PDF_PASSWORD"] = "open-pass" }, "pdf", "read", "secret.pdf", "--password-env", "PDF_PASSWORD");

        Assert.Equal("PASSWORD_REQUIRED", Error(refused)["code"]!.GetValue<string>());
        Assert.True(read.ExitCode == 0, read.StdErr);
        Assert.True(JsonNode.Parse(read.StdOut)!["input"]!["encrypted"]!.GetValue<bool>());
    }

    /// <summary>
    /// In evaluation mode the engine's page enumerator gives the first four pages while
    /// Pages.Count gives every one (KNOWN-ISSUES PDF-EVALUATION-COLLECTIONS): read lists what the
    /// enumerator gives, counts with Count, and reports an address past the fourth as the engine's error.
    /// </summary>
    [Fact]
    public void EvaluationMode_ListsWhatTheEngineGives_AndCountsWhatItCounts()
    {
        WritePdf("six.pdf", pages: 6);

        CliResult read = _workspace.Run("pdf", "read", "six.pdf", "--kind", "page", "--license-mode", "evaluation");
        CliResult past = _workspace.Run("pdf", "read", "six.pdf", "--at", "page/5", "--license-mode", "evaluation");

        Assert.True(read.ExitCode == 0, read.StdErr);
        JsonNode result = JsonNode.Parse(read.StdOut)!;
        Assert.Equal("evaluation", result["license"]!["mode"]!.GetValue<string>());
        Assert.Equal(6, result["inventory"]!["read"]!["pages"]!.GetValue<int>());
        Assert.Equal(["page/1", "page/2", "page/3", "page/4"], result["objects"]!.AsArray().Select(static item => item!["at"]!.GetValue<string>()));
        Assert.Equal("ENGINE_ERROR", Error(past)["code"]!.GetValue<string>());
    }

    /// <summary>
    /// A Markdown output is published with the picture files the engine's save writes beside it,
    /// and the HTML the engine exports, which holds its fonts inline as data: addresses, converts
    /// back to a PDF.
    /// </summary>
    [Fact]
    public void ConvertOutputs_AreWhatTheEnginesSaveWrites()
    {
        TestLicense.Require("Evaluation mode adds its own content to what the engine saves.");
        File.WriteAllBytes(_workspace.File("dot.png"), Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAIAAAADnC86AAAAMElEQVR4nO3NMQ0AAAgDsCmZfynIwgQJT5P+zbQvIhaLxWKxWCwWi8VisVgsFvcsXhHvWVsvWSOBAAAAAElFTkSuQmCC"));
        File.WriteAllText(_workspace.File("pic.html"), """<html><body><p>Text before.</p><img src="dot.png"><p>Text after.</p></body></html>""");
        Assert.Equal(0, _workspace.Run("pdf", "convert", "pic.html", "--out", "pic.pdf").ExitCode);

        CliResult markdown = _workspace.Run("pdf", "convert", "pic.pdf", "--to", "md", "--out", "pic.md", "--output-format", "json");
        CliResult html = _workspace.Run("pdf", "convert", "pic.pdf", "--to", "html", "--out", "pic.out.html");
        CliResult back = _workspace.Run("pdf", "convert", "pic.out.html", "--out", "back.pdf");

        Assert.True(markdown.ExitCode == 0, markdown.StdErr);
        JsonArray outputs = JsonNode.Parse(markdown.StdOut)!["outputs"]!.AsArray();
        Assert.Equal("md", outputs[0]!["format"]!.GetValue<string>());
        Assert.True(outputs.Count > 1, "The picture the engine writes beside the Markdown is published with it.");
        Assert.All(outputs, static output => Assert.True(File.Exists(output!["path"]!.GetValue<string>())));
        Assert.True(html.ExitCode == 0, html.StdErr);
        Assert.True(back.ExitCode == 0, back.StdErr);
    }

    /// <summary>
    /// An HTML import the engine fails (KNOWN-ISSUES PDF-HTML-EMPTY-FONT) is the engine's error, and
    /// its details list each reference the loader supplied nothing for, with why, and nothing else.
    /// </summary>
    [Fact]
    public void ConvertHtml_ThatTheEngineFails_ListsTheReferencesTheLoaderSuppliedNothingFor()
    {
        TestLicense.Require("The engine's import is judged as it runs with a license.");
        File.WriteAllText(_workspace.File("fonts.html"), """
            <html><head><style>@font-face { font-family: F; src: url(http://127.0.0.1:9/f.woff2); } p { font-family: F; }</style></head>
            <body><p>Text</p><img src="missing.png"></body></html>
            """);

        CliResult result = _workspace.Run("pdf", "convert", "fonts.html", "--out", "fonts.pdf", "--output-format", "json");

        JsonNode error = Error(result);
        Assert.Equal("FEATURE_UNSUPPORTED", error["code"]!.GetValue<string>());
        Assert.Equal(
            [("http://127.0.0.1:9/f.woff2", "a network address"), ("missing.png", "not found")],
            error["details"]!["notSupplied"]!.AsArray()
                .Select(static item => (item!["reference"]!.GetValue<string>(), item["reason"]!.GetValue<string>()))
                .Order());
        Assert.False(File.Exists(_workspace.File("fonts.pdf")));
    }

    /// <summary>
    /// A hierarchical form lists each terminal field once by its full name, which addresses it: its
    /// value reads and sets, setting one leaves the other, and each widget's fullName names a listed
    /// field. The nodes above them, which the engine gives as the untyped Field, are not listed.
    /// </summary>
    [Fact]
    public void HierarchicalForm_ListsEachTerminalFieldByItsFullName()
    {
        TestLicense.Require("Evaluation mode shows the first four elements of a collection.");
        PdfEngineFixture.WriteRawPdf(_workspace.File("hier.pdf"),
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [6 0 R 7 0 R] >>",
            "<< /T (form) /Kids [5 0 R] >>",
            "<< /T (person) /Parent 4 0 R /Kids [6 0 R 7 0 R] >>",
            "<< /T (name) /Parent 5 0 R /FT /Tx /V (Bob) /Type /Annot /Subtype /Widget /Rect [50 700 250 720] /P 3 0 R >>",
            "<< /T (age) /Parent 5 0 R /FT /Tx /V (30) /Type /Annot /Subtype /Widget /Rect [50 660 250 680] /P 3 0 R >>",
        ]);
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"set":"field/form.person.name","props":{"value":"Carol"}}]}""");

        JsonNode fields = Json(_workspace.Run("pdf", "read", "hier.pdf", "--kind", "field", "--output-format", "json"));
        JsonNode age = Json(_workspace.Run("pdf", "read", "hier.pdf", "--at", "field/form.person.age", "--output-format", "json"));
        JsonNode widgets = Json(_workspace.Run("pdf", "read", "hier.pdf", "--kind", "annotation", "--output-format", "json"));
        JsonNode edited = Json(_workspace.Run("pdf", "edit", "hier.pdf", "--ops", "ops.json", "--out", "out.pdf", "--output-format", "json"));
        JsonNode after = Json(_workspace.Run("pdf", "read", "out.pdf", "--kind", "field", "--output-format", "json"));

        Assert.Equal([("field/form.person.name", "Bob"), ("field/form.person.age", "30")], Values(fields));
        Assert.Equal(2, fields["inventory"]!["read"]!["fields"]!.GetValue<int>());
        Assert.Equal("30", age["objects"]![0]!["props"]!["value"]!.GetValue<string>());
        Assert.Equal(["form.person.name", "form.person.age"],
            widgets["objects"]!.AsArray().Select(static widget => widget!["props"]!["fullName"]!.GetValue<string>()));
        Assert.Equal("Carol", edited["ops"]![0]!["now"]!["value"]!.GetValue<string>());
        Assert.Equal([("field/form.person.name", "Carol"), ("field/form.person.age", "30")], Values(after));

        static (string, string)[] Values(JsonNode read) =>
            [.. read["objects"]!.AsArray().Select(static field => (field!["at"]!.GetValue<string>(), field["props"]!["value"]!.GetValue<string>()))];
    }

    /// <summary>The file add attachment embeds is read under the input budget: past it, the edit is FILE_TOO_LARGE and writes nothing.</summary>
    [Fact]
    public void AddAttachment_ReadsItsFileUnderTheInputBudget()
    {
        WritePdf("doc.pdf", pages: 1);
        File.WriteAllBytes(_workspace.File("big.bin"), new byte[300_000]);
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"add":"attachment","args":{"file":"big.bin","name":"big.bin"}}]}""");

        CliResult result = _workspace.Run("pdf", "edit", "doc.pdf", "--ops", "ops.json", "--out", "out.pdf", "--max-input-bytes", "200000", "--output-format", "json");

        Assert.Equal("FILE_TOO_LARGE", Error(result)["code"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("out.pdf")));
    }

    private static JsonNode Json(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }

    /// <summary>A raw PDF of letter pages, each with the text "Portable PDF page n".</summary>
    private void WritePdf(string name, int pages)
    {
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", string.Empty, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
        var kids = new List<int>();
        for (int page = 1; page <= pages; page++)
        {
            kids.Add(objects.Count + 1);
            string content = $"BT /F1 12 Tf 72 720 Td (Portable PDF page {page}) Tj ET";
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {objects.Count + 2} 0 R >>");
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }

        objects[1] = $"<< /Type /Pages /Count {pages} /Kids [{string.Join(" ", kids.Select(static kid => $"{kid} 0 R"))}] >>";
        PdfEngineFixture.WriteRawPdf(_workspace.File(name), objects);
    }

    private static JsonNode Error(CliResult result) => JsonNode.Parse(result.StdErr)!["error"]!;

    public void Dispose() => _workspace.Dispose();
}

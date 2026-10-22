using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using SkiaSharp;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// What a redaction reaches: its cover spans the whole match, also over a scan's image, and lists
/// the text beside the match it takes with it; <c>redact_text</c> clears matching form values and
/// annotations, and an output where its text is still found anywhere the document keeps text is
/// not published.
/// </summary>
public sealed class PdfRedactionReachTests
{
    private const string Secret = "SECRET";

    /// <summary>
    /// A searchable scan: a full-width image under an invisible text layer whose two lines are set
    /// closer than their characters are high. The cover over one line spans its whole match, so
    /// none of the image under the match shows, and the characters of the neighbouring line it
    /// overlaps are counted.
    /// </summary>
    [Fact]
    public void RedactText_OnAScanTextLayer_ClearsTheWholeMatchAndCountsTheNeighbouringLine()
    {
        using var fixture = new PdfEngineFixture();
        string input = Scan(fixture);
        PdfRect match = Assert.Single(PdfSearch.Run(fixture.Session, Find(input, "SECRET VALUE")).Hits).Rect;

        AssertCleared(fixture, match, new RedactTextOp { Find = "SECRET VALUE" });
    }

    /// <summary>An area over the same line, taken from the search hit, clears all of it too and counts the same characters.</summary>
    [Fact]
    public void RedactArea_OnAScanTextLayer_ClearsTheWholeAreaAndCountsTheNeighbouringLine()
    {
        using var fixture = new PdfEngineFixture();
        string input = Scan(fixture);
        PdfRect match = Assert.Single(PdfSearch.Run(fixture.Session, Find(input, "SECRET VALUE")).Hits).Rect;

        AssertCleared(fixture, match, new RedactAreaOp
        {
            Page = 1,
            Rect = new PdfRectInput { X = match.X, Y = match.Y, Width = match.Width, Height = match.Height },
        });
    }

    /// <summary>
    /// Redacts the scan with <paramref name="operation"/> and checks that no pixel of the image
    /// shows inside <paramref name="area"/> and that the warning counts the neighbouring characters.
    /// </summary>
    private static void AssertCleared(PdfEngineFixture fixture, PdfRect area, PdfOp operation)
    {
        string output = fixture.File("scan.redacted.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = fixture.File("scan.pdf"),
            Batch = new PdfOpsBatch { Ops = [operation] },
            Output = TestOutput.At(output),
        });

        // At 72 dpi a pixel is a point; the image is pure red.
        string png = fixture.File("scan.redacted.png");
        PdfRender.Run(fixture.Session, new PdfRenderRequest { Input = output, Output = TestOutput.At(png, format: "png"), Dpi = 72 });
        using SKBitmap bitmap = SKBitmap.Decode(png);
        int red = 0, pixels = 0;
        for (int y = (int)Math.Ceiling(area.Y) + 1; y < (int)Math.Floor(area.Y + area.Height) - 1; y++)
        {
            for (int x = (int)Math.Ceiling(area.X) + 1; x < (int)Math.Floor(area.X + area.Width) - 1; x++)
            {
                SKColor color = bitmap.GetPixel(x, y);
                pixels++;
                red += color.Red > 160 && color.Green < 96 ? 1 : 0;
            }
        }

        Assert.True(pixels > 0);
        Assert.True(red == 0, string.Create(CultureInfo.InvariantCulture, $"{red} of {pixels} pixels of the area still show the image."));
        // The neighbouring line lost all twelve of its characters, which the warning counts but never names.
        Assert.Empty(PdfSearch.Run(fixture.Session, Find(output, "NEIGHBOR")).Hits);
        Assert.Empty(PdfSearch.Run(fixture.Session, Find(output, "LINE")).Hits);
        Warning beside = Assert.Single(result.Warnings ?? [], static warning => warning.Code.Name == "REDACTION_NEARBY_TEXT_REMOVED");
        Assert.Contains("on page 1: 12 characters on 1 neighbouring line", beside.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("NEIGHBOR", beside.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, beside.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A form field shows its value on the page, and an annotation its text, though neither is
    /// page text: redact_text clears a matching value and removes a matching annotation.
    /// </summary>
    [Fact]
    public void RedactText_ClearsMatchingFieldValuesAndRemovesMatchingAnnotations()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("form.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment($"Patient {Secret}"));
            document.Form.Add(new TextBoxField(page, new Rectangle(100, 600, 300, 630)) { PartialName = "Name", Value = $"John {Secret} Smith" }, 1);
            document.Form.Add(new TextBoxField(page, new Rectangle(100, 550, 300, 580)) { PartialName = "City", Value = "Springfield" }, 1);
            page.Annotations.Add(new TextAnnotation(page, new Rectangle(400, 600, 420, 620)) { Contents = $"call {Secret} back", Title = "Reviewer" });
            page.Annotations.Add(new TextAnnotation(page, new Rectangle(400, 550, 420, 570)) { Contents = "keep this note", Title = "Reviewer" });
            document.Save(input);
        }

        string output = fixture.File("form.redacted.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = Secret }] },
            Output = TestOutput.At(output),
        });

        Assert.True(File.Exists(output));
        Assert.False(result.HasFailures);
        Assert.Contains("field/Name", result.Applied[0].Targets);
        PdfFormInfo form = fixture.Forms(output);
        Assert.True(string.IsNullOrEmpty(form.Fields.Single(static field => field.Name == "Name").Value));
        Assert.Equal("Springfield", form.Fields.Single(static field => field.Name == "City").Value);
        using var reopened = new Document(output);
        string[] notes = [.. reopened.Pages[1].Annotations.OfType<TextAnnotation>().Select(static note => note.Contents)];
        Assert.Equal(["keep this note"], notes);
    }

    /// <summary>
    /// redact_text without pages promises the text is gone from the document: where it is still
    /// found in a bookmark title, a document property or an attachment, nothing is published and
    /// the verification names each place without repeating the text.
    /// </summary>
    [Fact]
    public void RedactText_LeavingItsTextInBookmarksPropertiesOrAttachments_PublishesNothing()
    {
        using var fixture = new PdfEngineFixture();
        string input = Leaky(fixture);
        string output = fixture.File("leaky.redacted.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = Secret }] },
            Output = TestOutput.At(output),
        });

        Assert.Null(result.Output);
        Assert.False(File.Exists(output));
        Assert.True(result.HasFailures);
        PdfEditVerification verification = Assert.IsType<PdfEditVerification>(result.Verification);
        Assert.False(verification.Ok);
        Assert.All(verification.Issues, static issue =>
        {
            Assert.Equal("REDACTED_TEXT_FOUND", issue.Code);
            Assert.DoesNotContain(Secret, issue.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, issue.Location ?? string.Empty, StringComparison.Ordinal);
        });
        string?[] locations = [.. verification.Issues.Select(static issue => issue.Location)];
        Assert.Contains("bookmark/1", locations);
        Assert.Contains("property/title", locations);
        Assert.Contains("attachment/all", locations);
        Assert.Contains("attachment/notes.txt", locations);
        Assert.DoesNotContain("page/1", locations);
    }

    /// <summary>A redact_text limited to pages leaves the text of the document's bookmarks, properties and attachments to the caller.</summary>
    [Fact]
    public void RedactText_LimitedToPages_PublishesWithItsTextLeftOutsideThePages()
    {
        using var fixture = new PdfEngineFixture();
        string input = Leaky(fixture);
        string output = fixture.File("leaky.page.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = Secret, Pages = "1" }] },
            Output = TestOutput.At(output),
        });

        Assert.True(File.Exists(output));
        Assert.False(result.HasFailures);
        Assert.Empty(PdfSearch.Run(fixture.Session, Find(output, Secret)).Hits);
    }

    /// <summary>
    /// A document property is checked by where its value came from, not by its name: a
    /// non-standard lowercase key and a custom key the document holds are searched, while the
    /// producer, the creator and the dates the save writes into a document that held none are
    /// the engine's and are not. The input holds no entry the save may or may not rewrite, so
    /// the outcome does not depend on what the engine stamps.
    /// </summary>
    [Fact]
    public void RedactText_ChecksThePropertiesTheDocumentHeldWhateverTheirKey()
    {
        using var fixture = new PdfEngineFixture();
        string input = Described(fixture);
        string output = fixture.File("described.redacted.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = Secret + @"|\d", Regex = true }] },
            Output = TestOutput.At(output),
        });

        Assert.Null(result.Output);
        Assert.False(File.Exists(output));
        string?[] locations = [.. result.Verification!.Issues.Select(static issue => issue.Location)];
        Assert.Contains("property/keywords", locations);
        Assert.Contains("property/Reference", locations);
        Assert.DoesNotContain("property/producer", locations);
        Assert.DoesNotContain("property/creator", locations);
        Assert.DoesNotContain("property/modificationDate", locations);
        Assert.DoesNotContain("property/creationDate", locations);
    }

    /// <summary>
    /// A date the document's XMP holds is the document's text: a redaction of digits is refused
    /// with a hint naming clear_properties, and publishes once clear_properties comes before it.
    /// </summary>
    [Fact]
    public void RedactText_MatchingTheDocumentsXmp_NamesClearPropertiesAndPublishesWithIt()
    {
        using var fixture = new PdfEngineFixture();
        string input = Described(fixture);
        PdfEditResult Redact(string output, params PdfOp[] ops) => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [.. ops, new RedactTextOp { Find = @"\d", Regex = true }] },
            Output = TestOutput.At(fixture.File(output)),
        });

        PdfEditResult refused = Redact("digits.pdf");
        Assert.True(refused.HasFailures);
        Assert.Null(refused.Output);
        Assert.False(File.Exists(fixture.File("digits.pdf")));
        VerificationIssue modified = Assert.Single(refused.Verification!.Issues, static issue => issue.Location == "property/xmp:xmp:ModifyDate");
        Assert.Contains("clear_properties", modified.Hint, StringComparison.Ordinal);

        PdfEditResult cleared = Redact("cleared.pdf", new ClearPropertiesOp());
        Assert.False(cleared.HasFailures, string.Join("; ", cleared.Verification?.Issues.Select(static issue => issue.Location) ?? []));
        Assert.True(File.Exists(fixture.File("cleared.pdf")));
        Assert.Empty(PdfSearch.Run(fixture.Session, Find(fixture.File("cleared.pdf"), "Case 2026")).Hits);
    }

    /// <summary>
    /// A cover that takes characters of the next line never names them: here they are a phone
    /// number a later operation of the batch redacts, which the result must not repeat.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void RedactText_NeverNamesTheNeighbouringTextItTakes()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        const string phone = "13998765432";
        const string content = "BT /F1 20 Tf 1 0 0 1 20 612 Tm (" + phone + ") Tj 1 0 0 1 20 600 Tm (ZHANG) Tj ET";
        PdfEngineFixture.WriteRawPdf(workspace.File("contacts.pdf"),
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 800] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ]);

        CliResult run = workspace.Run("pdf", "edit", "contacts.pdf", "--out", "out.pdf", "--output-format", "json", "--ops",
            """[{"op":"redact_text","find":"ZHANG"},{"op":"redact_text","find":"\\d{4,}","regex":true}]""");

        JsonNode result = run.Json();
        Assert.Contains(result["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "REDACTION_NEARBY_TEXT_REMOVED");
        for (int start = 0; start + 4 <= phone.Length; start++)
        {
            Assert.DoesNotContain(phone.Substring(start, 4), run.StdOut + run.StdErr, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A pattern that takes too long on text the publish check searches leaves the output
    /// unchecked, so it is not published.
    /// </summary>
    [Fact]
    public void RedactText_WhosePatternTimesOutInTheCheck_PublishesNothing()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("backtracking.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("x"));
            document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = new string('a', 40) + "!", Action = new GoToAction(page) });
            document.Save(input);
        }

        string output = fixture.File("backtracking.redacted.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = "(a+)+$", Regex = true }] },
            Output = TestOutput.At(output),
        });

        Assert.Null(result.Output);
        Assert.False(File.Exists(output));
        Assert.True(result.HasFailures);
        Assert.Contains(result.Verification!.Issues, static issue => issue.Code == "VERIFICATION_INCOMPLETE");
    }

    /// <summary>A redaction whose text is left in the output leaves an input edited in place as it was.</summary>
    [Fact]
    public void RedactText_LeavingItsText_LeavesAnInPlaceInputUnchanged()
    {
        using var fixture = new PdfEngineFixture();
        string input = Leaky(fixture);
        byte[] before = File.ReadAllBytes(input);
        string[] files = Directory.GetFiles(Path.GetDirectoryName(input)!);

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = Secret }] },
            Output = TestOutput.At(input, overwrite: true),
        });

        Assert.Null(result.Output);
        Assert.Equal(before, File.ReadAllBytes(input));
        Assert.Equal(files, Directory.GetFiles(Path.GetDirectoryName(input)!));
    }

    /// <summary>A redaction whose text is left in the output writes no backup of an input edited in place.</summary>
    [Fact]
    public void RedactText_LeavingItsText_WritesNoBackup()
    {
        using var fixture = new PdfEngineFixture();
        string input = Leaky(fixture);
        string backup = fixture.File("leaky.backup.pdf");
        byte[] before = File.ReadAllBytes(input);
        string[] files = Directory.GetFiles(Path.GetDirectoryName(input)!);

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = Secret }] },
            Output = TestOutput.At(input, overwrite: true, backup: backup),
        });

        Assert.Null(result.Output);
        Assert.Null(result.Backup);
        Assert.False(File.Exists(backup));
        Assert.Equal(before, File.ReadAllBytes(input));
        Assert.Equal(files, Directory.GetFiles(Path.GetDirectoryName(input)!));
    }

    /// <summary>The command exits 8 and writes nothing, whether it runs in its own process or under a supervised worker.</summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RedactText_LeavingItsText_ExitsEightAndWritesNothing(bool supervised)
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        File.Copy(Leaky(fixture), workspace.File("leaky.pdf"));

        CliResult run = workspace.Run(
            ["pdf", "edit", "leaky.pdf", "--ops", $$"""[{"op":"redact_text","find":"{{Secret}}"}]""", "--out", "out.pdf", "--output-format", "json",
             .. (supervised ? new[] { "--timeout", TestDeadline.CliTimeout } : Array.Empty<string>())]);

        Assert.Equal(8, run.ExitCode);
        JsonNode result = JsonNode.Parse(run.StdOut)!;
        Assert.False(result["verification"]!["ok"]!.GetValue<bool>());
        Assert.Null(result["output"]);
        Assert.False(File.Exists(workspace.File("out.pdf")));
        Assert.DoesNotContain(Secret, run.StdOut.Replace($"\"find\":\"{Secret}\"", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    /// <summary>One page whose text, bookmark, title, attachment name and attachment description all hold the secret.</summary>
    private static string Leaky(PdfEngineFixture fixture)
    {
        string path = fixture.File("leaky.pdf");
        using var document = new Document();
        Page page = document.Pages.Add();
        page.Paragraphs.Add(new TextFragment($"Patient {Secret}"));
        document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = $"{Secret} chapter", Action = new GoToAction(page) });
        document.Info.Title = $"{Secret} report";
        document.EmbeddedFiles.Add(new FileSpecification(new MemoryStream(Encoding.ASCII.GetBytes("one")), $"{Secret}.txt") { Description = "plain" });
        document.EmbeddedFiles.Add(new FileSpecification(new MemoryStream(Encoding.ASCII.GetBytes("two")), "notes.txt") { Description = $"about {Secret}" });
        document.Save(path);
        return path;
    }

    /// <summary>
    /// One page whose text holds the secret and a year, with a lowercase non-standard /keywords
    /// entry holding the secret, a producer and a modification date of another writer, and XMP
    /// with its own modification date and producer.
    /// </summary>
    private static string Described(PdfEngineFixture fixture)
    {
        const string xmp = "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
            + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" "
            + "xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\">"
            + "<xmp:ModifyDate>2020-01-02T03:04:05Z</xmp:ModifyDate><pdf:Producer>Writer 1.0</pdf:Producer>"
            + "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";
        const string content = "BT /F1 12 Tf 72 720 Td (Case 2026 SECRET) Tj ET";
        string path = fixture.File("described.pdf");
        PdfEngineFixture.WriteRawPdf(path,
        [
            "<< /Type /Catalog /Pages 2 0 R /Metadata 6 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Type /Metadata /Subtype /XML /Length {xmp.Length} >>\nstream\n{xmp}\nendstream",
            $"<< /keywords ({Secret}) /Reference (Case 2026) >>",
        ], info: 7);
        return path;
    }

    /// <summary>
    /// A 400 by 800 point page whose top half is a red image under an invisible (Tr 3) text layer
    /// of two 20-point lines set 12 points apart.
    /// </summary>
    private static string Scan(PdfEngineFixture fixture)
    {
        const int side = 400;
        var image = new StringBuilder(side * side * 6 + 1);
        for (int pixel = 0; pixel < side * side; pixel++)
        {
            image.Append("FF0000");
        }

        image.Append('>');
        const string content = "q 400 0 0 400 0 400 cm /Im1 Do Q\nBT /F1 20 Tf 3 Tr 1 0 0 1 20 612 Tm (NEIGHBOR LINE) Tj 1 0 0 1 20 600 Tm (SECRET VALUE) Tj ET";
        string path = fixture.File("scan.pdf");
        PdfEngineFixture.WriteRawPdf(path,
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 800] /Resources << /Font << /F1 5 0 R >> /XObject << /Im1 6 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Type /XObject /Subtype /Image /Width {side} /Height {side} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode /Length {image.Length} >>\nstream\n{image}\nendstream",
        ]);
        return path;
    }

    private static PdfSearchRequest Find(string input, string pattern) => new()
    {
        Input = input,
        Query = new Aspose.Cli.Sdk.Text.SearchQuery(Aspose.Cli.Sdk.Text.TextSearch.Create(pattern, regex: false, caseSensitive: false), 100, Scope: null),
    };
}

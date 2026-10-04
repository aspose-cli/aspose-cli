using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Pdf;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Page geometry reported by inspect and judged by the review gate.</summary>
public sealed class PdfReviewAndInfoTests
{
    [Theory]
    [InlineData(Rotation.None, 0)]
    [InlineData(Rotation.on90, 90)]
    [InlineData(Rotation.on180, 180)]
    [InlineData(Rotation.on270, 270)]
    public void InspectPreview_ReportsRotationInDegrees(Rotation rotation, int degrees)
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument("rotated.pdf", pages: 1);
        using (var document = new Document(path))
        {
            document.Pages[1].Rotate = rotation;
            document.Save(path);
        }

        PdfPageInfo page = Assert.Single(fixture.Engine.GetInfo(path, new PdfInfoRequest { IncludePreview = true }).Pages!);

        Assert.Equal(degrees, page.Rotation);
    }

    [Fact]
    public void Review_FlagsAnUnusualPageSizeOnTheInspectedPage()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.CreateDocument("tiny.pdf", pages: 2);
        using (var document = new Document(input))
        {
            document.Pages[2].SetPageSize(60, 40);
            document.Save(input);
        }

        CliResult review = workspace.Run(["review", input, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        Assert.False(JsonNode.Parse(review.StdOut)!["sourceEncrypted"]!.GetValue<bool>());
        JsonNode finding = Assert.Single(
            JsonNode.Parse(review.StdOut)!["findings"]!.AsArray(),
            static item => item!["code"]!.GetValue<string>() == "PDF_PAGE_SIZE_UNUSUAL")!;
        Assert.Equal("page 2", finding["location"]!.GetValue<string>());
        Assert.Contains("60 x 40 pt", finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
        // The evidence is the image of that page alone.
        Assert.EndsWith(
            "page-0002.png",
            Assert.Single(finding["evidence"]!.AsArray())!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Review_SaysTheSourceIsEncrypted()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.CreateEncryptedDocument("reader", "owner", "locked.pdf");

        CliResult review = workspace.RunWithEnv(
            new Dictionary<string, string?> { ["ASPOSE_CLI_TEST_PASSWORD"] = "reader" },
            "review", input, "--out", workspace.File("review"), "--password-env", "ASPOSE_CLI_TEST_PASSWORD", "--output", "json");

        Assert.True(review.ExitCode == 0, review.StdErr);
        Assert.True(JsonNode.Parse(review.StdOut)!["sourceEncrypted"]!.GetValue<bool>());
    }

    [Fact]
    public void Review_CountsALargeImageAsPageContent()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        // Pages 1 and 2 carry one short line of text, pages 1 and 3 an image over a third of the page.
        string input = fixture.CreateRawDocument("pictures.pdf", pages: 3,
            textPages: new HashSet<int> { 1, 2 }, imagePages: new HashSet<int> { 1, 3 }, imagePoints: 400);

        CliResult review = workspace.Run(["review", input, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        Assert.Equal(
            [("PDF_PAGE_UTILIZATION_LOW", "page 2")],
            JsonNode.Parse(review.StdOut)!["findings"]!.AsArray()
                .Select(static item => (item!["code"]!.GetValue<string>(), item["location"]!.GetValue<string>()))
                .Where(static item => item.Item1.StartsWith("PDF_PAGE_", StringComparison.Ordinal)));
    }

    [Fact]
    public void Review_NamesAScannedPageThatSearchAndRedactionCannotReach()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        // Page 3 is one image over the whole page with no text, as a scan is.
        string input = fixture.CreateRawDocument("scanned.pdf", pages: 3,
            textPages: new HashSet<int> { 1, 2 }, imagePages: new HashSet<int> { 3 }, imagePoints: 700,
            textContent: "BT /F1 12 Tf 72 720 Td (A line of readable text on a text page) Tj ET");

        CliResult review = workspace.Run(["review", input, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        JsonNode finding = Assert.Single(
            result["findings"]!.AsArray(),
            static item => item!["code"]!.GetValue<string>().StartsWith("PDF_PAGE_", StringComparison.Ordinal))!;
        Assert.Equal("PDF_PAGE_WITHOUT_TEXT_LAYER", finding["code"]!.GetValue<string>());
        Assert.Equal("info", finding["severity"]!.GetValue<string>());
        Assert.Equal("page 3", finding["location"]!.GetValue<string>());
        Assert.Contains("redact_area", finding["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(1, result["coverage"]!["metrics"]!.AsArray()
            .Single(static metric => metric!["name"]!.GetValue<string>() == "scannedPages")!["value"]!.GetValue<int>());
    }

    /// <summary>
    /// A redaction moves the text after the value it removes left, under its cover
    /// (PDF-REDACT-TEXT-SHIFT): the text is still in the file, but the page no longer shows it.
    /// </summary>
    [Fact]
    public void Review_FlagsTextThatARedactionCoverHides()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.CreateRawDocument("runs.pdf", pages: 2, textContent: PdfMutateTests.ConsecutiveRuns);
        string output = fixture.File("runs.redacted.pdf");
        fixture.Engine.ApplyOps(
            input,
            new PdfOpsBatch
            {
                Ops =
                [
                    new RedactTextOp { Pattern = "Jane Roe", Pages = "1" },
                    // Nothing follows "order." on its line, so no text moves under its cover.
                    new RedactTextOp { Pattern = "order.", Pages = "2" },
                ],
            },
            new PdfEditRequest { OutputPath = output });

        CliResult review = workspace.Run(["review", output, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode finding = Assert.Single(
            JsonNode.Parse(review.StdOut)!["findings"]!.AsArray(),
            static item => item!["code"]!.GetValue<string>() == "PDF_TEXT_COVERED")!;
        Assert.Equal("page 1", finding["location"]!.GetValue<string>());
        Assert.Contains("source document", finding["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Text drawn on a filled box after the box, such as a heading on a shaded band that follows
    /// other text, is visible; only the text drawn before a box lies under it.
    /// </summary>
    [Fact]
    public void Review_CountsOnlyTheTextDrawnBeforeABoxAsCovered()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.CreateRawDocument("band.pdf", pages: 1,
            textContent: "BT /F1 12 Tf 72 720 Td (Text above the band) Tj ET BT /F1 12 Tf 72 680 Td (Under the band) Tj ET");
        string banded = fixture.File("banded.pdf");
        using (var document = new Document(input))
        {
            Page page = document.Pages[1];
            XForm band = XForm.CreateNewForm(page, document);
            band.BBox = new Rectangle(0, 0, 400, 30);
            band.Contents.Clear(); // The new form starts with a copy of the page's contents.
            band.Contents.Add(new Aspose.Pdf.Operators.SetRGBColor(0.8, 0.8, 0.8));
            band.Contents.Add(new Aspose.Pdf.Operators.Re(0, 0, 400, 30));
            band.Contents.Add(new Aspose.Pdf.Operators.Fill());
            page.Resources.Forms.Add(band);
            page.Contents.Add(new Aspose.Pdf.Operators.GSave());
            page.Contents.Add(new Aspose.Pdf.Operators.ConcatenateMatrix(1, 0, 0, 1, 60, 670));
            page.Contents.Add(new Aspose.Pdf.Operators.Do(band.Name));
            page.Contents.Add(new Aspose.Pdf.Operators.GRestore());
            page.Contents.Add(new Aspose.Pdf.Operators.BT());
            page.Contents.Add(new Aspose.Pdf.Operators.SelectFont("F1", 12));
            page.Contents.Add(new Aspose.Pdf.Operators.MoveTextPosition(250, 680));
            page.Contents.Add(new Aspose.Pdf.Operators.ShowText("Heading on the band"));
            page.Contents.Add(new Aspose.Pdf.Operators.ET());
            document.Save(banded);
        }

        CliResult review = workspace.Run(["review", banded, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode finding = Assert.Single(
            JsonNode.Parse(review.StdOut)!["findings"]!.AsArray(),
            static item => item!["code"]!.GetValue<string>() == "PDF_TEXT_COVERED")!;
        Assert.StartsWith("1 text fragment(s)", finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The sentence an unlicensed save stamps on every page, as a later licensed run reads it,
    /// on one line or wrapped onto several, as Aspose.Words wraps its footer in a wide font.
    /// </summary>
    [Theory]
    [InlineData("Evaluation Only. Created with Aspose.PDF. Copyright 2002-2026 Aspose Pty Ltd.")]
    [InlineData("Evaluation Only. Created with Aspose.Words. Copyright 2003-2026 Aspose Pty", "Ltd.")]
    [InlineData("Evaluation Only. Created with", "Aspose.Words. Copyright", "2003-2026 Aspose", "Pty Ltd.")]
    public void Review_FlagsTheEvaluationWatermarkSavedIntoTheFile(params string[] lines)
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.CreateRawDocument("plain.pdf", pages: 2);
        string stamped = fixture.File("stamped.pdf");
        using (var document = new Document(input))
        {
            foreach (string line in lines)
            {
                document.Pages[2].Paragraphs.Add(new Aspose.Pdf.Text.TextFragment(line));
            }
            document.Save(stamped);
        }

        CliResult review = workspace.Run(["review", stamped, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode[] findings = [.. JsonNode.Parse(review.StdOut)!["findings"]!.AsArray()
            .Where(static item => item!["code"]!.GetValue<string>() == "PDF_EVALUATION_WATERMARK")
            .Select(static item => item!)];
        // Without a license the test's own save stamps every page.
        Assert.Equal(
            fixture.LicenseState == Aspose.Cli.Sdk.Licensing.LicenseState.Licensed ? ["page 2"] : ["page 1", "page 2"],
            findings.Select(static item => item["location"]!.GetValue<string>()));
        Assert.All(findings, static item => Assert.Contains("license", item["hint"]!.GetValue<string>(), StringComparison.Ordinal));
    }

    /// <summary>
    /// Each Aspose product prints its own evaluation notice into a PDF it saves without a
    /// license; review recognizes them all, as a later licensed run reads the file.
    /// </summary>
    [Theory]
    [Category(TestCategory.Slow)]
    [InlineData("cells")]
    [InlineData("words")]
    [InlineData("slides")]
    public void Review_FlagsTheEvaluationNoticeOfAPdfAnotherProductSaved(string product)
    {
        using var workspace = new TempWorkspace();
        string pdf = workspace.File($"{product}.pdf");
        string[][] commands = product switch
        {
            "cells" => [["cells", "convert", Write("data.csv", "Region,Total\nEast,1\n"), "--to", "pdf", "--out", pdf]],
            "words" => [["words", "convert", Write("note.md", "# Note\n\nA short note.\n"), "--to", "pdf", "--out", pdf]],
            _ => [["slides", "create", workspace.File("deck.pptx")], ["slides", "convert", workspace.File("deck.pptx"), "--to", "pdf", "--out", pdf]],
        };
        foreach (string[] command in commands)
        {
            CliResult created = workspace.Run([.. command, "--license-mode", "evaluation", "--output", "json"]);
            Assert.True(created.ExitCode == 0, created.StdErr);
        }

        CliResult review = workspace.Run(["review", pdf, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        Assert.Contains(
            JsonNode.Parse(review.StdOut)!["findings"]!.AsArray(),
            static item => item!["code"]!.GetValue<string>() == "PDF_EVALUATION_WATERMARK"
                && item["location"]!.GetValue<string>() == "page 1");

        string Write(string name, string contents)
        {
            File.WriteAllText(workspace.File(name), contents);
            return workspace.File(name);
        }
    }

    /// <summary>Inspect and review count form fields by name, so a radio group is one field.</summary>
    [Fact]
    public void InspectAndReview_CountARadioGroupAsOneField()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.File("form.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new Aspose.Pdf.Forms.TextBoxField(page, new Rectangle(72, 700, 300, 720)) { PartialName = "company" });
            var color = new Aspose.Pdf.Forms.RadioButtonField(page) { PartialName = "color" };
            foreach ((string option, double x) in new[] { ("Red", 72.0), ("Green", 112.0), ("Blue", 152.0) })
            {
                color.Add(new Aspose.Pdf.Forms.RadioButtonOptionField(page, new Rectangle(x, 620, x + 20, 640)) { OptionName = option });
            }
            document.Form.Add(color);
            document.Save(input);
        }

        PdfInfoResult info = fixture.Engine.GetInfo(input, new PdfInfoRequest { Details = ["forms"] });
        CliResult review = workspace.Run(["review", input, "--out", workspace.File("review"), "--output", "json"]);

        Assert.Equal(2, info.Forms!.FieldCount);
        Assert.Equal(2, fixture.Engine.ReadForm(input, new PdfFormReadRequest()).Fields.Select(static field => field.Name).Distinct().Count());
        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        Assert.Contains(result["findings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "PDF_FORM_APPEARANCE_REVIEW_REQUIRED"
            && item["message"]!.GetValue<string>().Contains("2 form field(s)", StringComparison.Ordinal));
        Assert.Equal(2, result["coverage"]!["metrics"]!.AsArray()
            .Single(static metric => metric!["name"]!.GetValue<string>() == "formFields")!["value"]!.GetValue<int>());
    }

    [Fact]
    public void Review_DeclaresEveryCheckItsAssessmentReports()
    {
        IReadOnlyList<ReviewCheck> declared = new PdfViewAdapter().Checks;

        // Findings are built only from PdfReviewChecks members, so each must be declared.
        Assert.Equal(
            typeof(PdfReviewChecks).GetProperties()
                .Where(static property => property.PropertyType == typeof(ReviewCheck))
                .Select(static property => (ReviewCheck)property.GetValue(null)!)
                .OrderBy(static check => check.Code, StringComparer.Ordinal),
            declared.OrderBy(static check => check.Code, StringComparer.Ordinal));
    }
}

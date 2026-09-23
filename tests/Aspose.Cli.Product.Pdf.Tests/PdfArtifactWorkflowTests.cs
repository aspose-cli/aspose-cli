using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfArtifactWorkflowTests
{
    [Fact]
    public void Render_ProducesOnlyTheSelectedPage()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("render.pdf", pages: 2);
        string output = fixture.File("selected.png");

        PdfRenderResult result = fixture.Engine.Render(input, new PdfRenderRequest
        {
            TargetFormatId = "png",
            OutputPath = output,
            Pages = Sdk.Addressing.PageRange.Parse("2"),
            Dpi = 96,
        });

        PdfPageOutput page = Assert.Single(result.Outputs);
        Assert.Equal(2, page.Page);
        Assert.Equal(output, page.Output.Path);
        Assert.True(new FileInfo(output).Length > 100);
    }

    [Fact]
    public void Create_MarkdownAppliesPageGeometryBeforeLayout()
    {
        using var fixture = new PdfEngineFixture();
        string markdown = fixture.File("appendix.md");
        File.WriteAllText(markdown, "# Delivery appendix\n\nReadiness evidence.");
        string output = fixture.File("appendix.pdf");
        fixture.Engine.Create(new NewPdfRequest
        {
            TextPath = markdown,
            Markdown = true,
            OutputPath = output,
            PageSize = "Letter",
            Margins = new PdfMargins(72, 54, 60, 90),
        });

        using var reopened = new Document(output);
        Page page = Assert.Single(reopened.Pages);
        Assert.Equal(612, page.Rect.Width, precision: 1);
        Assert.Equal(792, page.Rect.Height, precision: 1);
        var absorber = new TextFragmentAbsorber("Delivery appendix");
        page.Accept(absorber);
        TextFragment title = Assert.Single(absorber.TextFragments);
        Assert.True(title.Rectangle.LLX >= 89, $"Heading starts at {title.Rectangle.LLX}, outside the requested left margin.");
        Assert.True(title.Rectangle.URY <= 721, $"Heading ends at {title.Rectangle.URY}, outside the requested top margin.");
    }

    [Fact]
    public void Create_PlainTextAppliesPageSizeAndMarginsAndFlowsAcrossPages()
    {
        using var fixture = new PdfEngineFixture();
        string text = fixture.File("long.txt");
        File.WriteAllLines(text, ["Opening\tline", .. Enumerable.Range(2, 90).Select(static line => $"Line {line}")]);
        string output = fixture.File("long.pdf");

        fixture.Engine.Create(new NewPdfRequest
        {
            TextPath = text,
            OutputPath = output,
            PageSize = "Letter",
            Margins = new PdfMargins(72, 54, 60, 90),
        });

        using var reopened = new Document(output);
        Assert.True(reopened.Pages.Count > 1);
        Assert.All(reopened.Pages, static page =>
        {
            Assert.Equal(612, page.Rect.Width, precision: 1);
            Assert.Equal(792, page.Rect.Height, precision: 1);
        });
        var absorber = new TextFragmentAbsorber("Opening");
        reopened.Pages[1].Accept(absorber);
        TextFragment first = Assert.Single(absorber.TextFragments);
        Assert.True(first.Rectangle.LLX >= 89, $"Text starts at {first.Rectangle.LLX}, outside the requested left margin.");
        Assert.True(first.Rectangle.URY <= 721, $"Text ends at {first.Rectangle.URY}, outside the requested top margin.");
        Assert.Contains("Opening line", PageText(reopened.Pages[1]), StringComparison.Ordinal);
        Assert.Contains("Line 91", PageText(reopened.Pages[reopened.Pages.Count]), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateMergeAndSplit_PreserveTheArtifactSequence()
    {
        using var fixture = new PdfEngineFixture();
        string text = fixture.File("created.txt");
        File.WriteAllText(text, "Created by the PDF workflow");
        string created = fixture.File("created.pdf");

        fixture.Engine.Create(new NewPdfRequest
        {
            TextPath = text,
            OutputPath = created,
        });
        using (var document = new Document(created))
        {
            Assert.Contains(
                "Created by the PDF workflow",
                PageText(document.Pages[1]),
                StringComparison.Ordinal);
        }

        string second = fixture.CreateDocument("second.pdf", pages: 1);
        string merged = fixture.File("merged.pdf");
        fixture.Engine.Merge(new PdfMergeRequest
        {
            InputPaths = [created, second],
            OutputPath = merged,
        });
        using (var document = new Document(merged))
        {
            Assert.Equal(2, document.Pages.Count);
        }

        PdfSplitResult split = fixture.Engine.Split(merged, new PdfSplitRequest
        {
            Every = 1,
            OutputDirectory = fixture.File("parts"),
        });
        Assert.Equal(2, split.Outputs.Count);
        Assert.All(split.Outputs, part =>
        {
            Assert.True(File.Exists(part.Output.Path));
            using var document = new Document(part.Output.Path);
            Assert.Single(document.Pages);
        });
    }

    [Fact]
    public void ExtractAttachments_PublishesOriginalBytesAndMeasuredLengths()
    {
        using var fixture = new PdfEngineFixture();
        fixture.Gate.EnsureApplied();
        string input = fixture.File("attachments.pdf");
        byte[] payload = [0, 255, 4, 17, 0, 128];
        using (var document = new Document())
        using (var content = new MemoryStream(payload))
        using (var empty = new MemoryStream())
        {
            document.Pages.Add().Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Supporting evidence"));
            document.EmbeddedFiles.Add(new FileSpecification(content, "evidence.bin"));
            document.EmbeddedFiles.Add(new FileSpecification(empty, "empty.txt"));
            document.Save(input);
        }
        byte[] original = File.ReadAllBytes(input);
        string directory = fixture.File("extracted");
        PdfExtractResult result = fixture.Engine.Extract(input,
            new PdfExtractRequest { What = "attachments", OutputDirectory = directory });

        Assert.Equal(2, result.Items.Count);
        var evidence = Assert.Single(result.Items, item => item.Name == "evidence.bin");
        var emptyItem = Assert.Single(result.Items, item => item.Name == "empty.txt");
        Assert.Equal(payload.LongLength, evidence.SizeBytes);
        Assert.Equal(payload, File.ReadAllBytes(evidence.Path));
        Assert.Equal(0, emptyItem.SizeBytes);
        Assert.Empty(File.ReadAllBytes(emptyItem.Path));
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }

    [Fact]
    public void ExtractAttachments_LaterBudgetFailureRollsBackTheWholeSet()
    {
        using var fixture = new PdfEngineFixture();
        fixture.Gate.EnsureApplied();
        string input = fixture.File("attachment-budget.pdf");
        using (var document = new Document())
        using (var first = new MemoryStream([1]))
        using (var second = new MemoryStream([2, 3, 4]))
        {
            document.Pages.Add().Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Supporting evidence"));
            document.EmbeddedFiles.Add(new FileSpecification(first, "a.bin"));
            document.EmbeddedFiles.Add(new FileSpecification(second, "b.bin"));
            document.Save(input);
        }
        byte[] original = File.ReadAllBytes(input);
        ResourceBudgetLedger budgets = ProductTestBudgets.Create<PdfModule>();
        budgets.Consume(ResourceBudgetKinds.OutputBytes,
            budgets.Remaining(ResourceBudgetKinds.OutputBytes) - 2, "bytes", "test-reservation");
        var engine = new PdfDocumentEngine(fixture.Gate, budgets, new SafeFileWriter(budgets));
        string output = fixture.File("extracted");
        Directory.CreateDirectory(output);
        string unrelated = Path.Combine(output, "keep.txt");
        File.WriteAllText(unrelated, "Unrelated original file");

        CliException error = Assert.Throws<CliException>(() => engine.Extract(input,
            new PdfExtractRequest { What = "attachments", OutputDirectory = output }));

        Assert.Equal(ErrorCodes.ExtractBudgetExceeded, error.Code);
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.Equal("Unrelated original file", File.ReadAllText(unrelated));
        Assert.Equal([unrelated], Directory.GetFiles(output));
    }

    private static string PageText(Page page)
    {
        var absorber = new TextAbsorber();
        page.Accept(absorber);
        return absorber.Text ?? string.Empty;
    }
}

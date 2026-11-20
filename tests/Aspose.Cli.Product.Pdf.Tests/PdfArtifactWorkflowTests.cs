using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
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

        PdfRenderResult result = PdfRender.Run(fixture.Session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: "png"),
            Pages = Sdk.Addressing.PageRange.Parse("2"),
            Dpi = 96,
        });

        PdfPageOutput page = Assert.Single(result.Outputs);
        Assert.Equal(2, page.Page);
        Assert.Equal(output, page.Path);
        Assert.True(new FileInfo(output).Length > 100);
    }

    [Fact]
    public void Split_SpellsPagesAsRangesAndNumbersTheParts()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("region.pdf", pages: 4);

        PdfSplitResult split = PdfSplit.Run(fixture.Session, new PdfSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("parts")),
            Plan = new Sdk.Addressing.SplitPlan(Sdk.Addressing.SplitBy.Pages, null, [Sdk.Addressing.PageRange.Parse("1-3"), Sdk.Addressing.PageRange.Parse("1,3-4"), Sdk.Addressing.PageRange.Parse("2")], null),
        });

        Assert.Equal(["1-3", "1,3-4", "2"], split.Outputs.Select(static part => part.Pages));
        Assert.Equal(
            ["region.1.pdf", "region.2.pdf", "region.3.pdf"],
            split.Outputs.Select(static part => Path.GetFileName(part.Path)));
    }

    private static OutlineItemCollection Bookmark(
        Document document, ICollection<OutlineItemCollection> parent, string title, IAppointment destination)
    {
        var item = new OutlineItemCollection(document.Outlines) { Title = title, Destination = destination };
        parent.Add(item);
        return item;
    }

    /// <summary>The outline of a PDF as titles and pages, a child after its parent's ' > '.</summary>
    private static string Outline(string path)
    {
        using var document = new Document(path);
        static IEnumerable<string> Items(IEnumerable<OutlineItemCollection> items, string prefix) =>
            items.SelectMany(item => Items(item, $"{prefix}{item.Title}({((ExplicitDestination)item.Destination).PageNumber}) > ")
                .DefaultIfEmpty($"{prefix}{item.Title}({((ExplicitDestination)item.Destination).PageNumber})"));
        return string.Join(" | ", Items(document.Outlines, string.Empty));
    }

    [Fact]
    public void ExtractAttachments_PublishesOriginalBytesAndMeasuredLengths()
    {
        using var fixture = new PdfEngineFixture();
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
        PdfExtractResult result = PdfExtract.Run(fixture.Session, new PdfExtractRequest { Input = input, Output = new ResolvedDirectory(directory), What = "attachments" });

        Assert.Equal(2, result.Outputs.Count);
        var evidence = Assert.Single(result.Outputs, item => item.Name == "evidence.bin");
        var emptyItem = Assert.Single(result.Outputs, item => item.Name == "empty.txt");
        Assert.All(result.Outputs, static item => Assert.Null(item.Page));
        Assert.Equal(["bin", "txt"], result.Outputs.Select(static item => item.Format).Order());
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
        var session = PdfActivation.Session(fixture.Outputs(new SafeFileWriter(budgets)), budgets);
        string output = fixture.File("extracted");
        Directory.CreateDirectory(output);
        string unrelated = Path.Combine(output, "keep.txt");
        File.WriteAllText(unrelated, "Unrelated original file");

        CliException error = Assert.Throws<CliException>(() => PdfExtract.Run(session, new PdfExtractRequest { Input = input, Output = new ResolvedDirectory(output), What = "attachments" }));

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

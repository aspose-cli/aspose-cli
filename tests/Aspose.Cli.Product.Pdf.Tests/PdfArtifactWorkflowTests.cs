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

    private static string PageText(Page page)
    {
        var absorber = new TextAbsorber();
        page.Accept(absorber);
        return absorber.Text ?? string.Empty;
    }
}

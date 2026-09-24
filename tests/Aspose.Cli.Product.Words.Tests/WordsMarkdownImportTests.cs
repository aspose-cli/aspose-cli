using Aspose.Words;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>create, insert_markdown and Markdown headers import Markdown the same way.</summary>
public sealed class WordsMarkdownImportTests
{
    [Fact]
    public void Create_WithoutTemplate_UsesTheBuiltInA4Design()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("report.md");
        File.WriteAllText(markdown, "# Title\n\nBody with **bold** text.\n");
        string output = fixture.Temp.File("report.docx");

        fixture.Engine.CreateDocument(new NewDocumentRequest { OutputPath = output, MarkdownPath = markdown });

        var document = new Document(output);
        Assert.Equal(PaperSize.A4, document.FirstSection.PageSetup.PaperSize);
        Paragraph heading = Body(document).First(static paragraph => paragraph.GetText().Contains("Title", StringComparison.Ordinal));
        Assert.Equal(StyleIdentifier.Heading1, heading.ParagraphFormat.StyleIdentifier);
        Assert.NotNull(document.FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary]);
    }

    [Fact]
    public void BuiltInDesign_CarriesNoGeneratorMetadata()
    {
        using Stream stream = typeof(WordsDocumentEngine).Assembly.GetManifestResourceStream("Templates/default-a4.docx")!;
        using var package = new System.IO.Compression.ZipArchive(stream);
        foreach (System.IO.Compression.ZipArchiveEntry entry in package.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            Assert.DoesNotContain("Aspose", reader.ReadToEnd(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void InsertMarkdownAndMarkdownHeaders_TakeTheDestinationStyles()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        var source = new Document(input);
        source.Styles[StyleIdentifier.Heading2].Font.Color = System.Drawing.Color.FromArgb(0, 0, 128);
        source.Save(input, SaveFormat.Docx);

        string output = fixture.Temp.File("imported.docx");
        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new InsertMarkdownOp
                {
                    At = new WordsTarget { Find = "Operations remained" }, Position = "after", Markdown = "## Outlook\n\nPlain *emphasis*.",
                },
                new SetHeaderOp { Markdown = "## Confidential" },
            ],
        }, new WordsEditRequest { OutputPath = output });

        var document = new Document(output);
        Assert.Null(document.Styles["Heading 2_0"]);
        Paragraph outlook = Body(document).Single(static paragraph => paragraph.GetText().StartsWith("Outlook", StringComparison.Ordinal));
        Assert.Equal("Heading 2", outlook.ParagraphFormat.StyleName);
        Assert.False(outlook.Runs[0].Font.Bold && outlook.Runs[0].Font.Italic);
        Run emphasis = Body(document).SelectMany(static paragraph => paragraph.Runs.Cast<Run>())
            .Single(static run => run.Text == "emphasis");
        Assert.True(emphasis.Font.Italic);
        Paragraph header = WordsFixture.FirstAuthoredParagraph(document.FirstSection.HeadersFooters[HeaderFooterType.HeaderPrimary]);
        Assert.Equal("Confidential", header.GetText().Trim());
        Assert.Equal("Heading 2", header.ParagraphFormat.StyleName);
    }

    [Theory]
    [InlineData("first")]
    [InlineData("even")]
    public void FirstAndEvenHeaders_TurnOnTheSectionSettingThatShowsThem(string kind)
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File($"header-{kind}.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new SetHeaderOp(kind) { Paragraphs = ["Header"] }],
        }, new WordsEditRequest { OutputPath = output });

        PageSetup setup = new Document(output).FirstSection.PageSetup;
        Assert.Equal(kind == "first", setup.DifferentFirstPageHeaderFooter);
        Assert.Equal(kind == "even", setup.OddAndEvenPagesHeaderFooter);
    }

    private static IEnumerable<Paragraph> Body(Document document) =>
        document.FirstSection.Body.Paragraphs.Cast<Paragraph>();
}

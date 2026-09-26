using Aspose.Words;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>create, insert_markdown and Markdown headers import Markdown the same way.</summary>
public sealed class WordsMarkdownImportTests
{
    [Fact]
    public void Create_WithoutTemplate_TakesStylesPageSetupAndFooterFromTheBuiltInDesign()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("brief.md");
        File.WriteAllText(markdown, "# Brief\n\nPlain **bold** and `code`.\n");
        string output = fixture.Temp.File("brief.docx");

        fixture.Engine.Create(new NewDocumentRequest { OutputPath = output, MarkdownPath = markdown });

        var document = new Document(output);
        Section section = Assert.Single(document.Sections.Cast<Section>());
        Assert.Equal(PaperSize.A4, section.PageSetup.PaperSize);
        Assert.Contains(
            section.HeadersFooters[HeaderFooterType.FooterPrimary].Range.Fields.Cast<Aspose.Words.Fields.Field>(),
            static field => field.Type == Aspose.Words.Fields.FieldType.FieldPage);
        // Evaluation mode may add a banner paragraph; select the authored paragraphs by text.
        Paragraph[] paragraphs = section.Body.Paragraphs.Cast<Paragraph>().ToArray();
        Paragraph heading = Assert.Single(paragraphs, static paragraph => paragraph.GetText().Trim() == "Brief");
        Assert.Equal(StyleIdentifier.Heading1, heading.ParagraphFormat.StyleIdentifier);
        Assert.True(heading.Runs[0].Font.Bold);
        Run[] runs = Assert.Single(paragraphs, static paragraph => paragraph.GetText().StartsWith("Plain", StringComparison.Ordinal))
            .Runs.Cast<Run>().ToArray();
        Assert.Equal(["bold"], runs.Where(static run => run.Font.Bold).Select(static run => run.Text));
        Assert.Equal("InlineCode", Assert.Single(runs, static run => run.Text == "code").Font.StyleName);
        Assert.DoesNotContain(paragraphs, static paragraph => !paragraph.HasChildNodes);
    }
    [Fact]
    public void InspectFonts_ReportsTheFontsTheTextUses()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("fonts.md");
        File.WriteAllText(markdown, "# Title\n\nHello 你好\n");
        string output = fixture.Temp.File("fonts.docx");
        fixture.Engine.Create(new NewDocumentRequest { OutputPath = output, MarkdownPath = markdown });

        DocumentInfoResult info = fixture.Engine.GetInfo(output, new DocumentInfoRequest { Details = ["fonts"] });

        Assert.Equal(["Calibri", "Microsoft YaHei"], info.Fonts);
    }

    [Fact]
    public void BuiltInDesign_FontTableNamesExactlyTheFontsItsStylesUse()
    {
        Document template = Engine.Mapping.WordsDocumentLoader.OpenDefaultTemplate();

        string[] styleFonts = template.Styles.Cast<Style>()
            .Where(static style => style.Type is StyleType.Paragraph or StyleType.Character)
            .SelectMany(static style => new[] { style.Font.Name, style.Font.NameFarEast, style.Font.NameBi, style.Font.NameOther })
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["Calibri", "Microsoft YaHei"], styleFonts);
        Assert.Equal(styleFonts, template.FontInfos.Cast<Aspose.Words.Fonts.FontInfo>()
            .Select(static font => font.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BuiltInDesign_CarriesNoGeneratorMetadata()
    {
        using Stream stream = typeof(WordsEngine).Assembly.GetManifestResourceStream("Templates/default-a4.docx")!;
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
            Ops = [new SetHeaderOp { Kind = kind, Paragraphs = ["Header"] }],
        }, new WordsEditRequest { OutputPath = output });

        PageSetup setup = new Document(output).FirstSection.PageSetup;
        Assert.Equal(kind == "first", setup.DifferentFirstPageHeaderFooter);
        Assert.Equal(kind == "even", setup.OddAndEvenPagesHeaderFooter);
    }

    private static IEnumerable<Paragraph> Body(Document document) =>
        document.FirstSection.Body.Paragraphs.Cast<Paragraph>();
}

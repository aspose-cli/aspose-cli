using Aspose.Cli.Sdk.Licensing;
using Aspose.Words;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>create, insert_markdown and Markdown headers import Markdown the same way.</summary>
public sealed class WordsMarkdownImportTests
{
    [Fact]
    public void BuiltInDesign_KeepsTitleAndSubtitleOutOfTheTableOfContents()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("bid.md");
        File.WriteAllText(markdown, "# Chapter one\n\nBody text.\n");
        string created = fixture.Temp.File("bid.docx");
        fixture.Engine.Create(new NewDocumentRequest { Output = TestOutput.At(created), MarkdownPath = markdown });
        string output = fixture.Temp.File("bid-toc.docx");

        fixture.Engine.ApplyOps(created, new WordsOpsBatch
        {
            Ops =
            [
                new InsertParagraphsOp
                {
                    At = new WordsTarget { Block = 1 },
                    Position = "before",
                    Paragraphs = [new ParagraphInput { Text = "Contents", Style = "Title" }, new ParagraphInput { Text = "Bid 2026", Style = "Subtitle" }],
                },
                new InsertTocOp { At = new WordsTarget { Block = 1 }, Position = "before" },
            ],
        }, new WordsEditRequest { Output = TestOutput.At(output) });

        DocumentInfoResult info = fixture.Engine.GetInfo(output, new DocumentInfoRequest { Details = ["outline"] });
        Assert.Equal(["Chapter one"], info.Outline!.Select(static item => item.Text));
        string toc = new Document(output).Range.Fields.Cast<Aspose.Words.Fields.Field>()
            .Single(static field => field.Type == Aspose.Words.Fields.FieldType.FieldTOC).Result;
        Assert.Contains("Chapter one", toc, StringComparison.Ordinal);
        Assert.DoesNotContain("Contents", toc, StringComparison.Ordinal);
        Assert.DoesNotContain("Bid 2026", toc, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ClosesEmphasisAfterPunctuationOnlyAsCommonMarkDoes()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("struck.md");
        // A closing delimiter after punctuation needs a space or punctuation after it; HTML tags do not.
        File.WriteAllText(markdown, "施行。~~原《守则》~~同时废止。\n\n施行。<del>原《守则》</del>同时废止。\n");
        string output = fixture.Temp.File("struck.docx");

        fixture.Engine.Create(new NewDocumentRequest { Output = TestOutput.At(output), MarkdownPath = markdown });

        Paragraph[] paragraphs = [.. new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>().TakeLast(2)];
        Assert.Equal("施行。~~原《守则》~~同时废止。", paragraphs[0].GetText().TrimEnd('\r'));
        Assert.Equal("原《守则》", Assert.Single(paragraphs[1].Runs.Cast<Run>(), static run => run.Font.StrikeThrough).Text);
    }

    [Fact]
    public void Create_WithoutTemplate_BreaksChineseLinesByTheEastAsianRules()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("clauses.md");
        // Half of the characters are commas or full stops, which no line may start with.
        File.WriteAllText(markdown, string.Concat(Enumerable.Repeat("甲，乙。", 120)) + "\n");
        string output = fixture.Temp.File("clauses.docx");

        fixture.Engine.Create(new NewDocumentRequest { Output = TestOutput.At(output), MarkdownPath = markdown });

        IReadOnlyList<string> lines = WordsFixture.LayoutLines(new Document(output));
        Assert.True(lines.Count > 4, string.Join(" | ", lines));
        Assert.False(lines.Any(static line => line.StartsWith('，') || line.StartsWith('。')), string.Join(" | ", lines));
    }

    [Fact]
    public void Create_WithoutTemplate_TakesStylesPageSetupAndFooterFromTheBuiltInDesign()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("brief.md");
        File.WriteAllText(markdown, "# Brief\n\nPlain **bold** and `code`.\n");
        string output = fixture.Temp.File("brief.docx");

        fixture.Engine.Create(new NewDocumentRequest { Output = TestOutput.At(output), MarkdownPath = markdown });

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
    [LicensedFact]
    public void Create_FitsAnImageWiderThanThePageToTheTextColumn()
    {
        using var fixture = new WordsFixture();
        string chart = fixture.Temp.File("chart.png");
        using (var bitmap = new SkiaSharp.SKBitmap(1367, 1150))
        using (SkiaSharp.SKImage image = SkiaSharp.SKImage.FromBitmap(bitmap))
        using (SkiaSharp.SKData png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
        using (FileStream file = File.Create(chart))
        {
            png.SaveTo(file);
        }

        string markdown = fixture.Temp.File("report.md");
        File.WriteAllText(markdown, "# Report\n\n![Completion](chart.png)\n");
        string output = fixture.Temp.File("report.docx");

        fixture.Engine.Create(new NewDocumentRequest { Output = TestOutput.At(output), MarkdownPath = markdown });

        var document = new Document(output);
        PageSetup page = document.FirstSection.PageSetup;
        Aspose.Words.Drawing.Shape picture = Assert.Single(document.GetChildNodes(NodeType.Shape, true).OfType<Aspose.Words.Drawing.Shape>());
        Assert.InRange(picture.Width, 1, page.PageWidth - page.LeftMargin - page.RightMargin + 0.01);
        Assert.Equal(1150d / 1367d, picture.Height / picture.Width, 2);
    }

    [Fact]
    public void InspectFonts_ReportsTheFontsTheTextUses()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("fonts.md");
        File.WriteAllText(markdown, "# Title\n\nHello 你好\n");
        string output = fixture.Temp.File("fonts.docx");
        fixture.Engine.Create(new NewDocumentRequest { Output = TestOutput.At(output), MarkdownPath = markdown });

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
        }, new WordsEditRequest { Output = TestOutput.At(output) });

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
        }, new WordsEditRequest { Output = TestOutput.At(output) });

        PageSetup setup = new Document(output).FirstSection.PageSetup;
        Assert.Equal(kind == "first", setup.DifferentFirstPageHeaderFooter);
        Assert.Equal(kind == "even", setup.OddAndEvenPagesHeaderFooter);
    }

    [Fact]
    public void PlainHeaderAndFooterParagraphs_KeepTheFormatOfWhatTheyReplace()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Body");
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.ParagraphFormat.Alignment = ParagraphAlignment.Right;
        builder.Font.Size = 8;
        builder.Font.Name = "Arial";
        builder.Write("Old header");
        string input = fixture.Temp.File("aligned-header.docx");
        source.Save(input);
        string output = fixture.Temp.File("aligned-header-out.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new SetHeaderOp { Paragraphs = ["New header"] }, new SetFooterOp { Paragraphs = ["New footer"] }],
        }, new WordsEditRequest { Output = TestOutput.At(output) });

        Section section = new Document(output).FirstSection;
        Paragraph header = WordsFixture.FirstAuthoredParagraph(section.HeadersFooters[HeaderFooterType.HeaderPrimary]);
        Assert.Equal("New header", header.GetText().Trim());
        Assert.Equal(ParagraphAlignment.Right, header.ParagraphFormat.Alignment);
        Assert.Equal(8, header.Runs[0].Font.Size);
        Assert.Equal("Arial", header.Runs[0].Font.Name);
        // With nothing to replace, a footer takes Word's Footer style.
        Paragraph footer = section.HeadersFooters[HeaderFooterType.FooterPrimary].Paragraphs.Cast<Paragraph>()
            .Single(static paragraph => paragraph.GetText().Trim() == "New footer");
        Assert.Equal(StyleIdentifier.Footer, footer.ParagraphFormat.StyleIdentifier);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    public void PlainHeaderParagraphs_KeepTheFormatOfTheHeaderASectionContinues(int? target)
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("First");
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Write("Second");
        builder.MoveToSection(0);
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.ParagraphFormat.Alignment = ParagraphAlignment.Right;
        builder.Write("Old header");
        string input = fixture.Temp.File("continued-header.docx");
        source.Save(input);
        string output = fixture.Temp.File("continued-header-out.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new SetHeaderOp { Paragraphs = ["New header"], Section = target }],
        }, new WordsEditRequest { Output = TestOutput.At(output) });

        Section second = new Document(output).Sections[1];
        Paragraph header = WordsFixture.FirstAuthoredParagraph(second.HeadersFooters[HeaderFooterType.HeaderPrimary]);
        Assert.Equal("New header", header.GetText().Trim());
        // Evaluation mode saved the input with an empty header of the second section's own.
        if (fixture.LicenseState == LicenseState.Licensed)
        {
            Assert.Equal(ParagraphAlignment.Right, header.ParagraphFormat.Alignment);
        }
    }

    private static IEnumerable<Paragraph> Body(Document document) =>
        document.FirstSection.Body.Paragraphs.Cast<Paragraph>();
}

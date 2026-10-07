using System.IO.Compression;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesCreationTests
{
    [Fact]
    public void Create_WithoutTemplate_UsesTheBuiltInWidescreenDesign()
    {
        using var fixture = new SlidesEngineFixture();
        string output = fixture.File("blank.pptx");

        SlidesCreateResult result = fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(output) });

        Assert.Equal(1, result.SlideCount);
        Assert.Null(result.Template);
        using var deck = new Presentation(output);
        Assert.Equal(720f, deck.SlideSize.Size.Width);
        Assert.Equal(405f, deck.SlideSize.Size.Height);
        Assert.Contains(deck.LayoutSlides, static layout => layout.Name == "Two Content");
        Assert.Equal("Title Slide", Assert.Single(deck.Slides).LayoutSlide.Name);
        Assert.NotNull(SlidesPlaceholders.Title(deck.Slides[0]));
    }

    [Fact]
    public void Create_FromATemplateWithoutSlides_AddsOneTitleSlide()
    {
        using var fixture = new SlidesEngineFixture();
        string template = fixture.File("layouts-only.pptx");
        using (var source = new Presentation())
        {
            source.Slides.RemoveAt(0);
            source.Save(template, SaveFormat.Pptx);
        }

        SlidesCreateResult result = fixture.Engine.Create(new NewPresentationRequest
        {
            Output = TestOutput.At(fixture.File("from-template.pptx")),
            TemplatePath = template,
        });

        Assert.Equal(1, result.SlideCount);
        using var deck = new Presentation(fixture.File("from-template.pptx"));
        Assert.Equal(SlideLayoutType.Title, Assert.Single(deck.Slides).LayoutSlide.LayoutType);
    }

    [Theory]
    [InlineData("4x3", 720f, 540f)]
    [InlineData("16x9", 720f, 405f)]
    public void Create_WithSize_ScalesInheritedPlaceholdersWithTheCanvas(string size, float width, float height)
    {
        using var fixture = new SlidesEngineFixture();
        string output = fixture.File($"sized-{size}.pptx");

        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(output), Size = size });

        using var deck = new Presentation(output);
        Assert.Equal(width, deck.SlideSize.Size.Width);
        Assert.Equal(height, deck.SlideSize.Size.Height);
        IEnumerable<IShape> inherited = deck.Masters.SelectMany(static master => master.Shapes)
            .Concat(deck.LayoutSlides.SelectMany(static layout => layout.Shapes))
            .Where(static shape => shape.Placeholder is not null);
        Assert.All(inherited, shape =>
        {
            Assert.InRange(shape.X, -0.5f, width);
            Assert.InRange(shape.Y, -0.5f, height);
            Assert.True(shape.X + shape.Width <= width + 0.5f, $"{shape.Name} exceeds the canvas width.");
            Assert.True(shape.Y + shape.Height <= height + 0.5f, $"{shape.Name} exceeds the canvas height.");
        });
    }

    [Fact]
    public void BuiltInDesign_CarriesNoGeneratorMetadata()
    {
        using Stream stream = typeof(SlidesPresentationLoader).Assembly
            .GetManifestResourceStream("Templates/default-16x9.pptx")!;
        using var package = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.DoesNotContain(package.Entries, static entry => entry.FullName.StartsWith("ppt/tags/", StringComparison.Ordinal));
        Assert.DoesNotContain(package.Entries, static entry => entry.FullName.Contains("thumbnail", StringComparison.OrdinalIgnoreCase));
        foreach (ZipArchiveEntry entry in package.Entries.Where(static entry => entry.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(entry.Open());
            string xml = reader.ReadToEnd();
            Assert.DoesNotContain("AS_OS", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("Aspose", xml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BuiltInDesign_GivesContentTextOneSizeOnEveryLayout()
    {
        using var fixture = new SlidesEngineFixture();
        string output = fixture.File("design.pptx");
        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(output) });

        using var deck = new Presentation(output);
        (string Layout, float Size)[] sizes = deck.LayoutSlides
            .SelectMany(static layout => layout.Shapes.OfType<IAutoShape>()
                .Where(static shape => shape.Placeholder?.Type == PlaceholderType.Object)
                .Select(shape => (layout.Name, shape.TextFrame.Paragraphs[0].Portions[0].PortionFormat.GetEffective().FontHeight)))
            .ToArray();

        Assert.Contains(sizes, static size => size.Layout == "Two Content");
        Assert.Single(sizes.Select(static size => size.Size).Distinct());
    }

    [Fact]
    public void SlideSelection_OnAPresentationWithoutSlides_ReportsSlideNotFound()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("empty.pptx");
        using (var source = new Presentation())
        {
            source.Slides.RemoveAt(0);
            source.Save(input, SaveFormat.Pptx);
        }

        CliException render = Assert.Throws<CliException>(() => fixture.Engine.Render(
            input,
            new PresentationRenderRequest { Output = TestOutput.At(fixture.File("slide.png"), format: "png") }));
        CliException all = Assert.Throws<CliException>(() => fixture.Engine.Render(
            input,
            new PresentationRenderRequest { Output = TestOutput.At(fixture.File("all.png"), format: "png"), AllSlides = true }));
        CliException convert = Assert.Throws<CliException>(() => fixture.Engine.Convert(
            input,
            new PresentationConvertRequest { Output = TestOutput.At(fixture.File("converted.png"), format: "png") }));
        CliException read = Assert.Throws<CliException>(() => fixture.Engine.Read(
            input,
            new PresentationReadRequest { Slides = PageRange.Parse("1") }));

        Assert.All(
            new[] { render, all, convert, read },
            static error => Assert.Equal(SlidesDiagnostics.SlideNotFound, error.Code));
        Assert.Empty(Directory.GetFiles(fixture.Temp.Path, "*.png"));
    }

    [Fact]
    public void Markdown_FillsTheBuiltInDesignLayoutsWithoutOwnStyling()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("outline.md");
        File.WriteAllText(
            markdown,
            "# Review\nFor the board\n\n## Results\n- Revenue up\n  - Enterprise\nPlain note\n\n## Close\n");
        string output = fixture.File("outline.pptx");

        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(output), MarkdownPath = markdown });

        using var deck = new Presentation(output);
        Assert.Equal(
            ["Title Slide", "Title and Content", "Title Only"],
            deck.Slides.Select(static slide => slide.LayoutSlide.Name));
        // Evaluation mode may add watermark shapes; the authored shapes are the placeholders.
        IAutoShape[] authored = deck.Slides
            .SelectMany(static slide => slide.Shapes.OfType<IAutoShape>())
            .Where(static shape => shape.Name is "Title" or "Body")
            .ToArray();
        Assert.Equal(5, authored.Length);
        Assert.All(authored, static shape => Assert.NotNull(shape.Placeholder));
        IAutoShape body = deck.Slides[1].Shapes.OfType<IAutoShape>()
            .Single(static shape => shape.Placeholder?.Type == PlaceholderType.Object);
        Assert.Equal([0, 1, 0], body.TextFrame.Paragraphs.Select(static paragraph => (int)paragraph.ParagraphFormat.Depth));
        Assert.Equal(BulletType.None, body.TextFrame.Paragraphs[2].ParagraphFormat.Bullet.Type);
        Assert.All(
            authored.SelectMany(static shape => shape.TextFrame.Paragraphs)
                .SelectMany(static paragraph => paragraph.Portions),
            static portion =>
            {
                Assert.Null(portion.PortionFormat.LatinFont);
                Assert.True(float.IsNaN(portion.PortionFormat.FontHeight));
                Assert.Equal(FillType.NotDefined, portion.PortionFormat.FillFormat.FillType);
            });
    }

    [Fact]
    public void Markdown_PlainParagraphWrapsFlushWithItsFirstLine()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("plain.md");
        File.WriteAllText(markdown, "## Results\n- Revenue up\nA plain note\n");
        string output = fixture.File("plain.pptx");

        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(output), MarkdownPath = markdown });

        // The body level's hanging indent makes room for a bullet; without one, wrapped
        // lines would start a bullet's width right of the first.
        using var deck = new Presentation(output);
        IParagraph[] paragraphs = Assert.Single(SlidesPlaceholders.Content(deck.Slides[0])).TextFrame.Paragraphs.ToArray();
        IParagraphFormatEffectiveData bulleted = paragraphs[0].ParagraphFormat.GetEffective();
        IParagraphFormatEffectiveData plain = paragraphs[1].ParagraphFormat.GetEffective();
        Assert.True(bulleted.Indent < 0);
        Assert.Equal(0f, plain.Indent);
        Assert.Equal(bulleted.MarginLeft + bulleted.Indent, plain.MarginLeft);
    }

    [Fact]
    public void Markdown_TableBecomesAStyledSlideTableInTheFreePlaceholder()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("table.md");
        File.WriteAllText(
            markdown,
            """
            ## Pipeline
            | Stage | Value |
            |:--|--:|
            | Won | 12 |
            | Lost | 3 |

            ## Mix
            By region
            | Region | Share |
            |---|:-:|
            | EMEA | 40% |
            """);
        string output = fixture.File("table.pptx");

        SlidesCreateResult result = fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(output), MarkdownPath = markdown });

        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code == SlidesDiagnostics.TableOverflow);
        using var deck = new Presentation(output);
        Assert.Equal(["Title and Content", "Title and Content"], deck.Slides.Select(static slide => slide.LayoutSlide.Name));

        ITable table = Assert.Single(deck.Slides[0].Shapes.OfType<ITable>());
        IShape area = deck.Slides[0].LayoutSlide.Shapes.Single(static shape => shape.Placeholder?.Type == PlaceholderType.Object);
        Assert.Equal((area.X, area.Y), (table.X, table.Y));
        Assert.Equal(area.Width, table.Width, 0.5f);
        Assert.True(table.FirstRow);
        Assert.Equal(TableStylePreset.MediumStyle2Accent1, table.StylePreset);
        Assert.Equal(
            ["Stage", "Value", "Won", "12", "Lost", "3"],
            Enumerable.Range(0, 3).SelectMany(row => Enumerable.Range(0, 2).Select(column => table[column, row].TextFrame.Text)));
        Assert.All(Enumerable.Range(0, 3), row =>
        {
            Assert.Equal(TextAlignment.Left, table[0, row].TextFrame.Paragraphs[0].ParagraphFormat.Alignment);
            Assert.Equal(TextAlignment.Right, table[1, row].TextFrame.Paragraphs[0].ParagraphFormat.Alignment);
        });
        Assert.DoesNotContain(deck.Slides[0].Shapes, static shape => shape.Placeholder?.Type == PlaceholderType.Object);

        // Body text sits above the table, which still spans the content area.
        ISlide mixed = deck.Slides[1];
        IAutoShape text = mixed.Shapes.OfType<IAutoShape>().Single(static shape => shape.Name == "Body");
        Assert.Single(text.TextFrame.Paragraphs);
        ITable shares = Assert.Single(mixed.Shapes.OfType<ITable>());
        Assert.Equal(TextAlignment.NotDefined, shares[0, 1].TextFrame.Paragraphs[0].ParagraphFormat.Alignment);
        Assert.Equal(TextAlignment.Center, shares[1, 1].TextFrame.Paragraphs[0].ParagraphFormat.Alignment);
        Assert.Equal((area.X, area.Y), (text.X, text.Y));
        Assert.InRange(text.Height, 1, 80);
        Assert.Equal(text.Y + text.Height, shares.Y, 0.5f);
        Assert.Equal(area.X, shares.X, 0.5f);
        Assert.Equal(area.Width, shares.Width, 0.5f);
    }

    [Fact]
    public void Markdown_TableColumnsFollowTheirContent()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("columns.md");
        File.WriteAllText(
            markdown,
            """
            ## 分产品线营收
            各产品线 Q3 营收（万元）
            | 产品线 | 说明 | Q3 |
            |:--|:--|--:|
            | 软件授权 | 年度订阅与永久授权，包含续约客户和新签客户的全部许可收入，以及渠道伙伴代理销售的授权 | 1,410 |
            | 实施服务 | 项目交付 | 930 |
            """);
        string output = fixture.File("columns.pptx");

        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(output), MarkdownPath = markdown });

        using var deck = new Presentation(output);
        ITable table = Assert.Single(deck.Slides[0].Shapes.OfType<ITable>());
        // Short labels and numbers keep one line; the long description takes the spare width
        // and wraps. Lines are compared by height, since cells do not count their lines.
        float line = table[1, 0].TextFrame.Paragraphs[0].GetRect().Height;
        Assert.All(Enumerable.Range(0, 3), row =>
        {
            Assert.Equal(line, table[0, row].TextFrame.Paragraphs[0].GetRect().Height, 1f);
            Assert.Equal(line, table[2, row].TextFrame.Paragraphs[0].GetRect().Height, 1f);
        });
        Assert.True(table[1, 1].TextFrame.Paragraphs[0].GetRect().Height > 1.5 * line);
        Assert.True(table.Columns[1].Width > table.Columns[0].Width + table.Columns[2].Width);
    }

    [Fact]
    public void Markdown_TableUnderATitleSlide_TakesAContentSlideInsteadOfTheSubtitle()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("title-table.md");
        File.WriteAllText(
            markdown,
            """
            # Deck
            For the board
            | Stage | Value |
            |---|---|
            | Won | 12 |
            | Lost | 3 |
            """);
        string output = fixture.File("title-table.pptx");

        SlidesCreateResult result = fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(output), MarkdownPath = markdown });

        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code == SlidesDiagnostics.TableOverflow);
        using var deck = new Presentation(output);
        Assert.Equal(["Title Slide", "Title and Content"], deck.Slides.Select(static slide => slide.LayoutSlide.Name));
        Assert.Empty(deck.Slides[0].Shapes.OfType<ITable>());
        IAutoShape subtitle = deck.Slides[0].Shapes.OfType<IAutoShape>()
            .Single(static shape => shape.Placeholder?.Type == PlaceholderType.Subtitle);
        // Evaluation mode truncates longer text, so only its start is compared.
        Assert.StartsWith("For t", subtitle.TextFrame.Text, StringComparison.Ordinal);
        Assert.Equal("Deck", SlidesPlaceholders.Title(deck.Slides[1])!.TextFrame.Text);
        ITable table = Assert.Single(deck.Slides[1].Shapes.OfType<ITable>());
        IShape area = deck.Slides[1].LayoutSlide.Shapes.Single(static shape => shape.Placeholder?.Type == PlaceholderType.Object);
        Assert.Equal((area.X, area.Y), (table.X, table.Y));
    }

    [Fact]
    public void Markdown_TableTallerThanItsArea_IsReported()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("long.md");
        File.WriteAllText(
            markdown,
            "## Intro\n\n## Backlog\n| Item | Owner |\n|---|---|\n"
                + string.Concat(Enumerable.Range(1, 30).Select(static row => $"| Item {row} | Team |\n")));

        SlidesCreateResult result = fixture.Engine.Create(new NewPresentationRequest
        {
            Output = TestOutput.At(fixture.File("long.pptx")),
            MarkdownPath = markdown,
        });

        Warning warning = Assert.Single(result.Warnings!, static item => item.Code == SlidesDiagnostics.TableOverflow);
        Assert.Equal("slide 2", warning.Location);
        Assert.NotNull(warning.Hint);
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(3, false)]
    public void InsertTable_TallerThanItsRect_IsReported(int rows, bool reported)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("insert-table.pptx", slides: 2);

        SlidesEditResult result = fixture.Engine.ApplyOps(input, new SlidesOpsBatch
        {
            Ops =
            [
                new SlidesInsertTableOp
                {
                    Slide = 2,
                    Rect = new SlidesRectInput { X = 40, Y = 150, Width = 640, Height = 240 },
                    RowCount = rows,
                    ColumnCount = 2,
                    Data = [.. Enumerable.Range(1, rows).Select(static row => (IReadOnlyList<string>)[$"Item {row}", "Team"])],
                },
            ],
        }, new PresentationEditRequest { Output = TestOutput.At(fixture.File("insert-table.out.pptx")) });

        Warning[] overflow = (result.Warnings ?? []).Where(static item => item.Code == SlidesDiagnostics.TableOverflow).ToArray();
        Assert.Equal(reported ? 1 : 0, overflow.Length);
        Assert.All(overflow, static warning => Assert.Equal("slide 2", warning.Location));
    }
}
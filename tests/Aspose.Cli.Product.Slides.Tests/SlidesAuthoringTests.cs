using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesAuthoringTests
{
    [Fact]
    public void Parse_MapsHeadingsListsEmphasisAndImageTitles()
    {
        MarkdownSlide slide = Assert.Single(SlidesMarkdownBuilder.Parse(
            """
            ## Plan
            ### Scope
            + Keep *this* and **that** with `code`
            1. First
              2) Nested
            Use snake_case and [the docs](https://example.test).
            ![Chart of revenue](<charts/q 1.png> "Quarter one")
            """,
            "fallback"));

        Assert.Equal("Plan", slide.Title);
        Assert.False(slide.TitleSlide);
        AuthoredParagraph[] blocks = [.. slide.Blocks];
        Assert.Equal(5, blocks.Length);

        Assert.Equal(ParagraphList.None, blocks[0].List);
        Assert.Equal("Scope", Assert.Single(blocks[0].Runs).Text);
        Assert.True(blocks[0].Runs[0].Bold);

        Assert.Equal(ParagraphList.Inherit, blocks[1].List);
        Assert.Equal(
            ["Keep ", "this", " and ", "that", " with ", "code"],
            blocks[1].Runs.Select(static run => run.Text));
        Assert.True(blocks[1].Runs[1].Italic);
        Assert.True(blocks[1].Runs[3].Bold);
        Assert.True(blocks[1].Runs[5].Code);

        Assert.Equal((ParagraphList.Numbered, 0), (blocks[2].List, blocks[2].Level));
        Assert.Equal((ParagraphList.Numbered, 1), (blocks[3].List, blocks[3].Level));
        Assert.Equal("Nested", Assert.Single(blocks[3].Runs).Text);

        Assert.Equal("Use snake_case and the docs.", string.Concat(blocks[4].Runs.Select(static run => run.Text)));
        Assert.All(blocks[4].Runs, static run => Assert.False(run.Italic));

        Assert.Equal(new MarkdownImage("charts/q 1.png", "Chart of revenue", "Quarter one"), slide.Image);
    }

    [Fact]
    public void Parse_ReadsPipeTablesWithAlignmentEscapesAndRaggedRows()
    {
        IReadOnlyList<MarkdownSlide> slides = SlidesMarkdownBuilder.Parse(
            """
            ## Pipeline
            Deals by stage
            | Stage | Owner | Value |
            |:------|:-----:|------:|
            | **Won** | A\|B | 12 |
            | Lost |
            | Open | C | 3 | extra |
            After the table
            a | b
            not a delimiter
            | Second | Table |
            | --- | --- |
            | x | y |
            """,
            "fallback");

        Assert.Equal(["Pipeline", "Pipeline"], slides.Select(static slide => slide.Title));
        MarkdownSlide first = slides[0];
        Assert.Equal(
            ["Deals by stage", "After the table", "a | b", "not a delimiter"],
            first.Blocks.Select(static block => string.Concat(block.Runs.Select(static run => run.Text))));
        MarkdownTable table = Assert.IsType<MarkdownTable>(first.Table);
        Assert.Equal([TextAlignment.Left, TextAlignment.Center, TextAlignment.Right], table.Alignments);
        Assert.Equal(
            [
                ["Stage", "Owner", "Value"],
                ["Won", "A|B", "12"],
                ["Lost", "", ""],
                ["Open", "C", "3"],
            ],
            table.Rows.Select(static row => row.Select(static cell => string.Concat(cell.Select(static run => run.Text))).ToArray()));
        Assert.True(Assert.Single(table.Rows[1][0]).Bold);

        // A slide holds one table; the next one continues on a slide with the same title.
        MarkdownSlide continuation = slides[1];
        Assert.False(continuation.TitleSlide);
        Assert.Empty(continuation.Blocks);
        Assert.Equal([TextAlignment.NotDefined, TextAlignment.NotDefined], continuation.Table!.Alignments);
        Assert.Equal(2, continuation.Table.Rows.Count);
    }

    [Fact]
    public void Parse_TableCellBackslashEscapesOnlyTheNextCharacter()
    {
        MarkdownSlide slide = Assert.Single(SlidesMarkdownBuilder.Parse(
            """
            ## Paths
            | Path | Note |
            |---|---|
            | C:\\| a\|b \* |
            C:\\|
            """,
            "fallback"));

        // In \\| the pipe closes the cell; \| stays a literal pipe; other pairs stay as written.
        MarkdownTable table = Assert.IsType<MarkdownTable>(slide.Table);
        Assert.Equal(
            [
                ["Path", "Note"],
                [@"C:\\", @"a|b \*"],
                [@"C:\\", ""],
            ],
            table.Rows.Select(static row => row.Select(static cell => string.Concat(cell.Select(static run => run.Text))).ToArray()));
    }

    [Fact]
    public void Parse_TitleSlideTableOrPictureContinuesOnANewSlide()
    {
        IReadOnlyList<MarkdownSlide> slides = SlidesMarkdownBuilder.Parse(
            """
            # Deck
            For the board
            | A | B |
            |---|---|
            | 1 | 2 |
            After the table

            # Launch
            ![Logo](logo.png)
            """,
            "fallback");

        Assert.Equal(["Deck", "Deck", "Launch", "Launch"], slides.Select(static slide => slide.Title));
        Assert.Equal([true, false, true, false], slides.Select(static slide => slide.TitleSlide));
        Assert.Equal(
            ["For the board"],
            slides[0].Blocks.Select(static block => string.Concat(block.Runs.Select(static run => run.Text))));
        Assert.False(slides[0].HasObject);
        Assert.NotNull(slides[1].Table);
        Assert.Equal(
            ["After the table"],
            slides[1].Blocks.Select(static block => string.Concat(block.Runs.Select(static run => run.Text))));
        Assert.False(slides[2].HasObject);
        Assert.Equal("logo.png", slides[3].Image?.Path);
    }

    [Fact]
    public void AddTable_MarksTheFirstRowAsTheHeaderRow()
    {
        using var presentation = new Presentation();
        ITable table = SlidesAuthoring.AddTable(presentation.Slides[0], 10, 10, 200, 60, 2, 2);

        Assert.True(table.FirstRow);
    }

    [Fact]
    public void Parse_KeepsPipeLinesWithoutAMatchingDelimiterRowAsText()
    {
        MarkdownSlide slide = Assert.Single(SlidesMarkdownBuilder.Parse(
            """
            ## Notes
            | a | b |
            |---|
            ---
            """,
            "fallback"));

        Assert.Null(slide.Table);
        Assert.Equal(
            ["| a | b |", "|---|"],
            slide.Blocks.Select(static block => string.Concat(block.Runs.Select(static run => run.Text))));
    }

    [Fact]
    public void SetTitle_WritesOnlyTheTextAndLeavesTheTitleAutofit()
    {
        // Shrinking text on overflow does not fix an overflowing title, so set_title never turns it on.
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("outline.md");
        File.WriteAllText(markdown, "# Proposal\n\n## Next steps\n\n- Confirm the pilot site\n");
        string deck = fixture.File("deck.pptx");
        string edited = fixture.File("deck.titled.pptx");
        SlidesCreate.Run(fixture.Session, new NewPresentationRequest { Output = TestOutput.At(deck), MarkdownPath = markdown });
        SlidesEdit.Run(fixture.Session, new PresentationEditRequest
        {
            Input = deck,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetTitleOp { Slide = 2, Text = string.Join(' ', Enumerable.Repeat("A much longer title", 8)) }],
            },
            Output = TestOutput.At(edited),
        });

        using var before = new Presentation(deck);
        using var after = new Presentation(edited);
        IAutoShape original = SlidesPlaceholders.Title(before.Slides[1])!;
        IAutoShape title = SlidesPlaceholders.Title(after.Slides[1])!;
        // Evaluation mode truncates the long text, so only the start that survives it is compared.
        Assert.StartsWith("A muc", title.TextFrame.Text, StringComparison.Ordinal);
        Assert.NotEqual(original.TextFrame.Text, title.TextFrame.Text);
        Assert.NotEqual(TextAutofitType.Normal, title.TextFrame.TextFrameFormat.AutofitType);
        Assert.Equal(original.TextFrame.TextFrameFormat.AutofitType, title.TextFrame.TextFrameFormat.AutofitType);
    }

    [Fact]
    public void SetTitle_WithoutATitlePlaceholder_NeverWritesIntoASubtitleAndReusesItsBox()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("no-title.pptx");
        using (var source = new Presentation())
        {
            ISlide slide = source.Slides[0];
            foreach (IShape shape in slide.Shapes.ToArray())
            {
                slide.Shapes.Remove(shape);
            }

            IAutoShape subtitle = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 300, 300, 40);
            subtitle.Name = "Subtitle 2";
            subtitle.TextFrame.Text = "Keep";
            source.Save(input, SaveFormat.Pptx);
        }

        string output = fixture.File("titled.pptx");
        SlidesEdit.Run(fixture.Session, new PresentationEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetTitleOp { Slide = 1, Text = "One" }, new SetTitleOp { Slide = 1, Text = "Two" }],
            },
            Output = TestOutput.At(output),
        });

        using var deck = new Presentation(output);
        IAutoShape[] shapes = deck.Slides[0].Shapes.OfType<IAutoShape>().ToArray();
        Assert.Equal("Keep", shapes.Single(static shape => shape.Name == "Subtitle 2").TextFrame.Text);
        IAutoShape title = Assert.Single(shapes, static shape => shape.Name == SlidesAuthoring.TitleName);
        Assert.Equal("Two", title.TextFrame.Text);
        Assert.Equal(FillType.NoFill, title.FillFormat.GetEffective().FillType);
        Assert.Equal(FillType.NoFill, title.LineFormat.GetEffective().FillFormat.FillType);
        float width = deck.SlideSize.Size.Width;
        Assert.InRange(title.X + title.Width, 0, width);
    }

    [Fact]
    public void SetBody_InheritsTheLayoutBulletsInsteadOfForcingThem()
    {
        using var fixture = new SlidesEngineFixture();
        using var deck = new Presentation(SetBodyOfAContentSlide(
            fixture,
            new SlidesParagraphInput { Text = "Top" },
            new SlidesParagraphInput { Text = "Sub", Level = 1 }));
        IAutoShape body = Assert.Single(SlidesPlaceholders.Content(deck.Slides[1]));
        Assert.Equal([0, 1], body.TextFrame.Paragraphs.Select(static paragraph => (int)paragraph.ParagraphFormat.Depth));
        Assert.All(
            body.TextFrame.Paragraphs,
            static paragraph => Assert.Equal(BulletType.NotDefined, paragraph.ParagraphFormat.Bullet.Type));
    }

    [Fact]
    public void SetBody_ParagraphWithoutBullet_IsAPlainParagraph()
    {
        using var fixture = new SlidesEngineFixture();
        using var deck = new Presentation(SetBodyOfAContentSlide(
            fixture,
            new SlidesParagraphInput { Text = "Note", Bullet = false },
            new SlidesParagraphInput { Text = "Point" }));
        IAutoShape body = Assert.Single(SlidesPlaceholders.Content(deck.Slides[1]));
        IParagraph[] paragraphs = [.. body.TextFrame.Paragraphs];
        Assert.Equal(BulletType.None, paragraphs[0].ParagraphFormat.GetEffective().Bullet.Type);
        Assert.True(paragraphs[0].ParagraphFormat.GetEffective().Indent >= 0);
        Assert.Equal(BulletType.NotDefined, paragraphs[1].ParagraphFormat.Bullet.Type);
    }

    [Fact]
    public void BodyPlaceholder_IsFilledInPlaceAndAddressedByItsRole()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("outline.md");
        File.WriteAllText(markdown, "## Results\n- Draft\n");
        string input = fixture.File("outline.pptx");
        SlidesCreate.Run(fixture.Session, new NewPresentationRequest { Output = TestOutput.At(input), MarkdownPath = markdown });
        string filled = fixture.File("filled.pptx");
        SlidesEdit.Run(fixture.Session, new PresentationEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetBodyOp { Slide = 1, Paragraphs = [new SlidesParagraphInput { Text = "Final" }] }],
            },
            Output = TestOutput.At(filled),
        });
        string addressed = fixture.File("addressed.pptx");
        SlidesEdit.Run(fixture.Session, new PresentationEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetTextOp { Slide = 1, Placeholder = "body", Text = "Final" }],
            },
            Output = TestOutput.At(addressed),
        });

        foreach (string output in new[] { filled, addressed })
        {
            using var deck = new Presentation(output);
            IAutoShape body = Assert.Single(SlidesPlaceholders.Content(deck.Slides[0]));
            Assert.Equal("Final", body.TextFrame.Text);
            // The layout's content placeholder takes the text; no second shape carries it.
            Assert.Single(deck.Slides[0].Shapes.OfType<IAutoShape>(), static shape => shape.TextFrame?.Text == "Final");
        }
        PresentationReadResult read = SlidesRead.Run(fixture.Session, new PresentationReadRequest
        {
            Input = addressed,
            Slides = PageRange.Parse("1"), Scope = PresentationReadScopes.Shapes,
        });
        Assert.Contains(read.Slides[0].Shapes!, static shape => shape.Placeholder == "body" && shape.Text == "Final");
    }

    /// <summary>Adds a Title and Content slide after the first and sets its body; returns the edited file.</summary>
    private static string SetBodyOfAContentSlide(SlidesEngineFixture fixture, params SlidesParagraphInput[] paragraphs)
    {
        string input = fixture.File("layout.pptx");
        SlidesCreate.Run(fixture.Session, new NewPresentationRequest { Output = TestOutput.At(input) });
        SlidesEdit.Run(fixture.Session, new PresentationEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new AddSlideOp { Layout = "Title and Content" }],
            },
            Output = TestOutput.At(input, overwrite: true),
        });
        string edited = fixture.File("layout.body.pptx");
        SlidesEdit.Run(fixture.Session, new PresentationEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetBodyOp { Slide = 2, Paragraphs = paragraphs }],
            },
            Output = TestOutput.At(edited),
        });
        return edited;
    }
}

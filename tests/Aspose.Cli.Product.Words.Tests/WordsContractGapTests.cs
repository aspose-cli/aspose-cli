using Aspose.Words;
using Aspose.Words.Notes;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsContractGapTests
{
    [Fact]
    public void InsertParagraphs_WithListLevel_CreatesListItemsAtThatLevel()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("list.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new InsertParagraphsOp
                {
                    At = new WordsTarget { Find = "Operations remained" },
                    Position = "after",
                    Paragraphs =
                    [
                        new ParagraphInput { Text = "Point", ListLevel = 0 },
                        new ParagraphInput { Text = "Detail", ListLevel = 1 },
                        new ParagraphInput { Text = "Plain" },
                    ],
                },
            ],
        }, new WordsEditRequest { OutputPath = output });

        Paragraph[] paragraphs = new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>().ToArray();
        Paragraph point = paragraphs.Single(static p => p.GetText().Trim() == "Point");
        Paragraph detail = paragraphs.Single(static p => p.GetText().Trim() == "Detail");
        Assert.True(point.IsListItem);
        Assert.Equal(0, point.ListFormat.ListLevelNumber);
        Assert.Equal(1, detail.ListFormat.ListLevelNumber);
        Assert.Same(point.ListFormat.List, detail.ListFormat.List);
        Assert.False(paragraphs.Single(static p => p.GetText().Trim() == "Plain").IsListItem);
    }

    [Fact]
    public void InsertParagraphs_WithListLevel_TakesTheIndentOfTheListItemsAtThatLevel()
    {
        // Documents converted from RTF often hold a clause's indent on the paragraph rather
        // than on its list level; a new clause lines up with the clauses of its level.
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        Aspose.Words.Lists.List list = source.Lists.Add(Aspose.Words.Lists.ListTemplate.NumberArabicDot);
        list.ListLevels[1].NumberFormat = "\u0000.\u0001";
        list.ListLevels[1].TextPosition = 0;
        list.ListLevels[1].NumberPosition = 0;
        void Clause(string text, int level, double left, double firstLine)
        {
            builder.ListFormat.List = list;
            builder.ListFormat.ListLevelNumber = level;
            builder.ParagraphFormat.LeftIndent = left;
            builder.ParagraphFormat.FirstLineIndent = firstLine;
            builder.Writeln(text);
        }

        Clause("General", 0, 21, -21);
        Clause("Scope clause", 1, 49.35, -28.35);
        Clause("Travel", 0, 21, -21);
        Clause("Second clause", 1, 49.35, -28.35);
        builder.ListFormat.RemoveNumbers();
        builder.ParagraphFormat.LeftIndent = 0;
        builder.ParagraphFormat.FirstLineIndent = 0;
        builder.Write("Closing");
        string input = fixture.Temp.File("clauses.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("clauses-out.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new InsertParagraphsOp
                {
                    At = new WordsTarget { Find = "Second clause" },
                    Position = "after",
                    Paragraphs =
                    [
                        new ParagraphInput { Text = "New clause", ListLevel = 1 },
                        new ParagraphInput { Text = "New chapter", ListLevel = 0 },
                    ],
                },
            ],
        }, new WordsEditRequest { OutputPath = output });

        Paragraph[] paragraphs = new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>().ToArray();
        Paragraph clause = paragraphs.Single(static p => p.GetText().Trim() == "New clause");
        Paragraph chapter = paragraphs.Single(static p => p.GetText().Trim() == "New chapter");
        Assert.Equal(1, clause.ListFormat.ListLevelNumber);
        Assert.Equal(49.35, clause.ParagraphFormat.LeftIndent, 2);
        Assert.Equal(-28.35, clause.ParagraphFormat.FirstLineIndent, 2);
        Assert.Equal(0, chapter.ListFormat.ListLevelNumber);
        Assert.Equal(21, chapter.ParagraphFormat.LeftIndent, 2);
        Assert.Equal(-21, chapter.ParagraphFormat.FirstLineIndent, 2);
    }

    [Fact]
    public void InsertParagraphs_WithListLevel_KeepsFollowingTheLevelWhenItsItemsDo()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.ListFormat.List = source.Lists.Add(Aspose.Words.Lists.ListTemplate.NumberDefault);
        builder.Writeln("Point");
        builder.ListFormat.ListLevelNumber = 1;
        builder.Writeln("Detail");
        builder.ListFormat.RemoveNumbers();
        builder.Write("Closing");
        string input = fixture.Temp.File("plain-list.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("plain-list-out.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new InsertParagraphsOp
                {
                    At = new WordsTarget { Find = "Detail" },
                    Position = "after",
                    Paragraphs = [new ParagraphInput { Text = "New detail", ListLevel = 1 }],
                },
            ],
        }, new WordsEditRequest { OutputPath = output });

        // The new item has no indent of its own: moving the level moves it with the others.
        var document = new Document(output);
        Paragraph[] paragraphs = [.. document.FirstSection.Body.Paragraphs.Cast<Paragraph>()];
        Paragraph detail = paragraphs.Single(static p => p.GetText().Trim() == "Detail");
        Paragraph added = paragraphs.Single(static p => p.GetText().Trim() == "New detail");
        Aspose.Words.Lists.ListLevel level = added.ListFormat.ListLevel;
        level.TextPosition = 90;
        level.NumberPosition = 72;
        Assert.Equal((90, -18), (detail.ParagraphFormat.LeftIndent, detail.ParagraphFormat.FirstLineIndent));
        Assert.Equal((90, -18), (added.ParagraphFormat.LeftIndent, added.ParagraphFormat.FirstLineIndent));
    }

    [Fact]
    public void RegexReplace_HonorsSubstitutions()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("regex.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new ReplaceTextOp { Find = @"(\w+) percent", Replace = "$1 %", Regex = true }],
        }, new WordsEditRequest { OutputPath = output });

        Assert.Contains("twelve %", new Document(output).GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void BodySearch_ExcludesCommentsAndFootnotesAndAllReportsEachHitOnce()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Body needle");
        builder.InsertFootnote(FootnoteType.Footnote, "Footnote needle");
        var comment = new Comment(source, "A", "A", DateTime.Now);
        comment.AppendChild(new Paragraph(source));
        comment.FirstParagraph.AppendChild(new Run(source, "Comment needle"));
        builder.CurrentParagraph.AppendChild(comment);
        string input = fixture.Temp.File("search.docx");
        source.Save(input, SaveFormat.Docx);

        WordsSearchResult body = fixture.Engine.Search(input, WordsFixture.Search("needle", "body"));
        WordsSearchResult all = fixture.Engine.Search(input, WordsFixture.Search("needle", "all"));

        Assert.Equal(["body"], body.Hits.Select(static hit => hit.Scope));
        Assert.Equal(["body", "comments", "footnotes"], all.Hits.Select(static hit => hit.Scope).Order());
    }

    [Fact]
    public void Read_ReportsTheBreakThatFollowsABlock()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Before page");
        builder.ParagraphFormat.PageBreakBefore = true;
        builder.Writeln("Starts a page");
        builder.ParagraphFormat.PageBreakBefore = false;
        builder.Write("Ends section");
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Write("Last");
        string input = fixture.Temp.File("breaks.docx");
        source.Save(input, SaveFormat.Docx);

        DocumentReadResult read = fixture.Engine.Read(input, new DocumentReadRequest { Scope = "text" });

        string? BreakAfter(string text) => read.Blocks.Single(block => block.Text == text).BreakAfter;
        Assert.Equal("page", BreakAfter("Before page"));
        Assert.Null(BreakAfter("Starts a page"));
        Assert.Equal("section", BreakAfter("Ends section"));
        Assert.Null(BreakAfter("Last"));
    }

    [Fact]
    public void Read_ReportsAPageBreakThatStartsAParagraphAfterTheBlockBeforeIt()
    {
        // A document converted from PDF starts each page's first paragraph with its page break.
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Page one");
        builder.Writeln("\f第五条 违约责任");
        builder.Write("Body");
        string input = fixture.Temp.File("leading-break.docx");
        source.Save(input, SaveFormat.Docx);

        DocumentReadResult read = fixture.Engine.Read(input, new DocumentReadRequest { Scope = "full" });

        Assert.Equal(["Page one", "第五条 违约责任", "Body"], read.Blocks.Select(static block => block.Text));
        Assert.Equal(["page", null, null], read.Blocks.Select(static block => block.BreakAfter));
        Assert.Equal("第五条 违约责任", string.Concat(read.Blocks[1].Runs!.Select(static run => run.Text)));
    }
}

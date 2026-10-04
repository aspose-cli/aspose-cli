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

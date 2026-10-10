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

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
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
            },
            Output = TestOutput.At(output),
        });

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
    public void InsertParagraphs_AtOneAnchor_StackAfterItInReverseAndBeforeItInBatchOrder()
    {
        // The editing reference documents this order; anchoring every piece before the next
        // block keeps the batch order as the reading order.
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("stacked.docx");
        static InsertParagraphsOp Insert(string text, string position) => new()
        {
            At = new WordsTarget { Find = "Operations remained" },
            Position = position,
            Paragraphs = [new ParagraphInput { Text = text }],
        };

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [Insert("After one", "after"), Insert("After two", "after"), Insert("Before one", "before"), Insert("Before two", "before")],
            },
            Output = TestOutput.At(output),
        });

        string[] texts = new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>()
            .Select(static p => p.GetText().Trim())
            .Where(static text => text.Contains(" one", StringComparison.Ordinal) || text.Contains(" two", StringComparison.Ordinal) || text.StartsWith("Operations remained", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(["Before one", "Before two"], texts[..2]);
        Assert.StartsWith("Operations remained", texts[2], StringComparison.Ordinal);
        Assert.Equal(["After two", "After one"], texts[3..]);
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

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
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
            },
            Output = TestOutput.At(output),
        });

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
    public void InsertParagraphs_WithListLevel_JoinsTheNestedListAtThatLevel()
    {
        // Markdown nesting makes each level its own list; a new item at another level than the
        // anchor's joins the list its level uses around the anchor, numbered like its siblings.
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("nested.md");
        File.WriteAllText(markdown, "# T\n\n1. First\n    1. Sub A\n    2. Sub B\n2. Second\n    1. Sub C\n");
        string input = fixture.Temp.File("nested.docx");
        WordsCreate.Run(fixture.Session, new NewDocumentRequest { Output = TestOutput.At(input), MarkdownPath = markdown });
        string output = fixture.Temp.File("nested-out.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertParagraphsOp
                    {
                        At = new WordsTarget { Find = "First" },
                        Position = "after",
                        Paragraphs = [new ParagraphInput { Text = "New sub", ListLevel = 1 }],
                    },
                    new InsertParagraphsOp
                    {
                        At = new WordsTarget { Find = "Sub C" },
                        Position = "after",
                        Paragraphs =
                        [
                            new ParagraphInput { Text = "Later sub", ListLevel = 1 },
                            new ParagraphInput { Text = "Third", ListLevel = 0 },
                        ],
                    },
                    new InsertParagraphsOp
                    {
                        At = new WordsTarget { Find = "Second" },
                        Position = "before",
                        Paragraphs = [new ParagraphInput { Text = "Between", ListLevel = 0 }],
                    },
                ],
            },
            Output = TestOutput.At(output),
        });

        var document = new Document(output);
        document.UpdateListLabels();
        Paragraph[] paragraphs = [.. document.FirstSection.Body.Paragraphs.Cast<Paragraph>()];
        Paragraph Item(string text) => paragraphs.Single(p => p.GetText().Trim() == text);
        void SameAs(string sibling, string added)
        {
            Paragraph expected = Item(sibling);
            Paragraph item = Item(added);
            Assert.True(item.IsListItem, added);
            Assert.Equal(expected.ListFormat.List.ListId, item.ListFormat.List.ListId);
            Assert.Equal(expected.ListFormat.ListLevelNumber, item.ListFormat.ListLevelNumber);
            Assert.Equal(expected.ParagraphFormat.LeftIndent, item.ParagraphFormat.LeftIndent, 2);
            Assert.Equal(expected.ParagraphFormat.FirstLineIndent, item.ParagraphFormat.FirstLineIndent, 2);
        }

        SameAs("Sub A", "New sub");
        SameAs("Sub C", "Later sub");
        SameAs("First", "Between");
        SameAs("First", "Third");
        Assert.Equal(
            ["1.", "1.", "2.", "3.", "2.", "3.", "1.", "2.", "4."],
            new[] { "First", "New sub", "Sub A", "Sub B", "Between", "Second", "Sub C", "Later sub", "Third" }
                .Select(text => Item(text).ListLabel.LabelString));
    }

    [Fact]
    public void InsertParagraphs_WithListLevel_StartsABulletListWhenNoListNumbersThatLevel()
    {
        // A Markdown list defines only its own level; a deeper item without siblings at its
        // level still becomes a visible, indented list item rather than a plain paragraph.
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("flat.md");
        File.WriteAllText(markdown, "1. First\n2. Second\n");
        string input = fixture.Temp.File("flat.docx");
        WordsCreate.Run(fixture.Session, new NewDocumentRequest { Output = TestOutput.At(input), MarkdownPath = markdown });
        string output = fixture.Temp.File("flat-out.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertParagraphsOp
                    {
                        At = new WordsTarget { Find = "First" },
                        Position = "after",
                        Paragraphs = [new ParagraphInput { Text = "Detail", ListLevel = 1 }],
                    },
                ],
            },
            Output = TestOutput.At(output),
        });

        var document = new Document(output);
        document.UpdateListLabels();
        Paragraph[] paragraphs = [.. document.FirstSection.Body.Paragraphs.Cast<Paragraph>()];
        Paragraph first = paragraphs.Single(static p => p.GetText().Trim() == "First");
        Paragraph detail = paragraphs.Single(static p => p.GetText().Trim() == "Detail");
        Assert.True(detail.IsListItem);
        Assert.Equal(1, detail.ListFormat.ListLevelNumber);
        Assert.NotEqual(first.ListFormat.List.ListId, detail.ListFormat.List.ListId);
        Assert.NotEmpty(detail.ListLabel.LabelString);
        Assert.True(detail.ParagraphFormat.LeftIndent > first.ParagraphFormat.LeftIndent);
        Assert.Equal("2.", paragraphs.Single(static p => p.GetText().Trim() == "Second").ListLabel.LabelString);
    }

    [Fact]
    public void InsertParagraphs_WithListLevel_TakesTheRunFormatOfItsLevel()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.ListFormat.List = source.Lists.Add(Aspose.Words.Lists.ListTemplate.NumberDefault);
        builder.Font.Bold = true;
        builder.Writeln("Chapter");
        builder.Font.Bold = false;
        builder.ListFormat.ListLevelNumber = 1;
        builder.Writeln("Detail");
        builder.ListFormat.RemoveNumbers();
        builder.Write("Closing");
        string input = fixture.Temp.File("bold-list.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("bold-list-out.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertParagraphsOp
                    {
                        At = new WordsTarget { Find = "Chapter" },
                        Position = "after",
                        Paragraphs = [new ParagraphInput { Text = "First detail", ListLevel = 1 }],
                    },
                ],
            },
            Output = TestOutput.At(output),
        });

        Paragraph added = new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>()
            .Single(static p => p.GetText().Trim() == "First detail");
        Assert.Equal(1, added.ListFormat.ListLevelNumber);
        Assert.False(added.Runs[0].Font.Bold);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void InsertParagraphs_WithListLevel_KeepsTheFormatOfLaterItemsOfTheOperation(bool markdown, bool trackChanges)
    {
        // The second item finds the first as its level's nearest item; that item is a tracked
        // insertion when changes are tracked, so it must not be the format the second continues.
        using var fixture = new WordsFixture();
        string input = fixture.Temp.File("items.docx");
        if (markdown)
        {
            string source = fixture.Temp.File("items.md");
            File.WriteAllText(source, "1. **First**\n2. Second\n");
            WordsCreate.Run(fixture.Session, new NewDocumentRequest { Output = TestOutput.At(input), MarkdownPath = source });
        }
        else
        {
            var source = new Document();
            var builder = new DocumentBuilder(source);
            builder.ListFormat.List = source.Lists.Add(Aspose.Words.Lists.ListTemplate.NumberDefault);
            builder.Font.Name = "Courier New";
            builder.Font.Size = 14;
            builder.Writeln("First");
            builder.Write("Second");
            source.Save(input, SaveFormat.Docx);
        }

        string output = fixture.Temp.File("items-out.docx");
        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertParagraphsOp
                    {
                        At = new WordsTarget { Find = markdown ? "First" : "Second" },
                        Position = markdown ? "after" : "before",
                        Paragraphs =
                        [
                            new ParagraphInput { Text = "Detail one", ListLevel = 1 },
                            new ParagraphInput { Text = "Detail two", ListLevel = 1 },
                        ],
                    },
                ],
            },
            Output = TestOutput.At(output),
            TrackChanges = trackChanges,
            Author = "Reviewer",
        });

        Paragraph[] paragraphs = [.. new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>()];
        Paragraph first = paragraphs.Single(static p => p.GetText().Trim() == "Detail one");
        Paragraph second = paragraphs.Single(static p => p.GetText().Trim() == "Detail two");
        Assert.True(second.IsListItem);
        Assert.Equal(first.ListFormat.List.ListId, second.ListFormat.List.ListId);
        Assert.Equal(1, second.ListFormat.ListLevelNumber);
        Assert.Equal(first.ParagraphFormat.StyleName, second.ParagraphFormat.StyleName);
        Assert.Equal(first.ParagraphFormat.LeftIndent, second.ParagraphFormat.LeftIndent, 2);
        Assert.Equal(markdown, first.Runs[0].Font.Bold);
        Assert.Equal(first.Runs[0].Font.Bold, second.Runs[0].Font.Bold);
        Assert.Equal(first.Runs[0].Font.Name, second.Runs[0].Font.Name);
        Assert.Equal(first.Runs[0].Font.Size, second.Runs[0].Font.Size);
        if (!markdown)
        {
            Assert.Equal(14, second.Runs[0].Font.Size);
        }
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

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
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
            },
            Output = TestOutput.At(output),
        });

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

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new ReplaceTextOp { Find = @"(\w+) percent", Replace = "$1 %", Regex = true }],
            },
            Output = TestOutput.At(output),
        });

        Assert.Contains("twelve %", new Document(output).GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceText_WarnsWhenItMatchesNothing()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("unmatched.docx");

        WordsEditResult result = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new ReplaceTextOp { Find = "eleven percent", Replace = "ten percent" },
                    new ReplaceTextOp { Find = "twelve", Replace = "ten" },
                ],
            },
            Output = TestOutput.At(output),
        });

        Assert.Equal([0L, 1L], result.Applied.Select(static applied => applied.ItemsAffected));
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "REPLACE_NO_MATCH");
        Assert.Contains("replace_text matched no text", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("eleven", warning.Message + warning.Hint, StringComparison.Ordinal);
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

        WordsSearchResult body = WordsSearch.Run(fixture.Session, WordsFixture.Search(input, "needle", "body"));
        WordsSearchResult all = WordsSearch.Run(fixture.Session, WordsFixture.Search(input, "needle", "all"));

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

        DocumentReadResult read = WordsRead.Run(fixture.Session, new DocumentReadRequest { Input = input, Scope = "text" });

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

        DocumentReadResult read = WordsRead.Run(fixture.Session, new DocumentReadRequest { Input = input, Scope = "full" });

        Assert.Equal(["Page one", "第五条 违约责任", "Body"], read.Blocks.Select(static block => block.Text));
        Assert.Equal(["page", null, null], read.Blocks.Select(static block => block.BreakAfter));
        Assert.Equal("第五条 违约责任", string.Concat(read.Blocks[1].Runs!.Select(static run => run.Text)));
    }
}

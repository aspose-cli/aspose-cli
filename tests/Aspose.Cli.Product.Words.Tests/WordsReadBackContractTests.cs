using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// What the operations write reads back under the operations' names: an image takes exactly the
/// width and height given, a paragraph's list reads as the <c>list</c> it was given, a break as
/// <c>break.kind</c>, and a revision's date as a UTC date-time.
/// </summary>
public sealed class WordsReadBackContractTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [Fact]
    public void InsertImage_WithWidthAndHeight_TakesExactlyBoth()
    {
        using var fixture = new WordsFixture();
        string image = fixture.Temp.File("dot.png");
        File.WriteAllBytes(image, Convert.FromBase64String(Png));

        string output = Edit(fixture, new InsertImageOp { At = new WordsTarget { Block = 1 }, Position = "after", Path = image, Width = 50, Height = 40 });

        WordsBlockImageData read = Assert.Single(Read(fixture, output).Blocks.SelectMany(static block => block.Images ?? []));
        Assert.Equal((50d, 40d), (read.Width, read.Height));
    }

    [Fact]
    public void InsertParagraphs_List_ReadsBackAsTheListItWasGiven()
    {
        using var fixture = new WordsFixture();

        string output = Edit(fixture, new InsertParagraphsOp
        {
            At = new WordsTarget { Block = 1 },
            Position = "after",
            Paragraphs = [new WordsParagraphInput { Text = "First step", List = new WordsListInput { Kind = "number", Level = 1 } }],
        });

        WordsBlockData block = Read(fixture, output).Blocks.Single(static block => block.Text == "First step");
        Assert.Equal(("number", 1), (block.List!.Kind, block.List.Level));
    }

    [Fact]
    public void InsertBreak_ReadsBackAsTheBreakAfterTheBlock()
    {
        using var fixture = new WordsFixture();

        string output = Edit(fixture, new InsertBreakOp { At = new WordsTarget { Block = 1 }, Position = "after", Kind = "page" });

        Assert.Contains(Read(fixture, output).Blocks, static block => block.Break?.Kind == "page");
    }

    [Fact]
    public void TrackedEdit_RecordsItsRevisionsAtTheUtcTimeOfTheEdit_AndReadsThemBack()
    {
        using var fixture = new WordsFixture();
        string output = fixture.Temp.File("tracked.docx");
        DateTimeOffset before = DateTimeOffset.UtcNow;

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = fixture.CreateReport(),
            Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "Revenue", Replace = "Income" }] },
            Output = TestOutput.At(output),
            TrackChanges = true,
            Author = "Reviewer",
        });
        DateTimeOffset after = DateTimeOffset.UtcNow;

        WordsRevisionData[] revisions = [.. WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = output, Details = ["revisions"] }).Revisions!];
        Assert.NotEmpty(revisions);
        Assert.All(revisions, revision =>
        {
            Assert.Equal(TimeSpan.Zero, revision.Date!.Value.Offset);
            Assert.InRange(revision.Date.Value, before.AddSeconds(-2), after.AddSeconds(2));
        });
    }

    [Fact]
    public void Revisions_ReportTheDateTheFileDeclaresAsUtc()
    {
        using var fixture = new WordsFixture();
        string input = fixture.Temp.File("tracked.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Base text.");
        document.StartTrackRevisions("Reviewer", new DateTime(2026, 9, 1, 10, 30, 0, DateTimeKind.Utc));
        builder.Write("Added.");
        document.StopTrackRevisions();
        document.Save(input);

        WordsRevisionData revision = Assert.Single(WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = input, Details = ["revisions"] }).Revisions!);

        Assert.Equal(new DateTimeOffset(2026, 9, 1, 10, 30, 0, TimeSpan.Zero), revision.Date);
    }

    [Fact]
    public void AddComment_RecordsTheUtcTimeOfTheEdit()
    {
        using var fixture = new WordsFixture();
        DateTime before = DateTime.UtcNow;

        string output = Edit(fixture, new AddCommentOp { Target = new WordsTarget { Block = 1 }, Author = "Reviewer", Text = "Check" });

        DateTime after = DateTime.UtcNow;
        Comment comment = Assert.Single(new Document(output).GetChildNodes(NodeType.Comment, true).Cast<Comment>());
        Assert.InRange(comment.DateTime, before.AddSeconds(-2), after.AddSeconds(2));
    }

    [Fact]
    public void Compare_RecordsTheRedlineAtTheUtcTimeOfTheRun()
    {
        using var fixture = new WordsFixture();
        string left = fixture.CreateReport("left.docx");
        string right = fixture.Temp.File("right.docx");
        var changed = new Document(left);
        changed.Range.Replace("Revenue", "Income");
        changed.Save(right);
        string output = fixture.Temp.File("redline.docx");
        DateTime before = DateTime.UtcNow;

        WordsCompare.Run(fixture.Session, new WordsCompareRequest { Left = left, Right = right, Output = TestOutput.At(output) });

        DateTime after = DateTime.UtcNow;
        Revision[] revisions = [.. new Document(output).Revisions];
        Assert.NotEmpty(revisions);
        Assert.All(revisions, revision => Assert.InRange(revision.DateTime, before.AddSeconds(-2), after.AddSeconds(2)));
    }

    [Theory]
    [InlineData("Title")]
    [InlineData("custom:Status")]
    public void SetProperties_ACustomNameAReadKeyCouldNotTellApart_IsRefused(string name)
    {
        using var fixture = new WordsFixture();

        CliException error = Assert.Throws<CliException>(() => Edit(
            fixture, new SetPropertiesOp { Custom = new Dictionary<string, string?> { [name] = "Final" } }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(name, error.Message, StringComparison.Ordinal);
    }

    private static string Edit(WordsFixture fixture, WordsOp op)
    {
        string output = fixture.Temp.File($"edited-{Guid.NewGuid():N}.docx");
        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = fixture.CreateReport(),
            Batch = new WordsOpsBatch { Ops = [op] },
            Output = TestOutput.At(output),
        });
        return output;
    }

    private static WordsReadResult Read(WordsFixture fixture, string path) =>
        WordsRead.Run(fixture.Session, new WordsReadRequest { Input = path, Content = "full", MaxBlocks = 100 });
}

using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsFieldAndReviewTests
{
    [Fact]
    public void InsertToc_UpdatesOnlyTheInsertedTableOfContents()
    {
        using var fixture = new WordsFixture();
        fixture.Gate.EnsureApplied();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("Scope");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        Field date = builder.InsertField("DATE", "Stored");
        builder.Writeln();
        string input = fixture.Temp.File("fields.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("toc.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new InsertTocOp { At = new WordsTarget { Heading = "Scope" }, Position = "before" }],
        }, new WordsEditRequest { OutputPath = output });

        var document = new Document(output);
        Field[] fields = document.Range.Fields.Cast<Field>().ToArray();
        Assert.Equal("Stored", Assert.Single(fields, static field => field.Type == FieldType.FieldDate).Result);
        FieldToc toc = Assert.Single(fields.OfType<FieldToc>());
        Assert.Contains("Scope", toc.Result, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveComments_RemovesEveryCommentAndItsRange()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string commented = fixture.Temp.File("commented.docx");
        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = Enumerable.Range(1, 3)
                .Select(static block => (WordsOp)new AddCommentOp { At = new WordsTarget { Block = block }, Author = "A", Text = "Note" })
                .ToArray(),
        }, new WordsEditRequest { OutputPath = commented });
        string output = fixture.Temp.File("clean.docx");

        WordsEditResult result = fixture.Engine.ApplyOps(
            commented,
            new WordsOpsBatch { Ops = [new RemoveCommentsOp()] },
            new WordsEditRequest { OutputPath = output });

        Assert.Equal(3, Assert.Single(result.Applied).ItemsAffected);
        var document = new Document(output);
        Assert.Equal(0, document.GetChildNodes(NodeType.Comment, true).Count);
        Assert.Equal(0, document.GetChildNodes(NodeType.CommentRangeStart, true).Count);
        Assert.Equal(0, document.GetChildNodes(NodeType.CommentRangeEnd, true).Count);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("Ann", 1)]
    public void AcceptRevisions_AcceptsAllOrOneAuthorsRevisions(string? author, int remaining)
    {
        using var fixture = new WordsFixture();
        fixture.Gate.EnsureApplied();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Base");
        foreach (string name in new[] { "Ann", "Bob" })
        {
            source.StartTrackRevisions(name);
            builder.Writeln($"By {name}");
            source.StopTrackRevisions();
        }

        string input = fixture.Temp.File("revisions.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("accepted.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new AcceptRevisionsOp { Author = author }],
        }, new WordsEditRequest { OutputPath = output });

        var document = new Document(output);
        Assert.Equal(remaining, document.Revisions.Select(static revision => revision.Author).Distinct().Count());
        Assert.All(document.Revisions, static revision => Assert.Equal("Bob", revision.Author));
    }
}

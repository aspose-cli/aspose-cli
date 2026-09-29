using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsTrackChangesTests
{
    [Fact]
    public void TrackedBatch_WithAnUntrackableOperation_IsRejectedBeforeAnyChange()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("tracked.docx");

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops =
                [
                    new ReplaceTextOp { Find = "twelve", Replace = "fifteen" },
                    new FormatTextOp { Target = new WordsTarget { Block = 1 }, Bold = true },
                    new SetPageSetupOp { Setup = new PageSetupInput { Orientation = "landscape" } },
                ],
            },
            new WordsEditRequest { OutputPath = output, TrackChanges = true, Author = "Reviewer" }));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Contains("format_text, set_page_setup", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void TrackedBatch_OfContentEdits_RecordsRevisions()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("tracked.docx");

        fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops =
                [
                    new ReplaceTextOp { Find = "twelve", Replace = "fifteen" },
                    new AddCommentOp { At = new WordsTarget { Block = 1 }, Author = "Reviewer", Text = "Check" },
                ],
            },
            new WordsEditRequest { OutputPath = output, TrackChanges = true, Author = "Reviewer" });

        var document = new Document(output);
        Assert.True(document.HasRevisions);
        Assert.All(document.Revisions, static revision => Assert.Equal("Reviewer", revision.Author));
    }

    [Fact]
    public void InspectRevisions_ListsEachChangeInDocumentOrder()
    {
        using var fixture = new WordsFixture();
        string input = fixture.Temp.File("reviewed.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("The notice period is thirty days.");
        builder.Write("This clause is removed.");
        document.StartTrackRevisions("Alice Legal", new DateTime(2026, 9, 1, 10, 30, 0));
        document.Range.Replace("thirty", "sixty");
        document.StopTrackRevisions();
        document.StartTrackRevisions("Bob Counsel", new DateTime(2026, 9, 2, 8, 0, 0));
        ((Paragraph)document.FirstSection.Body.Paragraphs[1]).Runs[0].Remove();
        builder.MoveToDocumentEnd();
        builder.Write("New ");
        builder.Font.Bold = true;
        builder.Write("governing law");
        builder.Font.Bold = false;
        builder.Write(" clause.");
        document.StopTrackRevisions();
        document.Save(input);

        DocumentInfoResult info = fixture.Engine.GetInfo(input, new DocumentInfoRequest { Details = ["revisions"] });

        Assert.Equal(
            [
                new RevisionData { Type = "deletion", Author = "Alice Legal", Date = "2026-09-01T10:30:00", Block = 1, Text = "thirty" },
                new RevisionData { Type = "insertion", Author = "Alice Legal", Date = "2026-09-01T10:30:00", Block = 1, Text = "sixty" },
                new RevisionData { Type = "deletion", Author = "Bob Counsel", Date = "2026-09-02T08:00:00", Block = 2, Text = "This clause is removed." },
                new RevisionData { Type = "insertion", Author = "Bob Counsel", Date = "2026-09-02T08:00:00", Block = 2, Text = "New governing law clause." },
            ],
            info.Revisions);
    }
}

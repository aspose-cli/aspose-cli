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
}

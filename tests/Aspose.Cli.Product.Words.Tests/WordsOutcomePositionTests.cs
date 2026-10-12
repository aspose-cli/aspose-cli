using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// What an edit reports it changed is where the output holds it: each operation's targets are the
/// blocks it changed, numbered as <c>words read</c> numbers the output, in document order, and
/// <c>pagesTouched</c> are the output pages of what the batch changed, empty when it changed nothing.
/// </summary>
public sealed class WordsOutcomePositionTests
{
    [Fact]
    public void Targets_NameTheChangedBlockWhereTheOutputHoldsIt_AfterALaterInsertionMovedIt()
    {
        using var fixture = new WordsFixture();

        WordsEditResult result = Edit(
            fixture,
            fixture.CreateReport(),
            new ReplaceTextOp { Find = "twelve", Replace = "fifteen" },
            new InsertParagraphsOp { At = new WordsTarget { Block = 1 }, Position = "before", Paragraphs = [new WordsParagraphInput { Text = "Draft" }] });

        int block = Read(fixture, result.Output!.Path).Single(static block => block.Text?.Contains("fifteen", StringComparison.Ordinal) == true).Block;
        Assert.Equal([$"block/{block}"], result.Applied[0].Targets);
        Assert.Equal(["block/1"], result.Applied[1].Targets);
    }

    [Fact]
    public void AcceptRevisions_ThatRemoveABlockBeforeAChangedOne_CountEachBlockOnceInDocumentOrder()
    {
        using var fixture = new WordsFixture();
        string tracked = Edit(
            fixture,
            Paragraphs(fixture, "Alpha.", "Beta.", "Gamma.", "Delta."),
            trackChanges: true,
            new DeleteBlocksOp { Target = new WordsTarget { Block = 2 } },
            new ReplaceTextOp { Find = "Delta", Replace = "Omega" }).Output!.Path;

        BoundedOperationOutcome outcome = Assert.Single(Edit(fixture, tracked, new AcceptRevisionsOp()).Applied);

        // Beta is gone, so its target is its number in the input; Omega is block 3 of the output.
        Assert.Equal(2L, outcome.ItemsAffected);
        Assert.Equal(["block/2", "block/3"], outcome.Targets);
    }

    [Fact]
    public void PagesTouched_OfABatchThatChangedNothing_IsOmitted()
    {
        using var fixture = new WordsFixture();
        string numbered = Edit(fixture, ThreePages(fixture), new SetPageNumbersOp { Location = "footer" }).Output!.Path;

        WordsEditResult result = Edit(fixture, numbered, new SetPageNumbersOp { Location = "footer" });

        Assert.Equal(0L, Assert.Single(result.Applied).ItemsAffected);
        Assert.Null(result.PagesTouched);
    }

    [Fact]
    public void PagesTouched_OfAHeader_IsEveryPageOfItsSection_AndOfAPropertyNone()
    {
        using var fixture = new WordsFixture();
        string input = ThreePages(fixture);

        Assert.Equal([1, 2, 3], Edit(fixture, input, new SetHeaderOp { Paragraphs = [new WordsParagraphInput { Text = "Draft" }] }).PagesTouched);
        Assert.Null(Edit(fixture, input, new SetPropertiesOp { Title = "Report" }).PagesTouched);
    }

    [Fact]
    public void PagesTouched_OfAWatermark_IsEveryPage()
    {
        using var fixture = new WordsFixture();

        WordsEditResult result = Edit(fixture, ThreePages(fixture), new SetWatermarkOp { Text = "DRAFT" });

        Assert.Equal([1, 2, 3], result.PagesTouched);
    }

    [Fact]
    public void PagesTouched_OfAcceptedRevisions_AreThePagesOfEveryBlockTheyChanged()
    {
        using var fixture = new WordsFixture();
        string tracked = Edit(
            fixture,
            ThreePages(fixture),
            trackChanges: true,
            new ReplaceTextOp { Find = "one", Replace = "uno" },
            new ReplaceTextOp { Find = "three", Replace = "tres" }).Output!.Path;

        WordsEditResult result = Edit(fixture, tracked, new AcceptRevisionsOp());

        Assert.Equal([1, 3], result.PagesTouched);
    }

    [Fact]
    public void PagesTouched_OfAStyleDefinition_AreThePagesOfTheTextInThatStyle()
    {
        using var fixture = new WordsFixture();

        WordsEditResult result = Edit(fixture, ThreePages(fixture), new DefineStyleOp { Name = "Heading 1", Color = "#C00000" });

        Assert.Equal([2], result.PagesTouched);
    }

    /// <summary>Three pages: body text on the first and third, a Heading 1 paragraph on the second.</summary>
    private static string ThreePages(WordsFixture fixture)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Page one.");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.ParagraphFormat.PageBreakBefore = true;
        builder.Writeln("Page two heading");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        builder.ParagraphFormat.PageBreakBefore = true;
        builder.Write("Page three.");
        string path = fixture.Temp.File($"pages-{Guid.NewGuid():N}.docx");
        document.Save(path);
        return path;
    }

    private static string Paragraphs(WordsFixture fixture, params string[] texts)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write(string.Join("\r", texts));
        string path = fixture.Temp.File($"paragraphs-{Guid.NewGuid():N}.docx");
        document.Save(path);
        return path;
    }

    private static IReadOnlyList<WordsBlockData> Read(WordsFixture fixture, string path) =>
        WordsRead.Run(fixture.Session, new WordsReadRequest { Input = path, Content = "text", MaxBlocks = 100 }).Blocks;

    private static WordsEditResult Edit(WordsFixture fixture, string input, params WordsOp[] ops) =>
        Edit(fixture, input, trackChanges: false, ops);

    private static WordsEditResult Edit(WordsFixture fixture, string input, bool trackChanges, params WordsOp[] ops) =>
        fixture.Disclosed(session => WordsEdit.Run(session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = ops },
            Output = TestOutput.At(fixture.Temp.File($"edited-{Guid.NewGuid():N}.docx")),
            TrackChanges = trackChanges,
            Author = trackChanges ? "Reviewer" : null,
        }));
}

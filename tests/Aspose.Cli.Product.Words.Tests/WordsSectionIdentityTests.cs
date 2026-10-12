using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsSectionIdentityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DeletingASection_InvalidatesItsOriginalBlockForLaterOperations(bool bestEffort, bool dryRun)
    {
        using var fixture = new WordsFixture();
        string input = CreateSections(fixture);
        byte[] original = File.ReadAllBytes(input);
        string output = fixture.Temp.File("invalid.docx");
        var batch = new WordsOpsBatch
        {
            Ops =
            [
                new DeleteSectionOp { Section = 1 },
                new SetTextOp { At = new WordsTarget { Find = "First" }, Text = "Lost" },
            ],
        };
        WordsEditResult Run() => WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = batch,
            Output = TestOutput.At(output),
            Options = new EditCommandOptions { BestEffort = bestEffort, DryRun = dryRun },
        });

        Assert.Equal(original, File.ReadAllBytes(input));
        if (!bestEffort)
        {
            CliException error = Assert.Throws<CliException>(Run);
            Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
            Assert.Equal("op-0002", error.Details!["id"]!.GetValue<string>());
            Assert.False(File.Exists(output));
            return;
        }

        // Best effort keeps the deletion and reports the operation that addressed what it deleted.
        WordsEditResult result = Run();
        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed], result.Applied.Select(static applied => applied.Status));
        Assert.Equal(ErrorCodes.OpsInvalid.Name, result.Applied[1].Error!.Code);
        Assert.Equal(!dryRun, File.Exists(output));
        Assert.Equal(original, File.ReadAllBytes(input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedSectionDeletion_DoesNotShiftToAnotherOriginalSection(bool bestEffort)
    {
        using var fixture = new WordsFixture();
        string input = CreateSections(fixture);
        string output = fixture.Temp.File("repeated.docx");
        int sections = new Document(input).Sections.Count;
        WordsEditResult Run() => WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new DeleteSectionOp { Section = 1 }, new DeleteSectionOp { Section = 1 }],
            },
            Output = TestOutput.At(output),
            Options = new EditCommandOptions { BestEffort = bestEffort },
        });

        if (!bestEffort)
        {
            Assert.Throws<CliException>(Run);
            Assert.False(File.Exists(output));
            return;
        }

        // The second deletion names the section the first deleted, never the next one.
        WordsEditResult result = Run();
        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed], result.Applied.Select(static applied => applied.Status));
        Assert.Equal(sections - 1, new Document(output).Sections.Count);
    }

    [Fact]
    public void SectionDeletion_RejectsAnEarlierOverlappingBlockDeletion()
    {
        using var fixture = new WordsFixture();
        string input = CreateSections(fixture);
        string output = fixture.Temp.File("overlap.docx");
        Assert.Throws<CliException>(() => WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new DeleteBlocksOp { Target = new WordsTarget { Find = "First" } },
                    new DeleteSectionOp { Section = 1 },
                ],
            },
            Output = TestOutput.At(output),
        }));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void InsertionsAndDeletions_PreserveOriginalSectionAndBlockTargets()
    {
        using var fixture = new WordsFixture();
        string input = CreateSections(fixture);
        string output = fixture.Temp.File("shifted.docx");
        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new AddSectionOp { Position = "start" },
                    new DeleteSectionOp { Section = 2 },
                    new SetHeaderOp { Section = 3, Paragraphs = ["Head"] },
                    new SetFooterOp { Section = 3, Paragraphs = ["Foot"] },
                    new SetPageSetupOp { Section = 3, Setup = new WordsPageSetupInput { Orientation = "landscape" } },
                    new SetPageNumbersOp { Section = 3, Start = 7 },
                    new SetTextOp { At = new WordsTarget { Find = "Third" }, Text = "Safe" },
                    new AddSectionOp { Position = "after", After = 1 },
                ],
            },
            Output = TestOutput.At(output),
        });
        var reopened = new Document(output);
        Assert.Equal(4, reopened.Sections.Count);
        Assert.Contains("First", reopened.Sections[1].Body.GetText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Second", reopened.GetText(), StringComparison.Ordinal);
        Section third = reopened.Sections[3];
        Assert.Contains("Safe", third.Body.GetText(), StringComparison.Ordinal);
        Assert.Equal(Orientation.Landscape, third.PageSetup.Orientation);
        Assert.Equal(7, third.PageSetup.PageStartingNumber);
        Assert.Contains("Head", third.HeadersFooters[HeaderFooterType.HeaderPrimary].GetText(), StringComparison.Ordinal);
        Assert.Contains("Foot", third.HeadersFooters[HeaderFooterType.FooterPrimary].GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void DistinctOriginalSectionDeletes_RetainTheCorrectSection()
    {
        using var fixture = new WordsFixture();
        string input = CreateSections(fixture);
        string output = fixture.Temp.File("remaining.docx");
        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new DeleteSectionOp { Section = 1 }, new DeleteSectionOp { Section = 2 }],
            },
            Output = TestOutput.At(output),
        });
        var reopened = new Document(output);
        Assert.Single(reopened.Sections.Cast<Section>());
        Assert.Contains("Third", reopened.GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void OmittedSectionSelection_ExcludesSectionsInsertedInTheBatch()
    {
        using var fixture = new WordsFixture();
        string input = CreateSections(fixture);
        string output = fixture.Temp.File("all-original.docx");
        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new AddSectionOp { Position = "start" }, new SetHeaderOp { Paragraphs = ["All"] }],
            },
            Output = TestOutput.At(output),
        });
        var reopened = new Document(output);
        Assert.Equal(4, reopened.Sections.Count);
        fixture.AssertNoOwnHeader(reopened.FirstSection);
        Assert.All(reopened.Sections.Cast<Section>().Skip(1), section =>
            Assert.Contains("All", section.HeadersFooters[HeaderFooterType.HeaderPrimary].GetText(), StringComparison.Ordinal));
    }

    [Fact]
    public void BestEffort_LastSectionFailureCanBeFollowedByAnIndependentSuccess()
    {
        using var fixture = new WordsFixture();
        string input = CreateSections(fixture);
        string output = fixture.Temp.File("partial.docx");
        WordsEditResult result = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new DeleteSectionOp { Section = 1 }, new DeleteSectionOp { Section = 2 },
                    new DeleteSectionOp { Section = 3 }, new SetPropertiesOp { Title = "Kept" },
                ],
            },
            Output = TestOutput.At(output),
            Options = new EditCommandOptions { BestEffort = true },
        });
        Assert.Equal(["ok", "ok", "failed", "ok"], result.Applied.Select(operation => operation.Status));
        var reopened = new Document(output);
        Assert.Single(reopened.Sections.Cast<Section>());
        Assert.Contains("Third", reopened.GetText(), StringComparison.Ordinal);
        Assert.Equal("Kept", reopened.BuiltInDocumentProperties.Title);
    }

    private static string CreateSections(WordsFixture fixture)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("First");
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Write("Second");
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Write("Third");
        string path = fixture.Temp.File("sections.docx");
        document.Save(path);
        return path;
    }
}

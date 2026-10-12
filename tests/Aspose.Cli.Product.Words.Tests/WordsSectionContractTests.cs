using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// The section operations under their target names: <c>set_page_setup</c> takes the shared page
/// size flat on the operation and inspect reads it back as <c>pageSize</c>; <c>insert_section</c>
/// places a section before or after the 1-based one <c>at</c> names; <c>import_sections</c> opens
/// an encrypted source with the password <c>sourcePasswordEnv</c> names; levels are 1-based.
/// </summary>
public sealed class WordsSectionContractTests
{
    [Fact]
    public void SetPageSetup_PageSizeReadsBackAsThePageSizeOfTheSection()
    {
        using var fixture = new WordsFixture();
        string output = fixture.Temp.File("letter.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = fixture.CreateReport(),
            Batch = new WordsOpsBatch { Ops = [new SetPageSetupOp { PageSize = new PageSizeInput { Preset = "letter" }, Orientation = "landscape" }] },
            Output = TestOutput.At(output),
        });

        WordsSectionData section = Assert.Single(WordsInspect.Run(fixture.Session, new WordsInspectRequest
        {
            Input = output,
            Details = [WordsInspectDetails.Sections],
        }).Sections!);
        Assert.Equal("landscape", section.Orientation);
        Assert.Equal(PageSizeInfo.Of(792, 612), section.PageSize);
        Assert.Equal("letter", section.PageSize.Preset);
    }

    [Theory]
    [InlineData(600, 800)]
    [InlineData(800, 600)]
    public void SetPageSetup_WidthAndHeightInLandscape_ReadBackAsTheyWriteBack(double width, double height)
    {
        using var fixture = new WordsFixture();
        string first = fixture.Temp.File("first.docx");
        string second = fixture.Temp.File("second.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = fixture.CreateReport(),
            Batch = new WordsOpsBatch { Ops = [new SetPageSetupOp { PageSize = new PageSizeInput { Width = width, Height = height }, Orientation = "landscape" }] },
            Output = TestOutput.At(first),
        });
        WordsSectionData read = Section(fixture, first);
        Assert.Equal(("landscape", 800d, 600d), (read.Orientation, read.PageSize.Width, read.PageSize.Height));

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = first,
            Batch = new WordsOpsBatch
            {
                Ops = [new SetPageSetupOp { PageSize = new PageSizeInput { Width = read.PageSize.Width, Height = read.PageSize.Height }, Orientation = read.Orientation }],
            },
            Output = TestOutput.At(second),
        });
        WordsSectionData again = Section(fixture, second);
        Assert.Equal((read.Orientation, read.PageSize), (again.Orientation, again.PageSize));
    }

    private static WordsSectionData Section(WordsFixture fixture, string path) =>
        Assert.Single(WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = path, Details = [WordsInspectDetails.Sections] }).Sections!);

    [Theory]
    [InlineData("before", 0)]
    [InlineData("after", 1)]
    public void InsertSection_GoesBeforeOrAfterTheSectionAtNames(string position, int added)
    {
        using var fixture = new WordsFixture();
        string output = fixture.Temp.File($"{position}.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = fixture.CreateReport(),
            Batch = new WordsOpsBatch { Ops = [new InsertSectionOp { At = 1, Position = position }] },
            Output = TestOutput.At(output),
        });

        var document = new Document(output);
        Assert.Equal(2, document.Sections.Count);
        // Evaluation mode adds its own text, so the sections are told apart by the report's.
        Assert.DoesNotContain("Quarterly report", document.Sections[added].Body.GetText(), StringComparison.Ordinal);
        Assert.Contains("Quarterly report", document.Sections[1 - added].Body.GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void ImportSections_OpensAnEncryptedSourceWithItsPasswordVariable()
    {
        using var fixture = new WordsFixture();
        string source = fixture.CreateEncryptedDocument("source-secret");
        string output = fixture.Temp.File("imported.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = fixture.CreateReport(),
            Batch = new WordsOpsBatch { Ops = [new ImportSectionsOp { Path = source, SourcePasswordEnv = "SOURCE_PASSWORD", Formatting = FormattingModes.UseDestination }] },
            Output = TestOutput.At(output),
            OpSecrets = new Dictionary<string, Secret> { ["SOURCE_PASSWORD"] = new("source-secret") },
        });

        Assert.Contains("Encrypted portable document", new Document(output).GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void SetList_LevelIsOneBased()
    {
        using var fixture = new WordsFixture();
        string output = fixture.Temp.File("list.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = fixture.CreateReport(),
            Batch = new WordsOpsBatch { Ops = [new SetListOp { Target = new WordsTarget { Block = 1 }, Kind = "number", Level = 2 }] },
            Output = TestOutput.At(output),
        });

        Paragraph first = new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>()
            .Single(static paragraph => paragraph.GetText().Contains("Quarterly report", StringComparison.Ordinal));
        Assert.True(first.ListFormat.IsListItem);
        Assert.Equal(1, first.ListFormat.ListLevelNumber);
    }
}

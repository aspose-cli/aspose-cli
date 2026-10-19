using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Results;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Every format the product declares is one the engine can load or save.</summary>
public sealed class WordsEngineFormatsTests : IClassFixture<WordsFixture>
{
    private readonly WordsFixture _fixture;

    public WordsEngineFormatsTests(WordsFixture fixture) => _fixture = fixture;

    [Fact]
    public void EveryDeclaredReadFormat_HasAnEngineLoadFormat()
    {
        string[] missing = WordsFormats.Definitions
            .Where(static format => format.Uses.HasFlag(FormatUse.Input))
            .Select(static format => format.Id)
            .Where(static id => Engine(id)?.Load is null)
            .ToArray();

        Assert.True(missing.Length == 0, $"Declared read formats without an engine load format: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryDeclaredWriteFormat_HasAnEngineSaveFormat()
    {
        string[] missing = WordsFormats.Definitions
            .Where(static format => format.Uses.HasFlag(FormatUse.Convert) || format.Uses.HasFlag(FormatUse.Render))
            .Select(static format => format.Id)
            .Where(static id => Engine(id)?.Save is null)
            .ToArray();

        Assert.True(missing.Length == 0, $"Declared write formats without an engine save format: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// The lists the commands and warnings read keep their members; the ones that reach help or
    /// output text keep their order too. Revision formats are only ever looked up, so they are a set.
    /// </summary>
    [Fact]
    public void FormatLists_KeepTheirMembers()
    {
        Assert.Equal(["pdf", "xps", "openxps", "ps", "pcl", "html-fixed"], WordsFormats.FixedPageFormats);
        Assert.Equal(["doc", "dot", "docx", "docm", "dotx", "dotm", "flatopc", "wordml"], WordsFormats.WordIds);
        Assert.Equal(
            new HashSet<string>(["doc", "dot", "docx", "docm", "dotx", "dotm", "flatopc", "wordml", "rtf", "odt", "ott"], StringComparer.Ordinal),
            new HashSet<string>(WordsFormats.RevisionIds, StringComparer.Ordinal));
        Assert.Equal(WordsFormats.RevisionIds.Count, WordsFormats.RevisionIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["doc", "dot", "docm", "dotm", "wordml"], WordsFormats.MacroIds);
        Assert.Equal(["doc", "dot", "docx", "docm", "dotx", "dotm", "flatopc", "rtf", "wordml", "odt", "ott"],
            WordsFormats.Editable.Select(static format => format.Id));
    }

    /// <summary>Create and edit write only formats the product converts to and loads back, each through the engine.</summary>
    [Fact]
    public void EveryEditableFormat_LoadsAndSaves()
    {
        Assert.NotEmpty(WordsFormats.Editable);
        Assert.All(WordsFormats.Editable, static format =>
        {
            Assert.True(format.Uses.HasFlag(FormatUse.Input) && format.Uses.HasFlag(FormatUse.Convert), format.Id);
            WordsEngineFormat? engine = Engine(format.Id);
            Assert.True(engine?.Load is not null && engine.Save is not null, format.Id);
        });
    }

    /// <summary>Every convert format the product declares.</summary>
    public static TheoryData<string> ConvertFormats() =>
        [.. WordsFormats.Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Convert)).Select(static format => format.Id)];

    /// <summary>
    /// Converting a plain document says the output cannot keep every Word feature exactly when
    /// the output is plain text, Markdown or HTML.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConvertFormats))]
    public void Converting_DisclosesTheLossOnlyForTextAndHtmlOutputs(string format)
    {
        string input = _fixture.CreateReport($"lossy-source-{format}.docx");
        string extension = WordsFormats.Definitions.Named(FormatUse.Convert, format)!.Extensions[0];

        WordsConvertResult converted = WordsConvert.Run(_fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(_fixture.Temp.File($"lossy-{format}{extension}"), format: format),
        });

        bool disclosed = (converted.Warnings ?? []).Any(warning => warning.Code == WarningCodes.LossyConversion
            && warning.Message == $"Conversion to {format} cannot preserve every Word feature.");
        Assert.Equal(format is "txt" or "md" or "html" or "html-fixed", disclosed);
    }

    /// <summary>
    /// Bytes the engine reads as plain text load as txt or md only under a name those formats
    /// declare; under a document container's name they are a damaged file.
    /// </summary>
    [Theory]
    [InlineData("letter.txt", "txt")]
    [InlineData("letter.md", "md")]
    [InlineData("letter.markdown", "md")]
    [InlineData("letter.docx", null)]
    [InlineData("letter.odt", null)]
    public void PlainTextBytes_LoadOnlyUnderATextName(string fileName, string? format)
    {
        string input = _fixture.Temp.File(fileName);
        File.WriteAllText(input, "Dear reader,\n\nThe quarterly report follows.\n");

        if (format is null)
        {
            CliException error = Assert.Throws<CliException>(() =>
                WordsInspect.Run(_fixture.Session, new WordsInspectRequest { Input = input }));
            Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
            return;
        }

        WordsInspectResult inspected = WordsInspect.Run(_fixture.Session, new WordsInspectRequest { Input = input });
        Assert.Equal(format, inspected.Input.Format);
    }

    private static WordsEngineFormat? Engine(string id) =>
        WordsEngineFormats.All.SingleOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
}

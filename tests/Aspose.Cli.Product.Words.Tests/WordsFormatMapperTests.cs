using Aspose.Cli.Product.Words.Engine.Mapping;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Every format the product declares is one the engine can load or save.</summary>
public sealed class WordsFormatMapperTests
{
    [Fact]
    public void EveryDeclaredReadFormat_HasAnEngineLoadFormat()
    {
        string[] missing = WordsFormats.Definitions
            .Where(static format => format.Uses.HasFlag(FormatUse.Input))
            .Select(static format => format.Id)
            .Where(static id => !WordsFormatMapper.Loads(id))
            .ToArray();

        Assert.True(missing.Length == 0, $"Declared read formats without an engine load format: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryDeclaredWriteFormat_HasAnEngineSaveFormat()
    {
        string[] missing = WordsFormats.Definitions
            .Where(static format => format.Uses.HasFlag(FormatUse.Convert) || format.Uses.HasFlag(FormatUse.Render))
            .Select(static format => format.Id)
            .Where(static id => !WordsFormatMapper.Saves(id))
            .ToArray();

        Assert.True(missing.Length == 0, $"Declared write formats without an engine save format: {string.Join(", ", missing)}");
    }
}

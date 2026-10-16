using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>Every format the product declares maps to what Aspose.Slides loads or writes.</summary>
public sealed class SlidesEngineFormatsTests
{
    public static TheoryData<string> Readable() =>
        [.. SlidesFormats.Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Input)).Select(static format => format.Id)];

    public static TheoryData<string> Converted() =>
        [.. SlidesFormats.Definitions
            .Where(static format => format.Uses.HasFlag(FormatUse.Convert))
            .Select(static format => format.Id)];

    [Theory]
    [MemberData(nameof(Readable))]
    public void EveryReadFormat_HasAnEngineLoadFormat(string id) =>
        Assert.True(Engine(id)?.Loads.Length > 0, $"'{id}' is declared readable but has no Aspose.Slides load format.");

    [Theory]
    [MemberData(nameof(Converted))]
    public void EveryConvertFormat_HasAnEngineSaveFormat(string id) =>
        Assert.True(Engine(id)?.Save is not null, $"'{id}' is declared a convert format but Aspose.Slides does not save it.");

    [Fact]
    public void CreateAndEditFormats_SaveTheWholePresentation() =>
        Assert.All(SlidesFormats.Writable, static format => SlidesEngineFormats.SaveFormatOf(format.Id));

    private static SlidesEngineFormat? Engine(string id) =>
        SlidesEngineFormats.All.SingleOrDefault(format => string.Equals(format.Id, id, StringComparison.Ordinal));
}

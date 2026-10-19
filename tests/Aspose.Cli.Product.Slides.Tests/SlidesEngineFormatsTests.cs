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

    public static TheoryData<string> Rendered() =>
        [.. SlidesFormats.Definitions
            .Where(static format => format.Uses.HasFlag(FormatUse.Render))
            .Select(static format => format.Id)];

    [Theory]
    [MemberData(nameof(Rendered))]
    public void EveryRenderFormat_HasASlideWriter(string id)
    {
        SlidesEngineFormat? engine = Engine(id);
        Assert.True(engine is { Save: null, Loads.Length: 0 }, $"'{id}' is declared a render format but has no slide image row.");
        SlidesEngineFormats.SlideImageOf(id);
    }

    /// <summary>A render format draws each slide as a bitmap exactly when it is declared raster; the others write SVG.</summary>
    [Theory]
    [MemberData(nameof(Rendered))]
    public void RasterRenderFormats_AreTheBitmapRows(string id) =>
        Assert.Equal(
            SlidesFormats.Definitions.Single(format => format.Id == id).Raster,
            SlidesEngineFormats.SlideImageOf(id) is not null);

    private static SlidesEngineFormat? Engine(string id) =>
        SlidesEngineFormats.All.SingleOrDefault(format => string.Equals(format.Id, id, StringComparison.Ordinal));
}

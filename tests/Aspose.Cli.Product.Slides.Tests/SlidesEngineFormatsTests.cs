using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>Every format the product declares maps to what Aspose.Slides loads or writes.</summary>
public sealed class SlidesEngineFormatsTests
{
    public static TheoryData<string> Readable() =>
        [.. SlidesFormats.Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Input)).Select(static format => format.Id)];

    public static TheoryData<string> Writable() =>
        [.. SlidesFormats.Definitions
            .Where(static format => format.Uses.HasFlag(FormatUse.Convert) || format.Uses.HasFlag(FormatUse.Render))
            .Select(static format => format.Id)];

    [Theory]
    [MemberData(nameof(Readable))]
    public void EveryReadFormat_HasAnEngineLoadFormat(string id) =>
        Assert.True(Engine(id)?.Loads.Length > 0, $"'{id}' is declared readable but has no Aspose.Slides load format.");

    [Theory]
    [MemberData(nameof(Writable))]
    public void EveryWriteFormat_IsSavedOrRenderedPerSlide(string id) =>
        Assert.True(Engine(id) is { } format && (format.Save is not null || format.SlideImage), $"'{id}' is declared writable but Aspose.Slides neither saves nor renders it.");

    [Fact]
    public void RenderFormats_AreWrittenOneImagePerSlide() =>
        Assert.All(
            SlidesFormats.Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Render)),
            static format => Assert.True(SlidesEngineFormats.IsSlideImage(format.Id), format.Id));

    [Fact]
    public void CreateAndEditFormats_SaveTheWholePresentation() =>
        Assert.All(SlidesFormats.Writable, static format => SlidesEngineFormats.SaveFormatOf(format.Id));

    private static SlidesEngineFormat? Engine(string id) =>
        SlidesEngineFormats.All.SingleOrDefault(format => string.Equals(format.Id, id, StringComparison.Ordinal));
}

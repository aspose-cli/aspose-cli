using System.Reflection;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Serialization;
using Xunit;

namespace Aspose.Cli.Sdk.Tests;

/// <summary>
/// The value sets and the page size every product shares, so one concept has one name and one
/// set of values in each product's operations and reads.
/// </summary>
public sealed class SharedValueSetsTests
{
    [Theory]
    [InlineData(595.28, 841.89, "a4")]
    [InlineData(841.89, 595.28, "a4")]
    [InlineData(612, 792, "letter")]
    [InlineData(792, 1224, "tabloid")]
    [InlineData(595, 842, "a4")]
    [InlineData(600, 800, null)]
    public void PageSize_NamesThePresetItsDimensionsMatchInEitherOrientation(double width, double height, string? preset)
    {
        PageSizeInfo size = PageSizeInfo.Of(width, height);

        Assert.Equal(preset, size.Preset);
        Assert.Equal(width, size.Width);
        Assert.Equal(height, size.Height);
    }

    [Fact]
    public void EveryPageSizePreset_IsLowerCase_AndRoundTripsThroughItsDimensions()
    {
        string[] presets = Constants(typeof(PageSizePresets));

        Assert.Equal(["a3", "a4", "a5", "letter", "legal", "tabloid"], presets);
        foreach (string preset in presets)
        {
            (double width, double height) = PageSizePresets.Dimensions(preset);
            Assert.True(width < height, $"{preset} is portrait");
            Assert.Equal(preset, PageSizePresets.Matching(width, height));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => PageSizePresets.Dimensions("A4"));
    }

    [Fact]
    public void PageSize_IsPublishedWithBothDimensionsRequired_AndThePresetVocabulary()
    {
        Assert.True(SdkSchemaCatalog.TryRead("v2/common/page-size", out string? document));
        JsonObject schema = JsonNode.Parse(document)!.AsObject();

        Assert.Equal(["width", "height"], schema["required"]!.AsArray().Select(static name => name!.GetValue<string>()));
        Assert.Equal(
            Constants(typeof(PageSizePresets)),
            schema["properties"]!["preset"]!["enum"]!.AsArray().Select(static value => value!.GetValue<string>()));
    }

    [Fact]
    public void NumberStylesAndFormattingModes_AreTheSharedValueSets()
    {
        Assert.Equal(["decimal", "roman-upper", "roman-lower", "letters-upper", "letters-lower", "none"], Constants(typeof(PageNumberStyles)));
        Assert.Equal(["keep-source", "use-destination"], Constants(typeof(FormattingModes)));
    }

    /// <summary>The string constants of a value set, in declaration order.</summary>
    private static string[] Constants(Type set) =>
        [.. set.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(static field => (string)field.GetRawConstantValue()!)];
}

using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides convert</c>: the file a presentation or its selected slides were converted to.</summary>
public sealed record SlidesConvertResult() : EngineResultEnvelope("convert-result", 2)
{
    /// <summary>The converted presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written files: the one file the presentation was converted to, then any companion files its format needs, as HTML5's scripts.</summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<SlidesConvertOutput> Outputs { get; init; }

    /// <summary>The <c>--slides</c> selection that was converted, normalized; omitted when every slide was.</summary>
    [PageRange]
    public string? Slides { get; init; }
}

/// <summary>A file a presentation was converted to, or a companion file its format writes beside it.</summary>
public sealed record SlidesConvertOutput : OutputItem
{
    /// <summary>Describes the converted <paramref name="file"/>.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Output</c> describes it.</param>
    internal SlidesConvertOutput(OutputInfo file)
        : base(file)
    {
    }
}

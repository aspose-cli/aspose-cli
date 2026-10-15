using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words convert</c>.</summary>
public sealed record WordsConvertResult() : EngineResultEnvelope("convert-result", 2)
{
    /// <summary>The converted document.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written files: the one file the document was converted to.</summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<WordsConvertOutput> Outputs { get; init; }

    /// <summary>The 1-based pages the output holds, as <c>--pages</c> selected them; absent for every page.</summary>
    public string? Pages { get; init; }
}

/// <summary>The file a document was converted to.</summary>
public sealed record WordsConvertOutput : OutputItem
{
    /// <summary>Describes the converted <paramref name="file"/>.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Output</c> describes it.</param>
    internal WordsConvertOutput(OutputInfo file)
        : base(file)
    {
    }
}

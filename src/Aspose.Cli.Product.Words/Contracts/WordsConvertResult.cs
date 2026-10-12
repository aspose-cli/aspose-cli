using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words convert</c>.</summary>
public sealed record WordsConvertResult() : EngineResultEnvelope("convert-result", 2)
{
    /// <summary>The converted document.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written file.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The 1-based pages the output holds, as <c>--pages</c> selected them; absent for every page.</summary>
    public string? Pages { get; init; }
}

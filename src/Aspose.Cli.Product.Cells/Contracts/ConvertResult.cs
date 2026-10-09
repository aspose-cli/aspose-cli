using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Result of <c>aspose-cli cells convert</c>.</summary>
public sealed record ConvertResult() : ResultEnvelope("convert-result", 2)
{
    /// <summary>The converted input file.</summary>
    [JsonPropertyOrder(-50)]
    [AlwaysPresent("fingerprint")]
    public required SourceInfo Input { get; init; }

    /// <summary>The produced output file.</summary>
    [JsonPropertyOrder(-49)]
    [AlwaysPresent("format")]
    public required OutputInfo Output { get; init; }

    /// <summary>Sheet the conversion was limited to, when <c>--sheet</c> was given.</summary>
    public string? Sheet { get; init; }
}

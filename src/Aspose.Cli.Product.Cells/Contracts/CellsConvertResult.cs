using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Result of <c>cells convert</c>: the file a workbook, or CSV or TSV text, was converted to.</summary>
public sealed record CellsConvertResult() : EngineResultEnvelope("convert-result")
{
    /// <summary>The converted input file.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written files: the one file the input was converted to.</summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<CellsConvertOutput> Outputs { get; init; }

    /// <summary>The sheets written, as <c>--sheet</c> named them; omitted when it named none.</summary>
    [MinItems(1)]
    public IReadOnlyList<string>? Sheets { get; init; }

    /// <summary>The warnings Aspose.Cells reported while it loaded the input and wrote the output, as it reported them; omitted when there were none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}

/// <summary>The file a workbook was converted to.</summary>
public sealed record CellsConvertOutput : OutputItem
{
    /// <summary>Describes the converted <paramref name="file"/>.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Output</c> describes it.</param>
    internal CellsConvertOutput(OutputInfo file)
        : base(file)
    {
    }
}

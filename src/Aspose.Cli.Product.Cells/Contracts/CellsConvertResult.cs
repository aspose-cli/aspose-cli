using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Result of <c>aspose-cli cells convert</c>.</summary>
public sealed record CellsConvertResult() : EngineResultEnvelope("convert-result", 2)
{
    /// <summary>The converted input file.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written files: the one file the workbook was converted to.</summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<CellsConvertOutput> Outputs { get; init; }

    /// <summary>The sheets the conversion was limited to, in workbook order, when <c>--sheets</c> selected some.</summary>
    [MinItems(1)]
    public IReadOnlyList<string>? Sheets { get; init; }
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

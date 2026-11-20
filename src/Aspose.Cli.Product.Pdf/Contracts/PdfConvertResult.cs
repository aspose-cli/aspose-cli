using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf convert</c>: the file the input was converted to.</summary>
public sealed record PdfConvertResult() : EngineResultEnvelope("convert-result")
{
    /// <summary>The converted PDF or HTML.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written file; <c>tiff</c> holds the pages in one multi-page file.</summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<PdfConvertOutput> Outputs { get; init; }

    /// <summary>The page range converted, as <c>--pages</c> gave it; absent when every page was.</summary>
    public string? Pages { get; init; }

    /// <summary>What Document.Convert returned for a PDF/A output; absent for any other.</summary>
    public bool? Converted { get; init; }

    /// <summary>The log Document.Convert wrote for a PDF/A output, as the engine wrote it; absent for any other.</summary>
    public string? ConversionLog { get; init; }
}

/// <summary>A file a PDF was converted to: the output, or a file the engine wrote beside it.</summary>
public sealed record PdfConvertOutput : OutputItem
{
    /// <summary>Describes the converted <paramref name="file"/>.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Output</c> describes it.</param>
    internal PdfConvertOutput(OutputInfo file)
        : base(file)
    {
    }
}

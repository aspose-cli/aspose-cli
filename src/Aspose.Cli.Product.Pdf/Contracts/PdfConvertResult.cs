using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf convert</c>: the file a PDF or its selected pages were converted to.</summary>
public sealed record PdfConvertResult() : EngineResultEnvelope("convert-result", 2)
{
    /// <summary>The converted PDF.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written file: the one file the PDF or its selected pages were converted to; <c>tiff</c> holds every page in one multi-page file.</summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<PdfConvertOutput> Outputs { get; init; }

    /// <summary>The page range converted, as <c>--pages</c> gave it; absent when every page was.</summary>
    public string? Pages { get; init; }
}

/// <summary>The file a PDF was converted to.</summary>
public sealed record PdfConvertOutput : OutputItem
{
    /// <summary>Describes the converted <paramref name="file"/>.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Output</c> describes it.</param>
    internal PdfConvertOutput(OutputInfo file)
        : base(file)
    {
    }
}

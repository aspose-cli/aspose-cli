using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf extract</c> for images, attachments, text or tables.</summary>
public sealed record PdfExtractResult() : EngineResultEnvelope("extract-result", 2)
{
    /// <summary>The PDF extracted from.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>What was extracted, as <c>--what</c> named it.</summary>
    [AllowedValues("images", "attachments", "text", "tables")]
    public required string What { get; init; }

    /// <summary>Each file written into the output directory.</summary>
    public required IReadOnlyList<PdfExtractedItem> Items { get; init; }
}

/// <summary>One extracted PDF asset or text file.</summary>
public sealed record PdfExtractedItem
{
    /// <summary>Absolute path of the file written.</summary>
    public required string Path { get; init; }

    /// <summary>What the file holds.</summary>
    [AllowedValues("image", "attachment", "text", "table")]
    public required string Kind { get; init; }

    /// <summary>The file's size in bytes.</summary>
    [Minimum(0)]
    public required long SizeBytes { get; init; }

    /// <summary>The 1-based page the item comes from; absent for an attachment.</summary>
    [Minimum(1)]
    public int? Page { get; init; }

    /// <summary>The attachment's name in the PDF; absent for other items.</summary>
    public string? Name { get; init; }

    /// <summary>Where a table lies on its page; absent for other items.</summary>
    public PdfRect? Rect { get; init; }

    /// <summary>How sure the engine is that a table was found, from 0 to 1; absent for other items.</summary>
    [Minimum(0)]
    [Maximum(1)]
    public double? Confidence { get; init; }
}

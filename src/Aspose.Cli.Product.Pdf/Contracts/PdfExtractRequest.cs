using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for bounded PDF extraction.</summary>
public sealed record PdfExtractRequest : IPdfExtractRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public required string What { get; init; }
    /// <summary>The resolved directory that receives the files.</summary>
    public required ResolvedDirectory Output { get; init; }
    public PageRange? Pages { get; init; }
    public Secret? Password { get; init; }

    /// <summary>Whether each table's UTF-8 CSV starts with a byte order mark.</summary>
    public bool ByteOrderMark { get; init; }
}

/// <summary>
/// A request of <c>pdf extract</c>: assets, text and tables (<see cref="PdfExtractRequest"/>) or
/// form data (<see cref="PdfFormExportRequest"/>), each with its own result.
/// </summary>
public interface IPdfExtractRequest
{
    /// <summary>The PDF to open.</summary>
    string Input { get; }
}

/// <summary>Stable PDF extraction families.</summary>
public static class PdfExtractKinds
{
    public static IReadOnlyList<string> All { get; } = ["images", "attachments", "text", "tables"];
}

using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for exporting form data, the <c>--what forms</c> request of <c>pdf extract</c>.</summary>
public sealed record PdfFormExportRequest : IPdfExtractRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public Secret? Password { get; init; }
}

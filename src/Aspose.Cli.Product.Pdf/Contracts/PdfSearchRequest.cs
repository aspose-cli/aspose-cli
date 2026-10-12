using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Pdf.Contracts;

public sealed record PdfSearchRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public required SearchQuery Query { get; init; }
    public PageRange? Pages { get; init; }
    public Secret? Password { get; init; }
}

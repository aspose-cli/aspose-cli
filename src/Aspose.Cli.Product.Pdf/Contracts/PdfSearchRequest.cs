using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Pdf.Contracts;

public sealed record PdfSearchRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public required SearchQuery Query { get; init; }
    public PageRange? Pages { get; init; }
    public Secret? Password { get; init; }

    /// <summary>
    /// The quoted command line that reads the input's form fields with their values, with the
    /// input password's environment variable when one gave it, which the warning that the text
    /// leaves out field values names; null for a search made without a command line.
    /// </summary>
    public string? FormsInspection { get; init; }
}

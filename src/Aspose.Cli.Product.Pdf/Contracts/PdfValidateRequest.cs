namespace Aspose.Cli.Product.Pdf.Contracts;

public sealed record PdfValidateRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public required string Profile { get; init; }
    public Secret? Password { get; init; }
}

using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>One <c>pdf create</c>: the engine's new document, new Document(), which has no page, or a template saved as a new one.</summary>
public sealed record PdfCreateRequest
{
    /// <summary>Where the document goes, in its format.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>The template, or null for the engine's new document.</summary>
    public SourceFile? Template { get; init; }

    /// <summary>The password the output opens with, or null.</summary>
    public Secret? EncryptPassword { get; init; }
}

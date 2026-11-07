using System.Diagnostics.CodeAnalysis;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>
/// Options of <c>pdf edit</c>. <c>EncryptPassword</c> is applied after the operations and replaces
/// the input's encryption, which an <c>encrypt_document</c> in the same batch may not also set;
/// null keeps what the operations leave.
/// </summary>
public sealed record PdfEditRequest : BoundedEditRequest<PdfOpsBatch>
{
    /// <summary>Creates a request whose fields an object initializer sets.</summary>
    public PdfEditRequest()
    {
    }

    /// <summary>Creates the request of one resolved edit invocation.</summary>
    [SetsRequiredMembers]
    public PdfEditRequest(BoundedEditInvocation<PdfOpsBatch> edit)
        : base(edit)
    {
    }

    /// <summary>
    /// The quoted command line that reads the bookmarks of the document as they were before the
    /// edit, with the input password's environment variable when one gave it, which the warning of
    /// stranded bookmarks names; null when there is no such file or no command line.
    /// </summary>
    public string? OutlineInspection { get; init; }
}

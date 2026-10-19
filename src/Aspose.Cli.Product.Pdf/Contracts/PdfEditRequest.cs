using System.Diagnostics.CodeAnalysis;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>
/// Options of <c>pdf edit</c>. <c>EncryptPassword</c> is applied after the operations and replaces
/// any encryption they leave; null keeps what they leave.
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
}

using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary><c>pdf edit</c>: the object table's edit of a document.</summary>
internal static class PdfEdit
{
    internal static PdfEditResult Run(PdfSession session, TableEditRequest request) =>
        new(TableEdit.Run(session.Outputs, new PdfDocuments(session.Loader), request));
}

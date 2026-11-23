using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary><c>pdf read</c>: the object table's read of a document.</summary>
internal static class PdfRead
{
    internal static PdfReadResult Run(PdfSession session, TableReadRequest request) =>
        new(TableRead.Run(session.Outputs, new PdfDocuments(session.Loader), request));
}

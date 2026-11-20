using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary><c>pdf read</c>: the object table's read of a document.</summary>
internal static class PdfRead
{
    internal static PdfReadResult Run(PdfSession session, PdfReadRequest request)
    {
        TableReadOutcome outcome = TableRead.Run(session.Outputs, new PdfDocuments(session.Loader), request);
        return new PdfReadResult
        {
            Input = outcome.Input,
            Objects = outcome.Objects,
            Inventory = outcome.Inventory,
            EngineWarnings = outcome.EngineWarnings,
            Window = outcome.Window,
            License = outcome.License,
            Warnings = outcome.Warnings,
        };
    }
}

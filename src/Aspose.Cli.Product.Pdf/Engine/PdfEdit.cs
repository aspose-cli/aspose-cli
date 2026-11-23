using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary><c>pdf edit</c>: the object table's edit of a document.</summary>
internal static class PdfEdit
{
    internal static PdfEditResult Run(PdfSession session, TableEditRequest request)
    {
        TableEditOutcome outcome = TableEdit.Run(session.Outputs, new PdfDocuments(session.Loader), request);
        return new PdfEditResult
        {
            Input = outcome.Input,
            Output = outcome.Output,
            Ops = outcome.Ops,
            Counts = outcome.Counts,
            EngineWarnings = outcome.EngineWarnings,
            License = outcome.License,
            Warnings = outcome.Warnings,
        };
    }
}

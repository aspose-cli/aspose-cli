using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary><c>pdf search</c>: the object table's search of the pages' text, with the engine's TextFragmentAbsorber.</summary>
internal static class PdfSearch
{
    internal static PdfSearchResult Run(PdfSession session, TableSearchRequest request)
    {
        TableSearchOutcome outcome = TableRead.Search(session.Outputs, new PdfDocuments(session.Loader), request);
        return new PdfSearchResult
        {
            Input = outcome.Input,
            Hits = outcome.Hits,
            EngineWarnings = outcome.EngineWarnings,
            Window = outcome.Window,
            License = outcome.License,
            Warnings = outcome.Warnings,
        };
    }
}

using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary><c>pdf search</c>: the object table's search of the pages' text, with the engine's TextFragmentAbsorber.</summary>
internal static class PdfSearch
{
    internal static PdfSearchResult Run(PdfSession session, TableSearchRequest request) =>
        new(TableRead.Search(session.Outputs, new PdfDocuments(session.Loader), request));
}

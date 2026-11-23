using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells search</c>: the object table's search of a workbook's text.</summary>
internal static class CellsSearch
{
    internal static CellsSearchResult Run(CellsSession session, TableSearchRequest request) =>
        new(TableRead.Search(session.Outputs, new CellsDocuments(session.Loader), request));
}

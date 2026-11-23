using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells edit</c>: the object table edit of a workbook.</summary>
internal static class CellsEdit
{
    internal static CellsEditResult Run(CellsSession session, TableEditRequest request) =>
        new(TableEdit.Run(session.Outputs, new CellsDocuments(session.Loader), request));
}

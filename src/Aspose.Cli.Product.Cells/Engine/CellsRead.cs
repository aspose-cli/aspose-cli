using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells read</c>: the object table's read of a workbook.</summary>
internal static class CellsRead
{
    internal static CellsReadResult Run(CellsSession session, CellsReadRequest request)
    {
        TableReadOutcome outcome = TableRead.Run(session.Outputs, new CellsDocuments(session.Loader), request);
        return new CellsReadResult
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

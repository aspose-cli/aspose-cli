using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells edit</c>: the object table edit of a workbook.</summary>
internal static class CellsEdit
{
    internal static CellsEditResult Run(CellsSession session, CellsEditRequest request)
    {
        TableEditOutcome outcome = TableEdit.Run(session.Outputs, new CellsDocuments(session.Loader), request);
        return new CellsEditResult
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

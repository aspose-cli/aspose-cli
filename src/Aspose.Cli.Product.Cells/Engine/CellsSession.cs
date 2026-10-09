using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>What the Cells handlers of one invocation share; activation builds it.</summary>
/// <param name="Outputs">The write pipeline: license state, evaluation disclosure and publishing.</param>
/// <param name="Budgets">The invocation's resource budgets.</param>
/// <param name="Loader">Opens workbooks within those budgets.</param>
internal sealed record CellsSession(
    OutputPipeline<Workbook> Outputs,
    ResourceBudgetLedger Budgets,
    CellsWorkbookLoader Loader);

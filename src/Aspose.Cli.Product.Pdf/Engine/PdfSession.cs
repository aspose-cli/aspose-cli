using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>The dependencies every PDF handler shares, created once per invocation.</summary>
/// <param name="Outputs">The write pipeline: license state, evaluation profile and atomic publication.</param>
/// <param name="Budgets">The invocation's resource budgets.</param>
/// <param name="Loader">The budgeted document loader.</param>
internal sealed record PdfSession(
    OutputPipeline<Document> Outputs,
    ResourceBudgetLedger Budgets,
    PdfDocumentLoader Loader);

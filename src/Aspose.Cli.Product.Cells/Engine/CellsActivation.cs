using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Activates the Cells session behind the SDK write pipeline for one invocation.</summary>
internal static class CellsActivation
{
    internal static ProductBinding<CellsSession> Activate(ProductActivationContext context, string productId) =>
        ProductBinding.Create<CellsSession, Aspose.Cells.Workbook>(
            context,
            productId,
            resolution => new CellsLicenseGate(resolution, context.EnvironmentVariable),
            new CellsEvaluationProfile(),
            outputs => new CellsSession(outputs, context.ResourceBudgets, new CellsWorkbookLoader(context.ResourceBudgets)),
            outputs => new CellsFontEnvironment(outputs, context.ResourceBudgets));
}

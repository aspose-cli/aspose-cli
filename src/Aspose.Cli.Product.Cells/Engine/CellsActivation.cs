using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Activates the Cells engine behind the SDK write pipeline for one invocation.</summary>
internal static class CellsActivation
{
    internal static ProductBinding<ICellsEngine> Activate(ProductActivationContext context, string productId) =>
        ProductBinding.Create<ICellsEngine, Aspose.Cells.Workbook>(
            context,
            productId,
            resolution => new CellsLicenseGate(resolution, context.EnvironmentVariable),
            new CellsEvaluationProfile(),
            outputs => new CellsEngine(outputs, context.ResourceBudgets),
            outputs => new CellsFontEnvironment(outputs, context.ResourceBudgets));
}

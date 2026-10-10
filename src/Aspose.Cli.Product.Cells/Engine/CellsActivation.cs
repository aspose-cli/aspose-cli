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
            ApplyLicense,
            new CellsEvaluationProfile(),
            outputs => new CellsSession(outputs, context.ResourceBudgets, new CellsWorkbookLoader(context.ResourceBudgets)),
            outputs => new CellsFontEnvironment(outputs, context.ResourceBudgets));

    /// <summary>Applies a license snapshot to Aspose.Cells.</summary>
    internal static void ApplyLicense(Stream stream) =>
        new Aspose.Cells.License().SetLicense(stream);
}

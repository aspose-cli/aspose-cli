using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Activates the PDF engine behind the SDK write pipeline for one invocation.</summary>
internal static class PdfActivation
{
    internal static ProductBinding<IPdfEngine> Activate(ProductActivationContext context, string productId) =>
        ProductBinding.Create<IPdfEngine, Aspose.Pdf.Document>(
            context,
            productId,
            resolution => new PdfLicenseGate(resolution, context.EnvironmentVariable),
            new PdfEvaluationProfile(),
            outputs => new PdfEngine(outputs, context.ResourceBudgets),
            outputs => new PdfFontEnvironment(outputs, context.ResourceBudgets));
}

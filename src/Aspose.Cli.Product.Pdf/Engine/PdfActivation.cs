using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Activates the PDF session behind the SDK write pipeline for one invocation.</summary>
internal static class PdfActivation
{
    internal static ProductBinding<PdfSession> Activate(ProductActivationContext context) =>
        ProductBinding.Create<PdfSession>(
            context,
            ProductBuildMetadata.ProductId,
            ApplyLicense,
            new PdfEvaluationProfile(),
            outputs => Session(outputs, context.ResourceBudgets),
            _ => new PdfFontEnvironment());

    /// <summary>The session of one invocation's write pipeline and budgets.</summary>
    internal static PdfSession Session(OutputPipeline outputs, ResourceBudgetLedger budgets) =>
        new(outputs, budgets, new PdfDocumentLoader(budgets, outputs));

    /// <summary>Applies a license snapshot to Aspose.PDF.</summary>
    internal static void ApplyLicense(Stream stream) =>
        new Aspose.Pdf.License().SetLicense(stream);
}

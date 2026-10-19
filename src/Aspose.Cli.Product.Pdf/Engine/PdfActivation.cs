using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Activates the PDF session behind the SDK write pipeline for one invocation.</summary>
internal static class PdfActivation
{
    internal static ProductBinding<PdfSession> Activate(ProductActivationContext context) =>
        ProductBinding.Create<PdfSession, Aspose.Pdf.Document>(
            context,
            ProductBuildMetadata.ProductId,
            ApplyLicense,
            new PdfEvaluationProfile(),
            outputs => Session(outputs, context.ResourceBudgets),
            outputs => new PdfFontEnvironment(outputs, context.ResourceBudgets));

    /// <summary>The session of one invocation's write pipeline and budgets.</summary>
    internal static PdfSession Session(OutputPipeline<Aspose.Pdf.Document> outputs, ResourceBudgetLedger budgets) =>
        new(outputs, budgets, new PdfDocumentLoader(budgets, outputs));

    /// <summary>Applies a license snapshot to Aspose.PDF.</summary>
    internal static void ApplyLicense(Stream stream) =>
        new Aspose.Pdf.License().SetLicense(stream);
}

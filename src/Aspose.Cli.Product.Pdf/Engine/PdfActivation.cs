using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Activates the PDF session behind the SDK write pipeline for one invocation.</summary>
internal static class PdfActivation
{
    internal static ProductBinding<PdfSession> Activate(ProductActivationContext context, string productId) =>
        ProductBinding.Create<PdfSession, Aspose.Pdf.Document>(
            context,
            productId,
            ApplyLicense,
            new PdfEvaluationProfile(),
            outputs => new PdfSession(outputs, context.ResourceBudgets, new PdfDocumentLoader(context.ResourceBudgets)),
            outputs => new PdfFontEnvironment(outputs, context.ResourceBudgets));

    /// <summary>Applies a license snapshot to Aspose.PDF.</summary>
    internal static void ApplyLicense(Stream stream) =>
        new Aspose.Pdf.License().SetLicense(stream);
}

using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Activates the Slides engine behind the SDK write pipeline for one invocation.</summary>
internal static class SlidesActivation
{
    internal static ProductBinding<SlidesSession> Activate(ProductActivationContext context) =>
        ProductBinding.Create<SlidesSession>(
            context,
            ProductBuildMetadata.ProductId,
            ApplyLicense,
            new SlidesEvaluationProfile(),
            outputs => Session(outputs, context.ResourceBudgets),
            _ => new SlidesFontEnvironment());

    /// <summary>The session of one invocation's write pipeline and budgets.</summary>
    internal static SlidesSession Session(OutputPipeline outputs, ResourceBudgetLedger budgets) =>
        new(outputs, budgets, new SlidesPresentationLoader(budgets, outputs));

    /// <summary>Applies a license snapshot to Aspose.Slides.</summary>
    internal static void ApplyLicense(Stream stream) =>
        new Aspose.Slides.License().SetLicense(stream);
}

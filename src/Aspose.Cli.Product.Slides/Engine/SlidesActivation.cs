using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Activates the Slides engine behind the SDK write pipeline for one invocation.</summary>
internal static class SlidesActivation
{
    internal static ProductBinding<SlidesSession> Activate(ProductActivationContext context, string productId) =>
        ProductBinding.Create<SlidesSession, Aspose.Slides.Presentation>(
            context,
            productId,
            ApplyLicense,
            new SlidesEvaluationProfile(),
            outputs => new SlidesSession(
                outputs,
                context.ResourceBudgets,
                new SlidesPresentationLoader(context.ResourceBudgets)),
            outputs => new SlidesFontEnvironment(outputs, context.ResourceBudgets));

    /// <summary>Applies a license snapshot to Aspose.Slides.</summary>
    internal static void ApplyLicense(Stream stream) =>
        new Aspose.Slides.License().SetLicense(stream);
}

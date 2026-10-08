using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Activates the Slides engine behind the SDK write pipeline for one invocation.</summary>
internal static class SlidesActivation
{
    internal static ProductBinding<ISlidesEngine> Activate(ProductActivationContext context, string productId) =>
        ProductBinding.Create<ISlidesEngine, Aspose.Slides.Presentation>(
            context,
            productId,
            resolution => new SlidesLicenseGate(resolution, context.EnvironmentVariable),
            new SlidesEvaluationProfile(),
            outputs => new SlidesEngine(outputs, context.ResourceBudgets),
            outputs => new SlidesFontEnvironment(outputs, context.ResourceBudgets));
}

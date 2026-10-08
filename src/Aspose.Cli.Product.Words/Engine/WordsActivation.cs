using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Activates the Words engine behind the SDK write pipeline for one invocation.</summary>
internal static class WordsActivation
{
    internal static ProductBinding<IWordsEngine> Activate(ProductActivationContext context, string productId) =>
        ProductBinding.Create<IWordsEngine, Aspose.Words.Document>(
            context,
            productId,
            resolution => new WordsLicenseGate(resolution, context.EnvironmentVariable),
            new WordsEvaluationProfile(),
            outputs => new WordsEngine(outputs, context.ResourceBudgets),
            outputs => new WordsFontEnvironment(outputs, context.ResourceBudgets));
}

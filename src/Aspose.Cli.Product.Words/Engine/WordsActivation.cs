using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Activates the Words engine behind the SDK write pipeline for one invocation.</summary>
internal static class WordsActivation
{
    internal static ProductBinding<WordsSession> Activate(ProductActivationContext context, string productId) =>
        ProductBinding.Create<WordsSession, Document>(
            context,
            productId,
            resolution => new WordsLicenseGate(resolution, context.EnvironmentVariable),
            new WordsEvaluationProfile(),
            outputs => Session(outputs, context.ResourceBudgets),
            outputs => new WordsFontEnvironment(outputs, context.ResourceBudgets));

    /// <summary>The session of one invocation's write pipeline and budgets.</summary>
    internal static WordsSession Session(OutputPipeline<Document> outputs, ResourceBudgetLedger budgets) =>
        new(outputs, budgets, new WordsDocumentLoader(budgets, outputs));
}

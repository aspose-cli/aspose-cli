using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Activates the Words engine behind the SDK write pipeline for one invocation.</summary>
internal static class WordsActivation
{
    internal static ProductBinding<WordsSession> Activate(ProductActivationContext context) =>
        ProductBinding.Create<WordsSession>(
            context,
            ProductBuildMetadata.ProductId,
            ApplyLicense,
            new WordsEvaluationProfile(),
            outputs => Session(outputs, context.ResourceBudgets),
            _ => new WordsFontEnvironment());

    /// <summary>The session of one invocation's write pipeline and budgets.</summary>
    internal static WordsSession Session(OutputPipeline outputs, ResourceBudgetLedger budgets) =>
        new(outputs, budgets, new WordsDocumentLoader(budgets, outputs));

    /// <summary>Applies a license snapshot to Aspose.Words.</summary>
    internal static void ApplyLicense(Stream stream) =>
        new Aspose.Words.License().SetLicense(stream);
}

/// <summary>
/// Aspose.Words evaluation mode: a run without a license says so in its result's license mode;
/// what the engine writes into a document it opens in evaluation mode is read as it is.
/// </summary>
internal sealed class WordsEvaluationProfile : IEvaluationProfile
{
    public string Engine => "Aspose.Words";
}

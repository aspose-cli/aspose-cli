using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Activates the Cells session behind the SDK write pipeline for one invocation.</summary>
internal static class CellsActivation
{
    internal static ProductBinding<CellsSession> Activate(ProductActivationContext context) =>
        ProductBinding.Create<CellsSession, Workbook>(
            context,
            ProductBuildMetadata.ProductId,
            ApplyLicense,
            new CellsEvaluationProfile(),
            outputs => new CellsSession(outputs, context.ResourceBudgets, new CellsWorkbookLoader(context.ResourceBudgets, outputs)),
            _ => new CellsFontEnvironment());

    /// <summary>Applies a license snapshot to Aspose.Cells.</summary>
    internal static void ApplyLicense(Stream stream) =>
        new License().SetLicense(stream);
}

/// <summary>
/// Names the engine whose evaluation mode the SDK discloses. It recognizes no mark: a run
/// without a license is stated once, as the result's license mode, and the evaluation warning
/// sheets the engine adds stay in the workbook and in what a read returns, as the engine made them.
/// It recognizes the engine's one evaluation limit, which the SDK reports as <c>EVALUATION_LIMIT</c>.
/// </summary>
internal sealed class CellsEvaluationProfile : IEvaluationProfile<Workbook>
{
    // How many files Aspose.Cells evaluation mode opens in one process before it refuses more.
    private const int OpenLimit = 100;

    public string Engine => "Aspose.Cells";

    /// <summary>
    /// Evaluation mode refuses to open a file once its process has opened <see cref="OpenLimit"/>,
    /// which only a long-lived process reaches.
    /// </summary>
    public EvaluationLimitFact? RecognizeLimit(Exception exception) => IsOpenLimit(exception)
        ? new EvaluationLimitFact($"opens at most {OpenLimit} files in one process", OpenLimit, "files")
        : null;

    /// <summary>Whether <paramref name="exception"/> is evaluation mode refusing to open another file.</summary>
    internal static bool IsOpenLimit(Exception exception) => exception is CellsException { Code: ExceptionType.License };
}

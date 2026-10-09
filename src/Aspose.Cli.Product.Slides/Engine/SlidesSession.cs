using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>The dependencies every Slides handler of one invocation shares.</summary>
/// <param name="Outputs">The write pipeline: license state and publishing.</param>
/// <param name="Budgets">The invocation's resource budgets.</param>
/// <param name="Loader">Opens presentation inputs within those budgets.</param>
internal sealed record SlidesSession(
    OutputPipeline<Presentation> Outputs,
    ResourceBudgetLedger Budgets,
    SlidesPresentationLoader Loader);

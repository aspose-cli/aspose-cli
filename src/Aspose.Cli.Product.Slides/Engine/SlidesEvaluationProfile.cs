using Aspose.Cli.Sdk.Licensing;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Names the engine whose evaluation mode the SDK discloses. It recognizes no mark: a run
/// without a license is stated once, as the result's license mode, and what evaluation mode
/// writes stays in the output and in what a read returns, as the engine wrote it.
/// </summary>
internal sealed class SlidesEvaluationProfile : IEvaluationProfile<Presentation>
{
    public string Engine => "Aspose.Slides";

    public EvaluationMarks Inspect(Presentation presentation) => EvaluationMarks.None;
}

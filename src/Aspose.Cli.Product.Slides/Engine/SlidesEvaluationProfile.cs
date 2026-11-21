using Aspose.Cli.Sdk.Licensing;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Names the engine whose evaluation mode a run without a license uses: the run is stated once,
/// as the result's license mode, and what evaluation mode writes stays in the output and in what
/// a read returns, as the engine wrote it.
/// </summary>
internal sealed class SlidesEvaluationProfile : IEvaluationProfile<Presentation>
{
    public string Engine => "Aspose.Slides";
}

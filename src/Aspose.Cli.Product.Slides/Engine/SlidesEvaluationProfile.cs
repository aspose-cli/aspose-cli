using Aspose.Cli.Sdk.Licensing;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Recognizes what Aspose.Slides evaluation mode writes into a presentation: the locked
/// watermark text box on each slide it saves (<see cref="SlidesEngineSupport.IsEvaluationWatermark"/>),
/// licensed or not. Evaluation mode also cuts longer text short where it reads it; the saved
/// presentation keeps the full text, but whatever reads the text sees only its start.
/// </summary>
internal sealed class SlidesEvaluationProfile : IEvaluationProfile<Presentation>
{
    public EvaluationMarks Inspect(Presentation presentation) =>
        new(
            presentation.Slides.Any(static slide => slide.Shapes.Any(IsEvaluationWatermark))
                ? [Watermark]
                : [],
            EvaluationInputTruncated(presentation));

    /// <summary>
    /// The marks of an output: the watermark boxes, and the text evaluation mode cut short only
    /// where the output holds text the CLI read, as Markdown does; presentations, PDFs and images
    /// the engine saves keep the full text.
    /// </summary>
    public EvaluationMarks Inspect(Presentation presentation, string format) =>
        format == "md" ? Inspect(presentation) : Inspect(presentation) with { IsTruncated = false };

    /// <summary>The marks images of some slides show: the watermark boxes on those slides.</summary>
    public EvaluationMarks Inspect(Presentation presentation, string format, IReadOnlyCollection<int> pages) =>
        new(pages.Any(number => presentation.Slides[number - 1].Shapes.Any(IsEvaluationWatermark)) ? [Watermark] : []);

    private const string Watermark = "the evaluation watermark text box 'Evaluation only. Created with Aspose.Slides' on its slides";
}

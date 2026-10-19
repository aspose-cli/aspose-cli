using Aspose.Cli.Sdk.Licensing;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEvaluationMarks;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Recognizes what Aspose.Slides evaluation mode writes into a presentation: the locked
/// watermark text box on each slide it saves (<see cref="IsEvaluationWatermark"/>),
/// licensed or not. Evaluation mode also cuts longer text short where it reads it; the saved
/// presentation keeps the full text, but whatever reads the text sees only its start.
/// </summary>
internal sealed class SlidesEvaluationProfile : IEvaluationProfile<Presentation>
{
    public string Engine => "Aspose.Slides";

    public string Truncation => "reads text longer than five characters as its first characters and a truncation notice";

    public EvaluationMarks Inspect(Presentation presentation) =>
        new(
            presentation.Slides.Any(static slide => slide.Shapes.Any(IsEvaluationWatermark))
                ? [Watermark]
                : [],
            EvaluationInputTruncated(presentation));

    /// <summary>
    /// Evaluation mode draws its watermark into every output but Markdown, whose export holds
    /// only the slides' text, cut short.
    /// </summary>
    public bool WritesWatermark(string format) => !SlidesEngineFormats.HoldsReadText(format);

    /// <summary>
    /// The marks of an output: the watermark boxes, and the text evaluation mode cut short only
    /// where the output holds text the CLI read, as Markdown does; presentations, PDFs and images
    /// the engine saves keep the full text.
    /// </summary>
    public EvaluationMarks Inspect(Presentation presentation, string format) =>
        SlidesEngineFormats.HoldsReadText(format) ? Inspect(presentation) : Inspect(presentation) with { IsTruncated = false };

    /// <summary>
    /// The marks of an output that shows some slides, as images or an export of selected slides
    /// do: the watermark boxes on those slides, and, in Markdown, the text cut short.
    /// </summary>
    public EvaluationMarks Inspect(Presentation presentation, string format, IReadOnlyCollection<int> pages) =>
        new(
            pages.Any(number => presentation.Slides[number - 1].Shapes.Any(IsEvaluationWatermark)) ? [Watermark] : [],
            SlidesEngineFormats.HoldsReadText(format) && EvaluationInputTruncated(presentation));

    private const string Watermark = "the evaluation watermark text box 'Evaluation only. Created with Aspose.Slides' on its slides";
}

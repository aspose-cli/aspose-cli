using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// How evaluation mode marks what it reads and saves: the notice it puts in place of text it
/// cuts short, and the watermark text box an evaluation save adds to every slide.
/// </summary>
internal static class SlidesEvaluationMarks
{
    internal const string EvaluationTruncationMarker = "truncated due to evaluation version limitation";

    /// <summary>
    /// Whether a shape is the watermark text box an evaluation save adds to every slide: a
    /// select- and position-locked shape, not a placeholder, whose text is the evaluation notice
    /// "Evaluation only." followed by "Created with Aspose.Slides ...". Evaluation mode reads each
    /// run of it cut to its first characters, so there the runs must start "Evalu" and "Creat";
    /// a box of the user's that only starts "Evaluation" is never taken for the watermark.
    /// </summary>
    internal static bool IsEvaluationWatermark(IShape shape)
    {
        if (shape is not IAutoShape { Placeholder: null, TextFrame: { } frame } box
            || box.ShapeLock is not { SelectLocked: true, PositionLocked: true })
        {
            return false;
        }

        string[] runs = [.. frame.Paragraphs.SelectMany(static paragraph => paragraph.Portions)
            .Select(static portion => portion.Text)
            .Where(static text => !string.IsNullOrWhiteSpace(text))];
        return runs.Any(CutByEvaluation)
            ? runs is [{ } evaluation, { } created, ..]
                && evaluation.StartsWith("Evalu", StringComparison.Ordinal)
                && created.StartsWith("Creat", StringComparison.Ordinal)
            : frame.Text is { } text
                && text.StartsWith("Evaluation only.", StringComparison.Ordinal)
                && text.Contains("Created with Aspose.Slides", StringComparison.Ordinal);
    }

    /// <summary>Whether evaluation mode cut short text it read from the presentation, other than its own watermark.</summary>
    internal static bool EvaluationInputTruncated(Presentation presentation) =>
        presentation.Slides.Any(static slide =>
            slide.Shapes.Any(static shape => !IsEvaluationWatermark(shape) && CutByEvaluation(ShapeText(shape)))
            || CutByEvaluation(Notes(slide)));

    /// <summary>Whether evaluation mode replaced the read text with its truncation notice.</summary>
    internal static bool CutByEvaluation(string? text) =>
        text?.Contains(EvaluationTruncationMarker, StringComparison.OrdinalIgnoreCase) == true;
}

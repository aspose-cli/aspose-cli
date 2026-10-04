using System.Globalization;
using System.Text;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Slides;

/// <summary>Conservative deterministic checks that complement required AI image inspection.</summary>
internal static class SlidesReviewAnalyzer
{
    private const double GeometryTolerance = 0.5;

    // Laid-out lines include their line spacing, which can sit a little outside the glyphs.
    private const double TextSlideTolerance = 2;
    private const double TextShapeTolerance = 4;
    internal const double SevereCoverage = 0.80;
    internal const double ChartCoverage = 0.30;
    private const string Hint = "Inspect the rendered evidence, adjust only confirmed layout defects, save, and review again.";

    /// <summary>
    /// Analyzes the slides. The watermark text boxes that evaluation saves add, which only an
    /// evaluation-mode read marks, are left out and counted.
    /// </summary>
    public static SlidesReviewAnalysis Analyze(
        IReadOnlyList<SlideData> slides,
        double slideWidth,
        double slideHeight)
    {
        var result = new SlidesReviewAnalysis
        {
            ExcludedEvaluationWatermarks = slides.Sum(static slide => slide.Shapes.Count(static shape => shape.EvaluationWatermark)),
        };
        slides = slides
            .Select(static slide => slide with { Shapes = slide.Shapes.Where(static shape => !shape.EvaluationWatermark).ToArray() })
            .ToArray();
        AnalyzeDuplicates(slides, result);
        foreach (SlideData slide in slides)
        {
            AnalyzeSlide(slide, slideWidth, slideHeight, result);
        }
        return result;
    }

    private static void AnalyzeDuplicates(
        IReadOnlyList<SlideData> slides,
        SlidesReviewAnalysis result)
    {
        var firstByFingerprint = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (SlideData slide in slides)
        {
            string? fingerprint = Fingerprint(slide);
            if (fingerprint is null)
            {
                continue;
            }
            if (firstByFingerprint.TryGetValue(fingerprint, out int first))
            {
                result.DuplicateSlides++;
                // A duplicate concerns two slides, so it names no single part and keeps every image as evidence.
                result.Findings.Add(SlidesReviewChecks.SlideDuplicate.Finding(
                    $"Slide {slide.Slide} has the same meaningful content and geometry as slide {first}; inspect both before removing either one.",
                    Location(slide.Slide),
                    Hint));
            }
            else
            {
                firstByFingerprint.Add(fingerprint, slide.Slide);
            }
        }
    }

    private static void AnalyzeSlide(
        SlideData slide,
        double slideWidth,
        double slideHeight,
        SlidesReviewAnalysis result)
    {
        if (IsBlank(slide))
        {
            result.BlankSlides++;
            result.Findings.Add(SlidesReviewChecks.SlideBlank.Finding(
                "The slide has no visible authored content; confirm that it is intentional.",
                Location(slide.Slide),
                Hint,
                Part(slide)));
        }

        foreach (SlideShapeData shape in slide.Shapes)
        {
            AddShapeFindings(slide, shape, slideWidth, slideHeight, result);
        }
        AnalyzeDensity(slide, slideWidth, slideHeight, result);
        AnalyzeOverlaps(slide, slideWidth, slideHeight, result);
        AnalyzeTextOverObjects(slide, slideWidth, slideHeight, result);
        AnalyzeEmptyPlaceholders(slide, result);
    }

    /// <summary>
    /// Text laid out over a table or chart is garbled whichever is in front, and an opaque shape
    /// in front of text hides it however small the shape is. The frames cannot show it, since a
    /// placeholder is usually far taller than its text, so this compares the laid-out text with
    /// the object. Text over a picture is left alone: captions often are.
    /// </summary>
    private static void AnalyzeTextOverObjects(
        SlideData slide,
        double slideWidth,
        double slideHeight,
        SlidesReviewAnalysis result)
    {
        double slideArea = Math.Max(1, slideWidth * slideHeight);
        foreach (SlideShapeData text in slide.Shapes.Where(static shape => shape.TextRect is not null && !IsDecorative(shape)))
        {
            SlideRect lines = text.TextRect!;
            foreach (SlideShapeData other in slide.Shapes.Where(shape => shape.ShapeId != text.ShapeId
                && (shape.Type is "table" or "chart" || shape.ZOrder > text.ZOrder && IsOccluder(shape, slideArea))))
            {
                double overlap = IntersectionArea(lines, other.Rect);
                if (overlap < 0.10 * Math.Max(1, Area(lines)))
                {
                    continue;
                }

                result.TextOverlaps++;
                result.Findings.Add(SlidesReviewChecks.TextOverlapsObject.Finding(
                    $"The text of {Label(text)} runs into {other.Type} {Label(other)} ({overlap / Math.Max(1, Area(lines)):P0} of the text area); move or shorten one of them.",
                    Location(slide.Slide),
                    Hint,
                    Part(slide)));
                break;
            }
        }
    }

    private static void AnalyzeEmptyPlaceholders(SlideData slide, SlidesReviewAnalysis result)
    {
        foreach (SlideShapeData shape in slide.Shapes.Where(static shape =>
                     shape.Placeholder is not (null or "footer" or "date" or "slide-number")
                     && shape.Type is not ("chart" or "table" or "image" or "audio" or "video")
                     && string.IsNullOrWhiteSpace(shape.Text)))
        {
            result.EmptyPlaceholders++;
            result.Findings.Add(SlidesReviewChecks.PlaceholderEmpty.Finding(
                $"Placeholder {Label(shape)} is empty; PowerPoint shows its prompt text while the deck is edited. Delete it or fill it.",
                Location(slide.Slide),
                Hint,
                Part(slide)));
        }
    }

    private static void AddShapeFindings(
        SlideData slide,
        SlideShapeData shape,
        double slideWidth,
        double slideHeight,
        SlidesReviewAnalysis result)
    {
        SlideRect rect = shape.Rect;
        if (rect.X < -GeometryTolerance
            || rect.Y < -GeometryTolerance
            || rect.X + rect.Width > slideWidth + GeometryTolerance
            || rect.Y + rect.Height > slideHeight + GeometryTolerance)
        {
            result.OutsideShapes++;
            result.Findings.Add(SlidesReviewChecks.ShapeOutsideSlide.Finding(
                $"Shape {Label(shape)} extends outside the slide.",
                Location(slide.Slide),
                Hint,
                Part(slide)));
        }
        else
        {
            AddTextPlacementFindings(slide, shape, slideWidth, slideHeight, result);
        }

        double minimum = shape.Runs?
            .Where(static run => !string.IsNullOrWhiteSpace(run.Text) && run.Size is > 0)
            .Select(static run => run.Size!.Value)
            .DefaultIfEmpty()
            .Min() ?? 0;
        if (minimum is > 0 and < 12)
        {
            result.SmallTextShapes++;
            result.Findings.Add(SlidesReviewChecks.TextTooSmall.Finding(
                $"Shape {Label(shape)} contains text below 12 pt.",
                Location(slide.Slide),
                Hint,
                Part(slide)));
        }
    }

    /// <summary>
    /// The frame of a shape says nothing about where its text ends up: a bottom-anchored title
    /// that wraps onto more lines grows upward, past its frame and off the top of the slide.
    /// Text cut off by a slide edge is reported alone; otherwise text spilling out of a shape
    /// that neither grows to fit it nor shrinks it on overflow is reported. A shape already
    /// outside the slide is not checked again here.
    /// </summary>
    private static void AddTextPlacementFindings(
        SlideData slide,
        SlideShapeData shape,
        double slideWidth,
        double slideHeight,
        SlidesReviewAnalysis result)
    {
        if (shape.TextRect is not { } text)
        {
            return;
        }

        var page = new SlideRect { X = 0, Y = 0, Width = slideWidth, Height = slideHeight };
        if (Overshoot(text, page, TextSlideTolerance) is { } cut)
        {
            result.TextOutsideSlide++;
            result.Findings.Add(SlidesReviewChecks.TextOutsideSlide.Finding(
                string.Create(CultureInfo.InvariantCulture, $"The text of {Label(shape)} runs {cut.Points:0} pt past the {cut.Edges} edge of the slide, which cuts it off; shorten the text, reduce its size, or enlarge the shape away from that edge."),
                Location(slide.Slide),
                Hint,
                Part(slide)));
            return;
        }

        if (!shape.TextAutofits && Overshoot(text, shape.Rect, TextShapeTolerance) is { } spill)
        {
            result.TextOverflows++;
            result.Findings.Add(SlidesReviewChecks.TextOverflowsShape.Finding(
                string.Create(CultureInfo.InvariantCulture, $"The text of {Label(shape)} spills {spill.Points:0} pt out of the {spill.Edges} of its shape; shorten the text, reduce its size, or enlarge the shape."),
                Location(slide.Slide),
                Hint,
                Part(slide)));
        }
    }

    /// <summary>The edges of <paramref name="bounds"/> that <paramref name="inner"/> passes by more than the tolerance, and by how much at most.</summary>
    private static (string Edges, double Points)? Overshoot(SlideRect inner, SlideRect bounds, double tolerance)
    {
        (string Edge, double Points)[] edges =
        [
            ("top", bounds.Y - inner.Y),
            ("bottom", inner.Y + inner.Height - (bounds.Y + bounds.Height)),
            ("left", bounds.X - inner.X),
            ("right", inner.X + inner.Width - (bounds.X + bounds.Width)),
        ];
        (string Edge, double Points)[] passed = edges.Where(edge => edge.Points > tolerance).ToArray();
        return passed.Length == 0
            ? null
            : (string.Join(" and ", passed.Select(static edge => edge.Edge)), passed.Max(static edge => edge.Points));
    }

    /// <summary>
    /// Density counts content objects and their characters. Text that evaluation mode replaced
    /// with the SDK's truncation marker has lost its length, so on such a slide only the object
    /// count is judged; the review's EVAL_INPUT_TRUNCATED warning discloses the replacement.
    /// </summary>
    private static void AnalyzeDensity(
        SlideData slide,
        double slideWidth,
        double slideHeight,
        SlidesReviewAnalysis result)
    {
        SlideShapeData[] content = slide.Shapes.Where(IsContent).ToArray();
        int characters = content.Sum(static shape => shape.Text?.Length ?? 0);
        bool textMeasured = !TextReplacedByEvaluation(slide);
        bool high = content.Length >= 18
            || (textMeasured && (characters >= 1400 || (content.Length >= 12 && characters >= 800)));
        if (high)
        {
            result.HighDensitySlides++;
            result.Findings.Add(SlidesReviewChecks.ContentDensityHigh.Finding(
                textMeasured
                    ? $"The slide contains {content.Length} content objects and {characters} text characters; inspect readability and consider splitting it."
                    : $"The slide contains {content.Length} content objects; inspect readability and consider splitting it.",
                Location(slide.Slide),
                Hint,
                Part(slide)));
            return;
        }

        bool hasRichMedia = content.Any(static shape => shape.Type is "chart" or "table" or "image" or "video");
        double occupied = BoundingArea(content) / Math.Max(1, slideWidth * slideHeight);
        if (textMeasured
            && content.Length >= 3
            && !hasRichMedia
            && characters is > 0 and < 40
            && occupied < 0.12)
        {
            result.LowDensitySlides++;
            result.Findings.Add(SlidesReviewChecks.ContentDensityLow.Finding(
                $"Three or more content objects occupy only {occupied:P0} of the slide with very little text; inspect for content stranded in a corner.",
                Location(slide.Slide),
                Hint,
                Part(slide)));
        }
    }

    private static void AnalyzeOverlaps(
        SlideData slide,
        double slideWidth,
        double slideHeight,
        SlidesReviewAnalysis result)
    {
        double slideArea = Math.Max(1, slideWidth * slideHeight);
        SlideShapeData[] ordered = slide.Shapes.OrderBy(static shape => shape.ZOrder).ToArray();
        for (int lowerIndex = 0; lowerIndex < ordered.Length; lowerIndex++)
        {
            SlideShapeData lower = ordered[lowerIndex];
            if (!IsContent(lower) || IsDecorative(lower))
            {
                continue;
            }
            for (int upperIndex = lowerIndex + 1; upperIndex < ordered.Length; upperIndex++)
            {
                SlideShapeData upper = ordered[upperIndex];
                if (!IsOccluder(upper, slideArea))
                {
                    continue;
                }

                double intersection = IntersectionArea(lower.Rect, upper.Rect);
                double lowerArea = Area(lower.Rect);
                if (intersection / Math.Max(1, slideArea) < 0.02 || lowerArea <= 0)
                {
                    continue;
                }
                double covered = intersection / lowerArea;
                if (lower.Type == "chart" && covered >= ChartCoverage)
                {
                    result.CoveredCharts++;
                    result.Findings.Add(SlidesReviewChecks.ChartCovered.Finding(
                        $"Opaque foreground shape {Label(upper)} covers {covered:P0} of chart {Label(lower)}; verify the rendered slide before changing it.",
                        Location(slide.Slide),
                        Hint,
                        Part(slide)));
                    break;
                }
                if (covered >= SevereCoverage)
                {
                    result.SevereOverlaps++;
                    result.Findings.Add(SlidesReviewChecks.ShapesOverlap.Finding(
                        $"Opaque foreground shape {Label(upper)} covers {covered:P0} of content shape {Label(lower)}; verify that this is intentional.",
                        Location(slide.Slide),
                        Hint,
                        Part(slide)));
                    break;
                }
            }
        }
    }

    private static bool IsBlank(SlideData slide) =>
        slide.Shapes.Count == 0
        || slide.Shapes.All(static shape =>
            string.IsNullOrWhiteSpace(shape.Text)
            && shape.Type is not ("chart" or "table" or "image" or "audio" or "video"));

    private static bool IsContent(SlideShapeData shape) =>
        !string.IsNullOrWhiteSpace(shape.Text)
        || shape.Type is "chart" or "table" or "image" or "audio" or "video";

    private static bool IsOccluder(SlideShapeData shape, double slideArea)
    {
        if (IsDecorative(shape) || Area(shape.Rect) / slideArea > 0.80)
        {
            return false;
        }
        return shape.HasOpaqueFill || shape.Type is "chart" or "table" or "video";
    }

    private static bool IsDecorative(SlideShapeData shape)
    {
        if (shape.Placeholder is "footer" or "date" or "slide-number")
        {
            return true;
        }
        string name = shape.ShapeName ?? string.Empty;
        return DecorativeNames.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] DecorativeNames =
    [
        "background", "decor", "accent", "separator", "border", "shadow",
        "watermark", "overlay", "callout",
    ];

    private static string? Fingerprint(SlideData slide)
    {
        if (slide.ContentTruncated || TextReplacedByEvaluation(slide))
        {
            return null;
        }
        int characters = slide.Shapes.Sum(static shape => shape.Text?.Length ?? 0);
        bool hasMedia = slide.Shapes.Any(static shape => shape.Type is "chart" or "table" or "image" or "video");
        if (characters < 20 && !hasMedia)
        {
            return null;
        }

        var value = new StringBuilder();
        foreach (SlideShapeData shape in slide.Shapes.Where(static shape => !IsDecorative(shape)))
        {
            value.Append(shape.Type).Append('|').Append(shape.Placeholder).Append('|')
                .Append(Normalize(shape.Text)).Append('|')
                .Append(Geometry(shape.Rect)).Append(';');
        }
        return value.ToString();
    }

    private static bool TextReplacedByEvaluation(SlideData slide) =>
        slide.Shapes.Any(static shape => shape.Text?.Contains(
            SlidesEngineSupport.EvaluationTruncationMarker,
            StringComparison.OrdinalIgnoreCase) == true);

    private static string Normalize(string? text) => string.IsNullOrWhiteSpace(text)
        ? string.Empty
        : string.Join(' ', text.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    private static string Geometry(SlideRect rect) => string.Create(
        CultureInfo.InvariantCulture,
        $"{Math.Round(rect.X, 1)},{Math.Round(rect.Y, 1)},{Math.Round(rect.Width, 1)},{Math.Round(rect.Height, 1)}");

    private static double BoundingArea(IReadOnlyList<SlideShapeData> shapes)
    {
        if (shapes.Count == 0)
        {
            return 0;
        }
        double left = shapes.Min(static shape => shape.Rect.X);
        double top = shapes.Min(static shape => shape.Rect.Y);
        double right = shapes.Max(static shape => shape.Rect.X + shape.Rect.Width);
        double bottom = shapes.Max(static shape => shape.Rect.Y + shape.Rect.Height);
        return Math.Max(0, right - left) * Math.Max(0, bottom - top);
    }

    private static double IntersectionArea(SlideRect left, SlideRect right)
    {
        double width = Math.Min(left.X + left.Width, right.X + right.Width) - Math.Max(left.X, right.X);
        double height = Math.Min(left.Y + left.Height, right.Y + right.Height) - Math.Max(left.Y, right.Y);
        return Math.Max(0, width) * Math.Max(0, height);
    }

    private static double Area(SlideRect rect) => Math.Max(0, rect.Width) * Math.Max(0, rect.Height);

    /// <summary>A shape as findings name it: its name, when it has one, and the shapeId that edit operations address.</summary>
    private static string Label(SlideShapeData shape) => string.IsNullOrEmpty(shape.ShapeName)
        ? string.Create(CultureInfo.InvariantCulture, $"shapeId {shape.ShapeId}")
        : string.Create(CultureInfo.InvariantCulture, $"'{shape.ShapeName}' (shapeId {shape.ShapeId})");

    private static string Location(int slide) => string.Create(CultureInfo.InvariantCulture, $"slide {slide}");

    /// <summary>The view part of the slide a finding concerns, so its evidence is that slide's image.</summary>
    private static string Part(SlideData slide) => SlidesViews.PartId(slide.SlideId);
}

internal sealed class SlidesReviewAnalysis
{
    public List<ReviewFinding> Findings { get; } = [];
    public int BlankSlides { get; set; }
    public int OutsideShapes { get; set; }
    public int SmallTextShapes { get; set; }
    public int DuplicateSlides { get; set; }
    public int SevereOverlaps { get; set; }
    public int CoveredCharts { get; set; }
    public int HighDensitySlides { get; set; }
    public int LowDensitySlides { get; set; }
    public int TextOverlaps { get; set; }
    public int TextOutsideSlide { get; set; }
    public int TextOverflows { get; set; }
    public int EmptyPlaceholders { get; set; }
    public int ExcludedEvaluationWatermarks { get; init; }
}

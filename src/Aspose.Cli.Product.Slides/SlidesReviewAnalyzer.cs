using System.Globalization;
using System.Text;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Slides;

/// <summary>Conservative deterministic checks that complement required AI image inspection.</summary>
internal static class SlidesReviewAnalyzer
{
    private const double GeometryTolerance = 0.5;
    private const double SevereCoverage = 0.80;
    private const double ChartCoverage = 0.30;

    public static SlidesReviewAnalysis Analyze(
        IReadOnlyList<SlideData> slides,
        double slideWidth,
        double slideHeight)
    {
        var result = new SlidesReviewAnalysis();
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
                result.Findings.Add(Finding(
                    "SLIDES_DUPLICATE_SLIDE",
                    $"Slide {slide.Slide} has the same meaningful content and geometry as slide {first}; inspect both before removing either one.",
                    slide.Slide));
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
            result.Findings.Add(Finding(
                "SLIDES_BLANK_SLIDE",
                "The slide has no visible authored content; confirm that it is intentional.",
                slide.Slide));
        }

        foreach (SlideShapeData shape in slide.Shapes)
        {
            AddShapeFindings(slide.Slide, shape, slideWidth, slideHeight, result);
        }
        AnalyzeDensity(slide, slideWidth, slideHeight, result);
        AnalyzeOverlaps(slide, slideWidth, slideHeight, result);
    }

    private static void AddShapeFindings(
        int slideNumber,
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
            result.Findings.Add(Finding(
                "SLIDES_SHAPE_OUTSIDE_CANVAS",
                $"Shape '{Label(shape)}' extends outside the slide canvas.",
                slideNumber));
        }

        double minimum = shape.Runs?
            .Where(static run => !string.IsNullOrWhiteSpace(run.Text) && run.Size is > 0)
            .Select(static run => run.Size!.Value)
            .DefaultIfEmpty()
            .Min() ?? 0;
        if (minimum is > 0 and < 12)
        {
            result.SmallTextShapes++;
            result.Findings.Add(Finding(
                "SLIDES_SMALL_TEXT",
                $"Shape '{Label(shape)}' contains text below 12 pt.",
                slideNumber));
        }
    }

    private static void AnalyzeDensity(
        SlideData slide,
        double slideWidth,
        double slideHeight,
        SlidesReviewAnalysis result)
    {
        SlideShapeData[] content = slide.Shapes.Where(IsContent).ToArray();
        int characters = content.Sum(static shape => shape.Text?.Length ?? 0);
        bool high = content.Length >= 18
            || characters >= 1400
            || (content.Length >= 12 && characters >= 800);
        if (high)
        {
            result.HighDensitySlides++;
            result.Findings.Add(Finding(
                "SLIDES_CONTENT_DENSITY_HIGH",
                $"The slide contains {content.Length} content objects and {characters} text characters; inspect readability and consider splitting it.",
                slide.Slide));
            return;
        }

        bool hasRichMedia = content.Any(static shape => shape.Type is "chart" or "table" or "image" or "video");
        double occupied = BoundingArea(content) / Math.Max(1, slideWidth * slideHeight);
        if (content.Length >= 3
            && !hasRichMedia
            && characters is > 0 and < 40
            && occupied < 0.12)
        {
            result.LowDensitySlides++;
            result.Findings.Add(Finding(
                "SLIDES_CONTENT_DENSITY_LOW",
                $"Three or more content objects occupy only {occupied:P0} of the slide with very little text; inspect for content stranded in a corner.",
                slide.Slide));
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
                    result.Findings.Add(Finding(
                        "SLIDES_CHART_COVERED",
                        $"Opaque foreground shape '{Label(upper)}' covers {covered:P0} of chart '{Label(lower)}'; verify the rendered slide before changing it.",
                        slide.Slide));
                    break;
                }
                if (covered >= SevereCoverage)
                {
                    result.SevereOverlaps++;
                    result.Findings.Add(Finding(
                        "SLIDES_SEVERE_OBJECT_OVERLAP",
                        $"Opaque foreground shape '{Label(upper)}' covers {covered:P0} of content shape '{Label(lower)}'; verify that this is intentional.",
                        slide.Slide));
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
        if (slide.ContentTruncated
            || slide.Shapes.Any(static shape => shape.Text?.Contains(
                SlidesEngineSupport.EvaluationTruncationMarker,
                StringComparison.OrdinalIgnoreCase) == true))
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

    private static string Label(SlideShapeData shape) => shape.ShapeName ?? shape.ShapeId.ToString(CultureInfo.InvariantCulture);

    private static ReviewFinding Finding(string code, string message, int slide) => new()
    {
        Code = code,
        Severity = "warning",
        Message = message,
        Location = $"slide {slide}",
        Hint = "Inspect the rendered evidence, adjust only confirmed layout defects, save, and review again.",
    };
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
}

using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The review adapter declares exactly the checks its analyzer can report, so a new finding
/// cannot ship without a declaration and a declaration cannot outlive its finding.
/// </summary>
public sealed class SlidesReviewCheckTests
{
    private const double Width = 720;
    private const double Height = 540;

    [Fact]
    public void Analyze_EmitsEveryDeclaredCheckAndNothingElse()
    {
        SlideShapeData repeated = Shape(1, new(50, 50, 300, 100), "Repeated content long enough to compare");
        SlideData[] slides =
        [
            Slide(1, repeated),
            Slide(2, repeated),
            Slide(3),
            Slide(4, Shape(1, new(700, 50, 100, 50), "Hi") with
            {
                Runs = [new SlideTextRunData { Text = "Hi", Size = 10 }],
            }),
            Slide(5, [.. Enumerable.Range(1, 18).Select(id => Shape(id, new(id * 30, 400, 20, 20), "Item"))]),
            Slide(6, Shape(1, new(0, 0, 10, 10), "a"), Shape(2, new(20, 0, 10, 10), "b"), Shape(3, new(0, 20, 10, 10), "c")),
            Slide(7, Shape(1, new(100, 100, 200, 200), type: "chart"), Occluder(2, new(100, 100, 100, 200))),
            Slide(8, Shape(1, new(100, 100, 200, 200), "Body"), Occluder(2, new(100, 100, 200, 200))),
            Slide(9, BodyWithLines(1, new(60, 150, 400, 60)), Shape(2, new(60, 195, 600, 280), type: "table")),
            Slide(10, Shape(1, new(60, 100, 600, 300)) with { Placeholder = "body" }),
        ];

        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(slides, Width, Height);

        Assert.Equal(
            SlidesReviewChecks.All.Select(static check => check.Code).Order(StringComparer.Ordinal),
            analysis.Findings.Select(static finding => finding.Code).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TextAboveATable_IsNotAnOverlap()
    {
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            [Slide(1, BodyWithLines(1, new(60, 120, 400, 60)), Shape(2, new(60, 195, 600, 280), type: "table"))],
            Width,
            Height);

        Assert.DoesNotContain(analysis.Findings, static finding => finding.Code == SlidesReviewChecks.TextOverlapsObject.Code);
    }

    [Fact]
    public void TextRect_IsWhereTheTextIsLaidOutOnTheSlide()
    {
        using var presentation = new Aspose.Slides.Presentation();
        Aspose.Slides.IAutoShape box = presentation.Slides[0].Shapes.AddAutoShape(Aspose.Slides.ShapeType.Rectangle, 100, 150, 400, 300);
        box.TextFrame.Text = "A summary line";
        box.TextFrame.TextFrameFormat.AnchoringType = Aspose.Slides.TextAnchorType.Top;

        SlideRect rect = Assert.IsType<SlideRect>(Engine.Mapping.SlidesReviewProjection.TextRect(box));

        Assert.InRange(rect.Y, 150, 170);
        Assert.InRange(rect.Height, 10, 60);
        Assert.True(rect.X >= 100 && rect.X + rect.Width <= 500);
    }

    private static SlideShapeData BodyWithLines(long id, Rect lines) =>
        Shape(id, new(lines.X, lines.Y, lines.Width, 350), "Two lines of summary text above the table") with
        {
            Placeholder = "body",
            TextRect = new SlideRect { X = lines.X, Y = lines.Y, Width = lines.Width, Height = lines.Height },
        };

    private static SlideData Slide(int number, params SlideShapeData[] shapes) => new()
    {
        Slide = number,
        SlideId = (uint)(255 + number),
        Shapes = shapes,
        ContentTruncated = false,
    };

    private static SlideShapeData Shape(long id, Rect rect, string? text = null, string type = "shape") => new()
    {
        ShapeId = id,
        ShapeName = $"Shape {id}",
        Type = type,
        Text = text,
        Rect = new SlideRect { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height },
        ZOrder = (int)id,
    };

    private static SlideShapeData Occluder(long id, Rect rect) => Shape(id, rect) with { HasOpaqueFill = true };

    private readonly record struct Rect(double X, double Y, double Width, double Height);
}

using System.Drawing;
using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>Resolves mutation targets and translates handler failures.</summary>
internal static class SlidesMutationSupport
{
    private const string ShapeListing = "'slides query slides --scope shapes' lists each shape's id, name and role";

    internal static ISlide ResolveSlide(Presentation presentation, SlideTargetOp op)
    {
        if (op.Slide is int number)
        {
            return number <= presentation.Slides.Count
                ? presentation.Slides[number - 1]
                : throw SlideNotFound(number, presentation.Slides.Count);
        }

        ISlide? slide = presentation.Slides.FirstOrDefault(item => item.SlideId == op.SlideId);
        return slide ?? throw CliErrors.NotFound(
            SlidesDiagnostics.SlideNotFound,
            "slide id",
            Invariant(op.SlideId!.Value),
            presentation.Slides.Select(static item => Invariant(item.SlideId)).ToArray(),
            hint: "Use a slideId from details.available, or address the slide by its number with 'slide'.");
    }

    internal static IReadOnlyList<ISlide> ResolveSlides(Presentation presentation, string range) =>
        ResolveSlideRange(PageRange.Parse(range), presentation.Slides.Count)
            .Select(number => presentation.Slides[number - 1])
            .ToArray();

    internal static IReadOnlyList<ISlide> ResolveOptionalSlides(Presentation presentation, string? range) =>
        range is null ? presentation.Slides.ToArray() : ResolveSlides(presentation, range);

    /// <summary>
    /// Finds the top-level shape an operation names by id, name or placeholder role. A name or
    /// role that several shapes share is refused rather than resolved to the first of them.
    /// </summary>
    internal static IShape ResolveShape(ISlide slide, ShapeTargetOp op)
    {
        IShape[] shapes = slide.Shapes.ToArray();
        if (op.ShapeId is long shapeId)
        {
            return shapes.FirstOrDefault(item => item.OfficeInteropShapeId == shapeId)
                ?? throw CliErrors.NotFound(
                    SlidesDiagnostics.ShapeNotFound,
                    "shape id",
                    Invariant(shapeId),
                    shapes.Select(static item => Invariant(item.OfficeInteropShapeId)).ToArray(),
                    hint: $"Use a shape id from details.available; {ShapeListing}.");
        }

        if (op.ShapeName is { } name)
        {
            return Unique(shapes.Where(item => string.Equals(item.Name, name, StringComparison.Ordinal)), $"Shape name '{name}'")
                ?? throw CliErrors.NotFound(
                    SlidesDiagnostics.ShapeNotFound,
                    "shape",
                    name,
                    Names(shapes.Select(static item => item.Name)),
                    hint: $"Use a shape name from details.available, or address the shape by its 'shapeId'; {ShapeListing}.");
        }

        string role = op.Placeholder!;
        return Unique(shapes.Where(item => PlaceholderRole(item) == role), $"Placeholder role '{role}'")
            ?? throw CliErrors.NotFound(
                SlidesDiagnostics.PlaceholderNotFound,
                "placeholder",
                role,
                Names(shapes.Select(PlaceholderRole)),
                hint: $"Use a placeholder role from details.available, or address the shape by its 'shapeId' or 'shapeName'; {ShapeListing}.");
    }

    internal static ILayoutSlide ResolveLayout(Presentation presentation, string name)
    {
        ILayoutSlide? layout = presentation.LayoutSlides.FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        return layout ?? throw CliErrors.NotFound(
            SlidesDiagnostics.LayoutNotFound,
            "layout",
            name,
            Names(presentation.LayoutSlides.Select(static item => item.Name)));
    }

    private static string? PlaceholderRole(IShape shape) => SlidesPlaceholders.Role(shape.Placeholder?.Type);

    private static IShape? Unique(IEnumerable<IShape> matches, string description)
    {
        IShape[] found = matches.ToArray();
        if (found.Length > 1)
        {
            throw new OperationInvalidException(
                $"{description} matches {found.Length} shapes on this slide (ids {string.Join(", ", found.Select(static item => Invariant(item.OfficeInteropShapeId)))}).",
                "Address the shape by its 'shapeId' instead.");
        }

        return found.FirstOrDefault();
    }

    /// <summary>The distinct non-empty names in document order, as not-found errors list them.</summary>
    private static string[] Names(IEnumerable<string?> names) =>
        names.OfType<string>().Where(static item => item.Trim().Length > 0).Distinct(StringComparer.Ordinal).ToArray();

    private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);

    internal static ChartType ChartTypeFor(string kind) => kind switch
    {
        SlidesChartKinds.Bar => ChartType.ClusteredBar,
        SlidesChartKinds.Column => ChartType.ClusteredColumn,
        SlidesChartKinds.Line => ChartType.LineWithMarkers,
        SlidesChartKinds.Pie => ChartType.Pie,
        SlidesChartKinds.Scatter => ChartType.ScatterWithStraightLinesAndMarkers,
        _ => throw ChartDataInvalid($"Unknown chart kind '{kind}'."),
    };

    internal static Color ParseColor(string value) => Color.FromArgb(
        int.Parse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    internal static void EnsureFile(string path)
    {
        if (!File.Exists(path))
        {
            throw CliErrors.FileNotFound(path);
        }
    }

    /// <summary>A slide number, or with <paramref name="subject"/> a slide position, past the <paramref name="count"/> there are.</summary>
    internal static CliException SlideNotFound(int requested, int count, string subject = "slide") =>
        CliErrors.NotFoundAt(SlidesDiagnostics.SlideNotFound, subject, Invariant(requested), count);

    internal static CliException ChartDataInvalid(string reason) => new(
        SlidesDiagnostics.ChartDataInvalid,
        $"Slides chart data is invalid: {reason}",
        hint: "Use matching category and series lengths with a supported chart kind.");

}


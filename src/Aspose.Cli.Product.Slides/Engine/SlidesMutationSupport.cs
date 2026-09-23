using System.Drawing;
using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Resolves mutation targets and translates handler failures.</summary>
internal static class SlidesMutationSupport
{
    internal static ISlide ResolveSlide(Presentation presentation, SlideTargetOp op)
    {
        if (op.Slide is int number)
        {
            return number <= presentation.Slides.Count
                ? presentation.Slides[number - 1]
                : throw SlideNotFound(number, presentation.Slides.Count);
        }

        ISlide? slide = presentation.Slides.FirstOrDefault(item => item.SlideId == op.SlideId);
        return slide ?? throw new CliException(
            SlidesDiagnostics.SlideNotFound,
            $"Slide id {op.SlideId} was not found.",
            hint: "Run 'slides query slides' and use a current slide number or slideId.");
    }

    internal static IReadOnlyList<ISlide> ResolveSlides(Presentation presentation, string range) =>
        ResolveSlideRange(PageRange.Parse(range), presentation.Slides.Count)
            .Select(number => presentation.Slides[number - 1])
            .ToArray();

    internal static IReadOnlyList<ISlide> ResolveOptionalSlides(Presentation presentation, string? range) =>
        range is null ? presentation.Slides.ToArray() : ResolveSlides(presentation, range);

    internal static IShape ResolveShape(ISlide slide, ShapeTargetOp op)
    {
        IShape? shape = op.Shape is long shapeId
            ? slide.Shapes.FirstOrDefault(item => item.OfficeInteropShapeId == shapeId)
            : op.ShapeName is not null
                ? slide.Shapes.FirstOrDefault(item => string.Equals(item.Name, op.ShapeName, StringComparison.Ordinal))
                : slide.Shapes.FirstOrDefault(item =>
                    string.Equals(SlidesPlaceholders.Role(item.Placeholder?.Type), op.Placeholder, StringComparison.Ordinal));
        if (shape is not null)
        {
            return shape;
        }

        string[] available = slide.Shapes.Take(30)
            .Select(item => $"{item.OfficeInteropShapeId}:{item.Name}")
            .ToArray();
        ErrorCode code = op.Placeholder is null ? ErrorCodes.ShapeNotFound : SlidesDiagnostics.PlaceholderNotFound;
        throw new CliException(
            code,
            op.Placeholder is null
                ? "The requested slide shape was not found."
                : $"Placeholder role '{op.Placeholder}' was not found on slide {slide.SlideId}.",
            hint: "Run 'slides query slides --scope shapes' and use a current shape id, name, or placeholder role.",
            details: new JsonObject
            {
                ["available"] = new JsonArray(
                    available.Select(static value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            });
    }

    internal static ILayoutSlide ResolveLayout(Presentation presentation, string name)
    {
        ILayoutSlide? layout = presentation.LayoutSlides.FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        return layout ?? throw new CliException(
            ErrorCodes.LayoutNotFound,
            $"Layout '{name}' was not found.",
            hint: "Run 'slides inspect --detail layouts' and use an available layout name.",
            details: new JsonObject
            {
                ["available"] = new JsonArray(
                    presentation.LayoutSlides.Select(static item => JsonValue.Create(item.Name)).Take(50).ToArray()),
            });
    }

    internal static ChartType ChartTypeFor(string kind) => kind switch
    {
        "bar" => ChartType.ClusteredBar,
        "column" => ChartType.ClusteredColumn,
        "line" => ChartType.LineWithMarkers,
        "pie" => ChartType.Pie,
        "scatter" => ChartType.ScatterWithStraightLinesAndMarkers,
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

    internal static CliException SlideNotFound(int requested, int available) => new(
        SlidesDiagnostics.SlideNotFound,
        $"Requested slide {requested} exceeds the available count of {available}.",
        hint: available > 0 ? $"Use a slide from 1 through {available}." : "Add a slide first.");

    internal static CliException ChartDataInvalid(string reason) => new(
        SlidesDiagnostics.ChartDataInvalid,
        $"Slides chart data is invalid: {reason}",
        hint: "Use matching category and series lengths with a supported chart kind.");

}


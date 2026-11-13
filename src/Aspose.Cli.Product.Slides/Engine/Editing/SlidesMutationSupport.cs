using System.Drawing;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.SlidesSlideSelection;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>Resolves mutation targets and translates handler failures.</summary>
internal static class SlidesMutationSupport
{
    internal const string ShapeListing = "'slides read --content shapes' lists each shape's shapeId, shapeName and placeholder";

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

    /// <summary>
    /// The shape whose text set_title replaces, the slide's title shape
    /// (<see cref="SlidesAuthoring.Title"/>). A slide without one is refused rather than given a
    /// box over whatever it shows, and the refusal says how it gets one (<see cref="TitledBy"/>).
    /// </summary>
    internal static IAutoShape ResolveTitle(Presentation presentation, ISlide slide) =>
        SlidesAuthoring.Title(slide)
            ?? throw CliErrors.NotFound(
                SlidesDiagnostics.PlaceholderNotFound,
                "placeholder",
                SlidesPlaceholderRoles.Title,
                Names(slide.Shapes.Select(PlaceholderRole)),
                hint: $"set_title writes a slide's title placeholder, and this slide has none{TitledBy(presentation, slide)}. Text that set_text writes into one of its shapes, or a text box insert_shape adds, shows on the slide but is not its title; {ShapeListing}.");

    /// <summary>
    /// How the slide gets a title placeholder that overwrites none it has: the set_layout of a layout
    /// of its master with one, its own first and Title Only next, that set_layout finds by its name
    /// and that keeps the role of each placeholder (<see cref="SlidesPlaceholders.Keeps"/>), or of
    /// each but the footers, dates and slide numbers, which the batch deletes and set_footer sets again.
    /// </summary>
    private static string TitledBy(Presentation presentation, ISlide slide)
    {
        ILayoutSlide[] titled = [.. slide.LayoutSlide.MasterSlide.LayoutSlides
            .Where(item => SlidesPlaceholders.HasTitle(item) && !string.IsNullOrWhiteSpace(item.Name) && ResolveLayout(presentation, item.Name, item.MasterSlide) == item)
            .OrderBy(item => item != slide.LayoutSlide).ThenBy(static item => item.LayoutType != SlideLayoutType.TitleOnly)];
        IEnumerable<bool> Moved(ILayoutSlide layout) => // whether each placeholder it would not keep is a footer, date or slide number
            slide.Shapes.Where(shape => shape.Placeholder is { } placeholder && !SlidesPlaceholders.Keeps(layout, placeholder))
            .Select(static shape => PlaceholderRole(shape) is SlidesPlaceholderRoles.Footer or SlidesPlaceholderRoles.Date or SlidesPlaceholderRoles.SlideNumber);
        ILayoutSlide? layout = titled.FirstOrDefault(item => !Moved(item).Any()) ?? titled.FirstOrDefault(item => Moved(item).All(static footer => footer));
        bool footers = layout is not null && Moved(layout).Any();
        return layout is not null
            ? $$""". To give it one, {{(footers ? "first delete_shape each footer, date and slide-number placeholder of the slide, then " : "")}}put {"op":"set_layout","slides":"{{Invariant(presentation.Slides.IndexOf(slide) + 1)}}","layout":"{{JsonEncodedText.Encode(layout.Name!, JavaScriptEncoder.UnsafeRelaxedJsonEscaping)}}"} before this set_title in its batch, where the slide's number counts the slides as the operations before it leave them{{(footers ? ", and after the set_title set them again with one set_footer that gives the footer's text, showNumber and showDate" : "")}}; the slide keeps its {{(footers ? "other " : "")}}shapes, so delete_shape a text box that stood for its title"""
            : titled.Length > 0
                ? ". A layout with one could give the title role to a placeholder the slide has other than a footer, date or slide number, whose text set_title would then overwrite, so look at its placeholders before you change its layout"
                : ", and no layout of its master that set_layout can name has one";
    }

    /// <summary>
    /// The shape whose paragraphs set_body replaces: the slide's first body placeholder, else its
    /// subtitle, else the box authoring named Body where the layout had neither. A slide with none
    /// of them is refused rather than given a box over whatever it shows.
    /// </summary>
    internal static IAutoShape ResolveBody(ISlide slide) =>
        SlidesPlaceholders.Content(slide).FirstOrDefault()
            ?? SlidesPlaceholders.Content(slide, includeSubtitle: true).FirstOrDefault()
            ?? SlidesAuthoring.Named(slide, SlidesAuthoring.BodyName)
            ?? throw CliErrors.NotFound(
                SlidesDiagnostics.PlaceholderNotFound,
                "placeholder",
                SlidesPlaceholderRoles.Body,
                Names(slide.Shapes.Select(PlaceholderRole)),
                hint: $"set_body writes a body placeholder, or a subtitle where the slide has none, and this slide has neither: write the text into one of its shapes with set_text, or add a text box with insert_shape; {ShapeListing}.");

    /// <summary>
    /// The layout a name names, case-insensitively: a layout of <paramref name="master"/>, a
    /// slide's own, by that name first, else the first layout of the presentation by that name.
    /// </summary>
    internal static ILayoutSlide ResolveLayout(Presentation presentation, string name, IMasterSlide? master = null)
    {
        ILayoutSlide? layout = (master?.LayoutSlides ?? Enumerable.Empty<ILayoutSlide>()).Concat(presentation.LayoutSlides).FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        return layout ?? throw CliErrors.NotFound(
            SlidesDiagnostics.LayoutNotFound,
            "layout",
            name,
            Names(presentation.LayoutSlides.Select(static item => item.Name)));
    }

    internal static string? PlaceholderRole(IShape shape) => SlidesPlaceholders.Role(shape.Placeholder?.Type);

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


using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Ink;
using Aspose.Slides.SmartArt;
using Row = Aspose.Cli.Sdk.Tables.Row;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

internal static partial class SlidesTable
{
    /// <summary>
    /// A shape of a slide, addressed by its <c>OfficeInteropShapeId</c> among every shape of the
    /// slide, those in groups included: <c>slide/256/shape/3</c>; or by its placeholder:
    /// <c>slide/256/placeholder/Title</c>, <c>slide/256/placeholder/Body/1</c>.
    /// </summary>
    internal static readonly ObjectKind<IShape> ShapeKind = new("shape", "A shape of a slide, in a group or not.")
    {
        Row.UnsignedInteger<IShape>("shapeId", "IShape.OfficeInteropShapeId", static s => s.OfficeInteropShapeId),
        Row.Text<IShape>("type", "The engine interface of the shape, such as IAutoShape or IPictureFrame", static s => SlidesShapeTypes.Of(s)),
        Row.Text<IShape>("name", "IShape.Name", static s => s.Name, static (s, v) => s.Name = v),
        Row.Length<IShape>("x", "IShape.X", static s => s.X, static (s, v) => s.X = v),
        Row.Length<IShape>("y", "IShape.Y", static s => s.Y, static (s, v) => s.Y = v),
        Row.Length<IShape>("width", "IShape.Width", static s => s.Width, static (s, v) => s.Width = v),
        Row.Length<IShape>("height", "IShape.Height", static s => s.Height, static (s, v) => s.Height = v),
        Row.Text<IShape>("alternativeText", "IShape.AlternativeText", static s => s.AlternativeText, static (s, v) => s.AlternativeText = v),
        Row.Of<IShape, PlaceholderType?>("placeholder.type", ValueKinds.Optional(ValueKinds.Enum<PlaceholderType>()), "IShape.Placeholder.Type", static s => s.Placeholder?.Type),
        Row.Of<IShape, uint?>("placeholder.index", ValueKinds.Optional(ValueKinds.UnsignedInteger), "IShape.Placeholder.Index", static s => s.Placeholder?.Index),
        Fill<IShape>("fill", static s => s.FillFormat),
        // A group shape has no line: the engine's IGroupShape.LineFormat is null.
        LineFill<IGeometryShape>("line", static s => s.LineFormat.FillFormat),
        LineFill<IGraphicalObject>("line", static s => s.LineFormat.FillFormat),
        Row.Enum<IAutoShape, ShapeType>("shapeType", "IAutoShape.ShapeType", static s => s.ShapeType),
        Row.Text<IAutoShape>("text", "IAutoShape.TextFrame.Text", static s => s.TextFrame?.Text, static (s, v) => (s.TextFrame ?? throw TableErrors.Absent("IAutoShape.TextFrame")).Text = v),
        Row.Of<IPictureFrame, object?>("picture", SlidesValues.Picture, "IPictureFrame.PictureFormat.Picture.Image, as its position among the presentation's images",
            static s => SlidesValues.PictureIndex(s.Slide.Presentation, s.PictureFormat.Picture.Image)),
        Row.Integer<ITable>("rowCount", "ITable.Rows.Count", static t => t.Rows.Count),
        Row.Integer<ITable>("columnCount", "ITable.Columns.Count", static t => t.Columns.Count),
        Row.Boolean<ITable>("firstRow", "ITable.FirstRow", static t => t.FirstRow, static (t, v) => t.FirstRow = v),
        Row.Enum<IChart, ChartType>("chartType", "IChart.Type", static c => c.Type),
        Row.Boolean<IChart>("hasTitle", "IChart.HasTitle", static c => c.HasTitle, static (c, v) => c.HasTitle = v),
        Row.Boolean<IChart>("hasLegend", "IChart.HasLegend", static c => c.HasLegend, static (c, v) => c.HasLegend = v),
    };

    private static void PlaceShapes(DocumentTable<Presentation> table)
    {
        var shapes = new Locator<ISlide, IShape>
        {
            Segment = "shape",
            Key = [KeyPart.Id],
            Find = static (slide, key) => SlidesShapeTypes.All(slide.Shapes).FirstOrDefault(shape => shape.OfficeInteropShapeId == key.Id()),
            List = static slide => SlidesShapeTypes.All(slide.Shapes),
            KeyOf = static (_, shape) => Key.Of(shape.OfficeInteropShapeId),
            Delete = static (slide, shape) => SlidesShapeTypes.CollectionOf(slide.Shapes, shape)!.Remove(shape),
        };
        table.Place(SlideKind, ShapeKind, shapes);
        table.Alias(SlideKind, ShapeKind, new AliasLocator<ISlide, IShape>
        {
            Segment = "placeholder",
            Key = [KeyPart.Name],
            Optional = KeyPart.Number,
            Description = "The shape whose IShape.Placeholder.Type is the name, and whose Placeholder.Index is the number when one follows.",
            Find = static (slide, key) => [.. SlidesShapeTypes.All(slide.Shapes).Where(shape => shape.Placeholder is { } placeholder
                && Enum.GetName(placeholder.Type) == key.Name()
                && (!key.Has(1) || placeholder.Index == key.Number(1)))],
            List = static slide => SlidesShapeTypes.All(slide.Shapes)
                .Where(static shape => shape.Placeholder is not null)
                .Select(static shape => Key.Of(Enum.GetName(shape.Placeholder.Type)!, shape.Placeholder.Index)),
        });
        table.Adds(SlideKind, ShapeKind, new Adder<ISlide, IShape>
        {
            Name = "autoShape",
            Description = "IShapeCollection.AddAutoShape(ShapeType shapeType, float x, float y, float width, float height)",
            Arguments =
            [
                Argument.Of("shapeType", ValueKinds.Enum<ShapeType>(), "The engine's ShapeType of the new shape."),
                Argument.Of("x", ValueKinds.Length, "The left edge, in points."),
                Argument.Of("y", ValueKinds.Length, "The top edge, in points."),
                Argument.Of("width", ValueKinds.Length, "The width, in points."),
                Argument.Of("height", ValueKinds.Length, "The height, in points."),
            ],
            Create = static (slide, a) => slide.Shapes.AddAutoShape(
                a.Get<ShapeType>("shapeType"), a.Get<float>("x"), a.Get<float>("y"), a.Get<float>("width"), a.Get<float>("height")),
        });
        table.Adds(SlideKind, ShapeKind, new Adder<ISlide, IShape>
        {
            Name = "pictureFrame",
            Description = "IImageCollection.AddImage(byte[] buffer), then IShapeCollection.AddPictureFrame(ShapeType shapeType, float x, float y, float width, float height, IPPImage image)",
            Arguments =
            [
                Argument.Of("image", ValueKinds.Image, "The image file the picture shows."),
                Argument.Of("shapeType", ValueKinds.Enum<ShapeType>(), "The engine's ShapeType of the frame, such as Rectangle."),
                .. Bounds,
            ],
            Create = static (slide, a) => slide.Shapes.AddPictureFrame(
                a.Get<ShapeType>("shapeType"), a.Get<float>("x"), a.Get<float>("y"), a.Get<float>("width"), a.Get<float>("height"),
                slide.Presentation.Images.AddImage(a.Get<byte[]>("image"))),
        });
        table.Adds(SlideKind, ShapeKind, new Adder<ISlide, IShape>
        {
            Name = "table",
            Description = "IShapeCollection.AddTable(float x, float y, double[] columnWidths, double[] rowHeights)",
            Arguments =
            [
                Argument.Of("x", ValueKinds.Length, "The left edge, in points."),
                Argument.Of("y", ValueKinds.Length, "The top edge, in points."),
                Argument.Of("columnWidths", ValueKinds.ListOf(ValueKinds.Number), "Each column's width, in points; their count is the table's column count."),
                Argument.Of("rowHeights", ValueKinds.ListOf(ValueKinds.Number), "Each row's height, in points; their count is the table's row count."),
            ],
            Create = static (slide, a) => slide.Shapes.AddTable(
                a.Get<float>("x"), a.Get<float>("y"), [.. a.Get<IReadOnlyList<double>>("columnWidths")], [.. a.Get<IReadOnlyList<double>>("rowHeights")]),
        });
        table.Adds(SlideKind, ShapeKind, new Adder<ISlide, IShape>
        {
            Name = "chart",
            Description = "IShapeCollection.AddChart(ChartType type, float x, float y, float width, float height), or AddChart(…, bool initWithSample) when initWithSample is given",
            Arguments =
            [
                Argument.Of("type", ValueKinds.Enum<ChartType>(), "The engine's ChartType of the new chart."),
                .. Bounds,
                Argument.Optional("initWithSample", ValueKinds.Boolean, "Whether the chart starts with the engine's sample data; not given, the five-parameter call is made, which starts with it."),
            ],
            Create = static (slide, a) => a.Has("initWithSample")
                ? slide.Shapes.AddChart(a.Get<ChartType>("type"), a.Get<float>("x"), a.Get<float>("y"), a.Get<float>("width"), a.Get<float>("height"), a.Get<bool>("initWithSample"))
                : slide.Shapes.AddChart(a.Get<ChartType>("type"), a.Get<float>("x"), a.Get<float>("y"), a.Get<float>("width"), a.Get<float>("height")),
        });
    }

    /// <summary>The position and size an add takes, in points.</summary>
    private static readonly Argument[] Bounds =
    [
        Argument.Of("x", ValueKinds.Length, "The left edge, in points."),
        Argument.Of("y", ValueKinds.Length, "The top edge, in points."),
        Argument.Of("width", ValueKinds.Length, "The width, in points."),
        Argument.Of("height", ValueKinds.Length, "The height, in points."),
    ];
}

/// <summary>The shapes of a slide and the engine interface each implements.</summary>
internal static class SlidesShapeTypes
{
    /// <summary>Every shape of a collection, each group followed by the shapes in it, in document order.</summary>
    internal static IEnumerable<IShape> All(IShapeCollection shapes)
    {
        foreach (IShape shape in shapes)
        {
            yield return shape;
            if (shape is IGroupShape group)
            {
                foreach (IShape inner in All(group.Shapes))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>The shapes of a collection that sit in a group, at any depth, in document order.</summary>
    internal static IEnumerable<IShape> Grouped(IShapeCollection shapes) =>
        shapes.OfType<IGroupShape>().SelectMany(static group => All(group.Shapes));

    /// <summary>
    /// The collection that holds <paramref name="shape"/>: the slide's own or a group's. The
    /// engine's IShape.ParentGroup is no guide: it gives a top-level shape the slide's root group.
    /// </summary>
    internal static IShapeCollection? CollectionOf(IShapeCollection shapes, IShape shape)
    {
        if (shapes.Contains(shape))
        {
            return shapes;
        }

        return shapes.OfType<IGroupShape>().Select(group => CollectionOf(group.Shapes, shape)).FirstOrDefault(static found => found is not null);
    }

    /// <summary>The engine interface a shape implements, the most specific first, such as IVideoFrame before IPictureFrame.</summary>
    internal static string Of(IShape shape) => shape switch
    {
        IVideoFrame => nameof(IVideoFrame),
        IAudioFrame => nameof(IAudioFrame),
        IPictureFrame => nameof(IPictureFrame),
        IAutoShape => nameof(IAutoShape),
        IConnector => nameof(IConnector),
        IGroupShape => nameof(IGroupShape),
        IChart => nameof(IChart),
        ITable => nameof(ITable),
        ISmartArt => nameof(ISmartArt),
        IOleObjectFrame => nameof(IOleObjectFrame),
        ISectionZoomFrame => nameof(ISectionZoomFrame),
        ISummaryZoomFrame => nameof(ISummaryZoomFrame),
        IZoomFrame => nameof(IZoomFrame),
        IInk => nameof(IInk),
        IGraphicalObject => nameof(IGraphicalObject),
        _ => nameof(IShape),
    };
}

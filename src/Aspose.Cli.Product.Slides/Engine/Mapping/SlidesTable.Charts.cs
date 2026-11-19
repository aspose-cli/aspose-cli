using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Row = Aspose.Cli.Sdk.Tables.Row;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

internal static partial class SlidesTable
{
    /// <summary>A chart's title: <c>…/title</c>.</summary>
    internal static readonly ObjectKind<IChartTitle> TitleKind = new("title", "A chart's title.")
    {
        Row.Text<IChartTitle>("text", "IChartTitle.TextFrameForOverriding.Text, null without one; set with AddTextFrameForOverriding(text)", static t => t.TextFrameForOverriding?.Text, static (t, v) => t.AddTextFrameForOverriding(v)),
        Row.Boolean<IChartTitle>("overlay", "IChartTitle.Overlay", static t => t.Overlay, static (t, v) => t.Overlay = v),
        Row.Enum<IChartTitle, TextVerticalType>("textVerticalType", "IChartTitle.TextFormat.TextBlockFormat.TextVerticalType", static t => t.TextFormat.TextBlockFormat.TextVerticalType, static (t, v) => t.TextFormat.TextBlockFormat.TextVerticalType = v),
    };

    /// <summary>A chart's legend: <c>…/legend</c>.</summary>
    internal static readonly ObjectKind<ILegend> LegendKind = new("legend", "A chart's legend.")
    {
        Row.Enum<ILegend, LegendPositionType>("position", "ILegend.Position", static l => l.Position, static (l, v) => l.Position = v),
        Row.Boolean<ILegend>("overlay", "ILegend.Overlay", static l => l.Overlay, static (l, v) => l.Overlay = v),
    };

    /// <summary>A chart's horizontal or vertical axis: <c>…/axis/horizontal</c>, <c>…/axis/vertical</c>.</summary>
    internal static readonly ObjectKind<IAxis> AxisKind = new("axis", "A chart's horizontal or vertical axis.")
    {
        Row.Boolean<IAxis>("isAutomaticMinValue", "IAxis.IsAutomaticMinValue", static a => a.IsAutomaticMinValue, static (a, v) => a.IsAutomaticMinValue = v),
        Row.Number<IAxis>("minValue", "IAxis.MinValue", static a => a.MinValue, static (a, v) => a.MinValue = v),
        Row.Enum<IAxis, CategoryAxisType>("categoryAxisType", "IAxis.CategoryAxisType", static a => a.CategoryAxisType, static (a, v) => a.CategoryAxisType = v),
        Row.Single<IAxis>("rotationAngle", "IAxis.TextFormat.TextBlockFormat.RotationAngle, in degrees", static a => a.TextFormat.TextBlockFormat.RotationAngle, static (a, v) => a.TextFormat.TextBlockFormat.RotationAngle = v),
        Row.Enum<IAxis, FillType>("minorGridLines.fillType", "IAxis.MinorGridLinesFormat.Line.FillFormat.FillType", static a => a.MinorGridLinesFormat.Line.FillFormat.FillType, static (a, v) => a.MinorGridLinesFormat.Line.FillFormat.FillType = v),
        Row.Enum<IAxis, FillType>("majorGridLines.fillType", "IAxis.MajorGridLinesFormat.Line.FillFormat.FillType", static a => a.MajorGridLinesFormat.Line.FillFormat.FillType, static (a, v) => a.MajorGridLinesFormat.Line.FillFormat.FillType = v),
    };

    /// <summary>The text format of a chart, its title, legend or an axis: <c>…/textFormat</c>.</summary>
    internal static readonly ObjectKind<IChartTextFormat> TextFormatKind = new("textFormat", "The character format of a chart's text, its title's, its legend's or an axis'.")
    {
        Characters<IChartTextFormat>(static f => f.PortionFormat),
    };

    /// <summary>A series of a chart, addressed by its position: <c>…/series/0</c>.</summary>
    internal static readonly ObjectKind<IChartSeries> SeriesKind = new("series", "A series of a chart.")
    {
        Row.Text<IChartSeries>("name", "IChartSeries.Name, as IStringChartValue.ToString() gives it", static s => s.Name.ToString()),
        Row.Of<IChartSeries, SlidesValues.Source?>("name.source", SlidesValues.ChartSource, "IChartSeries.Name.DataSourceType, and AsCells[0] when it is Worksheet", static s => NameSource(s.Name)),
        Row.Boolean<IChartSeries>("isColorVaried", "IChartSeries.ParentSeriesGroup.IsColorVaried", static s => s.ParentSeriesGroup.IsColorVaried, static (s, v) => s.ParentSeriesGroup.IsColorVaried = v),
    };

    /// <summary>A data point of a series, addressed by its position: <c>…/series/0/point/2</c>.</summary>
    internal static readonly ObjectKind<IChartDataPoint> PointKind = new("point", "A data point of a series.")
    {
        PointValue("value", static p => p.Value),
        PointValue("xValue", static p => p.XValue),
        PointValue("yValue", static p => p.YValue),
        PointValue("bubbleSize", static p => p.BubbleSize),
        PointValue("sizeValue", static p => p.SizeValue),
    };

    /// <summary>A category of a chart, addressed by its position: <c>…/category/0</c>.</summary>
    internal static readonly ObjectKind<IChartCategory> CategoryKind = new("category", "A category of a chart.")
    {
        Row.Of<IChartCategory, object?>("value", SlidesValues.ChartValue, "IChartCategory.Value", static c => c.Value, static (c, v) => c.Value = v),
        Row.Of<IChartCategory, SlidesValues.Source?>("value.source", SlidesValues.ChartSource, "IChartCategory.UseCell: Worksheet with AsCell, or Literal", static c => new SlidesValues.Source(c.UseCell ? "Worksheet" : "Literal", c.UseCell ? c.AsCell : null)),
    };

    /// <summary>
    /// A cell of a chart's workbook, addressed by worksheet, row and column, the engine's indexes
    /// from 0: <c>…/data/0/1/2</c>; the worksheet through <c>Worksheets[index]</c>, the cell
    /// through <c>GetCell</c>. A read does not list them; a value's source names its cell.
    /// </summary>
    internal static readonly ObjectKind<IChartDataCell> DataKind = new("data", "A cell of a chart's workbook.")
    {
        Row.Of<IChartDataCell, object?>("value", SlidesValues.ChartValue, "IChartDataCell.Value", static c => c.Value, static (c, v) => c.Value = v),
    };

    /// <summary>The 17 methods of IChartDataPointCollection that add a data point, each for one series type.</summary>
    private static readonly string[] PointMethods =
    [
        "AddDataPointForBarSeries", "AddDataPointForLineSeries", "AddDataPointForPieSeries", "AddDataPointForDoughnutSeries",
        "AddDataPointForAreaSeries", "AddDataPointForScatterSeries", "AddDataPointForBubbleSeries", "AddDataPointForRadarSeries",
        "AddDataPointForSurfaceSeries", "AddDataPointForStockSeries", "AddDataPointForHistogramSeries", "AddDataPointForTreemapSeries",
        "AddDataPointForSunburstSeries", "AddDataPointForWaterfallSeries", "AddDataPointForBoxAndWhiskerSeries", "AddDataPointForFunnelSeries",
        "AddDataPointForMapSeries",
    ];

    private static readonly ValueKind<string> PointMethod = ValueKinds.Create<string>(
        "point-method",
        "The IChartDataPointCollection method the add calls; each series type takes exactly one.",
        static () => new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. PointMethods.Select(static method => (JsonNode)method)]) },
        static method => JsonValue.Create(method),
        static value => value.ValueKind == JsonValueKind.String && value.GetString() is { } method && PointMethods.Contains(method, StringComparer.Ordinal)
            ? method
            : throw new TableValueException($"{(value.ValueKind == JsonValueKind.String ? $"'{value.GetString()}'" : "the value")} is no method that adds a data point; it is one of: {string.Join(", ", PointMethods)}"));

    private static IEnumerable<Row<IChartDataPoint>> PointValue(string name, Func<IChartDataPoint, IBaseChartValue> member) =>
    [
        Row.Of<IChartDataPoint, object?>(name, SlidesValues.ChartValue, $"IChartDataPoint.{char.ToUpperInvariant(name[0])}{name[1..]}.Data", p => member(p).Data, (p, v) => member(p).Data = v),
        Row.Of<IChartDataPoint, SlidesValues.Source?>($"{name}.source", SlidesValues.ChartSource, $"IChartDataPoint.{char.ToUpperInvariant(name[0])}{name[1..]}.DataSourceType, and AsCell when it is Worksheet", p => SlidesValues.SourceOf(member(p))),
    ];

    private static SlidesValues.Source NameSource(IStringChartValue name) =>
        new(Enum.GetName(name.DataSourceType) ?? name.DataSourceType.ToString(),
            name.DataSourceType == DataSourceType.Worksheet && name.AsCells.Count > 0 ? name.AsCells[0] : null);

    private static void PlaceCharts(DocumentTable<Presentation> table)
    {
        table.Place(ShapeKind, TitleKind, Single<IChartTitle>("title", static chart => chart.ChartTitle));
        table.Place(ShapeKind, LegendKind, Single<ILegend>("legend", static chart => chart.Legend));
        table.Place(ShapeKind, TextFormatKind, Single<IChartTextFormat>("textFormat", static chart => chart.TextFormat));
        table.Place(ShapeKind, AxisKind, new Locator<IShape, IAxis>
        {
            Segment = "axis",
            Key = [KeyPart.Name],
            Find = static (shape, key) => shape is IChart chart ? key.Name() switch
            {
                "horizontal" => chart.Axes.HorizontalAxis,
                "vertical" => chart.Axes.VerticalAxis,
                _ => null,
            } : null,
            List = static shape => shape is IChart chart ? new[] { chart.Axes.HorizontalAxis, chart.Axes.VerticalAxis }.OfType<IAxis>() : [],
            KeyOf = static (shape, axis) => Key.Of(ReferenceEquals(axis, ((IChart)shape).Axes.HorizontalAxis) ? "horizontal" : "vertical"),
        });
        table.Place(TitleKind, TextFormatKind, new Locator<IChartTitle, IChartTextFormat>
        {
            Segment = "textFormat",
            Find = static (title, _) => title.TextFormat,
            List = static title => [title.TextFormat],
            KeyOf = static (_, _) => Key.None,
        });
        table.Place(LegendKind, TextFormatKind, new Locator<ILegend, IChartTextFormat>
        {
            Segment = "textFormat",
            Find = static (legend, _) => legend.TextFormat,
            List = static legend => [legend.TextFormat],
            KeyOf = static (_, _) => Key.None,
        });
        table.Place(AxisKind, TextFormatKind, new Locator<IAxis, IChartTextFormat>
        {
            Segment = "textFormat",
            Find = static (axis, _) => axis.TextFormat,
            List = static axis => [axis.TextFormat],
            KeyOf = static (_, _) => Key.None,
        });
        table.Place(ShapeKind, SeriesKind, new Locator<IShape, IChartSeries>
        {
            Segment = "series",
            Key = [KeyPart.Index],
            Find = static (shape, key) => shape is IChart chart && key.Index() < chart.ChartData.Series.Count ? chart.ChartData.Series[key.Index()] : null,
            List = static shape => shape is IChart chart ? chart.ChartData.Series : [],
            KeyOf = static (shape, series) => Key.Of(Position(((IChart)shape).ChartData.Series, series)),
            Delete = static (shape, series) => ((IChart)shape).ChartData.Series.Remove(series),
        });
        table.Adds(ShapeKind, SeriesKind, new Adder<IChart, IChartSeries>
        {
            Name = "series",
            Description = "IChartSeriesCollection.Add(IChartDataCell cellWithSeriesName, ChartType type)",
            Arguments =
            [
                Argument.Of("cellWithSeriesName", SlidesValues.Cell, "The workbook cell that holds the series' name."),
                Argument.Of("type", ValueKinds.Enum<ChartType>(), "The engine's ChartType of the series."),
            ],
            Create = static (chart, a) => chart.ChartData.Series.Add(
                SlidesValues.Resolve(chart.ChartData, a.Get<SlidesValues.CellReference>("cellWithSeriesName")), a.Get<ChartType>("type")),
        });
        table.Place(SeriesKind, PointKind, new Locator<IChartSeries, IChartDataPoint>
        {
            Segment = "point",
            Key = [KeyPart.Index],
            Find = static (series, key) => key.Index() < series.DataPoints.Count ? series.DataPoints[key.Index()] : null,
            List = static series => series.DataPoints,
            KeyOf = static (series, point) => Key.Of(Position(series.DataPoints, point)),
            Delete = static (series, point) => series.DataPoints.RemoveAt(Position(series.DataPoints, point)),
        });
        table.Adds(SeriesKind, PointKind, new Adder<IChartSeries, IChartDataPoint>
        {
            Name = "point",
            Description = "One IChartDataPointCollection.AddDataPointFor…Series method, the one method names, with the workbook cells it takes",
            Arguments =
            [
                Argument.Of("method", PointMethod, "The method: AddDataPointForScatterSeries takes xValue and yValue; AddDataPointForBubbleSeries xValue, yValue and bubbleSize; AddDataPointForTreemapSeries and AddDataPointForSunburstSeries sizeValue; every other one value."),
                Argument.Optional("value", SlidesValues.Cell, "The cell of the point's value."),
                Argument.Optional("xValue", SlidesValues.Cell, "The cell of the point's x value."),
                Argument.Optional("yValue", SlidesValues.Cell, "The cell of the point's y value."),
                Argument.Optional("bubbleSize", SlidesValues.Cell, "The cell of the point's bubble size."),
                Argument.Optional("sizeValue", SlidesValues.Cell, "The cell of the point's size value."),
            ],
            Create = static (series, a) => AddPoint(series, a),
        });
        table.Place(ShapeKind, CategoryKind, new Locator<IShape, IChartCategory>
        {
            Segment = "category",
            Key = [KeyPart.Index],
            Find = static (shape, key) => shape is IChart chart && key.Index() < chart.ChartData.Categories.Count ? chart.ChartData.Categories[key.Index()] : null,
            List = static shape => shape is IChart chart ? chart.ChartData.Categories : [],
            KeyOf = static (shape, category) => Key.Of(((IChart)shape).ChartData.Categories.IndexOf(category)),
            Delete = static (shape, category) => ((IChart)shape).ChartData.Categories.RemoveAt(((IChart)shape).ChartData.Categories.IndexOf(category)),
        });
        table.Adds(ShapeKind, CategoryKind, new Adder<IChart, IChartCategory>
        {
            Name = "category",
            Description = "IChartCategoryCollection.Add(IChartDataCell chartDataCell)",
            Arguments = [Argument.Of("chartDataCell", SlidesValues.Cell, "The workbook cell that holds the category.")],
            Create = static (chart, a) => chart.ChartData.Categories.Add(
                SlidesValues.Resolve(chart.ChartData, a.Get<SlidesValues.CellReference>("chartDataCell"))),
        });
        table.Place(ShapeKind, DataKind, new Locator<IShape, IChartDataCell>
        {
            Segment = "data",
            Key = [KeyPart.Index, KeyPart.Index, KeyPart.Index],
            Find = static (shape, key) => shape is IChart chart ? SlidesValues.Resolve(chart.ChartData, new(key.Index(0), key.Index(1), key.Index(2))) : null,
            List = static _ => [],
            KeyOf = static (_, cell) => Key.Of(cell.ChartDataWorksheet.Index, cell.Row, cell.Column),
        });
    }

    /// <summary>The one part of a chart a segment names, such as its title.</summary>
    private static Locator<IShape, T> Single<T>(string segment, Func<IChart, T> part)
        where T : class => new()
        {
            Segment = segment,
            Find = (shape, _) => shape is IChart chart ? part(chart) : null,
            List = shape => shape is IChart chart ? [part(chart)] : [],
            KeyOf = static (_, _) => Key.None,
        };

    /// <summary>Calls the one AddDataPointFor…Series method the arguments name, with the cells it takes and no other.</summary>
    private static IChartDataPoint AddPoint(IChartSeries series, Arguments a)
    {
        string method = a.Get<string>("method");
        string[] takes = method switch
        {
            "AddDataPointForScatterSeries" => ["xValue", "yValue"],
            "AddDataPointForBubbleSeries" => ["xValue", "yValue", "bubbleSize"],
            "AddDataPointForTreemapSeries" or "AddDataPointForSunburstSeries" => ["sizeValue"],
            _ => ["value"],
        };
        string[] given = [.. new[] { "value", "xValue", "yValue", "bubbleSize", "sizeValue" }.Where(a.Has)];
        if (!given.Order(StringComparer.Ordinal).SequenceEqual(takes.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw TableErrors.InvalidArguments($"{method} takes args {string.Join(", ", takes)}; the operation gives {(given.Length == 0 ? "none of them" : string.Join(", ", given))}");
        }

        IChartData data = series.Chart.ChartData;
        IChartDataCell Cell(string name) => SlidesValues.Resolve(data, a.Get<SlidesValues.CellReference>(name));
        IChartDataPointCollection points = series.DataPoints;
        return method switch
        {
            "AddDataPointForBarSeries" => points.AddDataPointForBarSeries(Cell("value")),
            "AddDataPointForLineSeries" => points.AddDataPointForLineSeries(Cell("value")),
            "AddDataPointForPieSeries" => points.AddDataPointForPieSeries(Cell("value")),
            "AddDataPointForDoughnutSeries" => points.AddDataPointForDoughnutSeries(Cell("value")),
            "AddDataPointForAreaSeries" => points.AddDataPointForAreaSeries(Cell("value")),
            "AddDataPointForScatterSeries" => points.AddDataPointForScatterSeries(Cell("xValue"), Cell("yValue")),
            "AddDataPointForBubbleSeries" => points.AddDataPointForBubbleSeries(Cell("xValue"), Cell("yValue"), Cell("bubbleSize")),
            "AddDataPointForRadarSeries" => points.AddDataPointForRadarSeries(Cell("value")),
            "AddDataPointForSurfaceSeries" => points.AddDataPointForSurfaceSeries(Cell("value")),
            "AddDataPointForStockSeries" => points.AddDataPointForStockSeries(Cell("value")),
            "AddDataPointForHistogramSeries" => points.AddDataPointForHistogramSeries(Cell("value")),
            "AddDataPointForTreemapSeries" => points.AddDataPointForTreemapSeries(Cell("sizeValue")),
            "AddDataPointForSunburstSeries" => points.AddDataPointForSunburstSeries(Cell("sizeValue")),
            "AddDataPointForWaterfallSeries" => points.AddDataPointForWaterfallSeries(Cell("value")),
            "AddDataPointForBoxAndWhiskerSeries" => points.AddDataPointForBoxAndWhiskerSeries(Cell("value")),
            "AddDataPointForFunnelSeries" => points.AddDataPointForFunnelSeries(Cell("value")),
            _ => points.AddDataPointForMapSeries(Cell("value")),
        };
    }
}

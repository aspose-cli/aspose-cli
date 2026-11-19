using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Pivot;
using Aspose.Cli.Sdk.Tables;
using Row = Aspose.Cli.Sdk.Tables.Row;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

internal static partial class CellsTable
{
    /// <summary>A chart, addressed by its shape id: <c>sheet/Sales/chart/1</c>.</summary>
    internal static readonly ObjectKind<Chart> ChartKind = new("chart", "A chart of a worksheet.")
    {
        Row.Enum<Chart, ChartType>("type", "Chart.Type", static c => c.Type, static (c, v) => c.Type = v),
        Row.Text<Chart>("name", "Chart.Name", static c => c.Name),
        Row.Text<Chart>("anchor", "Chart.ChartObject.UpperLeftRow, UpperLeftColumn, LowerRightRow and LowerRightColumn, as A1 writes the area",
            static c => CellsValues.A1(CellArea.CreateCellArea(c.ChartObject.UpperLeftRow, c.ChartObject.UpperLeftColumn, c.ChartObject.LowerRightRow, c.ChartObject.LowerRightColumn))),
        Row.Text<Chart>("title.text", "Chart.Title.Text", static c => c.Title.Text, static (c, v) => c.Title.Text = v),
        Row.Boolean<Chart>("title.isAutoText", "Chart.Title.IsAutoText", static c => c.Title.IsAutoText),
        Row.Boolean<Chart>("showLegend", "Chart.ShowLegend", static c => c.ShowLegend, static (c, v) => c.ShowLegend = v),
        Row.Enum<Chart, LegendPositionType>("legend.position", "Chart.Legend.Position", static c => c.Legend.Position, static (c, v) => c.Legend.Position = v),
        Row.Text<Chart>("categoryAxis.title.text", "Chart.CategoryAxis.Title.Text", static c => c.CategoryAxis.Title.Text, static (c, v) => c.CategoryAxis.Title.Text = v),
        Row.Text<Chart>("valueAxis.title.text", "Chart.ValueAxis.Title.Text", static c => c.ValueAxis.Title.Text, static (c, v) => c.ValueAxis.Title.Text = v),
        Row.Text<Chart>("nSeries.categoryData", "Chart.NSeries.CategoryData, such as =Sales!$B$1:$E$1", static c => c.NSeries.CategoryData, static (c, v) => c.NSeries.CategoryData = v),
        Row.Boolean<Chart>("chartArea.border.isVisible", "Chart.ChartArea.Border.IsVisible", static c => c.ChartArea.Border.IsVisible, static (c, v) => c.ChartArea.Border.IsVisible = v),
        Row.Enum<Chart, FormattingType>("plotArea.area.formatting", "Chart.PlotArea.Area.Formatting", static c => c.PlotArea.Area.Formatting, static (c, v) => c.PlotArea.Area.Formatting = v),
        Row.Color<Chart>("valueAxis.majorGridLines.color", "Chart.ValueAxis.MajorGridLines.Color", static c => c.ValueAxis.MajorGridLines.Color, static (c, v) => c.ValueAxis.MajorGridLines.Color = v),
        Row.Boolean<Chart>("valueAxis.axisLine.isVisible", "Chart.ValueAxis.AxisLine.IsVisible", static c => c.ValueAxis.AxisLine.IsVisible, static (c, v) => c.ValueAxis.AxisLine.IsVisible = v),
        Row.Enum<Chart, TickMarkType>("valueAxis.majorTickMark", "Chart.ValueAxis.MajorTickMark", static c => c.ValueAxis.MajorTickMark, static (c, v) => c.ValueAxis.MajorTickMark = v),
        Row.Enum<Chart, TickMarkType>("categoryAxis.majorTickMark", "Chart.CategoryAxis.MajorTickMark", static c => c.CategoryAxis.MajorTickMark, static (c, v) => c.CategoryAxis.MajorTickMark = v),
        Row.Integer<Chart>("gapWidth", "Chart.GapWidth, in percent", static c => c.GapWidth, static (c, v) => c.GapWidth = v),
        Row.Of<Chart, object?>("valueAxis.minValue", CellsValues.CellValue, "Chart.ValueAxis.MinValue; null where the engine gives none", static c => c.ValueAxis.MinValue, static (c, v) => c.ValueAxis.MinValue = v),
        Row.Boolean<Chart>("valueAxis.isAutomaticMinValue", "Chart.ValueAxis.IsAutomaticMinValue", static c => c.ValueAxis.IsAutomaticMinValue, static (c, v) => c.ValueAxis.IsAutomaticMinValue = v),
    };

    /// <summary>A series of a chart, addressed by its position in Chart.NSeries: <c>…/chart/1/series/0</c>.</summary>
    internal static readonly ObjectKind<Series> SeriesKind = new("series", "A series of a chart.")
    {
        Row.Text<Series>("name", "Series.Name, a text or a reference such as =Sales!$A$2", static s => s.Name, static (s, v) => s.Name = v),
        Row.Text<Series>("values", "Series.Values, such as =Sales!$B$2:$E$2", static s => s.Values, static (s, v) => s.Values = v),
        Row.Text<Series>("xValues", "Series.XValues", static s => s.XValues, static (s, v) => s.XValues = v),
        Row.Enum<Series, FormattingType>("area.formatting", "Series.Area.Formatting", static s => s.Area.Formatting, static (s, v) => s.Area.Formatting = v),
        Row.Color<Series>("area.foregroundColor", "Series.Area.ForegroundColor", static s => s.Area.ForegroundColor, static (s, v) => s.Area.ForegroundColor = v),
        Row.Color<Series>("border.color", "Series.Border.Color", static s => s.Border.Color, static (s, v) => s.Border.Color = v),
        Row.Boolean<Series>("border.isVisible", "Series.Border.IsVisible", static s => s.Border.IsVisible, static (s, v) => s.Border.IsVisible = v),
        Row.Enum<Series, FormattingType>("marker.area.formatting", "Series.Marker.Area.Formatting", static s => s.Marker.Area.Formatting, static (s, v) => s.Marker.Area.Formatting = v),
        Row.Color<Series>("marker.area.foregroundColor", "Series.Marker.Area.ForegroundColor", static s => s.Marker.Area.ForegroundColor, static (s, v) => s.Marker.Area.ForegroundColor = v),
        Row.Color<Series>("marker.border.color", "Series.Marker.Border.Color", static s => s.Marker.Border.Color, static (s, v) => s.Marker.Border.Color = v),
        Row.Boolean<Series>("dataLabels.showValue", "Series.DataLabels.ShowValue", static s => s.DataLabels.ShowValue, static (s, v) => s.DataLabels.ShowValue = v),
        Row.Text<Series>("dataLabels.numberFormat", "Series.DataLabels.NumberFormat", static s => s.DataLabels.NumberFormat, static (s, v) => s.DataLabels.NumberFormat = v),
        Row.Boolean<Series>("dataLabels.numberFormatLinked", "Series.DataLabels.NumberFormatLinked", static s => s.DataLabels.NumberFormatLinked),
    };

    /// <summary>A data point of a series, addressed by its position in Series.Points: <c>…/series/0/point/2</c>.</summary>
    internal static readonly ObjectKind<ChartPoint> PointKind = new("point", "A data point of a series.")
    {
        Row.Enum<ChartPoint, FormattingType>("area.formatting", "ChartPoint.Area.Formatting", static p => p.Area.Formatting, static (p, v) => p.Area.Formatting = v),
        Row.Color<ChartPoint>("area.foregroundColor", "ChartPoint.Area.ForegroundColor", static p => p.Area.ForegroundColor, static (p, v) => p.Area.ForegroundColor = v),
    };

    /// <summary>A pivot table, addressed by its position in Worksheet.PivotTables: <c>sheet/Sales/pivot/0</c>.</summary>
    internal static readonly ObjectKind<PivotTable> PivotKind = new("pivot", "A pivot table of a worksheet.")
    {
        Row.Text<PivotTable>("name", "PivotTable.Name", static p => p.Name),
        Row.Of<PivotTable, IReadOnlyList<string?>>("dataSource", ValueKinds.ListOf(ValueKinds.Text), "PivotTable.DataSource", static p => p.DataSource),
        Row.Text<PivotTable>("tableRange1", "PivotTable.TableRange1, as A1 writes the area", static p => CellsValues.A1(p.TableRange1)),
        Row.Of<PivotTable, IReadOnlyList<string?>>("rowFields", ValueKinds.ListOf(ValueKinds.Text), "PivotTable.RowFields, each field's Name", static p => [.. p.RowFields.Cast<PivotField>().Select(static field => field.Name)]),
        Row.Of<PivotTable, IReadOnlyList<string?>>("columnFields", ValueKinds.ListOf(ValueKinds.Text), "PivotTable.ColumnFields, each field's Name", static p => [.. p.ColumnFields.Cast<PivotField>().Select(static field => field.Name)]),
        Row.Text<PivotTable>("grandTotalName", "PivotTable.GrandTotalName", static p => p.GrandTotalName, static (p, v) => p.GrandTotalName = v),
        Row.Text<PivotTable>("rowHeaderCaption", "PivotTable.RowHeaderCaption", static p => p.RowHeaderCaption, static (p, v) => p.RowHeaderCaption = v),
        Row.Text<PivotTable>("columnHeaderCaption", "PivotTable.ColumnHeaderCaption", static p => p.ColumnHeaderCaption, static (p, v) => p.ColumnHeaderCaption = v),
        Row.Text<PivotTable>("dataFieldHeaderName", "PivotTable.DataFieldHeaderName", static p => p.DataFieldHeaderName, static (p, v) => p.DataFieldHeaderName = v),
        Row.Boolean<PivotTable>("autofitColumnWidthOnUpdate", "PivotTable.AutofitColumnWidthOnUpdate", static p => p.AutofitColumnWidthOnUpdate, static (p, v) => p.AutofitColumnWidthOnUpdate = v),
        Row.Boolean<PivotTable>("isAutoFormat", "PivotTable.IsAutoFormat", static p => p.IsAutoFormat, static (p, v) => p.IsAutoFormat = v),
        Row.Boolean<PivotTable>("preserveCellFormattingOnUpdate", "PivotTable.PreserveCellFormattingOnUpdate", static p => p.PreserveCellFormattingOnUpdate, static (p, v) => p.PreserveCellFormattingOnUpdate = v),
    };

    /// <summary>A value field of a pivot table, addressed by its position in PivotTable.DataFields: <c>…/pivot/0/dataField/0</c>.</summary>
    internal static readonly ObjectKind<PivotField> DataFieldKind = new("dataField", "A value field of a pivot table; the add_pivot_field action adds one.")
    {
        Row.Text<PivotField>("name", "PivotField.Name", static f => f.Name),
        Row.Enum<PivotField, ConsolidationFunction>("function", "PivotField.Function", static f => f.Function, static (f, v) => f.Function = v),
        Row.Text<PivotField>("numberFormat", "PivotField.NumberFormat", static f => f.NumberFormat, static (f, v) => f.NumberFormat = v),
        Row.Text<PivotField>("displayName", "PivotField.DisplayName", static f => f.DisplayName, static (f, v) => f.DisplayName = v),
    };

    private static void PlaceCharts(DocumentTable<Workbook> table)
    {
        table.Place(SheetKind, ChartKind, new Locator<Worksheet, Chart>
        {
            Segment = "chart",
            Key = [KeyPart.Id],
            Find = static (s, key) => Charts(s).FirstOrDefault(chart => (uint)chart.ChartObject.Id == key.Id()),
            List = Charts,
            KeyOf = static (_, chart) => Key.Of((uint)chart.ChartObject.Id),
            Delete = static (s, chart) => s.Charts.RemoveAt(IndexOf(Charts(s), chart)),
        });
        table.Adds(SheetKind, ChartKind, new Adder<Worksheet, Chart>
        {
            Name = "chart",
            Description = "ChartCollection.Add(ChartType type, int upperLeftRow, int upperLeftColumn, int lowerRightRow, int lowerRightColumn), with the area's corners",
            Arguments =
            [
                Argument.Of("type", ValueKinds.Enum<ChartType>(), "The engine's ChartType."),
                Argument.Of("area", CellsValues.Area, "The cells the chart covers, such as H2:O18."),
            ],
            Create = static (s, a) =>
            {
                CellArea area = CellsValues.CellArea(s, a.Get<string>("area"));
                return s.Charts[s.Charts.Add(a.Get<ChartType>("type"), area.StartRow, area.StartColumn, area.EndRow, area.EndColumn)];
            },
        });
        table.Place(ChartKind, SeriesKind, Positions<Chart, Series>("series",
            static c => Enumerable.Range(0, c.NSeries.Count).Select(index => c.NSeries[index]),
            static (c, index) => c.NSeries[index], static (c, index) => c.NSeries.RemoveAt(index)));
        table.Adds(ChartKind, SeriesKind, new Adder<Chart, Series>
        {
            Name = "series",
            Description = "SeriesCollection.Add(string area, bool isVertical): one call adds one series for each column of the area (isVertical true) or each row (false), and the address the edit reports is the first of them",
            Arguments =
            [
                Argument.Of("area", ValueKinds.Text, "The values, such as Sales!B2:E2."),
                Argument.Of("isVertical", ValueKinds.Boolean, "Whether each series is a column of the area rather than a row."),
            ],
            Create = static (c, a) => c.NSeries[c.NSeries.Add(a.Get<string>("area"), a.Get<bool>("isVertical"))],
        });
        table.Place(SeriesKind, PointKind, new Locator<Series, ChartPoint>
        {
            Segment = "point",
            Key = [KeyPart.Index],
            Find = static (s, key) => key.Index() < s.Points.Count ? s.Points[key.Index()] : null,
            List = static s => Enumerable.Range(0, s.Points.Count).Select(index => s.Points[index]),
            KeyOf = static (s, point) => Key.Of(IndexOf(Enumerable.Range(0, s.Points.Count).Select(index => s.Points[index]), point)),
            ListedWhenNamed = true,
        });
        table.Place(SheetKind, PivotKind, Positions<Worksheet, PivotTable>("pivot",
            static s => Enumerable.Range(0, s.PivotTables.Count).Select(index => s.PivotTables[index]),
            static (s, index) => s.PivotTables[index], static (s, index) => s.PivotTables.RemoveAt(index)));
        table.Adds(SheetKind, PivotKind, new Adder<Worksheet, PivotTable>
        {
            Name = "pivot",
            Description = "PivotTableCollection.Add(string sourceData, string destCellName, string tableName); the add_pivot_field action adds its fields and calculate_pivot fills its cells",
            Arguments =
            [
                Argument.Of("sourceData", ValueKinds.Text, "The source data, such as =Sales!A1:F6."),
                Argument.Of("destCellName", CellsValues.CellName, "The pivot table's top left cell, such as H2."),
                Argument.Of("tableName", ValueKinds.Text, "The pivot table's name."),
            ],
            Create = static (s, a) => s.PivotTables[s.PivotTables.Add(a.Get<string>("sourceData"), a.Get<string>("destCellName"), a.Get<string>("tableName"))],
        });
        table.Place(PivotKind, DataFieldKind, Positions<PivotTable, PivotField>("dataField",
            static p => p.DataFields.Cast<PivotField>(),
            static (p, index) => p.DataFields[index], static (p, index) => p.DataFields.RemoveAt(index)));
    }

    private static IEnumerable<Chart> Charts(Worksheet sheet) => Enumerable.Range(0, sheet.Charts.Count).Select(index => sheet.Charts[index]);
}

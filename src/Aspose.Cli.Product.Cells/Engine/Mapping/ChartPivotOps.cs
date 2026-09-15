using System.Drawing;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Pivot;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Creates native charts and pivot tables. Kept in the mapping layer because
/// the contract vocabulary (type and function names, header-name field
/// references) is translated to engine enums and addresses here.
/// </summary>
internal static class ChartPivotOps
{
    /// <summary>
    /// The default series palette of the modern chart look (the probe-ranked
    /// V4 scheme), cycled across series in order.
    /// </summary>
    private static readonly string[] ModernPalette = ["#1F4E79", "#2E75B6", "#9DC3E6", "#D9D9D9"];

    public static long? CreateChart(Worksheet sheet, CreateChartOp op)
    {
        RangeRef placement = A1.ParseRange(op.At).Range;
        int chartIndex = sheet.Charts.Add(
            ToChartType(op.Type),
            placement.Start.Row,
            placement.Start.Column,
            placement.End.Row,
            placement.End.Column);

        Chart chart = sheet.Charts[chartIndex];
        // Series-from-columns is the default; SetChartDataRange derives
        // series and category axes from the headers in the range.
        chart.SetChartDataRange(op.DataRange, isVertical: op.SeriesInRows is not true);

        if (op.Title is { } title)
        {
            chart.Title.Text = title;
        }

        // Defaults first, explicit cosmetics after — a user field always wins.
        ApplyModernDefaults(chart, op.Type);
        ApplyCosmetics(chart, op.Legend, op.AxisTitles, op.SeriesColors, op.DataLabels);
        return null;
    }

    public static long? ApplyPivot(Worksheet sheet, Op op)
    {
        // Pivot caches capture stored cell results. Calculate dependencies before
        // adding or refreshing the cache so preceding edits in this batch are visible.
        sheet.Workbook.CalculateFormula();
        return op switch
        {
            CreatePivotOp create => CreatePivot(sheet, create),
            RefreshPivotOp refresh => RefreshPivot(sheet, refresh),
            _ => throw new InvalidOperationException($"Unhandled pivot operation {op.GetType().Name}."),
        };
    }

    private static long? CreatePivot(Worksheet sheet, CreatePivotOp op)
    {
        // An unqualified source refers to the op's sheet; the engine API
        // requires the qualified form.
        string source = op.SourceRange.Contains('!', StringComparison.Ordinal)
            ? op.SourceRange
            : Sheets.Qualify(sheet.Name) + "!" + op.SourceRange;

        int pivotIndex = sheet.PivotTables.Add(source, op.At, op.Name ?? "PivotTable1");
        PivotTable pivot = sheet.PivotTables[pivotIndex];

        foreach (string row in op.Rows ?? [])
        {
            AddField(pivot, PivotFieldType.Row, row);
        }

        foreach (string column in op.Columns ?? [])
        {
            AddField(pivot, PivotFieldType.Column, column);
        }

        foreach (PivotValueField value in op.Values)
        {
            int fieldIndex = AddField(pivot, PivotFieldType.Data, value.Field);
            pivot.DataFields[fieldIndex].Function = ToFunction(value.Function);
            if (value.NumberFormat is { } numberFormat)
            {
                pivot.DataFields[fieldIndex].NumberFormat = numberFormat;
            }
        }

        pivot.CalculateData();
        return null;
    }

    // AddFieldToArea returns -1 for a name absent from the source headers. Left
    // unchecked that number diverged by area: a bad `values` field then indexed
    // DataFields[-1] and threw ArgumentOutOfRangeException — surfaced as
    // INTERNAL "report a bug" for what is an ordinary field-name typo — while a
    // bad `rows`/`columns` field was silently dropped, so the pivot built
    // without the breakdown the caller asked for. Reject either as a
    // self-correcting OPS_INVALID that names the fields actually available (the
    // batch runner attaches the op index).
    private static int AddField(PivotTable pivot, PivotFieldType area, string field)
    {
        int index = pivot.AddFieldToArea(area, field);
        if (index < 0)
        {
            var available = new List<string>(pivot.BaseFields.Count);
            for (int i = 0; i < pivot.BaseFields.Count; i++)
            {
                available.Add(pivot.BaseFields[i].Name);
            }

            throw CellsErrors.OpsInvalid(
                $"create_pivot references field '{field}', which is not a column of the source data; " +
                $"available fields: {string.Join(", ", available)}",
                hint: "Use one of the source header names (case-sensitive) for 'rows', 'columns' and 'values[].field'.");
        }

        return index;
    }

    private static long? RefreshPivot(Worksheet sheet, RefreshPivotOp op)
    {
        bool refreshedAny = false;
        foreach (PivotTable pivot in sheet.PivotTables)
        {
            if (op.Name is null || string.Equals(pivot.Name, op.Name, StringComparison.Ordinal))
            {
                bool autoFit = pivot.AutofitColumnWidthOnUpdate;
                bool autoFormat = pivot.IsAutoFormat;
                bool preserveFormatting = pivot.PreserveCellFormattingOnUpdate;
                try
                {
                    // Refresh values without resizing columns used by unrelated
                    // report content or replacing the user's cell formatting.
                    CellArea area = pivot.TableRange1;
                    var formats = new List<(int Row, int Column, Style Style)>();
                    foreach (Cell cell in sheet.Cells.CreateRange(
                        area.StartRow, area.StartColumn,
                        area.EndRow - area.StartRow + 1, area.EndColumn - area.StartColumn + 1))
                    {
                        if (cell.IsStyleSet)
                        {
                            formats.Add((cell.Row, cell.Column, cell.GetStyle()));
                        }
                    }

                    foreach ((int row, int column, Style style) in formats)
                    {
                        pivot.Format(row, column, style);
                    }

                    pivot.AutofitColumnWidthOnUpdate = false;
                    pivot.IsAutoFormat = false;
                    pivot.PreserveCellFormattingOnUpdate = true;
                    pivot.PivotCache.Refresh();
                }
                finally
                {
                    pivot.AutofitColumnWidthOnUpdate = autoFit;
                    pivot.IsAutoFormat = autoFormat;
                    pivot.PreserveCellFormattingOnUpdate = preserveFormatting;
                }
                refreshedAny = true;
            }
        }

        if (op.Name is { } name && !refreshedAny)
        {
            // The executor attaches the op index to this domain error.
            throw CellsErrors.OpsInvalid($"no pivot table named '{name}' on sheet '{sheet.Name}'");
        }

        return null;
    }

    public static long? UpdateChart(Worksheet sheet, UpdateChartOp op)
    {
        Chart chart = sheet.Charts[ResolveChart(sheet, op.Index, op.Name)];

        if (op.Type is { } type)
        {
            chart.Type = ToChartType(type);
        }

        if (op.DataRange is { } dataRange)
        {
            chart.SetChartDataRange(dataRange, isVertical: op.SeriesInRows is not true);
        }

        if (op.Title is { } title)
        {
            chart.Title.Text = title;
        }

        // After the type/data changes so the cosmetics see the final chart —
        // the pie/axis-title guard must judge the type the file will carry.
        ApplyCosmetics(chart, op.Legend, op.AxisTitles, op.SeriesColors, op.DataLabels);
        return null;
    }

    public static long? DeleteChart(Worksheet sheet, DeleteChartOp op)
    {
        sheet.Charts.RemoveAt(ResolveChart(sheet, op.Index, op.Name));
        return null;
    }

    /// <summary>
    /// Resolves the index/name addressing shared by <c>update_chart</c> and
    /// <c>delete_chart</c> to the chart's collection index (delete needs the
    /// index, not the object). The parser guarantees exactly one of the two
    /// is present.
    /// </summary>
    private static int ResolveChart(Worksheet sheet, int? index, string? name)
    {
        if (index is { } wanted)
        {
            if (wanted < 0 || wanted >= sheet.Charts.Count)
            {
                // The executor attaches the op index to this domain error.
                throw CellsErrors.OpsInvalid($"no chart at index {wanted} on sheet '{sheet.Name}'");
            }

            return wanted;
        }

        for (int i = 0; i < sheet.Charts.Count; i++)
        {
            if (string.Equals(sheet.Charts[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw CellsErrors.OpsInvalid($"no chart named '{name}' on sheet '{sheet.Name}'");
    }

    /// <summary>
    /// The modern default look, applied on create only and before the user's
    /// explicit cosmetics (which therefore always win). The set is the
    /// probe-ranked V4 recipe (acceptance PROBES.md, P9–P16): the engine's own
    /// defaults are the 2003 look — gray <c>#C0C0C0</c> plot area, an outer
    /// chart border, gap width 150 and a right-docked legend.
    /// <c>Chart.Style</c> is deliberately not used: it persists in the file
    /// but the renderer ignores it (probe: byte-identical renders).
    /// </summary>
    private static void ApplyModernDefaults(Chart chart, string type)
    {
        chart.ChartArea.Border.IsVisible = false;
        chart.PlotArea.Area.Formatting = FormattingType.None;
        chart.ShowLegend = true;
        chart.Legend.Position = LegendPositionType.Bottom;

        if (type != ChartTypes.Pie)
        {
            // A pie has no axes; the engine silently drops axis writes there
            // (probe P9), so the code skips them to stay honest.
            chart.ValueAxis.MajorGridLines.Color = StyleWriter.ParseHex("#D9D9D9");
            chart.ValueAxis.AxisLine.IsVisible = false;
            chart.ValueAxis.MajorTickMark = TickMarkType.None;
            chart.CategoryAxis.MajorTickMark = TickMarkType.None;
        }

        if (type is ChartTypes.Column or ChartTypes.Bar)
        {
            chart.GapWidth = 75;
        }

        // Default palette across every series (a pie's slices count as the
        // series), cycling when there are more series than palette entries.
        int count = IsPieFamily(chart.Type)
            ? (chart.NSeries.Count > 0 ? chart.NSeries[0].Points.Count : 0)
            : chart.NSeries.Count;
        var palette = new string[count];
        for (int i = 0; i < count; i++)
        {
            palette[i] = ModernPalette[i % ModernPalette.Length];
        }

        ApplySeriesColors(chart, palette);
    }

    /// <summary>
    /// Applies the optional cosmetic fields shared by <c>create_chart</c> and
    /// <c>update_chart</c>. Runs after the modern create defaults, so an
    /// explicit field always overrides them.
    /// </summary>
    private static void ApplyCosmetics(
        Chart chart,
        ChartLegendData? legend,
        ChartAxisTitlesData? axisTitles,
        IReadOnlyList<string>? seriesColors,
        ChartDataLabelsData? dataLabels)
    {
        if (legend is { } chartLegend)
        {
            if (chartLegend.Visible is { } visible)
            {
                chart.ShowLegend = visible;
            }

            if (chartLegend.Position is { } position)
            {
                chart.Legend.Position = ToLegendPosition(position);
            }
        }

        if (axisTitles is { } titles)
        {
            // The engine SILENTLY drops axis-title writes on a pie-family
            // chart — no throw, nothing stored, nothing rendered (probe P9).
            // create_chart rejects pie+axisTitles in the parser; update_chart
            // only knows the real type here, after resolving the chart, so
            // the never-silently bar puts the same guard in the mapper. The
            // executor attaches the op index to this domain error.
            if (IsPieFamily(chart.Type))
            {
                throw CellsErrors.OpsInvalid("a 'pie' chart has no axes; omit 'axisTitles'");
            }

            if (titles.Category is { } category)
            {
                chart.CategoryAxis.Title.Text = category;
            }

            if (titles.Value is { } value)
            {
                chart.ValueAxis.Title.Text = value;
            }
        }

        if (seriesColors is { } colors)
        {
            ApplySeriesColors(chart, colors);
        }

        if (dataLabels is { } labels)
        {
            foreach (Series series in chart.NSeries)
            {
                if (labels.Visible is { } visible)
                {
                    series.DataLabels.ShowValue = visible;
                }

                if (labels.Format is { } format)
                {
                    // Setting the string format auto-clears NumberFormatLinked
                    // (probe P9); the int-typed Number property is ignored.
                    series.DataLabels.NumberFormat = format;
                }
            }
        }
    }

    /// <summary>
    /// Colors the series with the per-type recipe the render probes pinned
    /// (P10): fills for column/bar/area, the line color for line, line plus
    /// marker for scatter (whose markers otherwise keep the default palette),
    /// and per-point slice fills for the pie family (one series, points
    /// pre-materialized per category). Colors beyond the series/slice count
    /// are ignored.
    /// </summary>
    private static void ApplySeriesColors(Chart chart, IReadOnlyList<string> colors)
    {
        if (IsPieFamily(chart.Type))
        {
            if (chart.NSeries.Count == 0)
            {
                return;
            }

            ChartPointCollection slices = chart.NSeries[0].Points;
            for (int i = 0; i < slices.Count && i < colors.Count; i++)
            {
                slices[i].Area.Formatting = FormattingType.Custom;
                slices[i].Area.ForegroundColor = StyleWriter.ParseHex(colors[i]);
            }

            return;
        }

        bool isLine = IsFamily(chart.Type, "Line");
        bool isScatter = IsFamily(chart.Type, "Scatter");
        for (int i = 0; i < chart.NSeries.Count && i < colors.Count; i++)
        {
            Series series = chart.NSeries[i];
            Color color = StyleWriter.ParseHex(colors[i]);
            if (isLine)
            {
                // The color alone flips the line to a solid non-auto stroke.
                series.Border.Color = color;
            }
            else if (isScatter)
            {
                series.Border.Color = color;
                series.Marker.Area.Formatting = FormattingType.Custom;
                series.Marker.Area.ForegroundColor = color;
                series.Marker.Border.Color = color;
            }
            else
            {
                // Column, bar, area — and any other solid-filled family an
                // existing workbook's chart may carry.
                series.Area.Formatting = FormattingType.Custom;
                series.Area.ForegroundColor = color;
            }
        }
    }

    /// <summary>
    /// Pie-family test (pie, 3-D/exploded/bar-of-pie variants, doughnut):
    /// the axis-less charts whose slices live in one series' points. Matched
    /// by enum-name family because update_chart can meet any of the ~75
    /// engine chart types in an existing workbook, not just the six the
    /// vocabulary creates.
    /// </summary>
    private static bool IsPieFamily(ChartType type) =>
        IsFamily(type, "Pie") || IsFamily(type, "Doughnut");

    private static bool IsFamily(ChartType type, string prefix) =>
        type.ToString().StartsWith(prefix, StringComparison.Ordinal);

    private static ChartType ToChartType(string type) => type switch
    {
        ChartTypes.Column => ChartType.Column,
        ChartTypes.Bar => ChartType.Bar,
        ChartTypes.Line => ChartType.Line,
        ChartTypes.Pie => ChartType.Pie,
        ChartTypes.Scatter => ChartType.Scatter,
        ChartTypes.Area => ChartType.Area,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Chart type is missing from the engine mapper."),
    };

    private static LegendPositionType ToLegendPosition(string position) => position switch
    {
        LegendPositions.Right => LegendPositionType.Right,
        LegendPositions.Bottom => LegendPositionType.Bottom,
        LegendPositions.Top => LegendPositionType.Top,
        LegendPositions.Left => LegendPositionType.Left,
        _ => throw new ArgumentOutOfRangeException(
            nameof(position), position, "Legend position is missing from the engine mapper."),
    };

    private static ConsolidationFunction ToFunction(string? function) => function switch
    {
        null or PivotFunctions.Sum => ConsolidationFunction.Sum,
        PivotFunctions.Count => ConsolidationFunction.Count,
        PivotFunctions.Average => ConsolidationFunction.Average,
        PivotFunctions.Max => ConsolidationFunction.Max,
        PivotFunctions.Min => ConsolidationFunction.Min,
        _ => throw new ArgumentOutOfRangeException(
            nameof(function), function, "Pivot function is missing from the engine mapper."),
    };

}

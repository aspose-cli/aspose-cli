using Aspose.Cli.Sdk.Operations;
using System.Drawing;
using System.Globalization;
using System.Text;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Pivot;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Creates native charts and pivot tables. Kept in the mapping layer because
/// the contract vocabulary (type and function names, header-name field
/// references) is translated to engine enums and addresses here.
/// </summary>
internal static class ChartPivotOps
{
    /// <summary>
    /// The default series palette of the modern chart look, cycled across
    /// series in order.
    /// </summary>
    private static readonly string[] ModernPalette = ["#1F4E79", "#2E75B6", "#9DC3E6", "#D9D9D9"];

    public static long? CreateChart(Worksheet sheet, CreateChartOp op)
    {
        RangeRef placement = A1.ParseRange(op.At).Range;
        string dataRange = Sheets.Reference(sheet, op.DataRange);
        int chartIndex = sheet.Charts.Add(
            ToChartType(op.Type),
            placement.Start.Row,
            placement.Start.Column,
            placement.End.Row,
            placement.End.Column);

        Chart chart = sheet.Charts[chartIndex];
        // Series-from-columns is the default; SetChartDataRange derives
        // series and category axes from the headers in the range.
        chart.SetChartDataRange(dataRange, isVertical: !op.SeriesInRows);

        if (op.Title is { } title)
        {
            chart.Title.Text = title;
        }

        // Defaults first, explicit cosmetics after — a user field always wins.
        ApplyModernDefaults(chart, op.Type);
        ApplyCosmetics(chart, op.Legend, op.AxisTitles, op.SeriesColors, op.DataLabels);
        EnsureHonestValueAxis(chart);
        return null;
    }

    public static long? CreatePivot(Worksheet sheet, CreatePivotOp op)
    {
        // An unqualified source refers to the op's sheet; the engine API
        // requires the qualified form.
        string source = Sheets.Reference(sheet, op.SourceRange);
        string name = op.Name ?? UnusedPivotName(sheet.Workbook);
        if (FindPivot(sheet, name) is not null)
        {
            throw new OperationInvalidException(
                $"sheet '{sheet.Name}' already has a pivot table named '{name}'",
                hint: "Give the new pivot a 'name' that is unique on its sheet, or omit it for a generated one.");
        }

        int pivotIndex = sheet.PivotTables.Add(source, op.At, name);
        PivotTable pivot = sheet.PivotTables[pivotIndex];
        try
        {
            foreach (string row in op.Rows ?? [])
            {
                AddField(pivot, PivotFieldType.Row, row);
            }

            foreach (string column in op.Columns ?? [])
            {
                AddField(pivot, PivotFieldType.Column, column);
            }

            var dataFields = new List<PivotField>(op.Values.Count);
            foreach (PivotValueField value in op.Values)
            {
                int fieldIndex = AddField(pivot, PivotFieldType.Data, value.Field);
                pivot.DataFields[fieldIndex].Function = ToFunction(value.Function);
                if (value.NumberFormat is { } numberFormat)
                {
                    pivot.DataFields[fieldIndex].NumberFormat = numberFormat;
                }

                dataFields.Add(pivot.DataFields[fieldIndex]);
            }

            ApplyCaptions(pivot, op, dataFields);
        }
        catch (OperationInvalidException)
        {
            // Field names are known only once the engine has read the source
            // headers; a rejected op must not leave a half-built pivot behind
            // for --best-effort to publish.
            sheet.PivotTables.RemoveAt(pivotIndex, keepData: false);
            throw;
        }

        pivot.CalculateData();
        return null;
    }

    /// <summary>
    /// Writes the pivot's captions after its functions are set, since the engine renames a
    /// data field whenever its function changes. <c>en</c> leaves every engine caption as it
    /// is. <c>zh</c> writes Excel's Simplified Chinese captions through the pivot's own caption
    /// properties, which the file stores and a refresh keeps. An explicit label wins in either
    /// language. Excel refuses a value field name that repeats a source header or another
    /// value field's name, ignoring case, while the engine accepts and saves one, so a label
    /// that would do so is refused here and a generated Chinese caption takes the next free
    /// number, as the engine's own <c>Sum of X2</c> does.
    /// </summary>
    private static void ApplyCaptions(PivotTable pivot, CreatePivotOp op, List<PivotField> dataFields)
    {
        bool chinese = op.Captions switch
        {
            PivotCaptionLanguages.Chinese => true,
            PivotCaptionLanguages.English => false,
            _ => (op.Rows ?? []).Concat(op.Columns ?? []).Concat(op.Values.Select(static value => value.Field)).Any(ContainsHan),
        };

        var sourceFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < pivot.BaseFields.Count; i++)
        {
            sourceFields.Add(pivot.BaseFields[i].Name);
        }

        var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < op.Values.Count; i++)
        {
            if (op.Values[i].Label is not { } label)
            {
                continue;
            }

            if (sourceFields.Contains(label))
            {
                throw LabelTaken(i, label, "a header of the source data");
            }

            if (!labels.TryAdd(label, i))
            {
                throw LabelTaken(i, label, $"the label of values[{labels[label]}]");
            }

            dataFields[i].DisplayName = label;
        }

        var taken = new HashSet<string>(labels.Keys, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < op.Values.Count; i++)
        {
            if (op.Values[i].Label is not null)
            {
                continue;
            }

            if (chinese)
            {
                string caption = UnusedCaption(ChineseCaptionPrefix(op.Values[i].Function) + op.Values[i].Field, sourceFields, taken);
                dataFields[i].DisplayName = caption;
                taken.Add(caption);
            }
            else if (labels.TryGetValue(dataFields[i].DisplayName, out int labelled))
            {
                throw LabelTaken(labelled, dataFields[i].DisplayName, $"the caption of values[{i}]");
            }
        }

        if (chinese)
        {
            pivot.GrandTotalName = "总计";
            pivot.RowHeaderCaption = "行标签";
            pivot.ColumnHeaderCaption = "列标签";
            pivot.DataFieldHeaderName = "值";
        }
    }

    private static OperationInvalidException LabelTaken(int index, string label, string owner) => new(
        $"create_pivot values[{index}].label '{label}' repeats {owner}; Excel refuses a value field name that is already taken",
        hint: "Give each value field a label that differs, ignoring case, from every source header and every other value field's caption.");

    /// <summary>Excel's Simplified Chinese value field prefixes, with its ASCII colon.</summary>
    private static string ChineseCaptionPrefix(string function) => function switch
    {
        PivotFunctions.Sum => "求和项:",
        PivotFunctions.Count => "计数项:",
        PivotFunctions.Average => "平均值项:",
        PivotFunctions.Max => "最大值项:",
        PivotFunctions.Min => "最小值项:",
        _ => throw new ArgumentOutOfRangeException(
            nameof(function), function, "Pivot function is missing from the caption mapper."),
    };

    private static string UnusedCaption(string caption, HashSet<string> sourceFields, HashSet<string> taken)
    {
        string candidate = caption;
        for (int number = 2; sourceFields.Contains(candidate) || taken.Contains(candidate); number++)
        {
            candidate = caption + number.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    /// <summary>Whether the text holds a Han character (a CJK unified or compatibility ideograph).</summary>
    private static bool ContainsHan(string text)
    {
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (rune.Value is (>= 0x3400 and <= 0x4DBF) or (>= 0x4E00 and <= 0x9FFF)
                or (>= 0xF900 and <= 0xFAFF) or (>= 0x20000 and <= 0x323AF))
            {
                return true;
            }
        }

        return false;
    }

    private static PivotTable? FindPivot(Worksheet sheet, string name)
    {
        foreach (PivotTable pivot in sheet.PivotTables)
        {
            if (string.Equals(pivot.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return pivot;
            }
        }

        return null;
    }

    /// <summary>
    /// Excel's own default naming, <c>PivotTableN</c>, with the first N no sheet of the
    /// workbook uses yet — so later ops and <c>refresh_pivot</c> can address it.
    /// </summary>
    private static string UnusedPivotName(Workbook workbook)
    {
        for (int number = 1; ; number++)
        {
            string candidate = "PivotTable" + number.ToString(CultureInfo.InvariantCulture);
            bool taken = false;
            foreach (Worksheet sheet in workbook.Worksheets)
            {
                taken |= FindPivot(sheet, candidate) is not null;
            }

            if (!taken)
            {
                return candidate;
            }
        }
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

            throw new OperationInvalidException(
                $"create_pivot references field '{field}', which is not a column of the source data; " +
                $"available fields: {string.Join(", ", available)}",
                hint: "Use one of the source header names (case-sensitive) for 'rows', 'columns' and 'values[].field'.");
        }

        return index;
    }

    public static long? RefreshPivot(Worksheet sheet, RefreshPivotOp op)
    {
        bool refreshedAny = false;
        foreach (PivotTable pivot in sheet.PivotTables)
        {
            if (op.Name is null || string.Equals(pivot.Name, op.Name, StringComparison.OrdinalIgnoreCase))
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
            var names = new List<string>(sheet.PivotTables.Count);
            foreach (PivotTable pivot in sheet.PivotTables)
            {
                names.Add(pivot.Name);
            }

            throw CliErrors.NotFound(CellsDiagnostics.PivotNotFound, "pivot table", name, names);
        }

        return null;
    }

    public static long? UpdateChart(Worksheet sheet, UpdateChartOp op)
    {
        Chart chart = sheet.Charts[ResolveChart(sheet, op.Index, op.Name)];
        string? dataRange = op.DataRange is { } text ? Sheets.Reference(sheet, text) : null;

        // The engine silently drops axis-title writes on a pie-family chart. The
        // parser rejects pie+axisTitles only when the op sets the type; the type
        // the chart will carry is known here, before anything changes.
        if (op.AxisTitles is not null && IsPieFamily(op.Type is { } target ? ToChartType(target) : chart.Type))
        {
            throw new OperationInvalidException("a 'pie' chart has no axes; omit 'axisTitles'");
        }

        if (op.Type is { } type)
        {
            chart.Type = ToChartType(type);
        }

        if (dataRange is not null)
        {
            chart.SetChartDataRange(dataRange, isVertical: op.SeriesInRows is not true);
        }

        if (op.Title is { } title)
        {
            chart.Title.Text = title;
        }

        // After the type/data changes so the cosmetics see the final chart. The
        // value-axis baseline is a creation default: an existing chart keeps the
        // axis its author chose.
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
    /// is present. Chart names need not be unique, so a name several charts
    /// share is refused rather than resolved to one of them.
    /// </summary>
    private static int ResolveChart(Worksheet sheet, int? index, string? name)
    {
        int count = sheet.Charts.Count;
        if (index is { } wanted)
        {
            return wanted < count ? wanted : throw ChartIndexNotFound(sheet, wanted);
        }

        var names = new string[count];
        var matches = new List<int>();
        for (int i = 0; i < count; i++)
        {
            names[i] = sheet.Charts[i].Name;
            if (string.Equals(names[i], name, StringComparison.Ordinal))
            {
                matches.Add(i);
            }
        }

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw CliErrors.NotFound(CellsDiagnostics.ChartNotFound, "chart", name!, names),
            _ => throw new OperationInvalidException(
                $"{matches.Count} charts on sheet '{sheet.Name}' are named '{name}', at indexes {string.Join(", ", matches)}",
                hint: "Address the chart by its zero-based 'index' instead of 'name'."),
        };
    }

    /// <summary>
    /// <c>CHART_NOT_FOUND</c> for an index past the sheet's charts. Chart indexes are
    /// zero-based, so the message and hint state the sheet's own index range.
    /// </summary>
    private static CliException ChartIndexNotFound(Worksheet sheet, int index)
    {
        int count = sheet.Charts.Count;
        return CliErrors.NotFoundAt(
            CellsDiagnostics.ChartNotFound,
            "chart index",
            index.ToString(CultureInfo.InvariantCulture),
            count,
            count == 0
                ? $"Sheet '{sheet.Name}' has no chart; select the sheet that holds it."
                : $"Use a zero-based index from 0 through {count - 1}, or address the chart by 'name'.");
    }

    /// <summary>
    /// The modern default look, applied on create only and before the user's
    /// explicit cosmetics (which therefore always win). The engine's own
    /// defaults are the 2003 look — gray <c>#C0C0C0</c> plot area, an outer
    /// chart border, gap width 150 and a right-docked legend.
    /// <c>Chart.Style</c> is deliberately not used: it persists in the file
    /// but the renderer ignores it (verified: byte-identical renders).
    /// </summary>
    private static void ApplyModernDefaults(Chart chart, string type)
    {
        chart.ChartArea.Border.IsVisible = false;
        chart.PlotArea.Area.Formatting = FormattingType.None;
        chart.ShowLegend = true;
        chart.Legend.Position = LegendPositionType.Bottom;

        if (type != ChartTypes.Pie)
        {
            // A pie has no axes; the engine silently drops axis writes there,
            // so the code skips them to stay honest.
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
        ChartLegend? legend,
        ChartAxisTitles? axisTitles,
        IReadOnlyList<string>? seriesColors,
        ChartDataLabels? dataLabels)
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
            // Both callers reject axis titles on a pie-family chart before
            // changing anything.
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
                    // Setting the string format auto-clears NumberFormatLinked;
                    // the int-typed Number property is ignored.
                    series.DataLabels.NumberFormat = format;
                }
            }
        }
    }

    /// <summary>
    /// Colors the series with a per-type recipe verified in renders: fills
    /// for column/bar/area, the line color for line, the markers and any
    /// drawn line for scatter (whose markers otherwise keep the default palette),
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
                // A color on the series line makes it visible, which turns a
                // markers-only scatter into one connected by lines (probe-verified:
                // it saves as ScatterConnectedByLinesWithDataMarker), so only a
                // line the chart already draws is colored.
                if (series.Border.IsVisible)
                {
                    series.Border.Color = color;
                }

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

    /// <summary>
    /// Keeps a chart that draws value as an extent from its baseline from
    /// exaggerating its own data. The engine's computed minimum answers this on its
    /// own: a minimum above zero means every plotted value is positive and the
    /// baseline was simply lifted off it. A minimum the author set is left alone,
    /// and one at or below zero already shows the values honestly.
    /// </summary>
    private static void EnsureHonestValueAxis(Chart chart)
    {
        if (chart.Type is not (ChartType.Column or ChartType.Bar or ChartType.Area)
            || chart.NSeries.Count == 0)
        {
            return;
        }

        chart.Calculate();
        Axis values = chart.ValueAxis;
        if (values.IsAutomaticMinValue
            && values.MinValue is IConvertible computed
            && Convert.ToDouble(computed, CultureInfo.InvariantCulture) > 0)
        {
            values.IsAutomaticMinValue = false;
            values.MinValue = 0d;
        }
    }

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

    private static ConsolidationFunction ToFunction(string function) => function switch
    {
        PivotFunctions.Sum => ConsolidationFunction.Sum,
        PivotFunctions.Count => ConsolidationFunction.Count,
        PivotFunctions.Average => ConsolidationFunction.Average,
        PivotFunctions.Max => ConsolidationFunction.Max,
        PivotFunctions.Min => ConsolidationFunction.Min,
        _ => throw new ArgumentOutOfRangeException(
            nameof(function), function, "Pivot function is missing from the engine mapper."),
    };

}

using System.Globalization;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.Editing.SlidesMutationSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// Updates an existing chart's data in its own workbook cells. Series, points and
/// categories keep their objects, so their fills, markers, data labels and number
/// formats survive; only added series and points take the chart's automatic style.
/// Every precondition is checked before the first cell changes.
/// </summary>
internal static class SlidesChartData
{
    internal static void Update(IChart chart, UpdateChartDataOp op)
    {
        IChartData data = chart.ChartData;
        IChartSeries[] existing = data.Series.ToArray();
        EnsureSupported(chart, existing);
        if (IsScatter(chart))
        {
            UpdateScatter(data, existing, op, chart.Type);
        }
        else
        {
            UpdateCategories(data, existing, op, chart.Type);
        }
    }

    /// <summary>The values of every series after an update, for data-driven presentation.</summary>
    internal static IReadOnlyList<IReadOnlyList<double>> Values(IChart chart) =>
        chart.ChartData.Series
            .Select(series => (IReadOnlyList<double>)series.DataPoints
                .Select(point => Number(IsScatter(chart) ? point.YValue.Data : point.Value.Data))
                .ToArray())
            .ToArray();

    private static void EnsureSupported(IChart chart, IChartSeries[] series)
    {
        if (chart.ChartData.DataSourceType != ChartDataSourceType.InternalWorkbook)
        {
            throw Unsupported("its data lives in an external workbook");
        }

        ChartType[] types = series.Select(static item => item.Type).Append(chart.Type).ToArray();
        ChartType? unsupported = types.Cast<ChartType?>().FirstOrDefault(static type => !IsSupported(type!.Value));
        if (unsupported is not null)
        {
            throw Unsupported($"{unsupported} charts are not supported by update_chart_data");
        }

        if (types.Any(ChartTypeCharacterizer.IsChartTypeScatter) && !types.All(ChartTypeCharacterizer.IsChartTypeScatter))
        {
            throw Unsupported("it combines scatter and category series");
        }
    }

    private static bool IsSupported(ChartType type) =>
        !ChartTypeCharacterizer.IsChartTypeBubble(type)
        && !ChartTypeCharacterizer.IsChartTypeStock(type)
        && !ChartTypeCharacterizer.IsChartTypeSurface(type)
        && (ChartTypeCharacterizer.IsChartTypeBar(type)
            || ChartTypeCharacterizer.IsChartTypeColumn(type)
            || ChartTypeCharacterizer.IsChartTypeLine(type)
            || ChartTypeCharacterizer.IsChartTypePie(type)
            || ChartTypeCharacterizer.IsChartTypeDoughnut(type)
            || ChartTypeCharacterizer.IsChartTypeArea(type)
            || ChartTypeCharacterizer.IsChartTypeRadar(type)
            || ChartTypeCharacterizer.IsChartTypeScatter(type));

    private static bool IsScatter(IChart chart) => ChartTypeCharacterizer.IsChartTypeScatter(chart.Type);

    private static void UpdateCategories(IChartData data, IChartSeries[] existing, UpdateChartDataOp op, ChartType chartType)
    {
        IChartCategory[] categories = data.Categories.ToArray();
        if (categories.Length > 0 && (!data.Categories.UseCells || data.Categories.GroupingLevelCount > 1))
        {
            throw Unsupported("its categories are literal or multi-level");
        }

        int count = op.Categories?.Count ?? categories.Length;
        if (op.Series is null && existing.Any(series => series.DataPoints.Count != count))
        {
            throw ChartDataInvalid(
                $"the chart's series hold {existing.FirstOrDefault()?.DataPoints.Count ?? 0} values but {count} categories were given; pass series with the new categories.");
        }

        if (op.Series?.Any(series => series.Values.Count != count) == true)
        {
            throw ChartDataInvalid($"each series needs exactly {count} values, one per category.");
        }

        if (op.Series is not null)
        {
            EnsureWorksheetBacked(existing.Take(op.Series.Count), static point => [point.Value]);
        }

        CellGrid grid = CellGrid.ForCategories(data, categories, existing);
        IChartDataWorkbook workbook = data.ChartDataWorkbook;

        // Categories: rewrite in place, extend along the category direction, trim from the end.
        if (op.Categories is not null)
        {
            for (int index = 0; index < count; index++)
            {
                if (index < categories.Length)
                {
                    categories[index].AsCell.Value = op.Categories[index];
                }
                else
                {
                    data.Categories.Add(workbook.GetCell(grid.Sheet, grid.Category(index).Row, grid.Category(index).Column, op.Categories[index]));
                }
            }

            for (int index = categories.Length - 1; index >= count; index--)
            {
                IChartDataCell cell = categories[index].AsCell;
                data.Categories.RemoveAt(index);
                cell.Value = null;
            }
        }

        if (op.Series is null)
        {
            return;
        }

        for (int index = 0; index < op.Series.Count; index++)
        {
            SlidesChartSeriesInput input = op.Series[index];
            IChartSeries series = index < existing.Length
                ? existing[index]
                : data.Series.Add(
                    workbook.GetCell(grid.Sheet, grid.SeriesName(index).Row, grid.SeriesName(index).Column, input.Name),
                    existing.Length > 0 ? existing[^1].Type : chartType);
            SetName(series, input.Name);
            IChartDataPoint[] points = series.DataPoints.ToArray();
            for (int point = 0; point < input.Values.Count; point++)
            {
                if (point < points.Length)
                {
                    points[point].Value.AsCell.Value = input.Values[point];
                }
                else
                {
                    (int row, int column) = grid.Value(NameCell(series), index, point);
                    AddPoint(series, workbook.GetCell(grid.Sheet, row, column, input.Values[point]));
                }
            }

            for (int point = points.Length - 1; point >= input.Values.Count; point--)
            {
                IChartDataCell cell = points[point].Value.AsCell;
                series.DataPoints.RemoveAt(point);
                cell.Value = null;
            }
        }

        for (int index = existing.Length - 1; index >= op.Series.Count; index--)
        {
            ClearSeries(existing[index], static point => [point.Value]);
            data.Series.RemoveAt(index);
        }
    }

    private static void UpdateScatter(IChartData data, IChartSeries[] existing, UpdateChartDataOp op, ChartType chartType)
    {
        if (op.Categories is not null)
        {
            throw ChartDataInvalid("scatter charts have no categories; update series with values and xValues.");
        }

        IReadOnlyList<SlidesChartSeriesInput> inputs = op.Series!;
        if (inputs.Any(static series => series.XValues?.Count != series.Values.Count))
        {
            throw ChartDataInvalid("scatter series require xValues matching values.");
        }

        IChartSeries[] kept = existing.Take(inputs.Count).ToArray();
        EnsureWorksheetBacked(kept, static point => [point.XValue, point.YValue]);
        if (kept.Any(series => series.DataPoints.Count == 0)
            && inputs.Take(kept.Length).Any(static series => series.Values.Count > 0))
        {
            throw Unsupported("a scatter series without points cannot show where new points belong");
        }

        EnsureSharedXCellsAgree(kept, inputs);
        IChartDataWorkbook workbook = data.ChartDataWorkbook;
        int sheet = existing.SelectMany(static series => series.DataPoints)
            .Select(static point => point.YValue.AsCell.ChartDataWorksheet.Index)
            .FirstOrDefault();
        int nameRow = existing.Select(NameCell).OfType<IChartDataCell>().Select(static cell => cell.Row).DefaultIfEmpty(0).First();
        int lastColumn = existing
            .SelectMany(static series => series.DataPoints.SelectMany(static point => new[] { point.XValue.AsCell, point.YValue.AsCell }))
            .Select(static cell => cell.Column)
            .DefaultIfEmpty(0)
            .Max();
        int firstRow = nameRow + 1;
        for (int index = 0; index < inputs.Count; index++)
        {
            SlidesChartSeriesInput input = inputs[index];
            IChartSeries series;
            int xColumn;
            int yColumn;
            if (index < existing.Length)
            {
                series = existing[index];
            }
            else
            {
                // A new series takes two fresh columns after every cell the chart uses.
                xColumn = ++lastColumn;
                yColumn = ++lastColumn;
                series = data.Series.Add(
                    workbook.GetCell(sheet, nameRow, yColumn, input.Name),
                    existing.Length > 0 ? existing[^1].Type : chartType);
                for (int point = 0; point < input.Values.Count; point++)
                {
                    series.DataPoints.AddDataPointForScatterSeries(
                        workbook.GetCell(sheet, firstRow + point, xColumn, input.XValues![point]),
                        workbook.GetCell(sheet, firstRow + point, yColumn, input.Values[point]));
                }

                continue;
            }

            SetName(series, input.Name);
            IChartDataPoint[] points = series.DataPoints.ToArray();
            for (int point = 0; point < input.Values.Count; point++)
            {
                if (point < points.Length)
                {
                    points[point].XValue.AsCell.Value = input.XValues![point];
                    points[point].YValue.AsCell.Value = input.Values[point];
                    continue;
                }

                // Continue the series' own cell pattern past its last point.
                IChartDataCell lastX = points[^1].XValue.AsCell;
                IChartDataCell lastY = points[^1].YValue.AsCell;
                (int rowStep, int columnStep) = points.Length > 1
                    ? (lastY.Row - points[^2].YValue.AsCell.Row, lastY.Column - points[^2].YValue.AsCell.Column)
                    : (1, 0);
                int offset = point - points.Length + 1;
                series.DataPoints.AddDataPointForScatterSeries(
                    workbook.GetCell(sheet, lastX.Row + (rowStep * offset), lastX.Column + (columnStep * offset), input.XValues![point]),
                    workbook.GetCell(sheet, lastY.Row + (rowStep * offset), lastY.Column + (columnStep * offset), input.Values[point]));
            }

            for (int point = points.Length - 1; point >= input.Values.Count; point--)
            {
                IChartDataCell x = points[point].XValue.AsCell;
                IChartDataCell y = points[point].YValue.AsCell;
                series.DataPoints.RemoveAt(point);
                y.Value = null;
                if (!data.Series.Any(other => other.DataPoints.Any(remaining => SameCell(remaining.XValue.AsCell, x))))
                {
                    x.Value = null;
                }
            }
        }

        for (int index = existing.Length - 1; index >= inputs.Count; index--)
        {
            ClearSeries(existing[index], static point => [point.YValue]);
            data.Series.RemoveAt(index);
        }
    }

    // PowerPoint's own scatter charts share one X column between series. Writing different
    // X values into a shared cell would silently move another series' points.
    private static void EnsureSharedXCellsAgree(IChartSeries[] kept, IReadOnlyList<SlidesChartSeriesInput> inputs)
    {
        var assigned = new Dictionary<(int Sheet, int Row, int Column), double>();
        for (int index = 0; index < kept.Length; index++)
        {
            IChartDataPoint[] points = kept[index].DataPoints.ToArray();
            for (int point = 0; point < Math.Min(points.Length, inputs[index].Values.Count); point++)
            {
                IChartDataCell cell = points[point].XValue.AsCell;
                var key = (cell.ChartDataWorksheet.Index, cell.Row, cell.Column);
                double value = inputs[index].XValues![point];
                if (assigned.TryGetValue(key, out double other) && other != value)
                {
                    throw ChartDataInvalid(
                        "these series share their X cells; give them identical xValues or recreate the chart.");
                }

                assigned[key] = value;
            }
        }
    }

    private static void EnsureWorksheetBacked(
        IEnumerable<IChartSeries> series,
        Func<IChartDataPoint, IBaseChartValue[]> values)
    {
        if (series.SelectMany(static item => item.DataPoints).SelectMany(values)
            .Any(static value => value.DataSourceType != DataSourceType.Worksheet))
        {
            throw Unsupported("its values are literals rather than workbook cells");
        }
    }

    private static void SetName(IChartSeries series, string name)
    {
        if (NameCell(series) is { } cell)
        {
            cell.Value = name;
        }
        else
        {
            series.Name.AsLiteralString = name;
        }
    }

    private static IChartDataCell? NameCell(IChartSeries series) =>
        series.Name.DataSourceType == DataSourceType.Worksheet && series.Name.AsCells.Count > 0
            ? series.Name.AsCells[0]
            : null;

    private static void ClearSeries(IChartSeries series, Func<IChartDataPoint, IBaseChartValue[]> values)
    {
        foreach (IBaseChartValue value in series.DataPoints.SelectMany(values))
        {
            if (value is ISingleCellChartValue { } single && value.DataSourceType == DataSourceType.Worksheet)
            {
                single.AsCell.Value = null;
            }
        }

        if (NameCell(series) is { } name)
        {
            name.Value = null;
        }
    }

    private static IChartDataPoint AddPoint(IChartSeries series, IChartDataCell value)
    {
        ChartType type = series.Type;
        IChartDataPointCollection points = series.DataPoints;
        return type switch
        {
            _ when ChartTypeCharacterizer.IsChartTypeLine(type) => points.AddDataPointForLineSeries(value),
            _ when ChartTypeCharacterizer.IsChartTypePie(type) => points.AddDataPointForPieSeries(value),
            _ when ChartTypeCharacterizer.IsChartTypeDoughnut(type) => points.AddDataPointForDoughnutSeries(value),
            _ when ChartTypeCharacterizer.IsChartTypeArea(type) => points.AddDataPointForAreaSeries(value),
            _ when ChartTypeCharacterizer.IsChartTypeRadar(type) => points.AddDataPointForRadarSeries(value),
            _ => points.AddDataPointForBarSeries(value),
        };
    }

    private static bool SameCell(IChartDataCell left, IChartDataCell right) =>
        left.ChartDataWorksheet.Index == right.ChartDataWorksheet.Index
        && left.Row == right.Row
        && left.Column == right.Column;

    private static double Number(object? value) =>
        double.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double parsed)
            ? parsed
            : 0;

    private static Aspose.Cli.Sdk.Errors.CliException Unsupported(string reason) =>
        ChartDataInvalid($"this chart cannot be updated in place: {reason}. Recreate it with insert_chart.");

    /// <summary>
    /// Where new category, series-name and value cells go, following the chart's own
    /// layout: series in columns (categories down the first column) or series in rows.
    /// </summary>
    private sealed record CellGrid(int Sheet, bool SeriesInColumns, (int Row, int Column) FirstCategory, (int Row, int Column) FirstName)
    {
        private (int Row, int Column) CategoryStep => SeriesInColumns ? (1, 0) : (0, 1);

        private (int Row, int Column) SeriesStep => SeriesInColumns ? (0, 1) : (1, 0);

        internal (int Row, int Column) Category(int index) =>
            (FirstCategory.Row + (CategoryStep.Row * index), FirstCategory.Column + (CategoryStep.Column * index));

        internal (int Row, int Column) SeriesName(int index) =>
            (FirstName.Row + (SeriesStep.Row * index), FirstName.Column + (SeriesStep.Column * index));

        /// <summary>A value cell aligns with its category and its series name.</summary>
        internal (int Row, int Column) Value(IChartDataCell? name, int series, int point)
        {
            (int Row, int Column) header = name is null ? SeriesName(series) : (name.Row, name.Column);
            return SeriesInColumns
                ? (Category(point).Row, header.Column)
                : (header.Row, Category(point).Column);
        }

        internal static CellGrid ForCategories(IChartData data, IChartCategory[] categories, IChartSeries[] series)
        {
            IChartDataCell? category = categories.FirstOrDefault()?.AsCell;
            IChartDataCell? name = series.Select(NameCell).FirstOrDefault(static cell => cell is not null);
            IChartDataCell? value = series.FirstOrDefault(static item => item.DataPoints.Count > 0)?.DataPoints[0].Value.AsCell;
            bool seriesInColumns = category is not null && value is not null
                ? category.Row == value.Row
                : categories.Length < 2 || categories[0].AsCell.Column == categories[1].AsCell.Column;
            int sheet = category?.ChartDataWorksheet.Index ?? name?.ChartDataWorksheet.Index ?? 0;
            (int Row, int Column) firstCategory = category is null
                ? seriesInColumns ? (1, 0) : (0, 1)
                : (category.Row, category.Column);
            (int Row, int Column) firstName = name is null
                ? seriesInColumns ? (firstCategory.Row - 1, firstCategory.Column + 1) : (firstCategory.Row + 1, firstCategory.Column - 1)
                : (name.Row, name.Column);
            return new CellGrid(sheet, seriesInColumns, firstCategory, firstName);
        }
    }
}

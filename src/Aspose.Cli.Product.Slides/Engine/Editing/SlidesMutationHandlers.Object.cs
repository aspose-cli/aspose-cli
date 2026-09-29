using System.Drawing;
using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.Editing.SlidesMutationSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

// Pictures, shapes, tables and charts.
internal sealed partial class SlidesMutationHandlers
{
    public long Apply(SlidesInsertImageOp operation)
    {
        EnsureFile(operation.Path);
        IPPImage image = _presentation.Images.AddImage(_inputs.ReadAllBytes(operation.Path));
        // Without a rectangle the picture keeps its aspect ratio, centered in 80% x 75% of the slide.
        RectangleF rect = operation.Rect is { } given
            ? new RectangleF((float)given.X, (float)given.Y, (float)given.Width, (float)given.Height)
            : SlidesAuthoring.Fit(image, SlidesAuthoring.Canvas(Slide, new RectangleF(0.1f, 0.125f, 0.8f, 0.75f)));
        Slide.Shapes.AddPictureFrame(ShapeType.Rectangle, rect.X, rect.Y, rect.Width, rect.Height, image);
        _touched.Add(Slide.SlideId);
        return 1;
    }

    public long Apply(InsertShapeOp operation)
    {
        ShapeType type = operation.Kind switch
        {
            SlidesShapeKinds.Rectangle => ShapeType.Rectangle,
            SlidesShapeKinds.RoundedRectangle => ShapeType.RoundCornerRectangle,
            SlidesShapeKinds.Ellipse => ShapeType.Ellipse,
            SlidesShapeKinds.Line => ShapeType.Line,
            SlidesShapeKinds.Chevron => ShapeType.Chevron,
            _ => throw new OperationInvalidException($"Unknown shape kind '{operation.Kind}'."),
        };
        IAutoShape shape = Slide.Shapes.AddAutoShape(
            type,
            (float)operation.Rect.X,
            (float)operation.Rect.Y,
            (float)operation.Rect.Width,
            (float)operation.Rect.Height);
        if (operation.Text is not null)
        {
            shape.TextFrame!.Text = operation.Text;
        }

        if (operation.Style is not null)
        {
            ApplyStyle(shape, operation.Style);
        }
        _touched.Add(Slide.SlideId);
        return 1;
    }

    public long Apply(SlidesInsertTableOp operation)
    {
        ITable table = SlidesAuthoring.AddTable(
            Slide,
            operation.Rect.X,
            operation.Rect.Y,
            operation.Rect.Width,
            operation.Rect.Height,
            operation.Rows,
            operation.Cols);
        if (operation.Data is not null)
        {
            for (int row = 0; row < operation.Data.Count; row++)
            {
                for (int column = 0; column < operation.Data[row].Count; column++)
                {
                    table[column, row].TextFrame.Text = operation.Data[row][column];
                }
            }
        }

        _touched.Add(Slide.SlideId);
        return operation.Rows * operation.Cols;
    }

    public long Apply(SlidesSetTableCellOp operation)
    {
        if (Shape is not ITable table)
        {
            throw new OperationInvalidException($"Shape {Shape.OfficeInteropShapeId} is not a table.");
        }

        if (operation.Row > table.Rows.Count || operation.Col > table.Columns.Count)
        {
            throw new OperationInvalidException(
                $"Table cell ({operation.Row},{operation.Col}) exceeds {table.Rows.Count} rows and {table.Columns.Count} columns.");
        }

        table[operation.Col - 1, operation.Row - 1].TextFrame.Text = operation.Text;
        _touched.Add(Slide.SlideId);
        return 1;
    }

    public long Apply(InsertChartOp operation)
    {
        ChartType type = ChartTypeFor(operation.Kind);
        IChart chart = Slide.Shapes.AddChart(
            type,
            (float)operation.Rect.X,
            (float)operation.Rect.Y,
            (float)operation.Rect.Width,
            (float)operation.Rect.Height,
            true);
        PopulateChart(chart, type, operation.Categories, operation.Series);
        chart.HasTitle = operation.Title is not null;
        if (operation.Title is not null)
        {
            chart.ChartTitle.Overlay = false;
            chart.ChartTitle.AddTextFrameForOverriding(operation.Title);
            chart.ChartTitle.TextFormat.TextBlockFormat.TextVerticalType = TextVerticalType.Horizontal;
        }
        chart.HasLegend = operation.Series.Count > 1;
        chart.Legend.Position = LegendPositionType.Bottom;
        chart.Legend.Overlay = false;
        chart.LineFormat.FillFormat.FillType = FillType.NoFill;
        if (type != ChartType.Pie)
        {
            chart.Axes.VerticalAxis.MinorGridLinesFormat.Line.FillFormat.FillType = FillType.NoFill;
            chart.Axes.HorizontalAxis.MinorGridLinesFormat.Line.FillFormat.FillType = FillType.NoFill;
            IAxis categories = CategoryAxis(chart, type);
            categories.MajorGridLinesFormat.Line.FillFormat.FillType = FillType.NoFill;
            if (type != ChartType.ScatterWithStraightLinesAndMarkers)
            {
                categories.CategoryAxisType = CategoryAxisType.Text;
            }
        }

        _touched.Add(Slide.SlideId);
        return 1;
    }

    public long Apply(UpdateChartDataOp operation)
    {
        if (Shape is not IChart chart)
        {
            throw new OperationInvalidException($"Shape {Shape.OfficeInteropShapeId} is not a chart.");
        }

        SlidesChartData.Update(chart, operation);
        ApplyDataDrivenPresentation(chart, chart.Type, SlidesChartData.Values(chart));
        _touched.Add(Slide.SlideId);
        return 1;
    }

    /// <summary>Fills a newly inserted chart; existing charts are updated by <see cref="SlidesChartData"/>.</summary>
    private static void PopulateChart(
        IChart chart,
        ChartType type,
        IReadOnlyList<string> categories,
        IReadOnlyList<SlidesChartSeriesInput> series)
    {
        int pointCount = categories.Count;
        if (pointCount == 0 || series.Count == 0
            || series.Any(item => item.Values.Count != pointCount)
            || type == ChartType.ScatterWithStraightLinesAndMarkers
            && series.Any(item => item.XValues?.Count != item.Values.Count))
        {
            throw ChartDataInvalid(
                "Categories and series must be non-empty with matching value lengths; scatter series also require matching xValues.");
        }

        IChartDataWorkbook workbook = chart.ChartData.ChartDataWorkbook;
        chart.ChartData.Series.Clear();
        chart.ChartData.Categories.Clear();
        for (int category = 0; category < categories.Count; category++)
        {
            chart.ChartData.Categories.Add(workbook.GetCell(0, category + 1, 0, categories[category]));
        }

        for (int seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
        {
            SlidesChartSeriesInput input = series[seriesIndex];
            int valueColumn = type == ChartType.ScatterWithStraightLinesAndMarkers
                ? (seriesIndex * 2) + 2
                : seriesIndex + 1;
            IChartSeries output = chart.ChartData.Series.Add(
                workbook.GetCell(0, 0, valueColumn, input.Name),
                type);
            for (int valueIndex = 0; valueIndex < input.Values.Count; valueIndex++)
            {
                IChartDataCell value = workbook.GetCell(
                    0,
                    valueIndex + 1,
                    valueColumn,
                    input.Values[valueIndex]);
                if (type == ChartType.ScatterWithStraightLinesAndMarkers)
                {
                    IChartDataCell x = workbook.GetCell(
                        0,
                        valueIndex + 1,
                        valueColumn - 1,
                        input.XValues![valueIndex]);
                    output.DataPoints.AddDataPointForScatterSeries(
                        x,
                        value);
                }
                else if (type == ChartType.Pie)
                {
                    output.DataPoints.AddDataPointForPieSeries(value);
                }
                else if (type == ChartType.LineWithMarkers)
                {
                    output.DataPoints.AddDataPointForLineSeries(value);
                }
                else
                {
                    output.DataPoints.AddDataPointForBarSeries(value);
                }
            }
        }

        ApplyDataDrivenPresentation(chart, type, series.Select(static item => item.Values).ToArray());
    }

    /// <summary>
    /// Enables a missing legend for multiple series and gives automatic non-negative
    /// bar/column value axes a zero minimum. Stored explicit limits are preserved;
    /// later data changes can require an independent axis and layout review.
    /// </summary>
    private static void ApplyDataDrivenPresentation(
        IChart chart,
        ChartType type,
        IReadOnlyList<IReadOnlyList<double>> series)
    {
        // More than one series cannot be told apart without a legend.
        if (series.Count > 1 && !chart.HasLegend)
        {
            chart.HasLegend = true;
            chart.Legend.Position = LegendPositionType.Bottom;
            chart.Legend.Overlay = false;
        }

        // A bar or column encodes its value as a length, so an engine-chosen
        // non-zero baseline exaggerates the differences between non-negative
        // values. Preserve every stored explicit minimum, including a zero saved
        // by an earlier CLI operation; its origin cannot be inferred after reload.
        if (type is not (ChartType.ClusteredBar or ChartType.ClusteredColumn)
            || series.Any(static values => values.Any(static value => value < 0)))
        {
            return;
        }

        IAxis values = ValueAxis(chart, type);
        if (values.IsAutomaticMinValue)
        {
            values.IsAutomaticMinValue = false;
            values.MinValue = 0;
        }
    }

    private static IAxis ValueAxis(IChart chart, ChartType type) =>
        type == ChartType.ClusteredBar ? chart.Axes.HorizontalAxis : chart.Axes.VerticalAxis;

    private static IAxis CategoryAxis(IChart chart, ChartType type) =>
        type == ChartType.ClusteredBar ? chart.Axes.VerticalAxis : chart.Axes.HorizontalAxis;
}

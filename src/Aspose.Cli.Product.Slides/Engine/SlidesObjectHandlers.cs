using System.Drawing;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.SlideShow;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;
using static Aspose.Cli.Product.Slides.Engine.SlidesMutationSupport;
using static Aspose.Cli.Product.Slides.Engine.SlidesStyleHandlers;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Applies image, shape, table, and chart operations.</summary>
internal static class SlidesObjectHandlers
{
    internal static long InsertImage(
        InputSource inputs,
        Presentation presentation,
        ISlide slide,
        SlidesInsertImageOp op,
        ISet<uint> touched)
    {
        EnsureFile(op.Path);
        IPPImage image = presentation.Images.AddImage(
            inputs.ReadAllBytes(op.Path));
        SlidesRectInput rect = op.Rect ?? FitImage(presentation, image);
        slide.Shapes.AddPictureFrame(
            ShapeType.Rectangle,
            (float)rect.X,
            (float)rect.Y,
            (float)rect.Width,
            (float)rect.Height,
            image);
        touched.Add(slide.SlideId);
        return 1;
    }

    internal static long InsertShape(ISlide slide, InsertShapeOp op, ISet<uint> touched)
    {
        ShapeType type = op.Kind switch
        {
            "rectangle" => ShapeType.Rectangle,
            "rounded-rectangle" => ShapeType.RoundCornerRectangle,
            "ellipse" => ShapeType.Ellipse,
            "line" => ShapeType.Line,
            "chevron" => ShapeType.Chevron,
            _ => throw new OperationInvalidException($"Unknown shape kind '{op.Kind}'."),
        };
        IAutoShape shape = slide.Shapes.AddAutoShape(
            type,
            (float)op.Rect.X,
            (float)op.Rect.Y,
            (float)op.Rect.Width,
            (float)op.Rect.Height);
        if (op.Text is not null)
        {
            shape.TextFrame!.Text = op.Text;
        }

        if (op.Style is not null)
        {
            ApplyStyle(shape, op.Style);
        }
        touched.Add(slide.SlideId);
        return 1;
    }

    internal static long InsertTable(ISlide slide, SlidesInsertTableOp op, ISet<uint> touched)
    {
        double[] columns = Enumerable.Repeat(op.Rect.Width / op.Cols, op.Cols).ToArray();
        double[] rows = Enumerable.Repeat(op.Rect.Height / op.Rows, op.Rows).ToArray();
        ITable table = slide.Shapes.AddTable(
            (float)op.Rect.X,
            (float)op.Rect.Y,
            columns,
            rows);
        if (op.Data is not null)
        {
            for (int row = 0; row < op.Data.Count; row++)
            {
                for (int column = 0; column < op.Data[row].Count; column++)
                {
                    table[column, row].TextFrame.Text = op.Data[row][column];
                }
            }
        }

        touched.Add(slide.SlideId);
        return op.Rows * op.Cols;
    }

    internal static long SetTableCell(
        ISlide slide,
        IShape shape,
        SlidesSetTableCellOp op,
        ISet<uint> touched)
    {
        if (shape is not ITable table)
        {
            throw new OperationInvalidException($"Shape {shape.OfficeInteropShapeId} is not a table.");
        }

        if (op.Row > table.Rows.Count || op.Col > table.Columns.Count)
        {
            throw new OperationInvalidException(
                $"Table cell ({op.Row},{op.Col}) exceeds {table.Rows.Count} rows and {table.Columns.Count} columns.");
        }

        table[op.Col - 1, op.Row - 1].TextFrame.Text = op.Text;
        touched.Add(slide.SlideId);
        return 1;
    }

    internal static long InsertChart(ISlide slide, InsertChartOp op, ISet<uint> touched)
    {
        ChartType type = ChartTypeFor(op.Kind);
        IChart chart = slide.Shapes.AddChart(
            type,
            (float)op.Rect.X,
            (float)op.Rect.Y,
            (float)op.Rect.Width,
            (float)op.Rect.Height,
            true);
        PopulateChart(chart, type, op.Categories, op.Series);
        chart.HasTitle = op.Title is not null;
        if (op.Title is not null)
        {
            chart.ChartTitle.Overlay = false;
            chart.ChartTitle.AddTextFrameForOverriding(op.Title);
            chart.ChartTitle.TextFormat.TextBlockFormat.TextVerticalType = TextVerticalType.Horizontal;
        }
        chart.HasLegend = op.Series.Count > 1;
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

        touched.Add(slide.SlideId);
        return 1;
    }

    internal static long UpdateChart(
        ISlide slide,
        IShape shape,
        UpdateChartDataOp op,
        ISet<uint> touched)
    {
        if (shape is not IChart chart)
        {
            throw new OperationInvalidException($"Shape {shape.OfficeInteropShapeId} is not a chart.");
        }

        SlidesChartData.Update(chart, op);
        ApplyDataDrivenPresentation(chart, chart.Type, SlidesChartData.Values(chart));
        touched.Add(slide.SlideId);
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

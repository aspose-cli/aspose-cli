using Aspose.Slides;
using Aspose.Slides.Charts;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// The fills, outlines and run formats a shape states, each table cell's included: what a read
/// reports and format_shape writes.
/// </summary>
internal static class SlidesShapeFormats
{
    /// <summary>
    /// The formats of every run of text a shape shows: a text frame's runs, every table cell's
    /// runs, or a chart's text with that of its title, legend, axes and shown data labels, which
    /// can each override the chart's own. Null for a shape that shows no text.
    /// </summary>
    internal static IBasePortionFormat[]? TextFormats(IShape shape) => shape switch
    {
        IAutoShape { TextFrame: { } frame } => Runs(frame).ToArray(),
        ITable table => Cells(table).SelectMany(static cell => Runs(cell.TextFrame)).ToArray(),
        IChart chart => ChartTextFormats(chart).ToArray(),
        _ => null,
    };

    private static IEnumerable<IBasePortionFormat> ChartTextFormats(IChart chart)
    {
        yield return chart.TextFormat.PortionFormat;
        if (chart.HasTitle)
        {
            yield return chart.ChartTitle.TextFormat.PortionFormat;
            foreach (IBasePortionFormat run in chart.ChartTitle.TextFrameForOverriding is { } title ? Runs(title) : [])
            {
                yield return run;
            }
        }

        if (chart.HasLegend)
        {
            yield return chart.Legend.TextFormat.PortionFormat;
        }

        // A pie has no axes.
        IAxis?[] axes =
        [
            chart.Axes?.HorizontalAxis, chart.Axes?.VerticalAxis,
            chart.Axes?.SecondaryHorizontalAxis, chart.Axes?.SecondaryVerticalAxis,
        ];
        foreach (IAxis axis in axes.OfType<IAxis>().Where(static axis => axis.IsVisible))
        {
            yield return axis.TextFormat.PortionFormat;
        }

        foreach (IDataLabelFormat labels in chart.ChartData.Series
            .Select(static series => series.Labels.DefaultDataLabelFormat)
            .Where(static labels => labels.ShowValue || labels.ShowCategoryName || labels.ShowSeriesName || labels.ShowPercentage))
        {
            yield return labels.TextFormat.PortionFormat;
        }
    }

    private static IEnumerable<IBasePortionFormat> Runs(ITextFrame frame) =>
        frame.Paragraphs.SelectMany(static paragraph => paragraph.Portions).Select(static portion => portion.PortionFormat);

    private static IEnumerable<ICell> Cells(ITable table) => table.Rows.SelectMany(static row => row);

    internal static IEnumerable<IFillFormat> Fills(IShape shape) => shape is ITable table
        ? Cells(table).Select(static cell => cell.CellFormat.FillFormat)
        : shape.FillFormat is { } fill ? [fill] : [];

    internal static IEnumerable<ILineFillFormat> Lines(IShape shape) => shape is ITable table
        ? Cells(table).SelectMany(static cell => new[]
        {
            cell.CellFormat.BorderTop, cell.CellFormat.BorderBottom, cell.CellFormat.BorderLeft, cell.CellFormat.BorderRight,
        }).Select(static border => border.FillFormat)
        : shape.LineFormat?.FillFormat is { } line ? [line] : [];
}
